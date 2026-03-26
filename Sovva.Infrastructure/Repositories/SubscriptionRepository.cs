// Sovva.Infrastructure/Repositories/SubscriptionRepository.cs

using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public SubscriptionRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<IEnumerable<Subscription>> GetAllAsync()
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .ToListAsync();
        }

        public async Task<Subscription?> GetByIdAsync(int subscriptionId)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        }

        public async Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId)
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync()
        {
            var today = _time.TodayIst;  // ✅ Use IST instead of UTC
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                    .ThenInclude(u => u.AuthMapping)  // ✅ Important for scheduling
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)
                .Include(s => s.WeeklySchedule)  // ✅ NEW
                .Where(s => s.Active && s.StartDate <= today && s.EndDate >= today)
                .ToListAsync();
        }

        public async Task<Subscription> CreateAsync(Subscription subscription)
        {
            // CreatedAt/UpdatedAt handled by TimestampInterceptor

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
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
            var subscription = await _context.Subscriptions
                .Include(s => s.WeeklySchedule)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
                
            if (subscription == null)
                return false;

            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
            return true;
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
            var schedules = await _context.Set<SubscriptionSchedule>()
                .AsNoTracking()
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
                .Include(s => s.WeeklySchedule)
                .FirstOrDefaultAsync(s => 
                    s.UserId == userId && 
                    s.UserMealId == userMealId && 
                    s.Active == true &&
                    s.StartDate <= today && 
                    s.EndDate >= today
                );
        }
    }
}
