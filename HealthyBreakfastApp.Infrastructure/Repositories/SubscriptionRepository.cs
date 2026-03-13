// HealthyBreakfastApp.Infrastructure/Repositories/SubscriptionRepository.cs

using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AppDbContext _context;

        public SubscriptionRepository(AppDbContext context)
        {
            _context = context;
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
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
            subscription.CreatedAt = DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task<Subscription> UpdateAsync(Subscription subscription)
        {
            subscription.UpdatedAt = DateTime.UtcNow;
            
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();
            return subscription;
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
                schedule.CreatedAt = DateTime.UtcNow;
                schedule.UpdatedAt = DateTime.UtcNow;
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
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
