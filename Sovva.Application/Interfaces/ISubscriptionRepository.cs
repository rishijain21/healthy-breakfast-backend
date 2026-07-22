// Sovva.Application/Interfaces/ISubscriptionRepository.cs

using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<IEnumerable<Subscription>> GetAllAsync(int page = 1, int pageSize = 50);
        Task<(IEnumerable<Subscription> Items, int TotalCount)> GetAllWithCountAsync(int page = 1, int pageSize = 50);
        Task<Subscription?> GetByIdAsync(int subscriptionId);
        Task<Subscription?> GetByIdAndUserIdAsync(int subscriptionId, int userId);
        Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Subscription>> GetActiveSubscriptionsByUserIdAsync(int userId, DateOnly targetDate);
        Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync();
        Task<IEnumerable<Subscription>> GetExpiredActiveSubscriptionsAsync(DateOnly today);
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription> UpdateAsync(Subscription subscription);
        Task<bool> DeleteAsync(int subscriptionId);
        
        // ✅ NEW: Schedule management
        Task<IEnumerable<SubscriptionSchedule>> GetSchedulesBySubscriptionIdAsync(int subscriptionId);
        Task AddSchedulesAsync(int subscriptionId, IEnumerable<SubscriptionSchedule> schedules);
        Task RemoveSchedulesAsync(int subscriptionId);

        // ✅ NEW: Prevent duplicate subscriptions (checks active + date range)
        Task<Subscription?> GetActiveSubscriptionByUserMealIdAsync(int userId, int userMealId);

        // ✅ FIX BUG 2 & 4: Check any active subscription for this meal (ignores date range)
        Task<Subscription?> GetAnyActiveSubscriptionByUserMealIdAsync(int userId, int userMealId);
        Task<Subscription?> GetAnyActiveSubscriptionByMealIdAsync(int userId, int mealId);

        // ✅ NEW: Batch update for efficient DB operations
        Task UpdateBatchAsync(IEnumerable<Subscription> subscriptions);

        Task<int> CountAsync();

        /// <summary>
        /// Admin/analytics: returns soft-deleted subscriptions cancelled within the last N days.
        /// Uses IgnoreQueryFilters() to bypass the soft-delete EF Core filter.
        /// </summary>
        Task<IEnumerable<Subscription>> GetCancelledSubscriptionsAsync(int daysAgo = 30);

        /// <summary>
        /// Returns true if a subscription with the given ID exists AND belongs to the given userId.
        /// Uses a SELECT EXISTS projection — no entity materialization.
        /// </summary>
        Task<bool> BelongsToUserAsync(int subscriptionId, int userId);
    }
}
