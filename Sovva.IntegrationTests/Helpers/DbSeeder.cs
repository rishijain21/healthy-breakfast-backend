using Microsoft.EntityFrameworkCore;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Infrastructure.Data;

namespace Sovva.IntegrationTests.Helpers;

/// <summary>
/// Seed helpers for integration tests. Each method returns the ID of the created entity.
///
/// Design notes:
/// - User.UserId and ScheduledOrder.ScheduledOrderId are Postgres SERIAL (auto-generated).
///   We DO NOT set them explicitly — we let Postgres generate them.
/// - SeedScheduledOrderAsync returns the DB-assigned ScheduledOrderId.
/// - CleanAsync uses DELETE (not TRUNCATE) so sequences are not reset between tests
///   (which avoids FK conflicts when multiple test classes run in the same session).
/// </summary>
public static class DbSeeder
{
    private static int _phoneCounter = 100000000; // 9-digit base, safe for int

    /// <summary>
    /// Creates a user and seeds wallet transactions to produce the given balance.
    /// Returns the DB-assigned UserId.
    /// </summary>
    public static async Task<int> SeedUserWithBalanceAsync(AppDbContext ctx, decimal balance)
    {
        // Generate a unique phone number for this test user
        var phone = Interlocked.Increment(ref _phoneCounter).ToString("D10"); // 10-digit phone
        var tag   = Guid.NewGuid().ToString("N")[..8];

        var user = new User
        {
            // UserId: do NOT set — let Postgres generate via SERIAL
            Name          = $"TestUser_{tag}",
            Email         = $"test_{tag}@sovva-test.com",
            Phone         = phone,
            Role          = UserRole.Customer,
            AccountStatus = AccountStatus.Active,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(); // After this, user.UserId is populated by Postgres

        if (balance > 0)
        {
            ctx.WalletTransactions.Add(new WalletTransaction
            {
                UserId      = user.UserId,
                Amount      = balance,
                Type        = WalletConstants.Credit,
                Description = "Seed balance",
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        return user.UserId;
    }

    /// <summary>
    /// Seeds a minimal ScheduledOrder so AtomicDebitAsync FK constraints are satisfied.
    /// Returns the DB-assigned ScheduledOrderId.
    /// </summary>
    public static async Task<int> SeedScheduledOrderAsync(AppDbContext ctx, int userId)
    {
        var order = new ScheduledOrder
        {
            // ScheduledOrderId: do NOT set — let Postgres generate via SERIAL
            UserId             = userId,
            AuthId             = Guid.NewGuid(),
            MealName           = "Test Oats",
            ScheduledFor       = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DeliveryTimeSlot   = "10:00 AM",
            TotalPrice         = 100m,
            OrderStatus        = ScheduledOrderStatus.Scheduled,
            ExpiresAt          = DateTime.UtcNow.AddHours(12),
            IsProcessedToOrder = false,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        ctx.ScheduledOrders.Add(order);
        await ctx.SaveChangesAsync(); // After this, order.ScheduledOrderId is populated
        return order.ScheduledOrderId;
    }

    /// <summary>
    /// Removes all test data between test runs using DELETE (preserves sequences).
    /// Order respects FK constraints.
    /// </summary>
    public static async Task CleanAsync(AppDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""FailedOrderAttempts"";
            DELETE FROM ""ScheduledOrderIngredients"";
            DELETE FROM ""WalletTransactions"";
            DELETE FROM ""OrderItems"";
            DELETE FROM ""Orders"";
            DELETE FROM ""ScheduledOrders"";
            DELETE FROM ""SubscriptionSchedules"";
            DELETE FROM ""Subscriptions"";
            DELETE FROM ""UserMealIngredients"";
            DELETE FROM ""UserMeals"";
            DELETE FROM ""UserAddresses"";
            DELETE FROM ""Users"";
        ");
    }
}

