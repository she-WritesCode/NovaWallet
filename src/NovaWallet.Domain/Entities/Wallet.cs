using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class Wallet
{
    public Guid Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public WalletStatus Status { get; set; }

    public string Currency { get; set; } = "NGN";

    public long AvailableBalanceKobo { get; set; }
    public long BookBalanceKobo { get; set; }

    public long DailyOutboundTotalKobo { get; set; }
    public DateOnly DailyLimitResetDate { get; set; }

    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
