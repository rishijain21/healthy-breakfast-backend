// HealthyBreakfastApp.Application/Interfaces/ISubscriptionSchedulingService.cs

using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ISubscriptionSchedulingService
    {
        Task GenerateScheduledOrdersFromSubscriptionsAsync();
    }
}
