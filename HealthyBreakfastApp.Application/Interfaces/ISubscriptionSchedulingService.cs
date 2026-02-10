// HealthyBreakfastApp.Application/Interfaces/ISubscriptionSchedulingService.cs

using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ISubscriptionSchedulingService
    {
        Task GenerateScheduledOrdersFromSubscriptionsAsync();
        
        Task GenerateOrderForSubscriptionAsync(int subscriptionId, Guid authId);
        
        Task CancelOrderForSubscriptionAsync(int subscriptionId, Guid authId);
    }
}
