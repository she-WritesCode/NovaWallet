using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Domain.Exceptions;

namespace NovaWallet.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        ProblemDetails problemDetails;
        int statusCode;

        if (exception is WalletDomainException domainException)
        {
            _logger.LogWarning(
                domainException,
                "Domain error occurred: {ErrorCode} - {Message}",
                domainException.ErrorCode,
                domainException.Message);

            statusCode = domainException.StatusCode;

            problemDetails = new ProblemDetails
            {
                Type = $"https://errors.novapay.firstbank.ng/{domainException.ErrorCode.ToLowerInvariant().Replace('_', '-')}",
                Title = SplitPascalCase(domainException.GetType().Name.Replace("Exception", string.Empty)),
                Status = statusCode,
                Detail = domainException.Message,
                Instance = context.Request.Path
            };

            problemDetails.Extensions["errorCode"] = domainException.ErrorCode;
        }
        else if (exception is BadHttpRequestException badHttpRequestException)
        {
            _logger.LogWarning(badHttpRequestException, "Bad HTTP request: {Message}", badHttpRequestException.Message);
            statusCode = StatusCodes.Status400BadRequest;

            problemDetails = new ProblemDetails
            {
                Type = "https://errors.novapay.firstbank.ng/bad-request",
                Title = "Bad Request",
                Status = statusCode,
                Detail = badHttpRequestException.Message,
                Instance = context.Request.Path
            };
            problemDetails.Extensions["errorCode"] = "BAD_REQUEST";
        }
        else
        {
            _logger.LogError(exception, "An unhandled server exception occurred: {Message}", exception.Message);
            statusCode = StatusCodes.Status500InternalServerError;

            problemDetails = new ProblemDetails
            {
                Type = "https://errors.novapay.firstbank.ng/internal-server-error",
                Title = "Internal Server Error",
                Status = statusCode,
                Detail = "An unexpected error occurred while processing your request. Please try again later or contact customer support.",
                Instance = context.Request.Path
            };
            problemDetails.Extensions["errorCode"] = "INTERNAL_SERVER_ERROR";
        }

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
    }
}

