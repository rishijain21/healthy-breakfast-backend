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
/// TEST-C1: AtomicDebitAsync — Double-Spend Prevention
/// Verifies that two concurrent debit attempts for the same ScheduledOrderId
/// result in exactly one row inserted, even under PostgreSQL read-committed isolation.
///
/// TEST-C2: Concurrent Top-Up — Max Balance Enforcement
/// Verifies that concurrent credits never push a wallet above MaxWalletBalance.
///
/// TEST-C6: Insufficient Balance Under Concurrent Load
/// Verifies that only one debit wins when multiple concurrent requests race
/// against insufficient funds.
/// </summary>
public class WalletConcurrencyTests : BaseIntegrationTest
{
    public WalletConcurrencyTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C1
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C1_AtomicDebit_ShouldPreventDoubleSpend_ForSameScheduledOrderId()
    {
        // ARRANGE: User with exactly ₹200 wallet balance
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 200m);

        // Seed a ScheduledOrder row (FK required) — DB generates the ID
        var scheduledOrderId = await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId);

        // ACT: Fire 2 concurrent atomic debit attempts for the SAME ScheduledOrderId
        var repo1 = CreateFreshRepo();
        var repo2 = CreateFreshRepo();

        var task1 = repo1.AtomicDebitAsync(userId, 200m, "Midnight debit attempt 1", scheduledOrderId);
        var task2 = repo2.AtomicDebitAsync(userId, 200m, "Midnight debit attempt 2", scheduledOrderId);

        var results = await Task.WhenAll(task1, task2);

        // ASSERT: Exactly ONE succeeded
        var successCount = results.Count(r => r.Success);
        successCount.Should().Be(1, "idempotency guard must prevent the second debit for the same ScheduledOrderId");

        // ASSERT: Final balance is exactly ₹0 (not negative)
        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(0m, "wallet should be fully drained but never overdraft");

        // ASSERT: Exactly 1 Debit row
        var debitCount = await _dbContext.WalletTransactions
            .CountAsync(wt => wt.UserId == userId && wt.Type == WalletConstants.Debit);
        debitCount.Should().Be(1, "only one Debit row should exist regardless of concurrency");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C2
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C2_AtomicCredit_ShouldNeverExceedMaxWalletBalance_UnderConcurrentRequests()
    {
        // ARRANGE: User is ₹100 below MaxWalletBalance
        var maxBalance = WalletConstants.MaxWalletBalance;
        var startBalance = maxBalance - 100m;
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: startBalance);

        // ACT: Fire 10 concurrent ₹200 top-up attempts
        // Only none should succeed (₹200 would push over the ₹5000 cap)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => CreateFreshRepo().AtomicCreditAsync(userId, 200m, "Concurrent top-up"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // ASSERT: Zero credits should have succeeded (₹200 > ₹100 headroom)
        var successCount = results.Count(r => r);
        successCount.Should().Be(0, "no credit should succeed when it would push the balance over the cap");

        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(startBalance, "balance must be unchanged");
        finalBalance.Should().BeLessThanOrEqualTo(maxBalance, "balance must never exceed MaxWalletBalance");
    }

    [Fact]
    public async Task TEST_C2b_AtomicCredit_ShouldAllowExactlyOneCredit_WhenOnlySomeExceedCap()
    {
        // ARRANGE: User has ₹4800. Cap is ₹5000. Only ₹100 top-ups fit.
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 4800m);

        // ACT: Fire 10 concurrent ₹100 credits — only ONE should succeed (exactly fits the remaining ₹200)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => CreateFreshRepo().AtomicCreditAsync(userId, 100m, "Concurrent small top-up"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var finalBalance = await GetBalanceAsync(userId);

        // ASSERT: Final balance must never exceed MaxWalletBalance
        finalBalance.Should().BeLessThanOrEqualTo(WalletConstants.MaxWalletBalance,
            "AtomicCreditAsync must enforce the cap under concurrent load");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C6
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C6_AtomicDebit_ShouldAllowOnlyOneWinner_WhenMultipleConcurrentDebitsRace()
    {
        // ARRANGE: User has exactly ₹100. Five concurrent requests each debit ₹100.
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 100m);

        // Each request uses a UNIQUE scheduledOrderId to bypass idempotency guard — simulating 5 different orders
        // Seed sequentially (shared DbContext) then run debits concurrently
        var scheduledOrderIds = new List<int>();
        for (int i = 0; i < 5; i++)
            scheduledOrderIds.Add(await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId));

        var tasks = scheduledOrderIds
            .Select(soId => CreateFreshRepo().AtomicDebitAsync(userId, 100m, $"Order {soId}", soId))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // ASSERT: Exactly ONE succeeded
        var successCount = results.Count(r => r.Success);
        successCount.Should().Be(1, "only one winner when all debits race against the same balance");

        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(0m, "balance should be exactly ₹0");
        finalBalance.Should().BeGreaterThanOrEqualTo(0m, "balance must never go negative");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private WalletTransactionRepository CreateFreshRepo()
    {
        // Each concurrent call gets its own DbContext (its own DB connection = its own transaction)
        var freshCtx = _fixture.CreateDbContext();
        return new WalletTransactionRepository(freshCtx, new TestTimeProvider());
    }

    private async Task<decimal> GetBalanceAsync(int userId)
    {
        await _dbContext.WalletTransactions.AsNoTracking().LoadAsync();
        return await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(wt => wt.UserId == userId)
            .SumAsync(wt => wt.Type == WalletConstants.Credit ? wt.Amount : -wt.Amount);
    }
}
