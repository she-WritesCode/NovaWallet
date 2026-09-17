using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NovaWallet.Infrastructure.Data;
using NovaWallet.Infrastructure.Services;
using Xunit;

namespace NovaWallet.Tests;

public class IdempotencyServiceTests
{
    private (AppDbContext db, IdempotencyService service) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        var service = new IdempotencyService(db, NullLogger<IdempotencyService>.Instance);
        return (db, service);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNew_WhenKeyDoesNotExist()
    {
        var (db, service) = CreateTestContext();

        var result = await service.CheckAsync("NEW-KEY-123", "{\"amountKobo\":1000}");

        Assert.False(result.IsMatch);
        Assert.False(result.IsPayloadMismatch);
    }

    [Fact]
    public async Task CheckAsync_ReturnsCachedResponse_OnExactReplay()
    {
        var (db, service) = CreateTestContext();
        const string key = "REPLAY-KEY-456";
        const string payload = "{\"destinationWalletId\":\"c0a80101-0000-0000-0000-000000000001\",\"amountKobo\":5000}";
        const string cachedResponse = "{\"status\":\"Completed\",\"amountKobo\":5000}";

        // Save original execution
        await service.SaveAsync(key, payload, 200, cachedResponse);

        // Replay with identical payload
        var check = await service.CheckAsync(key, payload);

        Assert.True(check.IsMatch);
        Assert.False(check.IsPayloadMismatch);
        Assert.Equal(200, check.StatusCode);
        Assert.Equal(cachedResponse, check.ResponseBody);
    }

    [Fact]
    public async Task CheckAsync_DetectsPayloadMismatch_WhenKeyIsReusedWithDifferentBody()
    {
        var (db, service) = CreateTestContext();
        const string key = "TAMPERED-KEY-789";
        const string originalPayload = "{\"amountKobo\":1000}";
        const string alteredPayload = "{\"amountKobo\":9000}"; // Fraud attempt / tampering

        // Save original execution for ₦10
        await service.SaveAsync(key, originalPayload, 200, "{\"status\":\"Completed\"}");

        // Attempt replay with different payload of ₦90
        var check = await service.CheckAsync(key, alteredPayload);

        Assert.False(check.IsMatch);
        Assert.True(check.IsPayloadMismatch);
    }
}

