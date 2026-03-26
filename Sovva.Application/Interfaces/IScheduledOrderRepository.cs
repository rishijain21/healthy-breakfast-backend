using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IScheduledOrderRepository
    {
        Task<ScheduledOrder> CreateAsync(ScheduledOrder scheduledOrder);
        Task<List<ScheduledOrder>> GetByAuthIdAndDateAsync(Guid authId, DateTime date);
        Task<List<ScheduledOrder>> GetByUserIdAndDateAsync(int userId, DateTime date);
        Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId);
        Task<ScheduledOrder> UpdateAsync(ScheduledOrder scheduledOrder);
        Task DeleteAsync(int scheduledOrderId);
        Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateTime date);
        
        /// <summary>
        /// ✅ NEW: Fetches scheduled orders directly by DateOnly for the midnight job.
        /// Queries ScheduledFor (DATE column) by equality - no UTC conversion needed.
        /// </summary>
        Task<List<ScheduledOrder>> GetScheduledOrdersForDateAsync(DateOnly date);
        
        /// <summary>
        /// Fetches scheduled orders whose IST delivery date falls within the given UTC range.
        /// The implementation converts startUtc to IST to determine the calendar date,
        /// then queries ScheduledFor (a DATE column) by equality.
        /// </summary>
        Task<List<ScheduledOrder>> GetScheduledOrdersForUtcRangeAsync(DateTime startUtc, DateTime endUtc);
        
        Task<bool> HasScheduledOrdersForDateAsync(Guid authId, DateTime date);
        Task<List<ScheduledOrder>> GetBySubscriptionIdAsync(int subscriptionId);
        
        /// <summary>
        /// ✅ NEW: Check if a scheduled order already exists for a subscription on a specific date
        /// Used to prevent duplicate order generation on job retry
        /// </summary>
        Task<ScheduledOrder?> GetBySubscriptionIdAndDateAsync(int subscriptionId, DateOnly date);
    }
}
