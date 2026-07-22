using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;
using Sovva.Infrastructure.Repositories;
using Sovva.IntegrationTests.Fixtures;
using Sovva.IntegrationTests.Helpers;

namespace Sovva.IntegrationTests.Financial;

/// <summary>
/// TEST-C4: Midnight Job — Idempotent Re-Execution
/// Verifies that running ConfirmAllScheduledOrdersAsync twice for the same date
/// produces exactly the same financial outcome as running it once.
///
/// TEST-C5: Concurrent Scheduled Order Confirmation
/// Verifies that all orders are confirmed even when processed in parallel
/// with SemaphoreSlim concurrency.
/// </summary>
public class MidnightJobTests : BaseIntegrationTest
{
    public MidnightJobTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C4: Idempotent Job Re-Execution
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C4_AtomicDebit_ShouldBeIdempotent_WhenSameScheduledOrderProcessedTwice()
    {
        // ARRANGE: User with ₹500. One scheduled order at ₹100.
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 500m);
        var scheduledOrderId = await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId);

        var repo = new WalletTransactionRepository(_dbContext, new TestTimeProvider());

        // ACT: Run the debit twice (simulating Hangfire retry)
        var result1 = await repo.AtomicDebitAsync(userId, 100m, "Midnight debit", scheduledOrderId);
        var result2 = await repo.AtomicDebitAsync(userId, 100m, "Midnight debit retry", scheduledOrderId);

        // ASSERT: Only the FIRST succeeded — the second was blocked by the idempotency guard
        result1.Success.Should().BeTrue("first debit must succeed");
        result2.Success.Should().BeFalse("retry must be blocked — same scheduledOrderId already has a debit");

        // ASSERT: Exactly one Debit row exists
        var debitCount = await _dbContext.WalletTransactions
            .CountAsync(wt => wt.UserId == userId && wt.Type == WalletConstants.Debit);
        debitCount.Should().Be(1, "retry must not produce a second debit row");

        // ASSERT: Final balance is ₹400 (₹500 - ₹100), not ₹300
        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(400m, "balance must reflect exactly one debit");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C5: Concurrent Scheduled Order Processing
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C5_AtomicDebit_ShouldProcessAllOrders_WhenConcurrentWithSemaphore()
    {
        // ARRANGE: 20 users, each with ₹200 balance, each with a unique ScheduledOrder
        const int orderCount = 20;
        const decimal orderAmount = 100m;

        var userIds = new List<int>();
        var scheduledOrderIds = new List<int>();

        for (int i = 0; i < orderCount; i++)
        {
            var uid = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 200m);
            var soId = await DbSeeder.SeedScheduledOrderAsync(_dbContext, uid);
            userIds.Add(uid);
            scheduledOrderIds.Add(soId);
        }

        // ACT: Process all 20 orders concurrently (SemaphoreSlim(10) mirrors production)
        var semaphore = new SemaphoreSlim(10, 10);
        var results = await Task.WhenAll(
            Enumerable.Range(0, orderCount).Select(async i =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var repo = new WalletTransactionRepository(_fixture.CreateDbContext(), new TestTimeProvider());
                    return await repo.AtomicDebitAsync(userIds[i], orderAmount, $"Order {scheduledOrderIds[i]}", scheduledOrderIds[i]);
                }
                finally
                {
                    semaphore.Release();
                }
            })
        );

        // ASSERT: All 20 succeeded (each user had sufficient independent balance)
        var successCount = results.Count(r => r.Success);
        successCount.Should().Be(orderCount,
            "every user had sufficient balance — all debits must succeed");

        // ASSERT: Each user has exactly ₹100 remaining
        foreach (var uid in userIds)
        {
            var balance = await GetBalanceAsync(uid);
            balance.Should().Be(200m - orderAmount,
                $"user {uid} should have ₹{200m - orderAmount} after one debit");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<decimal> GetBalanceAsync(int userId)
    {
        return await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(wt => wt.UserId == userId)
            .SumAsync(wt => wt.Type == WalletConstants.Credit ? wt.Amount : -wt.Amount);
    }
}
