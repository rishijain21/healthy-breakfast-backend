using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Domain.Constants;

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

            _logger.LogInformation("Kitchen Preview: TOMORROW's delivery ({TomorrowDate})", tomorrowIst.ToString("yyyy-MM-dd"));

            var orders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowIst);

            var result = orders.Select(o => MapToDto(o)).ToList();

            _logger.LogInformation("Kitchen: {OrderCount} orders confirmed for TOMORROW", result.Count);

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
                throw new OrderNotFoundException(orderId);

            if (order.IsPrepared)
                throw new OrderAlreadyPreparedException(orderId);

            // Guard: only allow marking orders scheduled for today (IST)
            var todayIst = _time.ToIst(_time.UtcNow).Date;
            var orderDateIst = _time.ToIst(order.ScheduledFor).Date;
            if (orderDateIst != todayIst)
                throw new InvalidOperationException(
                    $"Order #{orderId} is scheduled for {orderDateIst:yyyy-MM-dd}, not today ({todayIst:yyyy-MM-dd}). Only today's orders can be marked prepared.");

            order.IsPrepared = true;
            await _kitchenRepository.UpdateOrderAsync(order);

            _logger.LogInformation("Order #{OrderId} marked as prepared", orderId);
        }

        public async Task<KitchenStatsDto> GetTodayStatsAsync()
        {
            var istNow = _time.ToIst(_time.UtcNow);
            var todayIst = istNow.Date;

            var todayOrders = await _kitchenRepository.GetOrdersForPreparationAsync(todayIst, includeDetails: false);

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

            var tomorrowOrders = await _kitchenRepository.GetOrdersForPreparationAsync(tomorrowIst, includeDetails: false);

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
                // Use the delivery location's slot — NOT a hardcoded value
                DeliveryTimeSlot = o.DeliveryAddress?.ServiceableLocation?.DeliveryTimeSlot ?? DeliveryConstants.DefaultDeliveryWindow,
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
