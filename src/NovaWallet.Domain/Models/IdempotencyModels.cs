namespace NovaWallet.Domain.Models;

public record IdempotencyCheckResult(
    bool IsMatch,
    bool IsPayloadMismatch,
    int? StatusCode = null,
    string? ResponseBody = null
)
{
    public static IdempotencyCheckResult New() => new(false, false);
    public static IdempotencyCheckResult PayloadMismatch() => new(false, true);
    public static IdempotencyCheckResult Replay(int statusCode, string? responseBody) => new(true, false, statusCode, responseBody);
}

