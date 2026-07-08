using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Sovva.Application.Interfaces;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;


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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMealRepository _mealRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

        public ScheduledOrderService(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            ILogger<ScheduledOrderService> logger,
            IUserAddressRepository userAddressRepository,
            IUnitOfWork unitOfWork,
            IMealRepository mealRepository,
            IOrderRepository orderRepository,
            IWalletTransactionRepository walletTransactionRepository,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _orderService = orderService;
            _time = time;
            _logger = logger;
            _userAddressRepository = userAddressRepository;
            _unitOfWork = unitOfWork;
            _mealRepository = mealRepository;
            _orderRepository = orderRepository;
            _walletTransactionRepository = walletTransactionRepository;
            _scopeFactory = scopeFactory;
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
                throw new UserNotFoundException(userId);

            // ✅ Determine delivery address: use DTO's address or fall back to primary
            int? deliveryAddressId = dto.DeliveryAddressId;
            UserAddress? primaryAddress = null;
            
            if (deliveryAddressId == null)
            {
                primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(user.UserId);
                
                if (primaryAddress == null)
                {
                    throw new AddressNotFoundException(user.UserId);
                }
                deliveryAddressId = primaryAddress.Id;
            }
            else
            {
                // ⭐ FIXED: Use GetByIdWithDetailsAsync to load ServiceableLocation
                primaryAddress = await _userAddressRepository.GetByIdWithDetailsAsync(deliveryAddressId.Value);
                if (primaryAddress == null || primaryAddress.UserId != user.UserId)
                {
                    throw new AddressNotFoundException(user.UserId);
                }
            }

            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new AddressNotFoundException(user.UserId, 
                    $"Sorry, we don't deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"} currently. " +
                    "Please update your delivery address.");
            }

            _logger.LogInformation("Delivery address validated: {Area}, {City}", primaryAddress.ServiceableLocation.Area, primaryAddress.ServiceableLocation.City);

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
            
            _logger.LogInformation("[ScheduledOrder] Order placed at: {Ist:yyyy-MM-dd HH:mm:ss} IST", _time.NowIst);
            _logger.LogInformation("[ScheduledOrder] Delivery scheduled for: {Date}", deliveryDate);

            // ✅ Price calculation logic
            decimal totalPrice;
            var ingredients = new List<(Ingredient ingredient, int quantity)>();

            // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
            }

            // ✅ FEATURED MEAL: Use fixed price if provided
            if (dto.MealPrice.HasValue && dto.MealPrice.Value > 0)
            {
                totalPrice = dto.MealPrice.Value;
                _logger.LogInformation("Using featured meal fixed price: {TotalPrice}", totalPrice);
            }
            else
            {
                // ✅ CUSTOM MEAL: Calculate from ingredients + BasePrice
                if (!dto.MealId.HasValue)
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("MealId is required for custom meal calculation");

                var meal = await _mealRepository.GetByIdAsync(dto.MealId.Value);
                if (meal == null)
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Meal {dto.MealId} not found");

                totalPrice = meal.BasePrice + ingredients.Sum(i => i.ingredient.Price * i.quantity);
                _logger.LogInformation("Calculated price from ingredients + BasePrice ({BasePrice}): {TotalPrice}", meal.BasePrice, totalPrice);
            }

            // Check wallet balance (now uses userId - PK lookup)
            // skipWalletCheck: bypass for subscription generation (wallet enforced at 11:59 PM confirmation)
            if (!skipWalletCheck && !await CheckWalletBalanceAsync(userId, totalPrice))
            {
                var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                throw new InsufficientBalanceException(totalPrice, currentBalance);
            }

            // Create ScheduledOrder
            var scheduledOrder = new ScheduledOrder
            {
                UserId = userId,
                AuthId = authId,
                MealName = dto.MealName ?? DeliveryConstants.DefaultMealName,
                MealId = dto.MealId,               // ✅ ADD: Soft reference for traceability
                MealImageUrl = CleanMealImageUrl(dto.MealImageUrl),   // ✅ ADD: Clean snapshot for display
                ScheduledFor = deliveryDate,       // ← DateOnly directly
                DeliveryTimeSlot = dto.DeliveryTimeSlot ?? DeliveryConstants.DefaultTimeSlot,
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = ScheduledOrderStatus.Scheduled,
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
            
            _logger.LogInformation("Order {OrderId} created for {DeliveryDate} delivery, total: {TotalPrice}", createdOrder.ScheduledOrderId, deliveryDate, totalPrice);
            
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
                _logger.LogInformation("Duplicating order {OrderId} for user {UserId}", scheduledOrderId, userId);

                // 1. Find original order
                var originalOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
                if (originalOrder == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for duplication", scheduledOrderId);
                    throw new ScheduledOrderNotFoundException(scheduledOrderId);
                }

                _logger.LogInformation("Found original order {OrderId}: {MealName}", scheduledOrderId, originalOrder.MealName);

                // 2. Validate order can be duplicated
                if (originalOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                {
                    _logger.LogWarning("Cannot duplicate order {OrderId} with status {OrderStatus}", scheduledOrderId, originalOrder.OrderStatus);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Cannot duplicate order with status '{originalOrder.OrderStatus}'");
                }

                // 3. Check wallet balance (now uses userId - PK lookup instead of authId join)
                if (!await CheckWalletBalanceAsync(userId, originalOrder.TotalPrice))
                {
                    _logger.LogWarning("Insufficient balance for duplication of order {OrderId}", scheduledOrderId);
                    var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                    throw new InsufficientBalanceException(originalOrder.TotalPrice, currentBalance);
                }

                // ✅ Validate primary address (userId already known from JWT)
                var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
                if (primaryAddress == null)
                {
                    _logger.LogWarning("No primary address for user {UserId}", userId);
                    throw new AddressNotFoundException(originalOrder.UserId);
                }

                if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning("Location inactive for user {UserId}", userId);
                    throw new AddressNotFoundException(originalOrder.UserId);
                }

                // 5. Validate all ingredients still exist
                if (originalOrder.Ingredients == null || originalOrder.Ingredients.Count == 0)
                {
                    _logger.LogWarning("Original order {OrderId} has no ingredients", scheduledOrderId);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("Original order has no ingredients");
                }

                // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
                var ingredientIds = originalOrder.Ingredients.Select(i => i.IngredientId).ToList();
                var existingIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
                var existingIds = existingIngredients.Keys.ToHashSet();

                if (ingredientIds.Any(id => !existingIds.Contains(id)))
                {
                    _logger.LogWarning("Some ingredients no longer available for order {OrderId}", scheduledOrderId);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("Some ingredients are no longer available");
                }

                _logger.LogInformation("All validations passed for order {OrderId}, creating duplicate", scheduledOrderId);

                // 6. Create duplicate order with UTC DateTimes
                var duplicateOrder = new ScheduledOrder
                {
                    UserId = userId,
                    AuthId = authId,
                    MealName = originalOrder.MealName,
                    MealId = originalOrder.MealId,               // ✅ ADD: Copy soft reference
                    MealImageUrl = CleanMealImageUrl(originalOrder.MealImageUrl),   // ✅ ADD: Copy clean snapshot
                    ScheduledFor = originalOrder.ScheduledFor,  // DateOnly → DateOnly
                    DeliveryTimeSlot = originalOrder.DeliveryTimeSlot,
                    TotalPrice = originalOrder.TotalPrice,
                    NutritionalSummary = originalOrder.NutritionalSummary,
                    OrderStatus = ScheduledOrderStatus.Scheduled,
                    CanModify = true,
                    ExpiresAt = _time.ToUtc(originalOrder.ScheduledFor.AddDays(1).ToDateTime(TimeOnly.MinValue)),
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

                _logger.LogInformation("Duplicate prepared with {IngredientCount} ingredients", duplicateOrder.Ingredients.Count);

                // 8. Save to database
                var createdOrder = await _scheduledOrderRepository.CreateAsync(duplicateOrder);

                _logger.LogInformation(
                    $"✅ Duplicated order #{scheduledOrderId} → #{createdOrder.ScheduledOrderId} " +
                    $"for {createdOrder.ScheduledFor:yyyy-MM-dd} (₹{createdOrder.TotalPrice})");

                return MapToResponseDto(createdOrder);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Duplication validation failed for order {OrderId}: {ErrorMessage}", scheduledOrderId, ex.Message);
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
                throw new ScheduledOrderNotFoundException(scheduledOrderId);

            // P1-3 FIX: Explicit userId ownership check — defense-in-depth beyond authId
            if (scheduledOrder.UserId != userId)
                throw new UnauthorizedAccessException("Order does not belong to this user");

            // Check if still editable
            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                throw new Sovva.Domain.Exceptions.BusinessRuleException("Order can no longer be modified");

            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal newTotalPrice = 0;

            // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
                newTotalPrice += ingredient.Price * ingredientDto.Quantity;
            }

            if (!await CheckWalletBalanceAsync(userId, newTotalPrice))
            {
                var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                throw new InsufficientBalanceException(newTotalPrice, currentBalance);
            }

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
            
            _logger.LogInformation("Order {OrderId} modified - New total: {NewTotalPrice}", scheduledOrderId, newTotalPrice);
        }


        // ----------------------------------------------------------------------------------------
        // CANCEL SCHEDULED ORDER - DELETE FROM DATABASE
        // ✅ UPDATED: Now accepts userId directly (from JWT claim)
        // ----------------------------------------------------------------------------------------
        public async Task CancelScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId)
        {
            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
            {
                // ✅ FIX: Idempotent delete. If the order is already deleted (e.g., via cascading subscription cancel), 
                // we treat the cancellation as successful instead of throwing a 404, preventing frontend UI errors.
                _logger.LogInformation("Order {OrderId} not found during cancellation (likely already deleted) - treating as success", scheduledOrderId);
                return;
            }

            // P1-3 FIX: Explicit userId ownership check — defense-in-depth beyond authId
            if (scheduledOrder.UserId != userId)
                throw new UnauthorizedAccessException("Order does not belong to this user");

            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                throw new Sovva.Domain.Exceptions.BusinessRuleException("Order can no longer be cancelled");

            _logger.LogInformation("User cancelled order {OrderId} - deleting from cart", scheduledOrderId);
            
            await _scheduledOrderRepository.DeleteAsync(scheduledOrderId);
            
            _logger.LogInformation("Order {OrderId} successfully removed from cart", scheduledOrderId);
        }


        // ----------------------------------------------------------------------------------------
        // BALANCE CHECK
        // ----------------------------------------------------------------------------------------
        // P0-4 FIX: Uses the WalletTransaction ledger (single source of truth) instead of
        // the stale User.WalletBalance computed property. HasSufficientBalanceAsync queries:
        //   SUM(CASE WHEN Type='Credit' THEN Amount ELSE -Amount END) >= amount
        // directly against the WalletTransactions table.
        public async Task<bool> CheckWalletBalanceAsync(int userId, decimal amount)
        {
            return await _walletService.HasSufficientBalanceAsync(userId, amount);
        }


        // ----------------------------------------------------------------------------------------
        // ✅ MIDNIGHT JOB – CONFIRM SCHEDULED ORDERS FOR TODAY (MILKBASKET LOGIC)
        // This runs at 12:00 AM every night to confirm orders for TODAY's delivery
        // ----------------------------------------------------------------------------------------
        public async Task<ProcessOrdersResponseDto> ConfirmAllScheduledOrdersAsync(DateOnly? targetDate = null)
        {
            // ✅ FIX [D-1]: When targetDate is null (called from Hangfire), default to TomorrowIst.
            // This ensures if the job runs at 11:59:59 PM IST, it correctly confirms for the next calendar day.
            // If called from manual admin trigger, targetDate will be provided.
            var deliveryDate = targetDate ?? _time.TomorrowIst;
            var istNow = _time.ToIst(_time.UtcNow);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("[MIDNIGHT JOB] Starting confirmation for Date: {Date} IST (System Today: {Today} IST)", 
                deliveryDate, _time.TodayIst);
            
            _logger.LogInformation("[MIDNIGHT JOB] Started at {IstNow} IST", istNow.ToString("yyyy-MM-dd HH:mm:ss"));
            _logger.LogInformation("Confirming orders for delivery on: {DeliveryDate}", deliveryDate);
            _logger.LogInformation("UTC: {UtcNow} | IST: {IstNow}", _time.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), istNow.ToString("yyyy-MM-dd HH:mm:ss"));
            
            // ✅ Pass DateOnly directly — no UTC range conversion needed
            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(deliveryDate);

            _logger.LogInformation("Found {TotalOrders} total orders for {DeliveryDate}", scheduledOrders.Count, deliveryDate);

            // ✅ IDEMPOTENCY: Skip orders already "scheduled" or "processing" to prevent double-run on retry
            // Also include "failed" to allow retry on failed orders
            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus == ScheduledOrderStatus.Scheduled
                         || o.OrderStatus == ScheduledOrderStatus.Processing
                         || o.OrderStatus == ScheduledOrderStatus.Failed)
                .ToList();

            _logger.LogInformation("{PendingCount} orders pending confirmation", pendingOrders.Count);

            if (pendingOrders.Count == 0)
            {
                var alreadyProcessed = scheduledOrders.Count(o => o.OrderStatus == ScheduledOrderStatus.Processed);
                return new ProcessOrdersResponseDto
                {
                    Success               = true,
                    Message               = $"No pending orders for {deliveryDate:yyyy-MM-dd}",
                    DeliveryDate          = deliveryDate.ToDateTime(TimeOnly.MinValue),
                    OrdersFound           = scheduledOrders.Count,
                    OrdersPending         = 0,
                    OrdersAlreadyConfirmed = alreadyProcessed,
                    OrdersConfirmed       = 0,
                    OrdersFailed          = 0,
                    Timestamp             = _time.UtcNow,
                    Note                  = "Safe to call multiple times — idempotent"
                };
            }

            // ✅ OPTIMIZED: Batch load all users and addresses in single queries to kill N+1
            var authIds = pendingOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            // ✅ FIX 13: Batch load idempotency data
            var scheduledOrderIds = pendingOrders.Select(o => o.ScheduledOrderId).ToList();
            var existingOrdersByScheduledOrderId = await _orderRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);
            var existingTransactionsByScheduledOrderId = await _walletTransactionRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);

            // ✅ FIX: Batch load delivery addresses
            var addressIds = pendingOrders
                .Where(o => o.DeliveryAddressId.HasValue)
                .Select(o => o.DeliveryAddressId!.Value)
                .Distinct().ToList();
            var addressesMap = (await _userAddressRepository.GetByIdsWithDetailsAsync(addressIds))
                .ToDictionary(a => a.Id);

            int confirmedCount = 0;
            int failedCount = 0;

            // ✅ PHASE 2: Parallelize order confirmation
            var semaphore = new SemaphoreSlim(10); // Process 10 orders concurrently
            var tasks = pendingOrders.Select(async scheduledOrder =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Isolated scope per order to ensure thread safety with EF Core DbContext
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var scopedService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IScheduledOrderService>(scope.ServiceProvider);
                    
                    var success = await scopedService.ProcessSingleScheduledOrderAsync(
                        scheduledOrder, 
                        usersByAuthId, 
                        existingOrdersByScheduledOrderId, 
                        existingTransactionsByScheduledOrderId,
                        addressesMap);
                        
                    if (success) Interlocked.Increment(ref confirmedCount);
                    else Interlocked.Increment(ref failedCount);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            _logger.LogInformation(
                "[JOB-METRICS] {@Metrics}", new
                {
                    Job = "scheduled-order-confirmation",
                    Date = deliveryDate.ToString("yyyy-MM-dd"),
                    Found = scheduledOrders.Count,
                    Pending = pendingOrders.Count,
                    Confirmed = confirmedCount,
                    Failed = failedCount,
                    DurationMs = stopwatch.ElapsedMilliseconds
                });

            _logger.LogInformation("[MIDNIGHT JOB] Complete! Confirmed: {Confirmed}, Failed: {Failed}, Already processed: {AlreadyProcessed}",
                confirmedCount, failedCount, scheduledOrders.Count - pendingOrders.Count);

            if (failedCount > 0 && confirmedCount == 0 && pendingOrders.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[MIDNIGHT JOB] All {failedCount} orders failed to confirm. " +
                    $"Check logs for {deliveryDate:yyyy-MM-dd}. " +
                    $"Common causes: wallet balance, inactive delivery location, missing address.");
            }

            return new ProcessOrdersResponseDto
            {
                Success               = confirmedCount > 0 || failedCount == 0,
                Message               = $"Processed {confirmedCount} orders for {deliveryDate:yyyy-MM-dd}",
                DeliveryDate          = deliveryDate.ToDateTime(TimeOnly.MinValue),
                OrdersFound           = scheduledOrders.Count,
                OrdersPending         = pendingOrders.Count,
                OrdersAlreadyConfirmed = scheduledOrders.Count - pendingOrders.Count,
                OrdersConfirmed       = confirmedCount,
                OrdersFailed          = failedCount,
                Timestamp             = _time.UtcNow,
                Note                  = "Safe to call multiple times — idempotent"
            };
        }

        public async Task<ProcessOrdersResponseDto> RetryFailedOrdersAsync(DateOnly? targetDate = null, string? correlationId = null)
        {
            var cid = correlationId ?? Guid.NewGuid().ToString("N")[..8];
            using var scope = _logger.BeginScope(new Dictionary<string, object> { { "CorrelationId", cid } });

            var failedOrders = await _scheduledOrderRepository.GetFailedScheduledOrdersAsync(targetDate);
            
            if (failedOrders.Count == 0)
            {
                return new ProcessOrdersResponseDto
                {
                    Success = true,
                    Message = "No failed orders found to retry.",
                    DeliveryDate = targetDate?.ToDateTime(TimeOnly.MinValue) ?? _time.UtcNow,
                    OrdersFound = 0,
                    OrdersPending = 0,
                    OrdersAlreadyConfirmed = 0,
                    OrdersConfirmed = 0,
                    OrdersFailed = 0,
                    Timestamp = _time.UtcNow
                };
            }

            var authIds = failedOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            var scheduledOrderIds = failedOrders.Select(o => o.ScheduledOrderId).ToList();
            var existingOrdersByScheduledOrderId = await _orderRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);
            var existingTransactionsByScheduledOrderId = await _walletTransactionRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);

            var addressIds = failedOrders
                .Where(o => o.DeliveryAddressId.HasValue)
                .Select(o => o.DeliveryAddressId!.Value)
                .Distinct().ToList();
            var addressesMap = (await _userAddressRepository.GetByIdsWithDetailsAsync(addressIds))
                .ToDictionary(a => a.Id);

            int confirmedCount = 0;
            int failedCount = 0;

            var semaphore = new SemaphoreSlim(10);
            var tasks = failedOrders.Select(async scheduledOrder =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await using var s = _scopeFactory.CreateAsyncScope();
                    var scopedService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IScheduledOrderService>(s.ServiceProvider);
                    
                    var success = await scopedService.ProcessSingleScheduledOrderAsync(
                        scheduledOrder, 
                        usersByAuthId, 
                        existingOrdersByScheduledOrderId, 
                        existingTransactionsByScheduledOrderId,
                        addressesMap);
                        
                    if (success) System.Threading.Interlocked.Increment(ref confirmedCount);
                    else System.Threading.Interlocked.Increment(ref failedCount);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return new ProcessOrdersResponseDto
            {
                Success = true,
                Message = $"Retry complete. {confirmedCount} succeeded, {failedCount} failed.",
                DeliveryDate = targetDate?.ToDateTime(TimeOnly.MinValue) ?? _time.UtcNow,
                OrdersFound = failedOrders.Count,
                OrdersPending = failedOrders.Count,
                OrdersAlreadyConfirmed = 0,
                OrdersConfirmed = confirmedCount,
                OrdersFailed = failedCount,
                Timestamp = _time.UtcNow
            };
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
                OrderStatus = order.OrderStatus.ToString(),
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
                    ImageUrl = i.Ingredient?.IconEmoji ?? string.Empty,
                    Calories = i.Ingredient?.Calories ?? 0,
                    Protein = i.Ingredient?.Protein ?? 0,
                    Fiber = i.Ingredient?.Fiber ?? 0
                }).ToList() ?? new List<ScheduledOrderIngredientDetailDto>(),
                
                // ✅ ADD: Subscription ID for filtering orders by subscription
                SubscriptionId = order.SubscriptionId
            };
        }

        // ----------------------------------------------------------------------------------------
        // ✅ Each order gets its own isolated execution scope
        // Safe to retry — idempotency handled inside ProcessSingleScheduledOrderAsync
        // ----------------------------------------------------------------------------------------
        public async Task<bool> ProcessSingleScheduledOrderAsync(
            ScheduledOrder scheduledOrder,
            IReadOnlyDictionary<Guid, User> usersByAuthId,
            IReadOnlyDictionary<int, Order> existingOrders,
            IReadOnlyDictionary<int, WalletTransaction> existingTransactions,
            IReadOnlyDictionary<int, UserAddress> addressesMap)
        {
            try
            {
                _logger.LogInformation(
                    "Processing order #{Id}", scheduledOrder.ScheduledOrderId);

                // ── STEP 1: Validate user ────────────────────────────────────────
                if (!usersByAuthId.TryGetValue(scheduledOrder.AuthId, out var user))
                {
                    _logger.LogWarning(
                        "User not found for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                // ── STEP 2: Validate address ─────────────────────────────────────
                if (scheduledOrder.DeliveryAddressId == null)
                {
                    _logger.LogWarning(
                        "No delivery address for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                if (!addressesMap.TryGetValue(scheduledOrder.DeliveryAddressId.Value, out var address))
                {
                    _logger.LogWarning(
                        "Address {AddressId} not found for order #{Id}",
                        scheduledOrder.DeliveryAddressId.Value, scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                if (address?.ServiceableLocation == null 
                    || !address.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning(
                        "Invalid/inactive address for order #{Id}",
                        scheduledOrder.ScheduledOrderId);
                    await _scheduledOrderRepository.MarkAsAsync(
                        scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                _logger.LogInformation(
                    "Address validated: {Area} — active: {Active}",
                    address.ServiceableLocation.Area,
                    address.ServiceableLocation.IsActive);

                // ── STEP 3: IDEMPOTENCY — did a previous attempt create the Order? ──
                // ✅ FIX 13: Look up from batch dictionary instead of DB
                existingOrders.TryGetValue(scheduledOrder.ScheduledOrderId, out var existingOrder);

                if (existingOrder != null)
                {
                    // ✅ FIX 13: Look up from batch dictionary instead of DB
                    var walletTxExists = existingTransactions.ContainsKey(scheduledOrder.ScheduledOrderId);

                    if (walletTxExists)
                    {
                        _logger.LogInformation(
                            "Order #{OrderId} exists + wallet debited — marking processed",
                            existingOrder.OrderId);
                        await _scheduledOrderRepository.MarkAsProcessedAsync(
                            scheduledOrder.ScheduledOrderId,
                            existingOrder.OrderId,
                            _time.UtcNow);
                        return true;
                    }
                    else
                    {
                        // Order row exists but wallet was NOT debited (partial failure in prior run)
                        // Use AtomicDebitAsync — single SQL that checks balance + inserts debit record
                        _logger.LogWarning(
                            "Order #{OrderId} exists but no wallet transaction found - completing payment now",
                            existingOrder.OrderId);

                        var debitResult = await _walletService.AtomicDebitAsync(
                            user.UserId,
                            scheduledOrder.TotalPrice,
                            $"Order #{existingOrder.OrderId} - {scheduledOrder.MealName}",
                            scheduledOrder.ScheduledOrderId);

                        if (!debitResult.Success)
                        {
                            await _scheduledOrderRepository.MarkAsAsync(
                                scheduledOrder.ScheduledOrderId, ScheduledOrderStatus.Cancelled.ToString());
                            return false;
                        }

                        await _scheduledOrderRepository.MarkAsProcessedAsync(
                            scheduledOrder.ScheduledOrderId,
                            existingOrder.OrderId,
                            _time.UtcNow);
                        return true;
                    }
                }

                // ── STEPS 4-6: Atomic Transaction ────────────────────────────────
                // P0-1/P0-2 FIX: AtomicDebitAsync replaces the broken two-step flow.
                // It does balance check + debit record insertion in a SINGLE SQL INSERT.
                // The Order creation and status update still need a transaction wrapper
                // for consistency — if Order creation fails, we need rollback.
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // ── STEP 4: Atomic wallet deduction + ledger write (single SQL) ──
                    var debitResult = await _walletService.AtomicDebitAsync(
                        user.UserId,
                        scheduledOrder.TotalPrice,
                        $"Scheduled Order #{scheduledOrder.ScheduledOrderId} - {scheduledOrder.MealName}",
                        scheduledOrder.ScheduledOrderId);

                    if (!debitResult.Success)
                    {
                        var currentBalance = await _walletService.GetUserBalanceAsync(user.UserId);
                        throw new InsufficientBalanceException(scheduledOrder.TotalPrice, currentBalance);
                    }

                    // ── STEP 5: Create Order row ──
                    var orderId = await _orderService.ConfirmScheduledOrderAsync(scheduledOrder, existingOrder);

                    // ── STEP 6: Mark scheduled order processed ──
                    await _scheduledOrderRepository.MarkAsProcessedAsync(
                        scheduledOrder.ScheduledOrderId,
                        orderId,
                        _time.UtcNow);

                    _logger.LogInformation(
                        "Confirmed Order #{OrderId} from ScheduledOrder #{Id} - {Price}",
                        orderId, scheduledOrder.ScheduledOrderId, scheduledOrder.TotalPrice);
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception confirming order #{Id}",
                    scheduledOrder.ScheduledOrderId);

                // For validation-like errors (e.g. insufficient balance), mark as failed so we don't keep retrying
                if (ex is InsufficientBalanceException)
                {
                    await _scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                }
                
                return false;
            }
        }

        private static string? CleanMealImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var idx = url.IndexOf("meal-images/", StringComparison.OrdinalIgnoreCase);
            var clean = idx >= 0 ? url.Substring(idx) : url;
            var qIdx = clean.IndexOf('?');
            return qIdx >= 0 ? clean.Substring(0, qIdx) : clean;
        }
    }
}
