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

            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var tomorrowIst = istNow.Date.AddDays(1);

            _logger.LogInformation($"📦 Order placed at: {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Delivery scheduled for: {tomorrowIst:yyyy-MM-dd} (next morning)");

            var deliveryDate = DateTime.SpecifyKind(tomorrowIst, DateTimeKind.Utc);

            // ✅ NEW: Price calculation logic
            decimal totalPrice;
            var ingredients = new List<(Ingredient ingredient, int quantity)>();

            // Load ingredients first (needed for cart display)
            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(ingredientDto.IngredientId);
                if (ingredient == null)
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
            }

            // ✅ FEATURED MEAL: Use fixed price if provided
            if (dto.MealPrice.HasValue && dto.MealPrice.Value > 0)
            {
                totalPrice = dto.MealPrice.Value;
                _logger.LogInformation($"💰 Using featured meal fixed price: ₹{totalPrice}");
            }
            else
            {
                // ✅ CUSTOM MEAL: Calculate from ingredients
                totalPrice = ingredients.Sum(i => i.ingredient.Price * i.quantity);
                _logger.LogInformation($"💰 Calculated price from ingredients: ₹{totalPrice}");
            }

            // Check wallet balance
            if (!await CheckWalletBalanceAsync(authId, totalPrice))
                throw new InvalidOperationException("Insufficient wallet balance");

            // Create ScheduledOrder
            var scheduledOrder = new ScheduledOrder
            {
                UserId = user.UserId,
                AuthId = authId,
                MealName = dto.MealName ?? "Custom Overnight Oats",
                ScheduledFor = deliveryDate,
                DeliveryTimeSlot = dto.DeliveryTimeSlot ?? "8:00 AM",
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = "scheduled",
                CanModify = true,
                ExpiresAt = deliveryDate.AddDays(1),
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
            _logger.LogInformation($"   💳 Total price: ₹{totalPrice}");
            
            return await MapToResponseDto(createdOrder);
        }

        // ----------------------------------------------------------------------------------------
        // ✅ DUPLICATE SCHEDULED ORDER - Creates exact copy without navigation
        // ----------------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------------
// ✅ DUPLICATE SCHEDULED ORDER - Creates exact copy without navigation
// ----------------------------------------------------------------------------------------
/// <summary>
/// ✅ DUPLICATE SCHEDULED ORDER - Fixed for PostgreSQL UTC
/// </summary>
public async Task<ScheduledOrderResponseDto> DuplicateScheduledOrderAsync(Guid authId, int scheduledOrderId)
{
    try
    {
        _logger.LogInformation($"🔄 Duplicating order #{scheduledOrderId} for user {authId}");

        // 1. Find original order
        var originalOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
        if (originalOrder == null)
        {
            _logger.LogWarning($"❌ Order #{scheduledOrderId} not found");
            throw new InvalidOperationException("Scheduled order not found");
        }

        _logger.LogInformation($"✅ Found original order: {originalOrder.MealName}");

        // 2. Validate order can be duplicated
        if (originalOrder.OrderStatus.ToLower() != "scheduled")
        {
            _logger.LogWarning($"❌ Cannot duplicate order with status: {originalOrder.OrderStatus}");
            throw new InvalidOperationException($"Cannot duplicate order with status '{originalOrder.OrderStatus}'");
        }

        // 3. Check wallet balance
        if (!await CheckWalletBalanceAsync(authId, originalOrder.TotalPrice))
        {
            _logger.LogWarning($"❌ Insufficient balance for duplication");
            throw new InvalidOperationException("Insufficient wallet balance");
        }

        // 4. Verify user exists
        var user = await _userRepository.GetByAuthIdAsync(authId);
        if (user == null)
        {
            _logger.LogWarning($"❌ User not found");
            throw new InvalidOperationException("User not found");
        }

        // 5. Validate all ingredients still exist
        if (originalOrder.Ingredients == null || originalOrder.Ingredients.Count == 0)
        {
            _logger.LogWarning($"❌ Original order has no ingredients");
            throw new InvalidOperationException("Original order has no ingredients");
        }

        foreach (var ingredient in originalOrder.Ingredients)
        {
            var currentIngredient = await _ingredientRepository.GetByIdAsync(ingredient.IngredientId);
            if (currentIngredient == null)
            {
                _logger.LogWarning($"❌ Ingredient {ingredient.IngredientId} not available");
                throw new InvalidOperationException("Some ingredients are no longer available");
            }
        }

        _logger.LogInformation($"✅ All validations passed, creating duplicate...");

        // 6. Create duplicate order with UTC DateTimes
        var duplicateOrder = new ScheduledOrder
        {
            UserId = user.UserId,
            AuthId = authId,
            MealName = originalOrder.MealName,
            ScheduledFor = DateTime.SpecifyKind(originalOrder.ScheduledFor, DateTimeKind.Utc), // ✅ FIX
            DeliveryTimeSlot = originalOrder.DeliveryTimeSlot,
            TotalPrice = originalOrder.TotalPrice,
            NutritionalSummary = originalOrder.NutritionalSummary,
            OrderStatus = "scheduled",
            CanModify = true,
            ExpiresAt = DateTime.SpecifyKind(originalOrder.ExpiresAt, DateTimeKind.Utc), // ✅ FIX
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 7. Copy ingredients
        foreach (var originalIngredient in originalOrder.Ingredients)
        {
            duplicateOrder.Ingredients.Add(new ScheduledOrderIngredient
            {
                IngredientId = originalIngredient.IngredientId,
                Quantity = originalIngredient.Quantity,
                UnitPrice = originalIngredient.UnitPrice,
                TotalPrice = originalIngredient.TotalPrice,
                CreatedAt = DateTime.UtcNow
            });
        }

        _logger.LogInformation($"✅ Duplicate prepared with {duplicateOrder.Ingredients.Count} ingredients");

        // 8. Save to database
        var createdOrder = await _scheduledOrderRepository.CreateAsync(duplicateOrder);

        _logger.LogInformation(
            $"✅ Duplicated order #{scheduledOrderId} → #{createdOrder.ScheduledOrderId} " +
            $"for {createdOrder.ScheduledFor:yyyy-MM-dd} (₹{createdOrder.TotalPrice})");

        return await MapToResponseDto(createdOrder);
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning($"⚠️ Duplication validation failed: {ex.Message}");
        throw; // Re-throw validation errors as-is
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"❌ Unexpected error duplicating order #{scheduledOrderId}");
        throw new InvalidOperationException("Failed to duplicate order. Please try again.", ex);
    }
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
