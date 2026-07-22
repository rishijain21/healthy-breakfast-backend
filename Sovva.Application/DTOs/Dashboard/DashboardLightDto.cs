using System;
using System.Collections.Generic;

namespace Sovva.Application.DTOs
{
    /// <summary>
    /// Lightweight dashboard summary for fast page load.
    /// Fetches only the fields needed by the Angular dashboard page.
    /// Does NOT replace DashboardSummaryDto — both endpoints coexist.
    /// </summary>
    public class DashboardLightDto
    {
        public decimal WalletBalance { get; set; }
        public int ActiveSubscriptionCount { get; set; }
        public List<SubscriptionSummaryDto> ActiveSubscriptions { get; set; } = new();
        public List<ScheduledOrderSummaryDto> TomorrowOrders { get; set; } = new();
        public int OrdersThisWeek { get; set; }

        /// <summary>Latest 5 transactions — updated after top-up for immediate dashboard refresh</summary>
        public List<WalletTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class SubscriptionSummaryDto
    {
        public int SubscriptionId { get; set; }
        public bool IsActive { get; set; }
        public string MealName { get; set; } = string.Empty;
        public string? MealImageUrl { get; set; }
        public decimal AgreedPrice { get; set; }
        public DateOnly? NextScheduledDate { get; set; }
    }

    public class ScheduledOrderSummaryDto
    {
        public int ScheduledOrderId { get; set; }
        public string MealName { get; set; } = string.Empty;
        public string? MealImageUrl { get; set; }
        public string DeliveryTimeSlot { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
    }
}
