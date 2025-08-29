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
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                .ThenInclude(um => um.Meal)
                .ToListAsync();
        }

        public async Task<Subscription?> GetByIdAsync(int subscriptionId)
        {
            return await _context.Subscriptions
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                .ThenInclude(um => um.Meal)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        }

        public async Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId)
        {
            return await _context.Subscriptions
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                .ThenInclude(um => um.Meal)
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync()
        {
            return await _context.Subscriptions
                .Include(s => s.User)
                .Include(s => s.UserMeal)
                .ThenInclude(um => um.Meal)
                .Where(s => s.Active && s.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && s.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
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
            var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
            if (subscription == null)
                return false;

            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
