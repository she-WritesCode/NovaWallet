using NovaWallet.Domain.Models;

namespace NovaWallet.Domain.Interfaces;

public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> CheckAsync(
        string idempotencyKey,
        string requestPayload,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string idempotencyKey,
        string requestPayload,
        int statusCode,
        string responseBody,
        Guid? transactionId = null,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    string ComputePayloadHash(string requestPayload);
}

