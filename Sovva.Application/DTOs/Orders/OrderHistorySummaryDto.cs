using System;
using Sovva.Domain.Enums;

namespace Sovva.Application.DTOs
{
    /// <summary>
    /// Lightweight order list DTO — only fields needed by the order history list view.
    /// Does NOT load ingredient details, reducing DB fetch size significantly.
    /// </summary>
    public class OrderHistorySummaryDto
    {
        public long OrderId { get; set; }
        public int UserId { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public string OrderStatusText => OrderStatus.ToString();
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public int? MealId { get; set; }
        public string? MealName { get; set; }
        public string? MealImageUrl { get; set; }
        public int? TotalCalories { get; set; }
        public decimal? TotalProtein { get; set; }
        public decimal? TotalFiber { get; set; }
        public bool CanReorder => OrderStatus == OrderStatus.Delivered;
        public bool CanRate => OrderStatus == OrderStatus.Delivered;
    }
}
