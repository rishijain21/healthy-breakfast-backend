using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<IEnumerable<SubscriptionDto>> GetAllSubscriptionsAsync();
        Task<SubscriptionDto?> GetSubscriptionByIdAsync(int subscriptionId);
        Task<IEnumerable<SubscriptionDto>> GetSubscriptionsByUserIdAsync(int userId);
        Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsAsync();
Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionInternalDto dto);
        Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto updateSubscriptionDto);
        Task<bool> DeleteSubscriptionAsync(int subscriptionId);
        Task<bool> ActivateSubscriptionAsync(int subscriptionId);
        Task<bool> DeactivateSubscriptionAsync(int subscriptionId);
    }
}
