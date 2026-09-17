using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Models;

public record CreditWalletCommand(
    Guid DestinationWalletId,
    long AmountKobo,
    string? Narration = null,
    string? Reference = null,
    TransactionChannel Channel = TransactionChannel.Nip,
    string? InitiatedBy = null,
    string? IpAddress = null
);

public record TransferCommand(
    Guid SourceWalletId,
    Guid DestinationWalletId,
    long AmountKobo,
    string? Narration = null,
    string? Reference = null,
    TransactionChannel Channel = TransactionChannel.MobileApp,
    string? InitiatedBy = null,
    string? IpAddress = null
);

public record WalletBalanceResult(
    Guid WalletId,
    string CustomerId,
    string Currency,
    long AvailableBalanceKobo,
    long BookBalanceKobo,
    WalletStatus Status,
    long DailyOutboundTotalKobo,
    DateOnly DailyLimitResetDate
);

public record StatementEntryResult(
    Guid EntryId,
    Guid TransactionId,
    string Reference,
    TransactionType TransactionType,
    EntryType EntryType,
    long AmountKobo,
    long BalanceAfterKobo,
    string? Narration,
    DateTime CreatedAt
);

public record PaginatedList<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
