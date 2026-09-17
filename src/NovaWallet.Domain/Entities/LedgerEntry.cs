using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class LedgerEntry
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }
    public Guid WalletId { get; set; }

    // Debit = money leaving this wallet
    // Credit = money arriving in this wallet
    public EntryType EntryType { get; set; }

    // Always positive — the direction is encoded in EntryType,
    // not in the sign of the amount
    public long AmountKobo { get; set; }

    // Snapshot of the wallet's book balance immediately after
    // this entry was posted — this is what makes statements
    // possible without replaying every entry from the start
    public long BalanceAfterKobo { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Transaction Transaction { get; set; } = null!;
    public Wallet Wallet { get; set; } = null!;
}
