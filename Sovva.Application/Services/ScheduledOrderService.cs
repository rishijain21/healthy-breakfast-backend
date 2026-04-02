using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;


namespace Sovva.Application.Services
{
    public class ScheduledOrderService : IScheduledOrderService
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IOrderService _orderService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<ScheduledOrderService> _logger;
        private readonly IUserAddressRepository _userAddressRepository;


        public ScheduledOrderService(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            ILogger<ScheduledOrderService> logger,
            IUserAddressRepository userAddressRepository)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _orderService = orderService;
            _time = time;
            _logger = logger;
            _userAddressRepository = userAddressRepository;
        }


        // ----------------------------------------------------------------------------------------
        // ✅ CREATE SCHEDULED ORDER (MILKBASKET LOGIC: Order today → Delivery tomorrow)
        // ✅ UPDATED: Now accepts userId directly (from JWT claim) - zero DB hit for user lookup
        // ----------------------------------------------------------------------------------------
        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(int userId, Guid authId, CreateScheduledOrderDto dto, bool skipWalletCheck = false)
        {
            // AuthId still needed for logging/audit, but userId is already known from JWT
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("User not found");

            // ✅ Determine delivery address: use DTO's address or fall back to primary
            int? deliveryAddressId = dto.DeliveryAddressId;
            UserAddress? primaryAddress = null;
            
            if (deliveryAddressId == null)
            {
                primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(user.UserId);
                
                if (primaryAddress == null)
                {
                    throw new InvalidOperationException(
                        "Please add a delivery address before scheduling an order. Go to Profile → Manage Addresses."
                    );
                }
                deliveryAddressId = primaryAddress.Id;
            }
            else
            {
                // ⭐ FIXED: Use GetByIdWithDetailsAsync to load ServiceableLocation
                primaryAddress = await _userAddressRepository.GetByIdWithDetailsAsync(deliveryAddressId.Value);
                if (primaryAddress == null || primaryAddress.UserId != user.UserId)
                {
                    throw new InvalidOperationException("Invalid delivery address");
                }
            }

            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new InvalidOperationException(
                    $"Sorry, we don't deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"} currently. " +
                    "Please update your delivery address."
                );
            }

            _logger.LogInformation($"✅ Delivery address validated: {primaryAddress.ServiceableLocation.Area}, {primaryAddress.ServiceableLocation.City}");

            // ✅ FIXED: Handle ScheduledFor as DateOnly (IST calendar date)
            DateOnly deliveryDate;
            var todayIst = _time.TodayIst;
            
            if (dto.ScheduledFor != default(DateTimeOffset))
            {
                // ✅ DateTimeOffset preserves +05:30 — convert to UTC then IST
                var utc = dto.ScheduledFor.UtcDateTime;           // 2026-04-02T18:30:00 UTC
                var ist = _time.ToIst(utc);                       // 2026-04-03T00:00:00 IST ✅
                deliveryDate = DateOnly.FromDateTime(ist);
                
                _logger.LogInformation("[ScheduledOrder] Parsed delivery date: {Date}", deliveryDate);
            }
            else
            {
                deliveryDate = todayIst.AddDays(1);
                _logger.LogInformation("[ScheduledOrder] No date provided, defaulting to tomorrow: {Date}", deliveryDate);
            }
            
            // ✅ Safety guard — never store today or past
            if (deliveryDate <= todayIst)
            {
                _logger.LogWarning("[ScheduledOrder] Date {Date} is today/past, overriding to tomorrow", deliveryDate);
                deliveryDate = todayIst.AddDays(1);
            }
            
            _logger.LogInformation("[ScheduledOrder] Order placed at: {Ist:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation("[ScheduledOrder] Delivery scheduled for: {Date}", deliveryDate);

            // ✅ Price calculation logic
            decimal totalPrice;
            var ingredients = new List<(Ingredient ingredient, int quantity)>();

            // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var allIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
            var ingredientMap = allIngredients
                .GroupBy(i => i.IngredientId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
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

            // Check wallet balance (now uses userId - PK lookup)
            // skipWalletCheck: bypass for subscription generation (wallet enforced at 11:59 PM confirmation)
            if (!skipWalletCheck && !await CheckWalletBalanceAsync(userId, totalPrice))
                throw new InvalidOperationException("Insufficient wallet balance");

            // Create ScheduledOrder
            var scheduledOrder = new ScheduledOrder
            {
                UserId = userId,
                AuthId = authId,
                MealName = dto.MealName ?? "Custom Overnight Oats",
                MealId = dto.MealId,               // ✅ ADD: Soft reference for traceability
                MealImageUrl = dto.MealImageUrl,   // ✅ ADD: Snapshot for display
                ScheduledFor = deliveryDate,       // ← DateOnly directly
                DeliveryTimeSlot = dto.DeliveryTimeSlot ?? "8:00 AM",
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = "scheduled",
                CanModify = true,
                // ExpiresAt is timestamptz — use UTC midnight of next day
                ExpiresAt = _time.ToUtc(deliveryDate.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                // CreatedAt/UpdatedAt handled by TimestampInterceptor
                DeliveryAddressId = deliveryAddressId,
                // ✅ ADD: Link to subscription if provided
                SubscriptionId = dto.SubscriptionId
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
            
            _logger.LogInformation($"✅ Order #{createdOrder.ScheduledOrderId} created for {deliveryDate:yyyy-MM-dd} delivery");
            _logger.LogInformation($"   💳 Total price: ₹{totalPrice}");
            
            return MapToResponseDto(createdOrder);
        }


        // ----------------------------------------------------------------------------------------
        // ✅ DUPLICATE SCHEDULED ORDER - Creates exact copy without navigation
        // ✅ UPDATED: Now accepts userId directly (from JWT claim) - zero DB hit for user lookup
        // ----------------------------------------------------------------------------------------
        public async Task<ScheduledOrderResponseDto> DuplicateScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId)
        {
            try
            {
                _logger.LogInformation($"🔄 Duplicating order #{scheduledOrderId} for user {userId}");

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

                // 3. Check wallet balance (now uses userId - PK lookup instead of authId join)
                if (!await CheckWalletBalanceAsync(userId, originalOrder.TotalPrice))
                {
                    _logger.LogWarning($"❌ Insufficient balance for duplication");
                    throw new InvalidOperationException("Insufficient wallet balance");
                }

                // ✅ Validate primary address (userId already known from JWT)
                var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
                if (primaryAddress == null)
                {
                    _logger.LogWarning($"❌ No primary address for user");
                    throw new InvalidOperationException("Please add a delivery address before duplicating order");
                }

                if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning($"❌ Location inactive");
                    throw new InvalidOperationException($"We don't deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"} currently");
                }

                // 5. Validate all ingredients still exist
                if (originalOrder.Ingredients == null || originalOrder.Ingredients.Count == 0)
                {
                    _logger.LogWarning($"❌ Original order has no ingredients");
                    throw new InvalidOperationException("Original order has no ingredients");
                }

                // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
                var ingredientIds = originalOrder.Ingredients.Select(i => i.IngredientId).ToList();
                var existingIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
                var existingIds = existingIngredients.Select(i => i.IngredientId).ToHashSet();

                if (ingredientIds.Any(id => !existingIds.Contains(id)))
                {
                    _logger.LogWarning($"❌ Some ingredients no longer available");
                    throw new InvalidOperationException("Some ingredients are no longer available");
                }

                _logger.LogInformation($"✅ All validations passed, creating duplicate...");

                // 6. Create duplicate order with UTC DateTimes
                var duplicateOrder = new ScheduledOrder
                {
                    UserId = userId,
                    AuthId = authId,
                    MealName = originalOrder.MealName,
                    MealId = originalOrder.MealId,               // ✅ ADD: Copy soft reference
                    MealImageUrl = originalOrder.MealImageUrl,   // ✅ ADD: Copy snapshot
                    ScheduledFor = originalOrder.ScheduledFor,  // DateOnly → DateOnly
                    DeliveryTimeSlot = originalOrder.DeliveryTimeSlot,
                    TotalPrice = originalOrder.TotalPrice,
                    NutritionalSummary = originalOrder.NutritionalSummary,
                    OrderStatus = "scheduled",
                    CanModify = true,
                    // ExpiresAt already UTC
                    // CreatedAt/UpdatedAt handled by TimestampInterceptor
                    DeliveryAddressId = originalOrder.DeliveryAddressId
                };

                // 7. Copy ingredients
                foreach (var originalIngredient in originalOrder.Ingredients)
                {
                    duplicateOrder.Ingredients.Add(new ScheduledOrderIngredient
                    {
                        IngredientId = originalIngredient.IngredientId,
                        Quantity = originalIngredient.Quantity,
                        UnitPrice = originalIngredient.UnitPrice,
                        TotalPrice = originalIngredient.TotalPrice
                        // CreatedAt handled by TimestampInterceptor
                    });
                }

                _logger.LogInformation($"✅ Duplicate prepared with {duplicateOrder.Ingredients.Count} ingredients");

                // 8. Save to database
                var createdOrder = await _scheduledOrderRepository.CreateAsync(duplicateOrder);

                _logger.LogInformation(
                    $"✅ Duplicated order #{scheduledOrderId} → #{createdOrder.ScheduledOrderId} " +
                    $"for {createdOrder.ScheduledFor:yyyy-MM-dd} (₹{createdOrder.TotalPrice})");

                return MapToResponseDto(createdOrder);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"⚠️ Duplication validation failed: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Unexpected error duplicating order #{scheduledOrderId}");
                throw new InvalidOperationException("Failed to duplicate order. Please try again.", ex);
            }
        }


        // ----------------------------------------------------------------------------------------
        // GET SCHEDULED ORDERS FOR SPECIFIC DATE
        // ✅ UPDATED: Now accepts userId directly (from JWT claim)
        // ----------------------------------------------------------------------------------------
        public async Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(int userId, Guid authId, DateTime date)
        {
            var orders = await _scheduledOrderRepository.GetByAuthIdAndDateAsync(authId, date);
            var result = new List<ScheduledOrderResponseDto>();

            foreach (var order in orders)
            {
                result.Add(MapToResponseDto(order));
            }

            return result;
        }


        // ----------------------------------------------------------------------------------------
        // MODIFY SCHEDULED ORDER
        // ✅ UPDATED: Now accepts userId directly (from JWT claim) - zero DB hit for user lookup
        // ----------------------------------------------------------------------------------------
        public async Task ModifyScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
                throw new InvalidOperationException("Scheduled order not found");

            // Check if still editable
            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != "scheduled")
                throw new InvalidOperationException("Order can no longer be modified");

            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal newTotalPrice = 0;

            // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var allIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
            var ingredientMap = allIngredients
                .GroupBy(i => i.IngredientId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
                    throw new InvalidOperationException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
                newTotalPrice += ingredient.Price * ingredientDto.Quantity;
            }

            if (!await CheckWalletBalanceAsync(userId, newTotalPrice))
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
            // UpdatedAt handled by TimestampInterceptor

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
            
            _logger.LogInformation($"✏️ Order #{scheduledOrderId} modified - New total: ₹{newTotalPrice}");
        }


        // ----------------------------------------------------------------------------------------
        // CANCEL SCHEDULED ORDER - DELETE FROM DATABASE
        // ✅ UPDATED: Now accepts userId directly (from JWT claim)
        // ----------------------------------------------------------------------------------------
        public async Task CancelScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId)
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
        // ✅ UPDATED: Uses userId directly - PK lookup instead of authId join
        public async Task<bool> CheckWalletBalanceAsync(int userId, decimal amount)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user != null && user.WalletBalance >= amount;
        }


        // ----------------------------------------------------------------------------------------
        // ✅ MIDNIGHT JOB – CONFIRM SCHEDULED ORDERS FOR TODAY (MILKBASKET LOGIC)
        // This runs at 12:00 AM every night to confirm orders for TODAY's delivery
        // ----------------------------------------------------------------------------------------
        public async Task ConfirmAllScheduledOrdersAsync()
        {
            var istNow = _time.ToIst(_time.UtcNow);
            
            // ✅ Job runs at 12:00 AM IST — TodayIst IS the delivery day
            // No AddDays(1) needed — today = the day users receive their breakfast
            var deliveryDate = _time.TodayIst;
            
            _logger.LogInformation($"🌙 [MIDNIGHT JOB] Started at {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Confirming orders for delivery on: {deliveryDate:yyyy-MM-dd}");
            _logger.LogInformation($"⏰ UTC: {_time.UtcNow:yyyy-MM-dd HH:mm:ss} | IST: {istNow:yyyy-MM-dd HH:mm:ss}");
            
            // ✅ Pass DateOnly directly — no UTC range conversion needed
            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(deliveryDate);

            _logger.LogInformation($"📦 Found {scheduledOrders.Count} total orders for {deliveryDate:yyyy-MM-dd}");

            // ✅ IDEMPOTENCY: Skip orders already "scheduled" or "processing" to prevent double-run on retry
            // Also include "failed" to allow retry on failed orders
            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled" 
                         || o.OrderStatus.ToLower() == "processing"
                         || o.OrderStatus.ToLower() == "failed")
                .ToList();

            _logger.LogInformation($"📋 {pendingOrders.Count} orders pending confirmation");

            // ✅ OPTIMIZED: Batch load all users and addresses in single queries to kill N+1
            var authIds = pendingOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in pendingOrders)
            {
                // ✅ INDUSTRY PATTERN: Each order fully isolated
                // One failure never affects the next order
                var success = await ConfirmSingleOrderAsync(scheduledOrder, usersByAuthId);
                if (success) confirmedCount++;
                else failedCount++;
            }

            _logger.LogInformation($"🎉 [MIDNIGHT JOB] Complete!");
            _logger.LogInformation($"   ✅ Confirmed: {confirmedCount}");
            _logger.LogInformation($"   ❌ Failed: {failedCount}");
            _logger.LogInformation($"   ⏭️  Already processed: {scheduledOrders.Count - pendingOrders.Count}");

            // ✅ FIX: If EVERY order failed, throw so Hangfire records a job failure.
            //    This triggers Hangfire's retry policy and surfaces the problem in the dashboard.
            //    Partial failures (some confirmed, some failed) are normal and do NOT throw.
            if (failedCount > 0 && confirmedCount == 0 && pendingOrders.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[MIDNIGHT JOB] All {failedCount} orders failed to confirm. " +
                    $"Check logs for {deliveryDate:yyyy-MM-dd}. " +
                    $"Common causes: wallet balance, inactive delivery location, missing address.");
            }
        }


        // ----------------------------------------------------------------------------------------
        // TIME TILL MIDNIGHT (IST)
        // ----------------------------------------------------------------------------------------
        public TimeSpan GetTimeTillMidnightIST()
        {
            // Note: Cannot be static since it needs _time instance
            var istNow = _time.ToIst(_time.UtcNow);
            var midnight = istNow.Date.AddDays(1);
            return midnight - istNow;
        }


        // ----------------------------------------------------------------------------------------
        // ✅ TIME TILL MIDNIGHT IN MINUTES (for countdown display)
        // ----------------------------------------------------------------------------------------
        public Task<int> GetTimeUntilMidnightMinutesAsync()
        {
            var timeTillMidnight = GetTimeTillMidnightIST();
            return Task.FromResult((int)timeTillMidnight.TotalMinutes);
        }


        // ----------------------------------------------------------------------------------------
        // PRIVATE MAPPING METHODS
        // ----------------------------------------------------------------------------------------
        private ScheduledOrderResponseDto MapToResponseDto(ScheduledOrder order)
        {
            // ✅ Deserialize NutritionalSummary from stored JSON string
            NutritionalSummaryDto? nutritionalSummary = null;
            if (!string.IsNullOrEmpty(order.NutritionalSummary))
            {
                try
                {
                    nutritionalSummary = JsonSerializer.Deserialize<NutritionalSummaryDto>(
                        order.NutritionalSummary
                    );
                }
                catch
                {
                    // Silently ignore malformed JSON — legacy orders may not have it
                }
            }

            return new ScheduledOrderResponseDto
            {
                ScheduledOrderId = order.ScheduledOrderId,
                MealName = order.MealName,
                MealId = order.MealId,               // ✅ ADD: Soft reference for traceability
                MealImageUrl = order.MealImageUrl,   // ✅ ADD: Snapshot for display
                ScheduledFor = order.ScheduledFor.ToDateTime(TimeOnly.MinValue),  // DateOnly → DateTime for DTO
                DeliveryTimeSlot = order.DeliveryTimeSlot,
                TotalPrice = order.TotalPrice,
                OrderStatus = order.OrderStatus,
                CanModify = order.CanModify,
                CreatedAt = order.CreatedAt,
                ExpiresAt = order.ExpiresAt,
                NutritionalSummary = nutritionalSummary,
                Ingredients = order.Ingredients?.Select(i => new ScheduledOrderIngredientDetailDto
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.Ingredient?.IngredientName ?? string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    Category = i.Ingredient?.IngredientCategory?.CategoryName ?? string.Empty,
                    ImageUrl = string.Empty
                }).ToList() ?? new List<ScheduledOrderIngredientDetailDto>(),
                
                // ✅ ADD: Subscription ID for filtering orders by subscription
                SubscriptionId = order.SubscriptionId
            };
        }

        // ----------------------------------------------------------------------------------------
        // ✅ Each order gets its own isolated execution scope
        // Safe to retry — idempotency handled inside ConfirmSingleOrderAsync
        // ----------------------------------------------------------------------------------------
        private async Task<bool> ConfirmSingleOrderAsync(
            ScheduledOrder scheduledOrder,
            Dictionary<Guid, User> usersByAuthId)
        {
            try
            {
                _logger.LogInformation(
                    "🔄 Processing order #{Id}", scheduledOrder.ScheduledOrderId);

                // ── STEP 1: Validate user ────────────────────────────────────────
                if (!usersByAuthId.TryGetValue(scheduledOrder.AuthId, out var user))
                {
                    _logger.LogWarning(
                        "❌ User not found for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "failed");
                    return false;
                }

                // ── STEP 2: Validate address ─────────────────────────────────────
                if (scheduledOrder.DeliveryAddressId == null)
                {
                    _logger.LogWarning(
                        "❌ No delivery address for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "failed");
                    return false;
                }

                var address = await _userAddressRepository
                    .GetByIdWithDetailsAsync(scheduledOrder.DeliveryAddressId.Value);

                if (address?.ServiceableLocation == null 
                    || !address.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning(
                        "❌ Invalid/inactive address for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "failed");
                    return false;
                }

                _logger.LogInformation(
                    "📍 Address validated: {Area} — active: {Active}",
                    address.ServiceableLocation.Area,
                    address.ServiceableLocation.IsActive);

                // ── STEP 3: IDEMPOTENCY — did a previous attempt create the Order? ──
                var existingOrder = await _orderService
                    .GetByScheduledOrderIdAsync(scheduledOrder.ScheduledOrderId);

                if (existingOrder != null)
                {
                    _logger.LogInformation(
                        "♻️ Order #{OrderId} already exists — marking processed",
                        existingOrder.OrderId);
                    await _scheduledOrderRepository.MarkAsProcessedAsync(
                        scheduledOrder.ScheduledOrderId,
                        existingOrder.OrderId,
                        _time.UtcNow);
                    return true;
                }

                // ── STEP 4: Atomic wallet deduction ──────────────────────────────
                bool deducted = await _userRepository
                    .DeductWalletBalanceAtomicAsync(user.UserId, scheduledOrder.TotalPrice);

                if (!deducted)
                {
                    _logger.LogWarning(
                        "❌ Insufficient balance for order #{Id}. Required: ₹{Price}",
                        scheduledOrder.ScheduledOrderId, scheduledOrder.TotalPrice);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "cancelled");
                    return false;
                }

                // ── STEP 5: Create Order row ──────────────────────────────────────
                var orderId = await _orderService
                    .ConfirmScheduledOrderAsync(scheduledOrder);

                // ── STEP 6: Mark scheduled order processed — raw SQL, no EF tracker
                await _scheduledOrderRepository.MarkAsProcessedAsync(
                    scheduledOrder.ScheduledOrderId,
                    orderId,
                    _time.UtcNow);

                _logger.LogInformation(
                    "✅ Confirmed! Order #{OrderId} ← ScheduledOrder #{Id} — ₹{Price}",
                    orderId, scheduledOrder.ScheduledOrderId, scheduledOrder.TotalPrice);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Exception confirming order #{Id}",
                    scheduledOrder.ScheduledOrderId);
                await _scheduledOrderRepository.MarkAsAsync(
                    scheduledOrder.ScheduledOrderId, "failed");
                return false;
            }
        }
    }
}
