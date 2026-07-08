// Sovva.Infrastructure/Repositories/SubscriptionRepository.cs

using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    internal class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public SubscriptionRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<IEnumerable<Subscription>> GetAllAsync(int page = 1, int pageSize = 50)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal) // ✅ ADDED
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .OrderByDescending(s => s.SubscriptionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Subscription> Items, int TotalCount)> GetAllWithCountAsync(int page = 1, int pageSize = 50)
        {
            var query = _context.Subscriptions.AsNoTracking();

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal)
                .Include(s => s.WeeklySchedule)
                .OrderByDescending(s => s.SubscriptionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Subscription?> GetByIdAsync(int subscriptionId)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal) // ✅ ADDED
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        }

        public async Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal) // ✅ ADDED
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync()
        {
            var today = _time.TodayIst;  // ✅ Use IST instead of UTC
            return await _context.Subscriptions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(s => s.User)
                    .ThenInclude(u => u.AuthMapping)  // ✅ Important for scheduling
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal) // ✅ ADDED
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .Where(s => s.IsActive && s.StartDate <= today && s.EndDate >= today)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subscription>> GetExpiredActiveSubscriptionsAsync(DateOnly today)
        {
            return await _context.Subscriptions
                .Where(s => s.IsActive && s.EndDate < today)
                .ToListAsync();
        }

        public async Task<Subscription> CreateAsync(Subscription subscription)
        {
            // CreatedAt/UpdatedAt handled by TimestampInterceptor
            try
            {
                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();
                return subscription;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
            {
                // ✅ SOLID-2 FIX: Catch infrastructure-specific duplicate key violation here (Infrastructure layer)
                // and translate to a domain exception that the Application layer understands.
                throw new Sovva.Application.Exceptions.DuplicateSubscriptionException();
            }
        }

public async Task<Subscription> UpdateAsync(Subscription subscription)
{
    // Detach any existing tracked instance to avoid identity conflict
    // This happens when batch-loaded entities are still in the DbContext tracker
    var tracked = _context.ChangeTracker.Entries<Subscription>()
        .FirstOrDefault(e => e.Entity.SubscriptionId == subscription.SubscriptionId);

    if (tracked != null)
        tracked.State = EntityState.Detached;

    _context.Subscriptions.Update(subscription);
    await _context.SaveChangesAsync();
    return subscription;
}
        /// <summary>
        /// ✅ NEW: Batch update multiple subscriptions in a single transaction
        /// </summary>
        public async Task UpdateBatchAsync(IEnumerable<Subscription> subscriptions)
        {
            // UpdatedAt handled by TimestampInterceptor
            foreach (var subscription in subscriptions)
            {
                _context.Subscriptions.Update(subscription);
            }
            
            // Single SaveChanges for all updates - much more efficient
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int subscriptionId)
        {
            // Note: No Include(WeeklySchedule) — child schedule rows are kept as
            // historical data for analytics. They are orphaned but harmless.
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (subscription == null)
                return false;

            // The TimestampInterceptor intercepts this Remove() call and converts it
            // to: subscription.DeletedAt = now (soft delete). No physical row is deleted.
            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Admin/analytics method to query soft-deleted subscriptions.
        /// Uses IgnoreQueryFilters() to bypass the DeletedAt filter.
        /// </summary>
        public async Task<IEnumerable<Subscription>> GetCancelledSubscriptionsAsync(int daysAgo = 30)
        {
            var cutoff = _time.UtcNow.AddDays(-daysAgo);
            return await _context.Subscriptions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.Meal)
                .Include(s => s.UserMeal)
                .Where(s => s.DeletedAt != null && s.DeletedAt >= cutoff)
                .OrderByDescending(s => s.DeletedAt)
                .ToListAsync();
        }

        // ✅ NEW: Schedule management methods
        public async Task<IEnumerable<SubscriptionSchedule>> GetSchedulesBySubscriptionIdAsync(int subscriptionId)
        {
            return await _context.Set<SubscriptionSchedule>()
                .AsNoTracking()
                .Where(s => s.SubscriptionId == subscriptionId)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();
        }

        public async Task AddSchedulesAsync(int subscriptionId, IEnumerable<SubscriptionSchedule> schedules)
        {
            foreach (var schedule in schedules)
            {
                schedule.SubscriptionId = subscriptionId;
                // CreatedAt/UpdatedAt handled by TimestampInterceptor
            }
            
            await _context.Set<SubscriptionSchedule>().AddRangeAsync(schedules);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveSchedulesAsync(int subscriptionId)
        {
            // ✅ FIX: Avoid AsNoTracking + RemoveRange on detached entities.
            // Prefer a direct SQL DELETE for reliability and performance.
            try
            {
                await _context.Set<SubscriptionSchedule>()
                    .Where(s => s.SubscriptionId == subscriptionId)
                    .ExecuteDeleteAsync();

                return;
            }
            catch (NotSupportedException)
            {
                // Fallback for providers/versions that don't support ExecuteDeleteAsync
            }

            var schedules = await _context.Set<SubscriptionSchedule>()
                .Where(s => s.SubscriptionId == subscriptionId)
                .ToListAsync();

            _context.Set<SubscriptionSchedule>().RemoveRange(schedules);
            await _context.SaveChangesAsync();
        }

        // ✅ NEW: Prevent duplicate subscriptions (checks active + date range)
        public async Task<Subscription?> GetActiveSubscriptionByUserMealIdAsync(int userId, int userMealId)
        {
            var today = _time.TodayIst;
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.Meal) // ✅ ADDED
                .Include(s => s.WeeklySchedule)
                .FirstOrDefaultAsync(s => 
                    s.UserId == userId && 
                    s.UserMealId == userMealId && 
                    s.IsActive == true &&
                    s.StartDate <= today && 
                    s.EndDate >= today
                );
        }

        // ✅ FIX BUG 2 & 4: Check any active subscription for this meal (ignores date range)
        public async Task<Subscription?> GetAnyActiveSubscriptionByUserMealIdAsync(int userId, int userMealId)
        {
            // ✅ PERF: Removed Includes. This is a duplicate check, we only need to know if the row exists.
            return await _context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.UserId     == userId     &&
                    s.UserMealId == userMealId &&
                    s.IsActive     == true       &&
                    s.EndDate    >= _time.TodayIst
                );
        }

        // ✅ FIX BUG 2: Check any active subscription for this meal (ignores date range)
        public async Task<Subscription?> GetAnyActiveSubscriptionByMealIdAsync(int userId, int mealId)
        {
            // ✅ PERF: Removed Includes. This is a duplicate check, we only need to know if the row exists.
            return await _context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => 
                    s.UserId == userId && 
                    s.IsActive == true &&
                    (s.MealId == mealId || (s.UserMeal != null && s.UserMeal.MealId == mealId))
                );
        }
        public async Task<int> CountAsync()
        {
            return await _context.Subscriptions.CountAsync();
        }
    }
}
