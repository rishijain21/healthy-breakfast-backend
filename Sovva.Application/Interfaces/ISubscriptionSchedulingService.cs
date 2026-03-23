// Sovva.Application/Interfaces/ISubscriptionSchedulingService.cs

using System;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface ISubscriptionSchedulingService
    {
        Task GenerateScheduledOrdersFromSubscriptionsAsync();
        
        Task GenerateOrderForSubscriptionAsync(int subscriptionId, int userId, Guid authId);
        
        Task CancelOrderForSubscriptionAsync(int subscriptionId, int userId, Guid authId);
    }
}
