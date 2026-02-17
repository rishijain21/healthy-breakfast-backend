// HealthyBreakfastApp.Application/Interfaces/ISubscriptionRepository.cs

using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<IEnumerable<Subscription>> GetAllAsync();
        Task<Subscription?> GetByIdAsync(int subscriptionId);
        Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync();
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription> UpdateAsync(Subscription subscription);
        Task<bool> DeleteAsync(int subscriptionId);
        
        // ✅ NEW: Schedule management
        Task<IEnumerable<SubscriptionSchedule>> GetSchedulesBySubscriptionIdAsync(int subscriptionId);
        Task AddSchedulesAsync(int subscriptionId, IEnumerable<SubscriptionSchedule> schedules);
        Task RemoveSchedulesAsync(int subscriptionId);

        // ✅ NEW: Prevent duplicate subscriptions (checks active + date range)
        Task<Subscription?> GetActiveSubscriptionByUserMealIdAsync(int userId, int userMealId);
    }
}
