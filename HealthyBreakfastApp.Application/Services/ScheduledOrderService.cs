using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class ScheduledOrderService : IScheduledOrderService
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IOrderService _orderService;
        private readonly ILogger<ScheduledOrderService> _logger;

        public ScheduledOrderService(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            ILogger<ScheduledOrderService> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _orderService = orderService;
            _logger = logger;
        }

        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(Guid authId, CreateScheduledOrderDto dto)
        {
            // Get user using the correct method name
            var user = await _userRepository.GetByAuthIdAsync(authId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Validate scheduled date (must be tomorrow)
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            if (dto.ScheduledFor.Date != tomorrow)
            {
                throw new InvalidOperationException("Orders can only be scheduled for tomorrow");
            }

            // Calculate price using correct property names
            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal totalPrice = 0;

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(ingredientDto.IngredientId);
                if (ingredient == null)
                {
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");
                }
                
                ingredients.Add((ingredient, ingredientDto.Quantity));
                totalPrice += ingredient.Price * ingredientDto.Quantity; // ✅ FIXED: Use Price instead of PricePerUnit
            }

            // Check wallet balance
            if (!await CheckWalletBalanceAsync(authId, totalPrice))
            {
                throw new InvalidOperationException("Insufficient wallet balance");
            }

            // Create scheduled order
            var scheduledOrder = new ScheduledOrder
            {
                UserId = user.UserId,
                AuthId = authId,
                MealName = "Custom Overnight Oats",
                ScheduledFor = dto.ScheduledFor,
                DeliveryTimeSlot = dto.DeliveryTimeSlot,
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null ? 
                    JsonSerializer.Serialize(dto.NutritionalSummary) : null,
                OrderStatus = "scheduled",
                CanModify = true,
                ExpiresAt = tomorrow.AddDays(1), // Next midnight
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add ingredients using correct property names
            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price, // ✅ FIXED: Use Price
                    TotalPrice = ingredient.Price * quantity // ✅ FIXED: Use Price
                });
            }

            var createdOrder = await _scheduledOrderRepository.CreateAsync(scheduledOrder);
            return await MapToResponseDto(createdOrder);
        }

        public async Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            var orders = await _scheduledOrderRepository.GetByAuthIdAndDateAsync(authId, date);
            var response = new List<ScheduledOrderResponseDto>();

            foreach (var order in orders)
            {
                response.Add(await MapToResponseDto(order));
            }

            return response;
        }

        public async Task ModifyScheduledOrderAsync(Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
            {
                throw new InvalidOperationException("Scheduled order not found");
            }

            if (!scheduledOrder.CanModify || scheduledOrder.ExpiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Order can no longer be modified");
            }

            // Calculate new price using correct property names
            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal newTotalPrice = 0;

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(ingredientDto.IngredientId);
                if (ingredient == null)
                {
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");
                }
                
                ingredients.Add((ingredient, ingredientDto.Quantity));
                newTotalPrice += ingredient.Price * ingredientDto.Quantity; // ✅ FIXED: Use Price
            }

            // Check wallet balance for new amount
            if (!await CheckWalletBalanceAsync(authId, newTotalPrice))
            {
                throw new InvalidOperationException("Insufficient wallet balance for modified order");
            }

            // Remove existing ingredients
            scheduledOrder.Ingredients.Clear();

            // Add new ingredients using correct property names
            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    ScheduledOrderId = scheduledOrder.ScheduledOrderId,
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price, // ✅ FIXED: Use Price
                    TotalPrice = ingredient.Price * quantity // ✅ FIXED: Use Price
                });
            }

            // Update order details
            scheduledOrder.TotalPrice = newTotalPrice;
            scheduledOrder.DeliveryTimeSlot = dto.DeliveryTimeSlot ?? scheduledOrder.DeliveryTimeSlot;
            scheduledOrder.NutritionalSummary = dto.NutritionalSummary != null ? 
                JsonSerializer.Serialize(dto.NutritionalSummary) : scheduledOrder.NutritionalSummary;

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
        }

        public async Task CancelScheduledOrderAsync(Guid authId, int scheduledOrderId)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
            {
                throw new InvalidOperationException("Scheduled order not found");
            }

            if (!scheduledOrder.CanModify || scheduledOrder.ExpiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Order can no longer be cancelled");
            }

            scheduledOrder.OrderStatus = "cancelled";
            scheduledOrder.CanModify = false;
            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
        }

        public async Task<bool> CheckWalletBalanceAsync(Guid authId, decimal amount)
        {
            var user = await _userRepository.GetByAuthIdAsync(authId);
            if (user == null) return false;

            return user.WalletBalance >= amount;
        }

        public async Task ConfirmAllScheduledOrdersAsync()
        {
            var today = DateTime.UtcNow.Date;
            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(today);

            _logger.LogInformation($"Found {scheduledOrders.Count} scheduled orders to confirm for {today:yyyy-MM-dd}");

            foreach (var scheduledOrder in scheduledOrders)
            {
                try
                {
                    // Check wallet balance one more time
                    if (!await CheckWalletBalanceAsync(scheduledOrder.AuthId, scheduledOrder.TotalPrice))
                    {
                        _logger.LogWarning($"Insufficient balance for scheduled order {scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        continue;
                    }

                    // Create actual order using correct DTO structure
                    var createOrderDto = new CreateOrderFromMealBuilderDto
                    {
                        UserId = scheduledOrder.UserId,
                        MealId = 1, // Default meal ID - you may need to adjust this
                        SelectedIngredients = scheduledOrder.Ingredients.Select(si => new SelectedIngredientDto // ✅ FIXED: Use correct class name
                        {
                            IngredientId = si.IngredientId,
                            Quantity = si.Quantity
                        }).ToList(),
                        ScheduledFor = scheduledOrder.ScheduledFor, // ✅ FIXED: Use DateTime instead of string
                        DeliveryAddress = "Default Address", // You may want to add this to scheduled orders
                        SpecialInstructions = $"Auto-confirmed from scheduled order #{scheduledOrder.ScheduledOrderId}"
                    };

                    var orderResponse = await _orderService.CreateOrderFromMealBuilderAsync(createOrderDto); // ✅ FIXED: Use correct method signature

                    // Mark scheduled order as confirmed
                    scheduledOrder.OrderStatus = "confirmed";
                    scheduledOrder.CanModify = false;
                    scheduledOrder.ConfirmedAt = DateTime.UtcNow;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                    _logger.LogInformation($"Successfully confirmed scheduled order {scheduledOrder.ScheduledOrderId} -> Order {orderResponse.OrderId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to confirm scheduled order {scheduledOrder.ScheduledOrderId}");
                    
                    scheduledOrder.OrderStatus = "failed";
                    scheduledOrder.CanModify = false;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                }
            }
        }

        public Task<int> GetTimeUntilMidnightMinutesAsync()
        {
            var now = DateTime.UtcNow;
            var midnight = now.Date.AddDays(1);
            var timeSpan = midnight - now;
            return Task.FromResult((int)timeSpan.TotalMinutes);
        }

        private Task<ScheduledOrderResponseDto> MapToResponseDto(ScheduledOrder order)
        {
            var nutritionalSummary = !string.IsNullOrEmpty(order.NutritionalSummary) ?
                JsonSerializer.Deserialize<NutritionalSummaryDto>(order.NutritionalSummary) : null;

            var responseDto = new ScheduledOrderResponseDto
            {
                ScheduledOrderId = order.ScheduledOrderId,
                MealName = order.MealName,
                ScheduledFor = order.ScheduledFor,
                DeliveryTimeSlot = order.DeliveryTimeSlot,
                TotalPrice = order.TotalPrice,
                OrderStatus = order.OrderStatus,
                CanModify = order.CanModify && order.ExpiresAt > DateTime.UtcNow,
                CreatedAt = order.CreatedAt,
                ExpiresAt = order.ExpiresAt,
                NutritionalSummary = nutritionalSummary,
                Ingredients = order.Ingredients.Select(si => new ScheduledOrderIngredientDetailDto
                {
                    IngredientId = si.IngredientId,
                    IngredientName = si.Ingredient.IngredientName, // ✅ FIXED: Use IngredientName
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    TotalPrice = si.TotalPrice,
                  Category = si.Ingredient.IngredientCategory?.CategoryName ?? "Other", // ✅ CORRECT
// ✅ FIXED: Use IngredientCategory.Name
                    ImageUrl = si.Ingredient.IconEmoji ?? "🥗" // ✅ FIXED: Use IconEmoji as fallback
                }).ToList()
            };

            return Task.FromResult(responseDto);
        }
    }
}
