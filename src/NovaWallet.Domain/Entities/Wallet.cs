using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class Wallet
{
    public Guid Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public WalletStatus Status { get; set; }

    // Always "NGN" for now, but modelled as a field
    // so the system can go multi-currency without a migration
    public string Currency { get; set; } = "NGN";

    // Both stored in kobo — no floats anywhere in the money path
    public long AvailableBalanceKobo { get; set; }
    public long BookBalanceKobo { get; set; }

    // Daily outbound limit tracking
    // Reset to 0 when today > DailyLimitResetDate
    public long DailyOutboundTotalKobo { get; set; }
    public DateOnly DailyLimitResetDate { get; set; }

    // Optimistic concurrency — the "version" we discussed
    // EF Core increments this automatically on every update
    // If two requests read version 3 and both try to write version 4,
    // the second one gets a DbUpdateConcurrencyException
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties — these let EF Core understand relationships
    // In TypeORM terms, these are like @OneToMany(() => LedgerEntry)
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
