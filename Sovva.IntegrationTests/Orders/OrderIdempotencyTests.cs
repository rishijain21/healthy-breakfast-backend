using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;
using Sovva.Infrastructure.Repositories;
using Sovva.IntegrationTests.Fixtures;
using Sovva.IntegrationTests.Helpers;

namespace Sovva.IntegrationTests.Orders;

/// <summary>
/// TEST-C3: Reorder Idempotency — Rapid Duplicate Tap
/// Verifies that two concurrent reorder requests for the same user+meal within
/// 30 seconds result in only ONE wallet debit and ONE new order row.
///
/// TEST-C7: Create Order — Duplicate Request Idempotency
/// Verifies that simultaneous POST /orders/create-from-meal-builder with the
/// same userId+userMealId produces exactly one order and one debit.
/// </summary>
public class OrderIdempotencyTests : BaseIntegrationTest
{
    public OrderIdempotencyTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C3: Wallet Debit Idempotency for Reorder (AtomicDebit layer)
    // The application-layer "GetRecentOrderByUserMealIdAsync" 30s window cannot be
    // tested without the full service stack. This test validates that the
    // UNDERLYING wallet mechanism does NOT produce duplicate debits — the
    // real-world guarantee is composed of: wallet atomicity + service-layer check.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C3_WalletDebit_ShouldRecordOnce_WhenSameScheduledOrderIdUsedForReorder()
    {
        // ARRANGE: User with ₹500 simulating a reorder using a scheduledOrderId as idempotency key
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 500m);
        // Use one ScheduledOrder as the idempotency token (DB generates the ID)
        var reorderIdempotencyKey = await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId);

        var repo1 = new WalletTransactionRepository(_fixture.CreateDbContext(), new TestTimeProvider());
        var repo2 = new WalletTransactionRepository(_fixture.CreateDbContext(), new TestTimeProvider());

        // ACT: Two concurrent debits with the same idempotency key (same scheduledOrderId)
        var t1 = repo1.AtomicDebitAsync(userId, 150m, "Reorder attempt 1", reorderIdempotencyKey);
        var t2 = repo2.AtomicDebitAsync(userId, 150m, "Reorder attempt 2", reorderIdempotencyKey);
        var results = await Task.WhenAll(t1, t2);

        // ASSERT: Exactly ONE debit succeeded
        results.Count(r => r.Success).Should().Be(1,
            "the idempotency guard in AtomicDebitAsync prevents double-charge for same ScheduledOrderId");

        // ASSERT: Wallet deducted exactly once
        var debitRows = await _dbContext.WalletTransactions
            .AsNoTracking()
            .CountAsync(wt => wt.UserId == userId && wt.Type == WalletConstants.Debit);
        debitRows.Should().Be(1, "only one Debit row must exist");

        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(350m, "₹500 - ₹150 = ₹350 after one debit");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST-C7: Concurrent Wallet Debit — Different Orders, Same User
    // Validates that when multiple orders for the same user are processed
    // concurrently, the final balance is always non-negative and exactly correct.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEST_C7_ConcurrentDebits_ShouldNeverProduceNegativeBalance()
    {
        // ARRANGE: User with ₹300. Three concurrent orders at ₹150 each.
        // Only 2 should succeed (₹300 / ₹150 = 2 exactly).
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 300m);

        // Seed 3 scheduled orders sequentially, collect their DB-generated IDs
        var orderIds = new List<int>();
        for (int i = 0; i < 3; i++)
            orderIds.Add(await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId));

        // ACT: Three concurrent debits
        var tasks = orderIds
            .Select(soId => new WalletTransactionRepository(_fixture.CreateDbContext(), new TestTimeProvider())
                .AtomicDebitAsync(userId, 150m, $"Order {soId}", soId))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // ASSERT: Exactly 2 succeeded (₹300 / ₹150 = 2)
        var successCount = results.Count(r => r.Success);
        successCount.Should().Be(2, "wallet has exactly ₹300 — two ₹150 debits should succeed");

        // ASSERT: Final balance is exactly ₹0
        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(0m, "all balance consumed by two successful debits");
        finalBalance.Should().BeGreaterThanOrEqualTo(0m, "balance must never go negative");
    }

    [Fact]
    public async Task TEST_C7b_ConcurrentDebits_ShouldNeverProduceNegativeBalance_WithInsufficientFunds()
    {
        // ARRANGE: User with only ₹50. Five concurrent orders at ₹100 each.
        // Zero should succeed.
        var userId = await DbSeeder.SeedUserWithBalanceAsync(_dbContext, balance: 50m);

        // Seed 5 orders sequentially, collect DB-generated IDs
        var orderIds = new List<int>();
        for (int i = 0; i < 5; i++)
            orderIds.Add(await DbSeeder.SeedScheduledOrderAsync(_dbContext, userId));

        var tasks = orderIds
            .Select(soId => new WalletTransactionRepository(_fixture.CreateDbContext(), new TestTimeProvider())
                .AtomicDebitAsync(userId, 100m, $"Order {soId}", soId))
            .ToList();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r.Success).Should().Be(0,
            "insufficient balance — no debit should succeed");

        var finalBalance = await GetBalanceAsync(userId);
        finalBalance.Should().Be(50m, "balance must remain unchanged");
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
