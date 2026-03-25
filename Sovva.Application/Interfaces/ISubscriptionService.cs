// Sovva.Application/Interfaces/ISubscriptionService.cs

using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
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
        
        // ✅ NEW: Update NextScheduledDate for all active subscriptions
        Task UpdateNextScheduledDatesAsync();
        
        // ✅ NEW: Expire subscriptions whose EndDate has passed
        Task ExpireSubscriptionsAsync();
    }
}
