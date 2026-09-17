using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }

    // The idempotency anchor — unique across all transactions
    // In the Node.js example this was the "reference" field
    public string Reference { get; set; } = string.Empty;

    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public TransactionChannel Channel { get; set; }

    // Principal amount in kobo
    public long AmountKobo { get; set; }

    // Fee handling — FeeAmountKobo is 0 for free transactions
    // FeeWalletId is the system wallet that collects the fee
    public long FeeAmountKobo { get; set; }
    public Guid? FeeWalletId { get; set; }

    public string Currency { get; set; } = "NGN";

    // Nullable because a deposit has no internal source —
    // money is arriving from outside (NIP inbound)
    public Guid? SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }

    public string? Narration { get; set; }

    // Who triggered this and from where — for audit purposes
    public string? InitiatedBy { get; set; }
    public string? IpAddress { get; set; }

    // Flexible bag for anything that doesn't deserve its own column
    // e.g. NIP session ID, agent ID, USSD session ref
    public string? Metadata { get; set; }

    // State transition timestamps — each is set once when the
    // transaction enters that terminal state, then never updated
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    // Navigation properties
    public Wallet? SourceWallet { get; set; }
    public Wallet? DestinationWallet { get; set; }
    public Wallet? FeeWallet { get; set; }
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public IdempotencyRecord? IdempotencyRecord { get; set; }
}
