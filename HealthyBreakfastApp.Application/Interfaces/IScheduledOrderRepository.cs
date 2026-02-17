using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IScheduledOrderRepository
    {
        Task<ScheduledOrder> CreateAsync(ScheduledOrder scheduledOrder);
        Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date);
        Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId);
        Task<ScheduledOrder> UpdateAsync(ScheduledOrder scheduledOrder);
        Task DeleteAsync(int scheduledOrderId);
        Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date);
        Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date);
        Task<List<ScheduledOrder>> GetBySubscriptionIdAsync(int subscriptionId);
    }
}
