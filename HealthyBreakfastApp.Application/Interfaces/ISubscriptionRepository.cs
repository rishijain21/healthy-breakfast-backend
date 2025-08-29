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
    }
}
