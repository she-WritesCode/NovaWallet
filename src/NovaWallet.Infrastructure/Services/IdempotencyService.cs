using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Interfaces;
using NovaWallet.Domain.Models;
using NovaWallet.Infrastructure.Data;

namespace NovaWallet.Infrastructure.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(AppDbContext dbContext, ILogger<IdempotencyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string ComputePayloadHash(string requestPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(requestPayload ?? string.Empty);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<IdempotencyCheckResult> CheckAsync(
        string idempotencyKey,
        string requestPayload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyCheckResult.New();
        }

        var now = DateTime.UtcNow;
        var record = await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);

        // Record not found or expired
        if (record == null || record.ExpiresAt <= now)
        {
            return IdempotencyCheckResult.New();
        }

        var incomingHash = ComputePayloadHash(requestPayload);

        // Reusing same key with a different payload is rejected per functional requirements
        if (!string.Equals(record.RequestHash, incomingHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Idempotency conflict detected for key {Key}. Stored hash: {Stored}, incoming hash: {Incoming}",
                idempotencyKey, record.RequestHash, incomingHash);

            return IdempotencyCheckResult.PayloadMismatch();
        }

        _logger.LogInformation("Idempotent replay detected for key {Key}. Returning cached status {Status}", idempotencyKey, record.StatusCode);
        return IdempotencyCheckResult.Replay(record.StatusCode, record.ResponseBody);
    }

    public async Task SaveAsync(
        string idempotencyKey,
        string requestPayload,
        int statusCode,
        string responseBody,
        Guid? transactionId = null,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var expiration = now.Add(ttl ?? TimeSpan.FromHours(24));
        var hash = ComputePayloadHash(requestPayload);

        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey.Trim(),
            RequestHash = hash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            TransactionId = transactionId,
            CreatedAt = now,
            ExpiresAt = expiration
        };

        try
        {
            _dbContext.IdempotencyRecords.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cached idempotency key {Key} with status {Status}", idempotencyKey, statusCode);
        }
        catch (DbUpdateException ex)
        {
            // Unique key collision under concurrent request interleaving
            _logger.LogWarning(ex, "Idempotency record for key {Key} already exists in database.", idempotencyKey);
        }
    }
}

