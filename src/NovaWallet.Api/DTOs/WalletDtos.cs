using System.ComponentModel.DataAnnotations;

namespace NovaWallet.Api.DTOs;

public record CreateWalletRequest(
    [Required(ErrorMessage = "CustomerId is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "CustomerId must be between 1 and 100 characters.")]
    string CustomerId,

    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO code.")]
    string? Currency = "NGN"
);

public record CreditWalletRequest(
    [Required(ErrorMessage = "AmountKobo is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "AmountKobo must be greater than 0.")]
    long AmountKobo,

    [StringLength(500, ErrorMessage = "Narration cannot exceed 500 characters.")]
    string? Narration = null,

    [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
    string? Reference = null
);

public record TransferRequest(
    [Required(ErrorMessage = "DestinationWalletId is required.")]
    Guid DestinationWalletId,

    [Required(ErrorMessage = "AmountKobo is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "AmountKobo must be greater than 0.")]
    long AmountKobo,

    [StringLength(500, ErrorMessage = "Narration cannot exceed 500 characters.")]
    string? Narration = null,

    [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
    string? Reference = null
);

public record WalletResponse(
    Guid Id,
    string CustomerId,
    string Currency,
    long AvailableBalanceKobo,
    long BookBalanceKobo,
    string Status,
    long DailyOutboundTotalKobo,
    DateTime CreatedAt
);

public record TransactionResponse(
    Guid Id,
    string Reference,
    string Type,
    string Status,
    string Channel,
    long AmountKobo,
    string Currency,
    Guid? SourceWalletId,
    Guid DestinationWalletId,
    string? Narration,
    DateTime CreatedAt
);

public record StatementEntryResponse(
    Guid EntryId,
    Guid TransactionId,
    string Reference,
    string TransactionType,
    string EntryType,
    long AmountKobo,
    long BalanceAfterKobo,
    string? Narration,
    DateTime CreatedAt
);

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

