using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
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
        private readonly ILogger<ScheduledOrderService> _logger;
        private readonly IUserAddressRepository _userAddressRepository;


        public ScheduledOrderService(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            ILogger<ScheduledOrderService> logger,
            IUserAddressRepository userAddressRepository)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _orderService = orderService;
            _logger = logger;
            _userAddressRepository = userAddressRepository;
        }


        // ----------------------------------------------------------------------------------------
        // ✅ CREATE SCHEDULED ORDER (MILKBASKET LOGIC: Order today → Delivery tomorrow)
        // ✅ UPDATED: Now accepts userId directly (from JWT claim) - zero DB hit for user lookup
        // ----------------------------------------------------------------------------------------
        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(int userId, Guid authId, CreateScheduledOrderDto dto)
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

            // ✅ FIXED: Handle ScheduledFor as DateTime (not string)
            DateTime deliveryDate;
            
            if (dto.ScheduledFor != default(DateTime))
            {
                // Use the date from the DTO (already a DateTime)
                deliveryDate = DateTime.SpecifyKind(dto.ScheduledFor.Date, DateTimeKind.Utc);
                
                _logger.LogInformation($"📅 Using scheduled date from request: {deliveryDate:yyyy-MM-dd}");
            }
            else
            {
                // Fallback: use tomorrow if not provided
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                var tomorrowIst = istNow.Date.AddDays(1);
                deliveryDate = DateTime.SpecifyKind(tomorrowIst, DateTimeKind.Utc);
                
                _logger.LogInformation($"📅 No date provided, using tomorrow: {deliveryDate:yyyy-MM-dd}");
            }
            
            var istZone2 = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow2 = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone2);
            
            _logger.LogInformation($"📦 Order placed at: {istNow2:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Delivery scheduled for: {deliveryDate:yyyy-MM-dd}");

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
            if (!await CheckWalletBalanceAsync(userId, totalPrice))
                throw new InvalidOperationException("Insufficient wallet balance");

            // Create ScheduledOrder
            var scheduledOrder = new ScheduledOrder
            {
                UserId = userId,
                AuthId = authId,
                MealName = dto.MealName ?? "Custom Overnight Oats",
                MealId = dto.MealId,               // ✅ ADD: Soft reference for traceability
                MealImageUrl = dto.MealImageUrl,   // ✅ ADD: Snapshot for display
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
                UpdatedAt = DateTime.UtcNow,
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

                if (!primaryAddress.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning($"❌ Location inactive");
                    throw new InvalidOperationException($"We don't deliver to {primaryAddress.ServiceableLocation.Area} currently");
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
                    ScheduledFor = DateTime.SpecifyKind(originalOrder.ScheduledFor, DateTimeKind.Utc),
                    DeliveryTimeSlot = originalOrder.DeliveryTimeSlot,
                    TotalPrice = originalOrder.TotalPrice,
                    NutritionalSummary = originalOrder.NutritionalSummary,
                    OrderStatus = "scheduled",
                    CanModify = true,
                    ExpiresAt = DateTime.SpecifyKind(originalOrder.ExpiresAt, DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
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
            scheduledOrder.UpdatedAt = DateTime.UtcNow;

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
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            
            // ✅ Process orders for TODAY (not tomorrow)
            var todayIst = istNow.Date;
            
            _logger.LogInformation($"🌙 [MIDNIGHT JOB] Started at {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"🚚 Processing orders for TODAY's delivery: {todayIst:yyyy-MM-dd}");
            _logger.LogInformation($"⏰ Current UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"⏰ Current IST: {istNow:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"📅 Target Date: {todayIst:yyyy-MM-dd}");
            
            // ✅ Convert IST date boundaries back to UTC for DB query
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(todayIst, DateTimeKind.Unspecified), istZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(todayIst.AddDays(1), DateTimeKind.Unspecified), istZone);

            _logger.LogInformation($"🔍 Querying UTC range: {startUtc:yyyy-MM-dd HH:mm:ss} → {endUtc:yyyy-MM-dd HH:mm:ss}");

            // Process orders for TODAY using UTC range
            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForUtcRangeAsync(startUtc, endUtc);

            _logger.LogInformation($"📦 Found {scheduledOrders.Count} total orders for {todayIst:yyyy-MM-dd}");

            // ✅ IDEMPOTENCY: Skip orders already "scheduled" or "processing" to prevent double-run on retry
            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus.ToLower() == "scheduled" || o.OrderStatus.ToLower() == "processing")
                .ToList();

            _logger.LogInformation($"📋 {pendingOrders.Count} orders pending confirmation");

            // ✅ OPTIMIZED: Batch load all users and addresses in single queries to kill N+1
            var authIds = pendingOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            var addressIds = pendingOrders
                .Where(o => o.DeliveryAddressId.HasValue)
                .Select(o => o.DeliveryAddressId!.Value)
                .Distinct().ToList();
            var addresses = await _userAddressRepository.GetByIdsAsync(addressIds);
            var addressesById = addresses.ToDictionary(a => a.Id);

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in pendingOrders)
            {
                try
                {
                    _logger.LogInformation($"🔄 Processing order #{scheduledOrder.ScheduledOrderId}");

                    // ✅ Use pre-loaded dictionary instead of DB call
                    if (!usersByAuthId.TryGetValue(scheduledOrder.AuthId, out var user))
                    {
                        _logger.LogWarning($"❌ User not found for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ Use the scheduled order's DeliveryAddressId (preserve original address)
                    var deliveryAddressId = scheduledOrder.DeliveryAddressId;
                    
                    if (deliveryAddressId == null)
                    {
                        _logger.LogWarning($"❌ No delivery address for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ Use pre-loaded dictionary instead of DB call
                    if (!addressesById.TryGetValue(deliveryAddressId.Value, out var address))
                    {
                        _logger.LogWarning($"❌ FAIL: Address not found for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // Load ServiceableLocation for address
                    var addressWithLocation = await _userAddressRepository.GetByIdWithDetailsAsync(deliveryAddressId.Value);
                    
                    _logger.LogInformation($"🔍 Validating order #{scheduledOrder.ScheduledOrderId}");
                    _logger.LogInformation($"📍 Address loaded: {addressWithLocation != null}");
                    _logger.LogInformation($"📍 ServiceableLocation loaded: {addressWithLocation?.ServiceableLocation != null}");
                    _logger.LogInformation($"📍 ServiceableLocation active: {addressWithLocation?.ServiceableLocation?.IsActive}");
                    
                    if (addressWithLocation == null)
                    {
                        _logger.LogWarning($"❌ FAIL: Address not found for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }
                    
                    if (addressWithLocation.ServiceableLocation == null)
                    {
                        _logger.LogWarning($"❌ FAIL: ServiceableLocation is null for address #{deliveryAddressId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }
                    
                    if (!address.ServiceableLocation.IsActive)
                    {
                        _logger.LogWarning($"❌ FAIL: ServiceableLocation inactive for order #{scheduledOrder.ScheduledOrderId}");
                        scheduledOrder.OrderStatus = "failed";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ FIX: Check wallet balance against FRESH database value to prevent race condition
                    // If we use the pre-loaded user dict, all 7 orders could pass with ₹100 wallet (14.29 each)
                    var freshUser = await _userRepository.GetByIdAsync(user.UserId);
                    if (freshUser == null || freshUser.WalletBalance < scheduledOrder.TotalPrice)
                    {
                        _logger.LogWarning(
                            $"❌ Insufficient balance for order #{scheduledOrder.ScheduledOrderId}. " +
                            $"Required: ₹{scheduledOrder.TotalPrice}, Available: ₹{freshUser?.WalletBalance ?? 0}");
                        
                        scheduledOrder.OrderStatus = "cancelled";
                        scheduledOrder.CanModify = false;
                        await _scheduledOrderRepository.UpdateAsync(scheduledOrder);
                        failedCount++;
                        continue;
                    }

                    // ✅ IDEMPOTENCY GUARD: Mark as "processing" FIRST before doing any financial operation
                    // If the job retries, "processing" orders will be skipped (see filter above)
                    scheduledOrder.OrderStatus = "processing";
                    await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

                    // ✅ NEW: Use dedicated method for confirming scheduled orders
                    // No catalogue lookup, no UserMeal creation, no price recalculation
                    var orderId = await _orderService.ConfirmScheduledOrderAsync(scheduledOrder);

                    _logger.LogInformation(
                        $"✅ Confirmed! Created Order #{orderId} from cart order #{scheduledOrder.ScheduledOrderId}");
                    _logger.LogInformation($"   📍 Delivers to address ID: {scheduledOrder.DeliveryAddressId}");
                    _logger.LogInformation($"   💰 Amount charged: ₹{scheduledOrder.TotalPrice}");

                    // ✅ FIX: Populate audit trail fields
                    scheduledOrder.OrderStatus = "processed";
                    scheduledOrder.CanModify = false;
                    scheduledOrder.ConfirmedAt = DateTime.UtcNow;
                    scheduledOrder.IsProcessedToOrder = true;    // ✅ Audit: mark as processed
                    scheduledOrder.ConfirmedOrderId = orderId; // ✅ Audit: link to confirmed order
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

            // ✅ FIX: If EVERY order failed, throw so Hangfire records a job failure.
            //    This triggers Hangfire's retry policy and surfaces the problem in the dashboard.
            //    Partial failures (some confirmed, some failed) are normal and do NOT throw.
            if (failedCount > 0 && confirmedCount == 0 && pendingOrders.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[MIDNIGHT JOB] All {failedCount} orders failed to confirm. " +
                    $"Check logs for {todayIst:yyyy-MM-dd}. " +
                    $"Common causes: wallet balance, inactive delivery location, missing address.");
            }
        }


        // ----------------------------------------------------------------------------------------
        // TIME TILL MIDNIGHT (IST)
        // ----------------------------------------------------------------------------------------
        public static TimeSpan GetTimeTillMidnightIST()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
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
                ScheduledFor = order.ScheduledFor,
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
    }
}
