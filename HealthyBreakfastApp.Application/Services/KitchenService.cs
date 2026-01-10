using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Services
{
    public class KitchenService : IKitchenService
    {
        private readonly IKitchenRepository _kitchenRepository;
        private readonly ILogger<KitchenService> _logger;

        public KitchenService(
            IKitchenRepository kitchenRepository,
            ILogger<KitchenService> logger)
        {
            _kitchenRepository = kitchenRepository;
            _logger = logger;
        }

        // ============================================================================
        // ✅ GET TODAY'S CONFIRMED ORDERS (MILKBASKET LOGIC)
        // Shows orders confirmed at midnight for TODAY's delivery
        // ============================================================================
        public async Task<List<KitchenOrderDto>> GetOrdersForPreparationAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            // ✅ MILKBASKET: Kitchen shows TODAY's confirmed deliveries
            var deliveryDate = DateTime.SpecifyKind(istNow.Date, DateTimeKind.Utc); // TODAY

            _logger.LogInformation($"🍳 Kitchen Dashboard: TODAY's delivery ({istNow.Date:yyyy-MM-dd})");

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(deliveryDate);

            var result = orders.Select(o => new KitchenOrderDto
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                CustomerName = o.User?.Name ?? "Unknown",
                ScheduledFor = o.ScheduledFor,
                DeliveryTimeSlot = "7:00 AM - 9:00 AM",
                TotalPrice = o.TotalPrice,
                IsPrepared = o.IsPrepared,
                CreatedAt = o.CreatedAt,
                Ingredients = o.UserMeal?.UserMealIngredients.Select(umi => new KitchenIngredientDto
                {
                    IngredientId = umi.IngredientId,
                    IngredientName = umi.Ingredient.IngredientName,
                    Quantity = umi.Quantity,
                    Category = umi.Ingredient.IngredientCategory?.CategoryName ?? "Other",
                    IconEmoji = umi.Ingredient.IconEmoji ?? "🥗"
                }).ToList() ?? new List<KitchenIngredientDto>()
            }).ToList();

            _logger.LogInformation($"📦 Kitchen: {result.Count} orders to prepare for TODAY");

            return result;
        }

        // ============================================================================
        // ✨ NEW: GET TOMORROW'S CONFIRMED ORDERS (PRE-PLANNING VIEW)
        // Shows orders already confirmed tonight for TOMORROW's delivery
        // ============================================================================
        public async Task<List<KitchenOrderDto>> GetOrdersForTomorrowAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            // ✅ Get TOMORROW's orders (already confirmed tonight for next day)
            var tomorrowDate = DateTime.SpecifyKind(istNow.Date.AddDays(1), DateTimeKind.Utc); // TOMORROW

            _logger.LogInformation($"🍳 Kitchen Preview: TOMORROW's delivery ({istNow.Date.AddDays(1):yyyy-MM-dd})");

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowDate);

            var result = orders.Select(o => new KitchenOrderDto
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                CustomerName = o.User?.Name ?? "Unknown",
                ScheduledFor = o.ScheduledFor,
                DeliveryTimeSlot = "7:00 AM - 9:00 AM",
                TotalPrice = o.TotalPrice,
                IsPrepared = o.IsPrepared,
                CreatedAt = o.CreatedAt,
                Ingredients = o.UserMeal?.UserMealIngredients.Select(umi => new KitchenIngredientDto
                {
                    IngredientId = umi.IngredientId,
                    IngredientName = umi.Ingredient.IngredientName,
                    Quantity = umi.Quantity,
                    Category = umi.Ingredient.IngredientCategory?.CategoryName ?? "Other",
                    IconEmoji = umi.Ingredient.IconEmoji ?? "🥗"
                }).ToList() ?? new List<KitchenIngredientDto>()
            }).ToList();

            _logger.LogInformation($"📦 Kitchen: {result.Count} orders confirmed for TOMORROW");

            return result;
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForDateAsync(DateTime date)
        {
            var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            
            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(utcDate);

            return orders.Select(o => new KitchenOrderDto
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                CustomerName = o.User?.Name ?? "Unknown",
                ScheduledFor = o.ScheduledFor,
                DeliveryTimeSlot = "7:00 AM - 9:00 AM",
                TotalPrice = o.TotalPrice,
                IsPrepared = o.IsPrepared,
                CreatedAt = o.CreatedAt,
                Ingredients = o.UserMeal?.UserMealIngredients.Select(umi => new KitchenIngredientDto
                {
                    IngredientId = umi.IngredientId,
                    IngredientName = umi.Ingredient.IngredientName,
                    Quantity = umi.Quantity,
                    Category = umi.Ingredient.IngredientCategory?.CategoryName ?? "Other",
                    IconEmoji = umi.Ingredient.IconEmoji ?? "🥗"
                }).ToList() ?? new List<KitchenIngredientDto>()
            }).ToList();
        }

        public async Task MarkOrderAsPreparedAsync(int orderId)
        {
            var order = await _kitchenRepository.GetOrderByIdAsync(orderId);
            
            if (order == null)
                throw new InvalidOperationException("Order not found");

            if (order.IsPrepared)
                throw new InvalidOperationException("Order already marked as prepared");

            order.IsPrepared = true;
            order.UpdatedAt = DateTime.UtcNow;

            await _kitchenRepository.UpdateOrderAsync(order);

            _logger.LogInformation($"✅ Order #{orderId} marked as prepared");
        }

        public async Task<KitchenStatsDto> GetTodayStatsAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            var deliveryDate = DateTime.SpecifyKind(istNow.Date, DateTimeKind.Utc); // TODAY

            var todayOrders = await _kitchenRepository.GetOrdersForPreparationAsync(deliveryDate);

            var stats = new KitchenStatsDto
            {
                TotalOrders = todayOrders.Count,
                PreparedOrders = todayOrders.Count(o => o.IsPrepared),
                PendingOrders = todayOrders.Count(o => !o.IsPrepared),
                TotalRevenue = todayOrders.Sum(o => o.TotalPrice),
                IngredientSummary = todayOrders
                    .SelectMany(o => o.UserMeal?.UserMealIngredients ?? new List<Domain.Entities.UserMealIngredient>())
                    .GroupBy(umi => umi.Ingredient.IngredientName)
                    .ToDictionary(g => g.Key, g => g.Sum(umi => umi.Quantity))
            };

            return stats;
        }

        // ============================================================================
        // ✨ NEW: GET TOMORROW'S KITCHEN STATISTICS
        // ============================================================================
        public async Task<KitchenStatsDto> GetTomorrowStatsAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            var tomorrowDate = DateTime.SpecifyKind(istNow.Date.AddDays(1), DateTimeKind.Utc); // TOMORROW

            var tomorrowOrders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowDate);

            var stats = new KitchenStatsDto
            {
                TotalOrders = tomorrowOrders.Count,
                PreparedOrders = tomorrowOrders.Count(o => o.IsPrepared),
                PendingOrders = tomorrowOrders.Count(o => !o.IsPrepared),
                TotalRevenue = tomorrowOrders.Sum(o => o.TotalPrice),
                IngredientSummary = tomorrowOrders
                    .SelectMany(o => o.UserMeal?.UserMealIngredients ?? new List<Domain.Entities.UserMealIngredient>())
                    .GroupBy(umi => umi.Ingredient.IngredientName)
                    .ToDictionary(g => g.Key, g => g.Sum(umi => umi.Quantity))
            };

            return stats;
        }
    }
}
