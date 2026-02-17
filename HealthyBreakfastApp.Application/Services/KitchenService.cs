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

        public async Task<List<KitchenOrderDto>> GetOrdersForPreparationAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var utcNow = DateTime.UtcNow;
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, istZone);
            
            // ✅ FIXED: Show orders for TODAY (not tomorrow)
            var today = istNow.Date;
            
            Console.WriteLine($"🍳 [KitchenService] UTC Time: {utcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"🍳 [KitchenService] IST Time: {istNow:yyyy-MM-dd HH:mm:ss} IST");
            Console.WriteLine($"🍳 [KitchenService] Fetching orders for TODAY: {today:yyyy-MM-dd}");
            
            var deliveryDate = DateTime.SpecifyKind(today, DateTimeKind.Utc);

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(deliveryDate);

            var result = orders.Select(o => MapToDto(o)).ToList();

            Console.WriteLine($"📦 [KitchenService] {result.Count} orders to prepare for TODAY ({today:yyyy-MM-dd})");

            return result;
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForTomorrowAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            var tomorrowDate = DateTime.SpecifyKind(istNow.Date.AddDays(1), DateTimeKind.Utc);

            _logger.LogInformation($"🍳 Kitchen Preview: TOMORROW's delivery ({istNow.Date.AddDays(1):yyyy-MM-dd})");

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowDate);

            var result = orders.Select(o => MapToDto(o)).ToList();

            _logger.LogInformation($"📦 Kitchen: {result.Count} orders confirmed for TOMORROW");

            return result;
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForDateAsync(DateTime date)
        {
            var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            
            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(utcDate);

            return orders.Select(o => MapToDto(o)).ToList();
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
            
            var deliveryDate = DateTime.SpecifyKind(istNow.Date, DateTimeKind.Utc);

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

        public async Task<KitchenStatsDto> GetTomorrowStatsAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            var tomorrowDate = DateTime.SpecifyKind(istNow.Date.AddDays(1), DateTimeKind.Utc);

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

        // ✅ Helper method to map Order entity to DTO
        private KitchenOrderDto MapToDto(Domain.Entities.Order o)
        {
            return new KitchenOrderDto
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                CustomerName = o.User?.Name ?? "Unknown",
                UserPhoneNumber = o.User?.Phone ?? "N/A",
                MealName = o.UserMeal?.MealName ?? "Custom Meal",
                ScheduledFor = o.ScheduledFor,
                DeliveryTimeSlot = "7:00 AM - 9:00 AM",
                TotalPrice = o.TotalPrice,
                IsPrepared = o.IsPrepared,
                CreatedAt = o.CreatedAt,
                
                // ✅ Map DeliveryAddress
                DeliveryAddress = o.DeliveryAddress != null ? new KitchenDeliveryAddressDto
                {
                    AddressId = o.DeliveryAddress.Id,
                    CompleteAddress = o.DeliveryAddress.CompleteAddress,
                    ServiceableLocation = o.DeliveryAddress.ServiceableLocation != null ? new ServiceableLocationDto
                    {
                        Id = o.DeliveryAddress.ServiceableLocation.Id,
                        Area = o.DeliveryAddress.ServiceableLocation.Area,
                        City = o.DeliveryAddress.ServiceableLocation.City,
                        Locality = o.DeliveryAddress.ServiceableLocation.Locality,
                        LandmarkOrSociety = o.DeliveryAddress.ServiceableLocation.LandmarkOrSociety,
                        Pincode = o.DeliveryAddress.ServiceableLocation.Pincode,
                        IsActive = o.DeliveryAddress.ServiceableLocation.IsActive,
                        Latitude = o.DeliveryAddress.ServiceableLocation.Latitude,
                        Longitude = o.DeliveryAddress.ServiceableLocation.Longitude,
                        DeliveryTimeSlot = o.DeliveryAddress.ServiceableLocation.DeliveryTimeSlot,
                        FullAddress = o.DeliveryAddress.ServiceableLocation.FullAddress,
                        CreatedAt = o.DeliveryAddress.ServiceableLocation.CreatedAt
                    } : null
                } : null,
                
                // ✅ Map Ingredients
                Ingredients = o.UserMeal?.UserMealIngredients.Select(umi => new KitchenIngredientDto
                {
                    IngredientId = umi.IngredientId,
                    IngredientName = umi.Ingredient.IngredientName,
                    Quantity = umi.Quantity,
                    Category = umi.Ingredient.IngredientCategory?.CategoryName ?? "Other",
                    IconEmoji = umi.Ingredient.IconEmoji ?? "🥗",
                    Unit = "units"
                }).ToList() ?? new List<KitchenIngredientDto>()
            };
        }
    }
}
