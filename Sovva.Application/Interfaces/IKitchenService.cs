using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IKitchenService
    {
        /// <summary>
        /// Get all orders for today that need preparation (IsPrepared = false)
        /// </summary>
        Task<List<KitchenOrderDto>> GetOrdersForPreparationAsync();

        /// <summary>
        /// ✨ NEW: Get orders confirmed for tomorrow's delivery (pre-planning)
        /// </summary>
        Task<List<KitchenOrderDto>> GetOrdersForTomorrowAsync();

        /// <summary>
        /// Get orders for a specific date
        /// </summary>
        Task<List<KitchenOrderDto>> GetOrdersForDateAsync(DateTime date);

        /// <summary>
        /// Mark an order as prepared
        /// </summary>
        Task MarkOrderAsPreparedAsync(int orderId);

        /// <summary>
        /// Get today's kitchen statistics
        /// </summary>
        Task<KitchenStatsDto> GetTodayStatsAsync();

        /// <summary>
        /// ✨ NEW: Get tomorrow's kitchen statistics
        /// </summary>
        Task<KitchenStatsDto> GetTomorrowStatsAsync();
    }
}
