namespace NovaWallet.Domain.Entities;

public class IdempotencyRecord
{
    public Guid Id { get; set; }

    // The value from the Idempotency-Key HTTP header
    public string IdempotencyKey { get; set; } = string.Empty;

    // SHA-256 hash of the request body — if someone reuses
    // the same key with a different payload, the hashes won't
    // match and we reject it instead of returning the cached response
    public string RequestHash { get; set; } = string.Empty;

    // Links back to the transaction this key produced (if any)
    public Guid? TransactionId { get; set; }

    // The HTTP status code and body we originally returned —
    // on a replay we short-circuit and return this directly
    // without touching the ledger
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; }

    // After expiry, the key can be reused
    // Typical TTL: 24-48 hours
    public DateTime ExpiresAt { get; set; }

    // Navigation property
    public Transaction? Transaction { get; set; }
}
