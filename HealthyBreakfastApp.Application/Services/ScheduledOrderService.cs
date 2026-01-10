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
        // ✅ CREATE SCHEDULED ORDER (MILKBASKET LOGIC: Order today → Delivery tomorrow)
        // ----------------------------------------------------------------------------------------
        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(Guid authId, CreateScheduledOrderDto dto)
        {
            var user = await _userRepository.GetByAuthIdAsync(authId);
            if (user == null)
                throw new InvalidOperationException("User not found");

            // ✅ MILKBASKET LOGIC: Orders placed today are for TOMORROW's delivery
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var tomorrowIst = istNow.Date.AddDays(1); // TOMORROW

            _logger.LogInformation($"📦 Order placed at: {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Delivery scheduled for: {tomorrowIst:yyyy-MM-dd} (next morning)");

            // Set delivery date to tomorrow (ignore what frontend sends)
            var deliveryDate = DateTime.SpecifyKind(tomorrowIst, DateTimeKind.Utc);

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

            // Create ScheduledOrder (goes to cart)
            var scheduledOrder = new ScheduledOrder
            {
                UserId = user.UserId,
                AuthId = authId,
                MealName = "Custom Overnight Oats",
                ScheduledFor = deliveryDate, // TOMORROW
                DeliveryTimeSlot = dto.DeliveryTimeSlot ?? "8:00 AM",
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = "scheduled", // In cart
                CanModify = true, // Editable until 11:59 PM
                ExpiresAt = deliveryDate.AddDays(1), // Editable until day after delivery
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
            
            _logger.LogInformation($"✅ Order #{createdOrder.ScheduledOrderId} created for {tomorrowIst:yyyy-MM-dd} delivery");
            
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

            // Check if still editable
            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != "scheduled")
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
            scheduledOrder.UpdatedAt = DateTime.UtcNow;

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
            
            _logger.LogInformation($"✏️ Order #{scheduledOrderId} modified - New total: ₹{newTotalPrice}");
        }

        // ----------------------------------------------------------------------------------------
        // CANCEL SCHEDULED ORDER - DELETE FROM DATABASE
        // ----------------------------------------------------------------------------------------
        public async Task CancelScheduledOrderAsync(Guid authId, int scheduledOrderId)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
                throw new InvalidOperationException("Scheduled order not found");

            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != "scheduled")
                throw new InvalidOperationException("Order can no longer be cancelled");

            _logger.LogInformation($"🗑️ User cancelled order #{scheduledOrderId} - deleting from cart");
            
            await _scheduledOrderRepository.DeleteAsync(scheduledOrderId);
            
            _logger.LogInformation($"✅ Order #{scheduledOrderId} successfully removed from cart");
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
        // ✅ MIDNIGHT JOB – CONFIRM SCHEDULED ORDERS FOR TODAY (MILKBASKET LOGIC)
        // This runs at 12:00 AM every night to confirm orders for TODAY's delivery
        // ----------------------------------------------------------------------------------------
        public async Task ConfirmAllScheduledOrdersAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var today = istNow.Date; // TODAY

            _logger.LogInformation($"🌙 [MIDNIGHT JOB] Started at {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Processing orders for TODAY's delivery: {today:yyyy-MM-dd}");

            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(today);

            _logger.LogInformation($"📦 Found {scheduledOrders.Count} total orders for {today:yyyy-MM-dd}");

            // Filter only "scheduled" status orders
            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled")
                .ToList();

            _logger.LogInformation($"📋 {pendingOrders.Count} orders pending confirmation");

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in pendingOrders)
            {
                try
                {
                    _logger.LogInformation($"🔄 Processing order #{scheduledOrder.ScheduledOrderId}");

                    // Get fresh user data
                    var user = await _userRepository.GetByAuthIdAsync(scheduledOrder.AuthId);
                    if (user == null)
                    {
                        _logger.LogWarning($"❌ User not found for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // Check wallet balance
                    if (user.WalletBalance < scheduledOrder.TotalPrice)
                    {
                        _logger.LogWarning(
                            $"❌ Insufficient balance for order #{scheduledOrder.ScheduledOrderId}. " +
                            $"Required: ₹{scheduledOrder.TotalPrice}, Available: ₹{user.WalletBalance}");
                        
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ Create the actual Order (goes to Orders table → Kitchen Dashboard)
                    var createOrderDto = new CreateOrderFromMealBuilderDto
                    {
                        UserId = scheduledOrder.UserId,
                        MealId = 1,
                        SelectedIngredients = scheduledOrder.Ingredients.Select(i => new SelectedIngredientDto
                        {
                            IngredientId = i.IngredientId,
                            Quantity = i.Quantity
                        }).ToList(),
                        ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor, DateTimeKind.Utc),
                        DeliveryAddress = "Default Address",
                        SpecialInstructions = $"Auto-confirmed at midnight from cart order #{scheduledOrder.ScheduledOrderId}"
                    };

                    var orderResponse = await _orderService.CreateOrderFromMealBuilderAsync(createOrderDto);

                    _logger.LogInformation(
                        $"✅ Confirmed! Created Order #{orderResponse.OrderId} from cart order #{scheduledOrder.ScheduledOrderId}");
                    _logger.LogInformation($"   💰 Amount charged: ₹{scheduledOrder.TotalPrice}");

                    // ✅ Mark scheduled order as processed (no longer in cart)
                    scheduledOrder.OrderStatus = "processed";
                    scheduledOrder.CanModify = false;
                    scheduledOrder.ConfirmedAt = DateTime.UtcNow;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                    confirmedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to confirm order #{scheduledOrder.ScheduledOrderId}");
                    scheduledOrder.OrderStatus = "failed";
                    scheduledOrder.CanModify = false;
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                    failedCount++;
                }
            }

            _logger.LogInformation($"🎉 [MIDNIGHT JOB] Complete!");
            _logger.LogInformation($"   ✅ Confirmed: {confirmedCount}");
            _logger.LogInformation($"   ❌ Failed: {failedCount}");
            _logger.LogInformation($"   ⏭️  Already processed: {scheduledOrders.Count - pendingOrders.Count}");
        }

        // ----------------------------------------------------------------------------------------
        // TIME TILL MIDNIGHT (IST)
        // ----------------------------------------------------------------------------------------
        public Task<int> GetTimeUntilMidnightMinutesAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var midnightIst = istNow.Date.AddDays(1);
            
            return Task.FromResult((int)(midnightIst - istNow).TotalMinutes);
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
                CanModify = order.CanModify && order.OrderStatus == "scheduled",
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
