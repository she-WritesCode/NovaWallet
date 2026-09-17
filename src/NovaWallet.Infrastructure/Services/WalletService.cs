using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Interfaces;
using NovaWallet.Domain.Models;
using NovaWallet.Infrastructure.Data;

namespace NovaWallet.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<WalletService> _logger;
    private readonly long _dailyLimitKobo;

    public WalletService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<WalletService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        // Default to ₦500,000.00 (50,000,000 kobo)
        _dailyLimitKobo = long.TryParse(configuration["WalletSettings:DailyOutboundLimitKobo"], out var limit)
            ? limit
            : 50_000_000L;
    }

    public async Task<Wallet> CreateWalletAsync(
        string customerId,
        string currency = "NGN",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        var normalizedCurrency = currency.ToUpperInvariant();

        // Ensure one wallet per customer per currency
        var existing = await _dbContext.Wallets
            .AsNoTracking()
            .AnyAsync(w => w.CustomerId == customerId && w.Currency == normalizedCurrency, cancellationToken);

        if (existing)
        {
            throw new DuplicateWalletException(customerId, normalizedCurrency);
        }

        var todayWat = GetTodayWatDate();
        var now = DateTime.UtcNow;

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId.Trim(),
            Currency = normalizedCurrency,
            Status = WalletStatus.Active,
            AvailableBalanceKobo = 0L,
            BookBalanceKobo = 0L,
            DailyOutboundTotalKobo = 0L,
            DailyLimitResetDate = todayWat,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Wallets.Add(wallet);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new wallet {WalletId} for customer {CustomerId}", wallet.Id, wallet.CustomerId);
        return wallet;
    }

    public async Task<WalletBalanceResult> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);

        if (wallet == null)
        {
            throw new WalletNotFoundException(walletId);
        }

        return new WalletBalanceResult(
            wallet.Id,
            wallet.CustomerId,
            wallet.Currency,
            wallet.AvailableBalanceKobo,
            wallet.BookBalanceKobo,
            wallet.Status,
            wallet.DailyOutboundTotalKobo,
            wallet.DailyLimitResetDate
        );
    }

    public async Task<WalletBalanceResult> GetBalanceByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == customerId, cancellationToken);

        if (wallet == null)
        {
            throw new WalletNotFoundException(customerId);
        }

        return new WalletBalanceResult(
            wallet.Id,
            wallet.CustomerId,
            wallet.Currency,
            wallet.AvailableBalanceKobo,
            wallet.BookBalanceKobo,
            wallet.Status,
            wallet.DailyOutboundTotalKobo,
            wallet.DailyLimitResetDate
        );
    }

    public async Task<Transaction> CreditWalletAsync(
        CreditWalletCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AmountKobo <= 0)
        {
            throw new InvalidAmountException(command.AmountKobo);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            // Pessimistic lock on the destination wallet row
            var wallet = await _dbContext.Wallets
                .FromSqlInterpolated($"SELECT * FROM \"Wallets\" WHERE \"Id\" = {command.DestinationWalletId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                throw new WalletNotFoundException(command.DestinationWalletId);
            }

            if (wallet.Status != WalletStatus.Active)
            {
                throw new WalletFrozenException(wallet.Id);
            }

            var now = DateTime.UtcNow;
            var beforeBalance = wallet.BookBalanceKobo;
            var afterBalance = beforeBalance + command.AmountKobo;

            // Mutate balances in sync
            wallet.AvailableBalanceKobo += command.AmountKobo;
            wallet.BookBalanceKobo += command.AmountKobo;
            wallet.UpdatedAt = now;

            var reference = !string.IsNullOrWhiteSpace(command.Reference)
                ? command.Reference
                : $"DEP-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N[..8]}";

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                Channel = command.Channel,
                AmountKobo = command.AmountKobo,
                FeeAmountKobo = 0L,
                Currency = wallet.Currency,
                SourceWalletId = null, // Inbound NIP money originates outside internal system
                DestinationWalletId = wallet.Id,
                Narration = command.Narration ?? "Inbound NIP deposit",
                InitiatedBy = command.InitiatedBy,
                IpAddress = command.IpAddress,
                CreatedAt = now,
                CompletedAt = now
            };

            var ledgerEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                WalletId = wallet.Id,
                EntryType = EntryType.Credit,
                AmountKobo = command.AmountKobo,
                BalanceAfterKobo = afterBalance,
                CreatedAt = now
            };

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                TransactionId = transaction.Id,
                EventType = "WalletCredited",
                AmountKobo = command.AmountKobo,
                BalanceBeforeKobo = beforeBalance,
                BalanceAfterKobo = afterBalance,
                PerformedBy = command.InitiatedBy,
                IpAddress = command.IpAddress,
                CreatedAt = now
            };

            _dbContext.Transactions.Add(transaction);
            _dbContext.LedgerEntries.Add(ledgerEntry);
            _dbContext.AuditLogs.Add(auditLog);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Credited wallet {WalletId} with {Amount} kobo. Ref: {Reference}",
                wallet.Id, command.AmountKobo, transaction.Reference);

            return transaction;
        });
    }

    public async Task<Transaction> TransferAsync(
        TransferCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AmountKobo <= 0)
        {
            throw new InvalidAmountException(command.AmountKobo);
        }

        if (command.SourceWalletId == command.DestinationWalletId)
        {
            throw new SameWalletTransferException(command.SourceWalletId);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            // Deterministic row-locking order: guarantees no circular wait / deadlocks
            var firstId = command.SourceWalletId.CompareTo(command.DestinationWalletId) < 0
                ? command.SourceWalletId
                : command.DestinationWalletId;
            var secondId = firstId == command.SourceWalletId
                ? command.DestinationWalletId
                : command.SourceWalletId;

            var firstWallet = await _dbContext.Wallets
                .FromSqlInterpolated($"SELECT * FROM \"Wallets\" WHERE \"Id\" = {firstId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);

            var secondWallet = await _dbContext.Wallets
                .FromSqlInterpolated($"SELECT * FROM \"Wallets\" WHERE \"Id\" = {secondId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);

            var sourceWallet = firstId == command.SourceWalletId ? firstWallet : secondWallet;
            var destinationWallet = firstId == command.DestinationWalletId ? firstWallet : secondWallet;

            if (sourceWallet == null)
            {
                throw new WalletNotFoundException(command.SourceWalletId);
            }

            if (destinationWallet == null)
            {
                throw new WalletNotFoundException(command.DestinationWalletId);
            }

            if (sourceWallet.Status != WalletStatus.Active)
            {
                throw new WalletFrozenException(sourceWallet.Id);
            }

            if (destinationWallet.Status != WalletStatus.Active)
            {
                throw new WalletFrozenException(destinationWallet.Id);
            }

            // Ensure matching currencies (cross-currency transfers require FX engine)
            if (!string.Equals(sourceWallet.Currency, destinationWallet.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(sourceWallet.Currency, destinationWallet.Currency);
            }

            // Midnight WAT Daily Limit check
            var todayWat = GetTodayWatDate();
            if (sourceWallet.DailyLimitResetDate != todayWat)
            {
                sourceWallet.DailyOutboundTotalKobo = 0L;
                sourceWallet.DailyLimitResetDate = todayWat;
            }

            if (sourceWallet.DailyOutboundTotalKobo + command.AmountKobo > _dailyLimitKobo)
            {
                throw new DailyLimitExceededException(
                    sourceWallet.Id, 
                    command.AmountKobo, 
                    sourceWallet.DailyOutboundTotalKobo, 
                    _dailyLimitKobo,
                    sourceWallet.Currency);
            }

            // Balance check (never allow balance to go negative)
            if (sourceWallet.AvailableBalanceKobo < command.AmountKobo)
            {
                throw new InsufficientFundsException(
                    sourceWallet.Id, 
                    command.AmountKobo, 
                    sourceWallet.AvailableBalanceKobo,
                    sourceWallet.Currency);
            }

            var now = DateTime.UtcNow;

            var sourceBefore = sourceWallet.BookBalanceKobo;
            var sourceAfter = sourceBefore - command.AmountKobo;

            var destBefore = destinationWallet.BookBalanceKobo;
            var destAfter = destBefore + command.AmountKobo;

            // Mutate balances
            sourceWallet.AvailableBalanceKobo -= command.AmountKobo;
            sourceWallet.BookBalanceKobo -= command.AmountKobo;
            sourceWallet.DailyOutboundTotalKobo += command.AmountKobo;
            sourceWallet.UpdatedAt = now;

            destinationWallet.AvailableBalanceKobo += command.AmountKobo;
            destinationWallet.BookBalanceKobo += command.AmountKobo;
            destinationWallet.UpdatedAt = now;

            var reference = !string.IsNullOrWhiteSpace(command.Reference)
                ? command.Reference
                : $"TRF-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N[..8]}";

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                Channel = command.Channel,
                AmountKobo = command.AmountKobo,
                FeeAmountKobo = 0L,
                Currency = sourceWallet.Currency,
                SourceWalletId = sourceWallet.Id,
                DestinationWalletId = destinationWallet.Id,
                Narration = command.Narration ?? "Transfer",
                InitiatedBy = command.InitiatedBy,
                IpAddress = command.IpAddress,
                CreatedAt = now,
                CompletedAt = now
            };

            // Double-entry postings: 1 Debit for source, 1 Credit for destination
            var debitEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                WalletId = sourceWallet.Id,
                EntryType = EntryType.Debit,
                AmountKobo = command.AmountKobo,
                BalanceAfterKobo = sourceAfter,
                CreatedAt = now
            };

            var creditEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                WalletId = destinationWallet.Id,
                EntryType = EntryType.Credit,
                AmountKobo = command.AmountKobo,
                BalanceAfterKobo = destAfter,
                CreatedAt = now
            };

            // Immutable audit logs
            var sourceAudit = new AuditLog
            {
                Id = Guid.NewGuid(),
                WalletId = sourceWallet.Id,
                TransactionId = transaction.Id,
                EventType = "WalletDebited",
                AmountKobo = command.AmountKobo,
                BalanceBeforeKobo = sourceBefore,
                BalanceAfterKobo = sourceAfter,
                PerformedBy = command.InitiatedBy,
                IpAddress = command.IpAddress,
                CreatedAt = now
            };

            var destAudit = new AuditLog
            {
                Id = Guid.NewGuid(),
                WalletId = destinationWallet.Id,
                TransactionId = transaction.Id,
                EventType = "WalletCredited",
                AmountKobo = command.AmountKobo,
                BalanceBeforeKobo = destBefore,
                BalanceAfterKobo = destAfter,
                PerformedBy = command.InitiatedBy,
                IpAddress = command.IpAddress,
                CreatedAt = now
            };

            _dbContext.Transactions.Add(transaction);
            _dbContext.LedgerEntries.AddRange(debitEntry, creditEntry);
            _dbContext.AuditLogs.AddRange(sourceAudit, destAudit);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Transfer successful: {Amount} kobo from {Source} to {Dest}. Ref: {Ref}",
                command.AmountKobo, sourceWallet.Id, destinationWallet.Id, transaction.Reference);

            return transaction;
        });
    }

    public async Task<PaginatedList<StatementEntryResult>> GetStatementAsync(
        Guid walletId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var exists = await _dbContext.Wallets
            .AsNoTracking()
            .AnyAsync(w => w.Id == walletId, cancellationToken);

        if (!exists)
        {
            throw new WalletNotFoundException(walletId);
        }

        var query = _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new StatementEntryResult(
                e.Id,
                e.TransactionId,
                e.Transaction.Reference,
                e.Transaction.Type,
                e.EntryType,
                e.AmountKobo,
                e.BalanceAfterKobo,
                e.Transaction.Narration,
                e.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedList<StatementEntryResult>(items, totalCount, page, pageSize);
    }

    private static DateOnly GetTodayWatDate()
    {
        TimeZoneInfo watZone;
        try
        {
            watZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                watZone = TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback to UTC+1 offset if system timezone IDs differ
                watZone = TimeZoneInfo.CreateCustomTimeZone("WAT", TimeSpan.FromHours(1), "WAT", "WAT");
            }
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone));
    }
}
