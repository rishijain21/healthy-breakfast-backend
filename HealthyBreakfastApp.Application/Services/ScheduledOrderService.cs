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

        // ----------------------------------------------------------------------------------------
        // CREATE SCHEDULED ORDER (IST VALIDATION)
        // ----------------------------------------------------------------------------------------
        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(Guid authId, CreateScheduledOrderDto dto)
        {
            var user = await _userRepository.GetByAuthIdAsync(authId);
            if (user == null)
                throw new InvalidOperationException("User not found");

            // Validate using IST timezone (must be tomorrow IST)
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var tomorrowIst = istNow.Date.AddDays(1);

            var scheduledDateIst = TimeZoneInfo.ConvertTimeFromUtc(dto.ScheduledFor, istZone).Date;

            if (scheduledDateIst != tomorrowIst)
            {
                throw new InvalidOperationException(
                    $"Orders can only be scheduled for tomorrow (IST). Expected: {tomorrowIst:yyyy-MM-dd}, Received: {scheduledDateIst:yyyy-MM-dd}"
                );
            }

            // Calculate price
            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal totalPrice = 0;

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(ingredientDto.IngredientId);
                if (ingredient == null)
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
                totalPrice += ingredient.Price * ingredientDto.Quantity;
            }

            // Check wallet balance
            if (!await CheckWalletBalanceAsync(authId, totalPrice))
                throw new InvalidOperationException("Insufficient wallet balance");

            // Create ScheduledOrder
            var scheduledOrder = new ScheduledOrder
            {
                UserId = user.UserId,
                AuthId = authId,
                MealName = "Custom Overnight Oats",
                ScheduledFor = dto.ScheduledFor,
                DeliveryTimeSlot = dto.DeliveryTimeSlot,
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = "scheduled",
                CanModify = true,
                ExpiresAt = dto.ScheduledFor.Date.AddDays(1), // next midnight after delivery day
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price,
                    TotalPrice = ingredient.Price * quantity
                });
            }

            var createdOrder = await _scheduledOrderRepository.CreateAsync(scheduledOrder);
            return await MapToResponseDto(createdOrder);
        }

        // ----------------------------------------------------------------------------------------
        // GET SCHEDULED ORDERS FOR SPECIFIC DATE
        // ----------------------------------------------------------------------------------------
        public async Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(Guid authId, DateTime date)
        {
            var orders = await _scheduledOrderRepository.GetByAuthIdAndDateAsync(authId, date);
            var result = new List<ScheduledOrderResponseDto>();

            foreach (var order in orders)
            {
                result.Add(await MapToResponseDto(order));
            }

            return result;
        }

        // ----------------------------------------------------------------------------------------
        // MODIFY SCHEDULED ORDER
        // ----------------------------------------------------------------------------------------
        public async Task ModifyScheduledOrderAsync(Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
                throw new InvalidOperationException("Scheduled order not found");

            if (!scheduledOrder.CanModify || scheduledOrder.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Order can no longer be modified");

            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal newTotalPrice = 0;

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(ingredientDto.IngredientId);
                if (ingredient == null)
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
                newTotalPrice += ingredient.Price * ingredientDto.Quantity;
            }

            if (!await CheckWalletBalanceAsync(authId, newTotalPrice))
                throw new InvalidOperationException("Insufficient wallet balance for modified order");

            // Reset ingredients
            scheduledOrder.Ingredients.Clear();

            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    ScheduledOrderId = scheduledOrder.ScheduledOrderId,
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price,
                    TotalPrice = ingredient.Price * quantity
                });
            }

            scheduledOrder.TotalPrice = newTotalPrice;
            scheduledOrder.DeliveryTimeSlot = dto.DeliveryTimeSlot ?? scheduledOrder.DeliveryTimeSlot;
            scheduledOrder.NutritionalSummary = dto.NutritionalSummary != null
                ? JsonSerializer.Serialize(dto.NutritionalSummary)
                : scheduledOrder.NutritionalSummary;

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
        }

        // ----------------------------------------------------------------------------------------
        // CANCEL SCHEDULED ORDER
        // ----------------------------------------------------------------------------------------
        public async Task CancelScheduledOrderAsync(Guid authId, int scheduledOrderId)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
                throw new InvalidOperationException("Scheduled order not found");

            if (!scheduledOrder.CanModify || scheduledOrder.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Order can no longer be cancelled");

            scheduledOrder.OrderStatus = "cancelled";
            scheduledOrder.CanModify = false;

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
        }

        // ----------------------------------------------------------------------------------------
        // BALANCE CHECK
        // ----------------------------------------------------------------------------------------
        public async Task<bool> CheckWalletBalanceAsync(Guid authId, decimal amount)
        {
            var user = await _userRepository.GetByAuthIdAsync(authId);
            return user != null && user.WalletBalance >= amount;
        }

        // ----------------------------------------------------------------------------------------
        // CRON JOB – CONFIRM SCHEDULED ORDERS FOR TODAY (IST + REAL ORDER CREATION)
        // ----------------------------------------------------------------------------------------
        public async Task ConfirmAllScheduledOrdersAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var today = istNow.Date;

            _logger.LogInformation($"🔄 Processing scheduled orders for IST date: {today:yyyy-MM-dd}");

            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(today);

            _logger.LogInformation($"📦 Found {scheduledOrders.Count} scheduled orders to process");

            foreach (var scheduledOrder in scheduledOrders)
            {
                try
                {
                    // Check wallet balance
                    if (!await CheckWalletBalanceAsync(scheduledOrder.AuthId, scheduledOrder.TotalPrice))
                    {
                        _logger.LogWarning($"❌ Insufficient balance for order {scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        continue;
                    }

                    // ✅ FIX: Create the actual Order with proper DateTime Kind
                    var createOrderDto = new CreateOrderFromMealBuilderDto
                    {
                        UserId = scheduledOrder.UserId,
                        MealId = 1,
                        SelectedIngredients = scheduledOrder.Ingredients.Select(i => new SelectedIngredientDto
                        {
                            IngredientId = i.IngredientId,
                            Quantity = i.Quantity
                        }).ToList(),
                        ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor, DateTimeKind.Utc), // ✅ CRITICAL FIX
                        DeliveryAddress = "Default Address",
                        SpecialInstructions = $"Auto-confirmed from scheduled order #{scheduledOrder.ScheduledOrderId}"
                    };

                    var orderResponse = await _orderService.CreateOrderFromMealBuilderAsync(createOrderDto);

                    _logger.LogInformation(
                        $"✅ Created order {orderResponse.OrderId} from scheduled order {scheduledOrder.ScheduledOrderId}");

                    // Mark scheduled order as processed
                    scheduledOrder.OrderStatus = "processed";
                    scheduledOrder.CanModify = false;
                    scheduledOrder.ConfirmedAt = DateTime.UtcNow;

                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to confirm scheduled order {scheduledOrder.ScheduledOrderId}");
                    scheduledOrder.OrderStatus = "failed";
                    scheduledOrder.CanModify = false;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                }
            }

            _logger.LogInformation("✅ Scheduled order processing complete");
        }

        // ----------------------------------------------------------------------------------------
        // TIME TILL MIDNIGHT (UTC)
        // ----------------------------------------------------------------------------------------
        public Task<int> GetTimeUntilMidnightMinutesAsync()
        {
            var now = DateTime.UtcNow;
            var midnight = now.Date.AddDays(1);
            return Task.FromResult((int)(midnight - now).TotalMinutes);
        }

        // ----------------------------------------------------------------------------------------
        // MAPPER
        // ----------------------------------------------------------------------------------------
        private Task<ScheduledOrderResponseDto> MapToResponseDto(ScheduledOrder order)
        {
            var nutritional = !string.IsNullOrEmpty(order.NutritionalSummary)
                ? JsonSerializer.Deserialize<NutritionalSummaryDto>(order.NutritionalSummary)
                : null;

            var dto = new ScheduledOrderResponseDto
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
                NutritionalSummary = nutritional,
                Ingredients = order.Ingredients.Select(i => new ScheduledOrderIngredientDetailDto
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.Ingredient.IngredientName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    Category = i.Ingredient.IngredientCategory?.CategoryName ?? "Other",
                    ImageUrl = i.Ingredient.IconEmoji ?? "🥗"
                }).ToList()
            };

            return Task.FromResult(dto);
        }
    }
}
