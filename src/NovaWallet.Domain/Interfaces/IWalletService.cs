using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Models;

namespace NovaWallet.Domain.Interfaces;

public interface IWalletService
{
    Task<Wallet> CreateWalletAsync(string customerId, string currency = "NGN", CancellationToken cancellationToken = default);

    Task<WalletBalanceResult> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken = default);

    Task<WalletBalanceResult> GetBalanceByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<Transaction> CreditWalletAsync(CreditWalletCommand command, CancellationToken cancellationToken = default);

    Task<Transaction> TransferAsync(TransferCommand command, CancellationToken cancellationToken = default);

    Task<PaginatedList<StatementEntryResult>> GetStatementAsync(Guid walletId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}

