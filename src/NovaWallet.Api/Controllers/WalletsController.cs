using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Api.DTOs;
using NovaWallet.Domain.Enums;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Interfaces;
using NovaWallet.Domain.Models;

namespace NovaWallet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<WalletsController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WalletsController(
        IWalletService walletService,
        IIdempotencyService idempotencyService,
        ILogger<WalletsController> logger)
    {
        _walletService = walletService;
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new wallet for a customer with a zero starting balance.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWallet(
        [FromBody] CreateWalletRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await _walletService.CreateWalletAsync(
            request.CustomerId,
            request.Currency ?? "NGN",
            cancellationToken);

        var response = new WalletResponse(
            wallet.Id,
            wallet.CustomerId,
            wallet.Currency,
            wallet.AvailableBalanceKobo,
            wallet.BookBalanceKobo,
            wallet.Status.ToString(),
            wallet.DailyOutboundTotalKobo,
            wallet.CreatedAt
        );

        return CreatedAtAction(nameof(GetBalance), new { id = wallet.Id }, response);
    }

    /// <summary>
    /// Returns the current balance and currency (NGN) with amounts in kobo.
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _walletService.GetBalanceAsync(id, cancellationToken);

        var response = new WalletResponse(
            result.WalletId,
            result.CustomerId,
            result.Currency,
            result.AvailableBalanceKobo,
            result.BookBalanceKobo,
            result.Status.ToString(),
            result.DailyOutboundTotalKobo,
            DateTime.UtcNow
        );

        return Ok(response);
    }

    /// <summary>
    /// Credits a wallet with funds, simulating an inbound NIP instant transfer.
    /// </summary>
    [HttpPost("{id:guid}/credit")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreditWallet(
        [FromRoute] Guid id,
        [FromBody] CreditWalletRequest request,
        CancellationToken cancellationToken)
    {
        var initiatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("customerId")
            ?? "NIP-Switch";

        var command = new CreditWalletCommand(
            id,
            request.AmountKobo,
            request.Narration,
            request.Reference,
            TransactionChannel.Nip,
            initiatedBy,
            HttpContext.Connection.RemoteIpAddress?.ToString()
        );

        var tx = await _walletService.CreditWalletAsync(command, cancellationToken);

        var response = new TransactionResponse(
            tx.Id,
            tx.Reference,
            tx.Type.ToString(),
            tx.Status.ToString(),
            tx.Channel.ToString(),
            tx.AmountKobo,
            tx.Currency,
            tx.SourceWalletId,
            tx.DestinationWalletId,
            tx.Narration,
            tx.CreatedAt
        );

        return Ok(response);
    }

    /// <summary>
    /// Moves funds atomically from one wallet to another. Concurrency-safe and idempotent.
    /// Requires an 'Idempotency-Key' request header.
    /// </summary>
    [HttpPost("{id:guid}/transfer")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer(
        [FromRoute] Guid id,
        [FromBody] TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new MissingIdempotencyKeyException();
        }

        // Serialize the incoming request payload for SHA-256 fingerprint verification
        var payloadJson = JsonSerializer.Serialize(request, JsonOptions);

        // Check for idempotency replay or payload tampering
        var check = await _idempotencyService.CheckAsync(idempotencyKey, payloadJson, cancellationToken);

        if (check.IsPayloadMismatch)
        {
            throw new IdempotencyConflictException(idempotencyKey);
        }

        if (check.IsMatch && check.StatusCode.HasValue)
        {
            Response.Headers["X-Cache"] = "HIT";
            Response.Headers["Idempotency-Key"] = idempotencyKey;

            return Content(
                check.ResponseBody ?? "{}",
                "application/json",
                System.Text.Encoding.UTF8);
        }

        var initiatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("customerId")
            ?? "MobileAppUser";

        var command = new TransferCommand(
            id,
            request.DestinationWalletId,
            request.AmountKobo,
            request.Narration,
            request.Reference,
            TransactionChannel.MobileApp,
            initiatedBy,
            HttpContext.Connection.RemoteIpAddress?.ToString()
        );

        var tx = await _walletService.TransferAsync(command, cancellationToken);

        var response = new TransactionResponse(
            tx.Id,
            tx.Reference,
            tx.Type.ToString(),
            tx.Status.ToString(),
            tx.Channel.ToString(),
            tx.AmountKobo,
            tx.Currency,
            tx.SourceWalletId,
            tx.DestinationWalletId,
            tx.Narration,
            tx.CreatedAt
        );

        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        // Save idempotency cache
        await _idempotencyService.SaveAsync(
            idempotencyKey,
            payloadJson,
            StatusCodes.Status200OK,
            responseJson,
            tx.Id,
            TimeSpan.FromHours(24),
            cancellationToken
        );

        Response.Headers["X-Cache"] = "MISS";
        Response.Headers["Idempotency-Key"] = idempotencyKey;

        return Ok(response);
    }

    /// <summary>
    /// Returns paginated transaction history for a wallet, newest first.
    /// </summary>
    [HttpGet("{id:guid}/statement")]
    [ProducesResponseType(typeof(PagedResponse<StatementEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _walletService.GetStatementAsync(id, page, pageSize, cancellationToken);

        var entries = result.Items.Select(e => new StatementEntryResponse(
            e.EntryId,
            e.TransactionId,
            e.Reference,
            e.TransactionType.ToString(),
            e.EntryType.ToString(),
            e.AmountKobo,
            e.BalanceAfterKobo,
            e.Narration,
            e.CreatedAt
        )).ToList();

        var response = new PagedResponse<StatementEntryResponse>(
            entries,
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages
        );

        return Ok(response);
    }
}

