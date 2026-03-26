using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;

namespace Sovva.Application.Services
{
    public class KitchenService : IKitchenService
    {
        private readonly IKitchenRepository _kitchenRepository;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<KitchenService> _logger;

        public KitchenService(
            IKitchenRepository kitchenRepository,
            IAppTimeProvider time,
            ILogger<KitchenService> logger)
        {
            _kitchenRepository = kitchenRepository;
            _time = time;
            _logger = logger;
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForPreparationAsync()
        {
            var istNow = _time.ToIst(_time.UtcNow);
            var todayIst = istNow.Date; // 2026-03-26

            _logger.LogInformation(
                "[Kitchen] UTC={Utc:u}  IST={Ist:u}  Querying for IST date={Date:yyyy-MM-dd}",
                _time.UtcNow, istNow, todayIst);

            // Pass the IST calendar date as Unspecified — repository owns the UTC conversion
            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(todayIst);

            _logger.LogInformation("[Kitchen] {Count} orders found for {Date:yyyy-MM-dd}", 
                orders.Count, todayIst);

            return orders.Select(MapToDto).ToList();
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForTomorrowAsync()
        {
            var istNow = _time.ToIst(_time.UtcNow);
            
            var tomorrowIst = istNow.Date.AddDays(1);

            _logger.LogInformation($"🍳 Kitchen Preview: TOMORROW's delivery ({tomorrowIst:yyyy-MM-dd})");

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowIst);

            var result = orders.Select(o => MapToDto(o)).ToList();

            _logger.LogInformation($"📦 Kitchen: {result.Count} orders confirmed for TOMORROW");

            return result;
        }

        public async Task<List<KitchenOrderDto>> GetOrdersForDateAsync(DateTime date)
        {
            // Pass the IST calendar date — repository owns the UTC conversion
            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(date.Date);

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
            // UpdatedAt handled by TimestampInterceptor

            await _kitchenRepository.UpdateOrderAsync(order);

            _logger.LogInformation($"✅ Order #{orderId} marked as prepared");
        }

        public async Task<KitchenStatsDto> GetTodayStatsAsync()
        {
            var istNow = _time.ToIst(_time.UtcNow);
            var todayIst = istNow.Date;

            var todayOrders = await _kitchenRepository.GetOrdersForPreparationAsync(todayIst);

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
            var istNow = _time.ToIst(_time.UtcNow);
            var tomorrowIst = istNow.Date.AddDays(1);

            var tomorrowOrders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowIst);

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
