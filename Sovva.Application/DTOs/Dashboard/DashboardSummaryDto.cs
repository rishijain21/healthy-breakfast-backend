// Sovva.Application/DTOs/DashboardSummaryDto.cs

namespace Sovva.Application.DTOs
{
    /// <summary>
    /// Aggregated dashboard data for frontend bootstrap on login
    /// </summary>
    public class DashboardSummaryDto
    {
        // User Profile (cached 5 min)
        public UserDto Profile { get; set; } = null!;
        
        // Wallet
        public decimal WalletBalance { get; set; }
        public List<WalletTransactionDto> RecentTransactions { get; set; } = new();
        
        // Subscriptions
        public List<SubscriptionDto> ActiveSubscriptions { get; set; } = new();
        
        // Tomorrow's scheduled orders (cart)
        public List<ScheduledOrderResponseDto> TomorrowOrders { get; set; } = new();

        // ✅ NEW: Computed stats for dashboard signals
        public int TotalTransactions { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
        public int LoyaltyPoints { get; set; }
        public decimal AverageCarbs { get; set; }
        public decimal AverageFats { get; set; }
    }
}