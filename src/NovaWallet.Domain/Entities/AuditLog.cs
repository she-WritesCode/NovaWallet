namespace NovaWallet.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }
    public Guid TransactionId { get; set; }

    // e.g. "WalletCredited", "WalletDebited", "TransferCompleted",
    // "TransferFailed", "WalletFrozen", "WalletCreated"
    public string EventType { get; set; } = string.Empty;

    public long AmountKobo { get; set; }

    // Before and after — so a judge or auditor can see
    // exactly what each operation did to the balance
    // without having to reconstruct it from ledger entries
    public long BalanceBeforeKobo { get; set; }
    public long BalanceAfterKobo { get; set; }

    public string? PerformedBy { get; set; }
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Wallet Wallet { get; set; } = null!;
    public Transaction Transaction { get; set; } = null!;
}
