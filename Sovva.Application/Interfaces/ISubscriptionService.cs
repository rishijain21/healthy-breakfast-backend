// Sovva.Application/Interfaces/ISubscriptionService.cs

using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<PagedResult<SubscriptionDto>> GetAllSubscriptionsAsync(int page = 1, int pageSize = 50);
        Task<SubscriptionDto?> GetSubscriptionByIdAsync(int subscriptionId);
        Task<SubscriptionDto?> GetSubscriptionByIdAndUserIdAsync(int subscriptionId, int userId);
        Task<IEnumerable<SubscriptionDto>> GetSubscriptionsByUserIdAsync(int userId);
        Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsByUserIdAsync(int userId);
        Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsAsync();
        Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionInternalDto dto);
        Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto updateSubscriptionDto);
        Task<bool> DeleteSubscriptionAsync(int subscriptionId);
        Task<bool> ActivateSubscriptionAsync(int subscriptionId);
        Task<bool> DeactivateSubscriptionAsync(int subscriptionId);
        
        // ✅ NEW: Get active subscription tied to a specific user meal ID
        Task<SubscriptionDto?> GetActiveSubscriptionByUserMealIdAsync(int userId, int userMealId);

        // ✅ NEW: Update NextScheduledDate for all active subscriptions
        Task UpdateNextScheduledDatesAsync();
        
        // ✅ NEW: Expire subscriptions whose EndDate has passed
        Task ExpireSubscriptionsAsync();
    }
}
