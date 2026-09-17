using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Enums;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Models;
using NovaWallet.Infrastructure.Data;
using NovaWallet.Infrastructure.Services;
using Xunit;

namespace NovaWallet.Tests;

public class WalletServiceTests
{
    private (AppDbContext db, WalletService service) CreateTestContext()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var db = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WalletSettings:DailyOutboundLimitKobo"] = "50000000" // ₦500,000
            })
            .Build();

        var service = new WalletService(db, config, NullLogger<WalletService>.Instance);
        return (db, service);
    }

    [Fact]
    public async Task CreateWallet_InitializesZeroBalance_AndActiveStatus()
    {
        var (db, service) = CreateTestContext();

        var wallet = await service.CreateWalletAsync("CUST-100", "NGN");

        Assert.NotNull(wallet);
        Assert.Equal("CUST-100", wallet.CustomerId);
        Assert.Equal("NGN", wallet.Currency);
        Assert.Equal(0L, wallet.AvailableBalanceKobo);
        Assert.Equal(0L, wallet.BookBalanceKobo);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }

    [Fact]
    public async Task CreditWallet_IncreasesBalance_AndRecordsDoubleEntryAuditLog()
    {
        var (db, service) = CreateTestContext();
        var wallet = await service.CreateWalletAsync("CUST-101", "NGN");

        var cmd = new CreditWalletCommand(wallet.Id, 500_000L, "Test Deposit"); // ₦5,000
        var tx = await service.CreditWalletAsync(cmd);

        Assert.NotNull(tx);
        Assert.Equal(TransactionType.Deposit, tx.Type);
        Assert.Equal(TransactionStatus.Completed, tx.Status);

        var balance = await service.GetBalanceAsync(wallet.Id);
        Assert.Equal(500_000L, balance.AvailableBalanceKobo);
        Assert.Equal(500_000L, balance.BookBalanceKobo);

        // Verify Ledger Entry
        var ledgerEntry = await db.LedgerEntries.FirstOrDefaultAsync(l => l.TransactionId == tx.Id);
        Assert.NotNull(ledgerEntry);
        Assert.Equal(EntryType.Credit, ledgerEntry.EntryType);
        Assert.Equal(500_000L, ledgerEntry.AmountKobo);
        Assert.Equal(500_000L, ledgerEntry.BalanceAfterKobo);

        // Verify Audit Log
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.TransactionId == tx.Id);
        Assert.NotNull(audit);
        Assert.Equal("WalletCredited", audit.EventType);
        Assert.Equal(0L, audit.BalanceBeforeKobo);
        Assert.Equal(500_000L, audit.BalanceAfterKobo);
    }

    [Fact]
    public async Task Transfer_MovesFundsBetweenWallets_AndCreatesBalancedEntries()
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-SENDER", "NGN");
        var receiver = await service.CreateWalletAsync("CUST-RECEIVER", "NGN");

        // Credit sender with ₦10,000
        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 1_000_000L));

        // Transfer ₦4,000 to receiver
        var transferCmd = new TransferCommand(sender.Id, receiver.Id, 400_000L, "Dinner split");
        var tx = await service.TransferAsync(transferCmd);

        Assert.NotNull(tx);
        Assert.Equal(TransactionType.Transfer, tx.Type);

        var senderBalance = await service.GetBalanceAsync(sender.Id);
        var receiverBalance = await service.GetBalanceAsync(receiver.Id);

        Assert.Equal(600_000L, senderBalance.AvailableBalanceKobo); // ₦6,000 left
        Assert.Equal(400_000L, receiverBalance.AvailableBalanceKobo); // ₦4,000 received

        // Verify two balanced ledger entries (1 Debit + 1 Credit)
        var entries = await db.LedgerEntries.Where(l => l.TransactionId == tx.Id).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.EntryType == EntryType.Debit && e.WalletId == sender.Id && e.BalanceAfterKobo == 600_000L);
        Assert.Contains(entries, e => e.EntryType == EntryType.Credit && e.WalletId == receiver.Id && e.BalanceAfterKobo == 400_000L);
    }

    [Fact]
    public async Task Transfer_ThrowsInsufficientFundsException_WhenBalanceTooLow()
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-BROKE", "NGN");
        var receiver = await service.CreateWalletAsync("CUST-RICH", "NGN");

        // Sender has ₦1,000 but attempts to send ₦5,000
        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 100_000L));

        var transferCmd = new TransferCommand(sender.Id, receiver.Id, 500_000L);

        var ex = await Assert.ThrowsAsync<InsufficientFundsException>(() => service.TransferAsync(transferCmd));
        Assert.Equal("INSUFFICIENT_FUNDS", ex.ErrorCode);
        Assert.Contains("₦1,000.00", ex.Message);
        Assert.Contains("₦5,000.00", ex.Message);

        // Balance must remain unchanged
        var balance = await service.GetBalanceAsync(sender.Id);
        Assert.Equal(100_000L, balance.AvailableBalanceKobo);
    }

    [Fact]
    public async Task Transfer_ThrowsDailyLimitExceededException_WhenCapped()
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-WHALE", "NGN");
        var receiver = await service.CreateWalletAsync("CUST-TARGET", "NGN");

        // Credit sender with ₦1,000,000 (100,000,000 kobo)
        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 100_000_000L));

        // Daily limit is ₦500,000. Attempting to send ₦600,000
        var transferCmd = new TransferCommand(sender.Id, receiver.Id, 60_000_000L);

        var ex = await Assert.ThrowsAsync<DailyLimitExceededException>(() => service.TransferAsync(transferCmd));
        Assert.Equal("DAILY_LIMIT_EXCEEDED", ex.ErrorCode);
        Assert.Contains("₦500,000.00", ex.Message);
    }

    [Fact]
    public async Task Transfer_ThrowsSameWalletTransferException_WhenSourceEqualsDestination()
    {
        var (db, service) = CreateTestContext();
        var wallet = await service.CreateWalletAsync("CUST-SELF", "NGN");

        var cmd = new TransferCommand(wallet.Id, wallet.Id, 50_000L);

        var ex = await Assert.ThrowsAsync<SameWalletTransferException>(() => service.TransferAsync(cmd));
        Assert.Equal("SELF_TRANSFER_NOT_ALLOWED", ex.ErrorCode);
    }

    [Theory]
    [InlineData(-40_000L)]
    [InlineData(0L)]
    public async Task CreditWallet_ThrowsInvalidAmountException_WhenAmountIsZeroOrNegative(long amountKobo)
    {
        var (db, service) = CreateTestContext();
        var wallet = await service.CreateWalletAsync("CUST-CREDIT-TEST", "NGN");

        var cmd = new CreditWalletCommand(wallet.Id, amountKobo, "NIP_CREDIT");

        var ex = await Assert.ThrowsAsync<InvalidAmountException>(() => service.CreditWalletAsync(cmd));
        Assert.Equal("INVALID_AMOUNT", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData(-40_000L)]
    [InlineData(0L)]
    public async Task Transfer_ThrowsInvalidAmountException_WhenAmountIsZeroOrNegative(long amountKobo)
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-SENDER", "NGN");
        var receiver = await service.CreateWalletAsync("CUST-RECEIVER", "NGN");

        // Pre-fund sender to confirm rejection is due to amount validation, not insufficient funds
        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 100_000L, "INITIAL_FUNDING"));

        var cmd = new TransferCommand(sender.Id, receiver.Id, amountKobo);

        var ex = await Assert.ThrowsAsync<InvalidAmountException>(() => service.TransferAsync(cmd));
        Assert.Equal("INVALID_AMOUNT", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateWallet_ThrowsDuplicateWalletException_WhenWalletAlreadyExistsForCurrency()
    {
        var (db, service) = CreateTestContext();
        await service.CreateWalletAsync("CUST-DUP-1", "NGN");

        var ex = await Assert.ThrowsAsync<DuplicateWalletException>(() => service.CreateWalletAsync("CUST-DUP-1", "NGN"));
        Assert.Equal("DUPLICATE_WALLET", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Transfer_ThrowsCurrencyMismatchException_WhenCurrenciesDiffer()
    {
        var (db, service) = CreateTestContext();
        var ngnWallet = await service.CreateWalletAsync("CUST-NGN", "NGN");
        var usdWallet = await service.CreateWalletAsync("CUST-USD", "USD");

        await service.CreditWalletAsync(new CreditWalletCommand(ngnWallet.Id, 100_000L));

        var cmd = new TransferCommand(ngnWallet.Id, usdWallet.Id, 50_000L);

        var ex = await Assert.ThrowsAsync<CurrencyMismatchException>(() => service.TransferAsync(cmd));
        Assert.Equal("CURRENCY_MISMATCH", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("NGN", ex.Message);
        Assert.Contains("USD", ex.Message);
    }

    [Fact]
    public async Task Transfer_ThrowsWalletFrozenException_WhenSourceOrDestinationIsFrozen()
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-FROZEN-SENDER", "NGN");
        var receiver = await service.CreateWalletAsync("CUST-ACTIVE-RECEIVER", "NGN");

        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 100_000L));

        // Freeze sender wallet
        sender.Status = WalletStatus.Frozen;
        await db.SaveChangesAsync();

        var cmd = new TransferCommand(sender.Id, receiver.Id, 50_000L);

        var ex = await Assert.ThrowsAsync<WalletFrozenException>(() => service.TransferAsync(cmd));
        Assert.Equal("WALLET_FROZEN", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task CreditWallet_ThrowsWalletFrozenException_WhenWalletIsFrozen()
    {
        var (db, service) = CreateTestContext();
        var wallet = await service.CreateWalletAsync("CUST-FROZEN-CREDIT", "NGN");

        wallet.Status = WalletStatus.Frozen;
        await db.SaveChangesAsync();

        var cmd = new CreditWalletCommand(wallet.Id, 50_000L);

        var ex = await Assert.ThrowsAsync<WalletFrozenException>(() => service.CreditWalletAsync(cmd));
        Assert.Equal("WALLET_FROZEN", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task Transfer_ThrowsWalletNotFoundException_WhenDestinationDoesNotExist()
    {
        var (db, service) = CreateTestContext();
        var sender = await service.CreateWalletAsync("CUST-EXISTS", "NGN");
        var nonExistentId = Guid.NewGuid();

        await service.CreditWalletAsync(new CreditWalletCommand(sender.Id, 100_000L));

        var cmd = new TransferCommand(sender.Id, nonExistentId, 50_000L);

        var ex = await Assert.ThrowsAsync<WalletNotFoundException>(() => service.TransferAsync(cmd));
        Assert.Equal("WALLET_NOT_FOUND", ex.ErrorCode);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetBalance_ThrowsWalletNotFoundException_WhenWalletDoesNotExist()
    {
        var (db, service) = CreateTestContext();
        var nonExistentId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<WalletNotFoundException>(() => service.GetBalanceAsync(nonExistentId));
        Assert.Equal("WALLET_NOT_FOUND", ex.ErrorCode);
        Assert.Equal(404, ex.StatusCode);
    }
}

