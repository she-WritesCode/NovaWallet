using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NovaWallet.Domain.Entities;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Models;
using NovaWallet.Infrastructure.Data;
using NovaWallet.Infrastructure.Services;
using Xunit;

namespace NovaWallet.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentTransfers_NeverAllowBalanceToGoNegative_UnderHeavyLoad()
    {
        // ARRANGE
        var dbName = "ConcurrencyTestDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WalletSettings:DailyOutboundLimitKobo"] = "50000000" // ₦500k limit
            })
            .Build();

        Guid sourceWalletId;
        Guid destinationWalletId;

        // Seed wallets with starting funds: Source has ₦10,000 (1,000,000 kobo), Destination has ₦0
        using (var setupDb = new AppDbContext(options))
        {
            var service = new WalletService(setupDb, config, NullLogger<WalletService>.Instance);
            var source = await service.CreateWalletAsync("CUST-ALICE-CONCURRENT", "NGN");
            var dest = await service.CreateWalletAsync("CUST-BOB-CONCURRENT", "NGN");

            // Credit Alice with exactly ₦10,000 (1,000,000 kobo)
            await service.CreditWalletAsync(new CreditWalletCommand(source.Id, 1_000_000L));

            sourceWalletId = source.Id;
            destinationWalletId = dest.Id;
        }

        // ACT: Fire 20 parallel transfer tasks of ₦1,000 (100,000 kobo) each simultaneously.
        // Total requested = 20 x ₦1,000 = ₦20,000 (Alice only has ₦10,000).
        const int concurrentRequests = 20;
        const long transferAmountKobo = 100_000L; // ₦1,000

        var successfulTransfers = new ConcurrentBag<Transaction>();
        var failedExceptions = new ConcurrentBag<Exception>();

        // We use a semaphore / barrier to simulate all 20 threads releasing at the exact same millisecond
        var barrier = new SemaphoreSlim(0, concurrentRequests);

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            // Each concurrent worker gets its own DbContext instance (mirroring real ASP.NET Core scoped DI per HTTP request)
            using var db = new AppDbContext(options);
            var service = new WalletService(db, config, NullLogger<WalletService>.Instance);

            var cmd = new TransferCommand(
                sourceWalletId,
                destinationWalletId,
                transferAmountKobo,
                $"Concurrent burst transfer #{i}",
                $"BURST-{i}");

            // Wait for all tasks to be spawned before hammering the wallet
            await barrier.WaitAsync();

            try
            {
                var tx = await service.TransferAsync(cmd);
                successfulTransfers.Add(tx);
            }
            catch (Exception ex)
            {
                failedExceptions.Add(ex);
            }
        }).ToList();

        // Release all 20 tasks concurrently!
        barrier.Release(concurrentRequests);
        await Task.WhenAll(tasks);

        // ASSERT: Strict financial invariants must hold under load!
        using (var verifyDb = new AppDbContext(options))
        {
            var service = new WalletService(verifyDb, config, NullLogger<WalletService>.Instance);
            var aliceBalance = await service.GetBalanceAsync(sourceWalletId);
            var bobBalance = await service.GetBalanceAsync(destinationWalletId);

            // Invariant 1: Exactly 10 transfers must succeed (10 x ₦1,000 = ₦10,000)
            Assert.Equal(10, successfulTransfers.Count);

            // Invariant 2: Exactly 10 transfers must fail with InsufficientFundsException
            Assert.Equal(10, failedExceptions.Count);
            Assert.All(failedExceptions, ex => Assert.IsType<InsufficientFundsException>(ex));

            // Invariant 3: Alice's balance must be EXACTLY 0 kobo (NEVER negative!)
            Assert.Equal(0L, aliceBalance.AvailableBalanceKobo);
            Assert.Equal(0L, aliceBalance.BookBalanceKobo);

            // Invariant 4: Bob must have received exactly ₦10,000 (1,000,000 kobo)
            Assert.Equal(1_000_000L, bobBalance.AvailableBalanceKobo);
            Assert.Equal(1_000_000L, bobBalance.BookBalanceKobo);

            // Invariant 5: Conservation of Money: Total system money must equal 1,000,000 kobo
            var totalSystemMoney = aliceBalance.AvailableBalanceKobo + bobBalance.AvailableBalanceKobo;
            Assert.Equal(1_000_000L, totalSystemMoney);

            // Invariant 6: Double-entry ledger entries must be balanced
            var debits = await verifyDb.LedgerEntries.Where(l => l.WalletId == sourceWalletId && l.EntryType == Domain.Enums.EntryType.Debit).ToListAsync();
            var credits = await verifyDb.LedgerEntries.Where(l => l.WalletId == destinationWalletId && l.EntryType == Domain.Enums.EntryType.Credit).ToListAsync();

            Assert.Equal(10, debits.Count);
            Assert.Equal(10, credits.Count);
            Assert.Equal(1_000_000L, debits.Sum(d => d.AmountKobo));
            Assert.Equal(1_000_000L, credits.Sum(c => c.AmountKobo));
        }
    }

    [Fact]
    public async Task BiDirectionalConcurrentTransfers_NeverDeadlock_AndConserveTotalSystemBalance()
    {
        // ARRANGE: Alice and Bob each start with ₦50,000 (5,000,000 kobo)
        var dbName = "DeadlockTestDb_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WalletSettings:DailyOutboundLimitKobo"] = "50000000" // ₦500k limit
            })
            .Build();

        Guid aliceWalletId;
        Guid bobWalletId;

        using (var setupDb = new AppDbContext(options))
        {
            var service = new WalletService(setupDb, config, NullLogger<WalletService>.Instance);
            var alice = await service.CreateWalletAsync("CUST-ALICE-DEADLOCK", "NGN");
            var bob = await service.CreateWalletAsync("CUST-BOB-DEADLOCK", "NGN");

            await service.CreditWalletAsync(new CreditWalletCommand(alice.Id, 5_000_000L));
            await service.CreditWalletAsync(new CreditWalletCommand(bob.Id, 5_000_000L));

            aliceWalletId = alice.Id;
            bobWalletId = bob.Id;
        }

        // ACT: 10 threads transfer Alice -> Bob, while 10 threads transfer Bob -> Alice simultaneously
        const int requestsPerDirection = 10;
        const int totalRequests = requestsPerDirection * 2;
        const long transferAmountKobo = 200_000L; // ₦2,000 per transfer

        var successfulTransfers = new ConcurrentBag<Transaction>();
        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new SemaphoreSlim(0, totalRequests);

        // 10 tasks: Alice -> Bob
        var aliceToBobTasks = Enumerable.Range(0, requestsPerDirection).Select(async i =>
        {
            using var db = new AppDbContext(options);
            var service = new WalletService(db, config, NullLogger<WalletService>.Instance);

            var cmd = new TransferCommand(aliceWalletId, bobWalletId, transferAmountKobo, $"Alice to Bob #{i}", $"A2B-{i}");
            await barrier.WaitAsync();

            try
            {
                var tx = await service.TransferAsync(cmd);
                successfulTransfers.Add(tx);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // 10 tasks: Bob -> Alice
        var bobToAliceTasks = Enumerable.Range(0, requestsPerDirection).Select(async i =>
        {
            using var db = new AppDbContext(options);
            var service = new WalletService(db, config, NullLogger<WalletService>.Instance);

            var cmd = new TransferCommand(bobWalletId, aliceWalletId, transferAmountKobo, $"Bob to Alice #{i}", $"B2A-{i}");
            await barrier.WaitAsync();

            try
            {
                var tx = await service.TransferAsync(cmd);
                successfulTransfers.Add(tx);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var allTasks = aliceToBobTasks.Concat(bobToAliceTasks).ToList();

        // Release all 20 threads simultaneously
        barrier.Release(totalRequests);

        // Timeout safety: if deadlock occurs, Task.WhenAny will trigger timeout failure
        var completedTask = await Task.WhenAny(Task.WhenAll(allTasks), Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(completedTask != Task.Delay(TimeSpan.FromSeconds(15)), "Deadlock detected! Bi-directional transfers timed out.");

        // ASSERT: Zero exceptions, all 20 completed cleanly
        Assert.Empty(exceptions);
        Assert.Equal(totalRequests, successfulTransfers.Count);

        using (var verifyDb = new AppDbContext(options))
        {
            var service = new WalletService(verifyDb, config, NullLogger<WalletService>.Instance);
            var alice = await service.GetBalanceAsync(aliceWalletId);
            var bob = await service.GetBalanceAsync(bobWalletId);

            // Since net transfer is 0 (10 sent, 10 received of identical amount), balances should remain ₦50,000
            Assert.Equal(5_000_000L, alice.AvailableBalanceKobo);
            Assert.Equal(5_000_000L, bob.AvailableBalanceKobo);

            // Conservation of money: total remains exactly ₦100,000
            Assert.Equal(10_000_000L, alice.AvailableBalanceKobo + bob.AvailableBalanceKobo);

            // Exactly 20 Debit entries and 20 Credit entries
            var totalDebits = await verifyDb.LedgerEntries.CountAsync(e => e.EntryType == Domain.Enums.EntryType.Debit);
            var totalCredits = await verifyDb.LedgerEntries.CountAsync(e => e.EntryType == Domain.Enums.EntryType.Credit);
            Assert.Equal(20, totalDebits);
            Assert.Equal(20 + 2, totalCredits); // +2 from initial funding credits
        }
    }
}

