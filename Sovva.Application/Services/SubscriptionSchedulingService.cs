// Sovva.Application/Services/SubscriptionSchedulingService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.Exceptions;
using Sovva.Domain.Constants;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Sovva.Application.Tests")]

namespace Sovva.Application.Services
{
    public class SubscriptionSchedulingService : ISubscriptionSchedulingService
    {
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IScheduledOrderRepository _scheduledOrderRepo;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IUserMealRepository _userMealRepo;
        private readonly IUserMealIngredientRepository _userMealIngredientRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUserAddressRepository _userAddressRepo;
        private readonly IMealRepository _mealRepo;
        private readonly IIngredientRepository _ingredientRepo;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<SubscriptionSchedulingService> _logger;

        public SubscriptionSchedulingService(
            ISubscriptionRepository subscriptionRepo,
            IScheduledOrderRepository scheduledOrderRepo,
            IScheduledOrderService scheduledOrderService,
            IUserMealRepository userMealRepo,
            IUserMealIngredientRepository userMealIngredientRepo,
            IUserRepository userRepo,
            IUserAddressRepository userAddressRepo,
            IMealRepository mealRepo,
            IIngredientRepository ingredientRepo,
            IAppTimeProvider time,
            ILogger<SubscriptionSchedulingService> logger)
        {
            _subscriptionRepo        = subscriptionRepo;
            _scheduledOrderRepo      = scheduledOrderRepo;
            _scheduledOrderService   = scheduledOrderService;
            _userMealRepo            = userMealRepo;
            _userMealIngredientRepo  = userMealIngredientRepo;
            _userRepo                = userRepo;
            _userAddressRepo         = userAddressRepo;
            _mealRepo                = mealRepo;
            _ingredientRepo          = ingredientRepo;
            _time                    = time;
            _logger                  = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // NIGHTLY JOB — 12:01 AM IST
        // Generates ScheduledOrders for tomorrow's delivery (today + 1)
        // Runs one minute AFTER the midnight confirm job.
        // ─────────────────────────────────────────────────────────────────────
        public async Task GenerateScheduledOrdersFromSubscriptionsAsync(string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString("N");
            using var loggerScope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
            var istNow      = _time.ToIst(_time.UtcNow);
            var today       = _time.TodayIst;           // April 3 (job runs at 12:01 AM April 3)
            var deliveryDay = today.AddDays(1);          // April 4 — the day we're scheduling for

            _logger.LogInformation(
                "[SUB-JOB] Started at {Now:yyyy-MM-dd HH:mm:ss} IST. Generating orders for {DeliveryDay:yyyy-MM-dd}",
                istNow, deliveryDay);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var allSubscriptions = await _subscriptionRepo.GetActiveSubscriptionsAsync();
            _logger.LogInformation("[SUB-JOB] Active subscriptions: {Count}", allSubscriptions.Count());

            // ── BATCH LOAD ──────
            var userMealIds = allSubscriptions.Where(s => s.UserMealId.HasValue).Select(s => s.UserMealId!.Value).Distinct().ToList();
            var mealIds     = allSubscriptions.Where(s => s.MealId.HasValue).Select(s => s.MealId!.Value).Distinct().ToList();
            var userIds     = allSubscriptions.Select(s => s.UserId).Distinct().ToList();

            var userMealsMap  = (await _userMealRepo.GetByIdsAsync(userMealIds))
                                .ToDictionary(m => m.UserMealId);

            var userMealIngredientsMap = (await _userMealIngredientRepo.GetByUserMealIdsAsync(userMealIds))
                                         .GroupBy(i => i.UserMealId)
                                         .ToDictionary(g => g.Key, g => g.ToList());

            var mealsMap      = (await _mealRepo.GetByIdsWithOptionsAsync(mealIds))
                                .ToDictionary(m => m.MealId);

            var usersMap      = (await _userRepo.GetByIdsWithAuthMappingAsync(userIds))
                                .ToDictionary(u => u.UserId);

            var addressesMap  = (await _userAddressRepo.GetPrimaryAddressesByUserIdsAsync(userIds))
                                .ToDictionary(a => a.UserId);

            var allIngredientIds = new HashSet<int>();
            foreach (var umiList in userMealIngredientsMap.Values)
                foreach (var umi in umiList)
                    allIngredientIds.Add(umi.IngredientId);

            foreach (var meal in mealsMap.Values)
            {
                var defaultOption = meal.MealOptions?.FirstOrDefault();
                if (defaultOption != null)
                    foreach (var moi in defaultOption.MealOptionIngredients)
                        allIngredientIds.Add(moi.IngredientId);
            }

            var allIngredientsMap = await _ingredientRepo.GetByIdsAsync(allIngredientIds);
            // ─────────────────────────────────────────────────────────────────

            int generated = 0, skipped = 0, failed = 0;
            
            // ✅ BATCH DUPLICATE CHECK
            var subscriptionIds = allSubscriptions.Select(s => s.SubscriptionId).ToList();
            var existingOrderSet = (await _scheduledOrderRepo.GetExistingSubscriptionOrdersForDateAsync(subscriptionIds, deliveryDay)).ToHashSet();

            // ✅ BATCH NEXT SCHEDULED DATE UPDATE
            var subscriptionsToUpdate = new List<Subscription>();
            
            // ✅ BATCH ORDER CREATION
            var scheduledOrdersToCreate = new List<ScheduledOrder>();

            foreach (var subscription in allSubscriptions)
            {
                try 
                {
                    // 1. Is this subscription due on deliveryDay?
                    if (!IsDueOnDate(subscription, deliveryDay))
                    {
                        _logger.LogDebug(
                            "[SUB-JOB] Subscription #{Id} ({Freq}) not due on {Date} — NextScheduledDate: {Next}",
                            subscription.SubscriptionId, subscription.Frequency, deliveryDay,
                            subscription.NextScheduledDate?.ToString("yyyy-MM-dd") ?? "null");
                        skipped++;
                        continue;
                    }

                    // 2. EndDate guard
                    if (subscription.EndDate <= today)
                    {
                        _logger.LogInformation(
                            "[SUB-JOB] Subscription #{Id} expired on {End}, skipping",
                            subscription.SubscriptionId, subscription.EndDate);
                        skipped++;
                        continue;
                    }

                    // 3. Duplicate guard — DB unique index also enforces this, but check first
                    //    to avoid noisy constraint violations in logs
                    if (existingOrderSet.Contains(subscription.SubscriptionId))
                    {
                        _logger.LogInformation(
                            "[SUB-JOB] Order already exists for subscription #{Id} on {Date}, skipping",
                            subscription.SubscriptionId, deliveryDay);
                        skipped++;
                        continue;
                    }

                    // 4. Resolve quantity (weekly subscriptions can have per-day quantities)
                    int quantity = GetQuantityForDate(subscription, deliveryDay);

                    // 5 & 6. Resolve Meal / Ingredients and Check Price Protection
                    string orderMealName;
                    string? orderMealImageUrl = null;
                    List<ScheduledOrderIngredient>? resolvedIngredients = null;
                    string? nutritionalSummaryJson = null;

                    if (subscription.UserMealId.HasValue)
                    {
                        if (!userMealsMap.TryGetValue(subscription.UserMealId.Value, out var userMeal))
                        {
                            _logger.LogWarning("[SUB-JOB] UserMeal {UserMealId} not found for subscription #{Id}", subscription.UserMealId, subscription.SubscriptionId);
                            failed++; continue;
                        }
                        
                        // Price Protection: Custom meals use TotalPrice at time of snapshot
                        orderMealName = $"{userMeal.MealName} (Subscription)";
                        var result = ResolveCustomIngredients(subscription.SubscriptionId, userMeal, userMealIngredientsMap, quantity, allIngredientsMap);
                        resolvedIngredients = result.Ingredients;
                        nutritionalSummaryJson = result.NutritionalSummary;
                    }
                    else if (subscription.MealId.HasValue)
                    {
                        if (!mealsMap.TryGetValue(subscription.MealId.Value, out var masterMeal))
                        {
                            _logger.LogWarning("[SUB-JOB] Master Meal {MealId} not found for subscription #{Id}", subscription.MealId, subscription.SubscriptionId);
                            failed++; continue;
                        }

                        // Price Protection: Check if master meal price increased
                        if (masterMeal.BasePrice > subscription.AgreedPrice)
                        {
                            _logger.LogWarning("[SUB-JOB] Price Protection Triggered for Subscription #{Id}. Agreed: {Agreed}, Current: {Current}. Pausing subscription.", 
                                subscription.SubscriptionId, subscription.AgreedPrice, masterMeal.BasePrice);
                                
                            subscription.IsActive = false;
                            subscription.PauseReason = $"Price increased from {subscription.AgreedPrice:C} to {masterMeal.BasePrice:C}";
                            await _subscriptionRepo.UpdateAsync(subscription);
                            skipped++; continue;
                        }
                        // If price decreased, auto-adjust agreed price downwards
                        else if (masterMeal.BasePrice < subscription.AgreedPrice)
                        {
                            _logger.LogInformation("[SUB-JOB] Price decreased for Subscription #{Id}. Updating AgreedPrice to {Current}.", 
                                subscription.SubscriptionId, masterMeal.BasePrice);
                            subscription.AgreedPrice = masterMeal.BasePrice;
                            await _subscriptionRepo.UpdateAsync(subscription);
                        }

                        orderMealName = $"{masterMeal.MealName} (Subscription)";
                        orderMealImageUrl = masterMeal.ImageUrl;
                        var result = ResolveFixedIngredients(subscription.SubscriptionId, masterMeal, quantity, allIngredientsMap);
                        resolvedIngredients = result.Ingredients;
                        nutritionalSummaryJson = result.NutritionalSummary;
                    }
                    else
                    {
                        failed++; continue;
                    }

                    if (resolvedIngredients == null)
                    {
                        failed++;
                        continue;
                    }

                    // 7. Resolve user
                    if (!usersMap.TryGetValue(subscription.UserId, out var user)
                        || user.AuthMapping?.AuthId == null)
                    {
                        _logger.LogWarning(
                            "[SUB-JOB] User {UserId} or AuthMapping missing for subscription #{Id}",
                            subscription.UserId, subscription.SubscriptionId);
                        failed++;
                        continue;
                    }

                    // 8. Resolve delivery address
                    int? deliveryAddressId = subscription.DeliveryAddressId;
                    if (deliveryAddressId == null)
                    {
                        if (!addressesMap.TryGetValue(subscription.UserId, out var primaryAddress))
                        {
                            _logger.LogWarning(
                                "[SUB-JOB] No primary address for user {UserId}, subscription #{Id}",
                                subscription.UserId, subscription.SubscriptionId);
                            failed++;
                            continue;
                        }
                        deliveryAddressId = primaryAddress.Id;
                    }

                    // 9. Build and persist ScheduledOrder directly — no service layer indirection
                    var scheduledOrder = new ScheduledOrder
                    {
                        UserId           = subscription.UserId,
                        AuthId           = user.AuthMapping!.AuthId,
                        MealName         = orderMealName,
                        ScheduledFor     = deliveryDay,
                        DeliveryTimeSlot = DeliveryConstants.DefaultTimeSlot,
                        TotalPrice       = resolvedIngredients?.Sum(i => i.TotalPrice) ?? (subscription.AgreedPrice * quantity),
                        OrderStatus      = ScheduledOrderStatus.Scheduled,
                        CanModify        = true,
                        ExpiresAt        = _time.ToUtc(
                                               deliveryDay.AddDays(1)
                                                          .ToDateTime(TimeOnly.MinValue)),
                        CreatedAt        = _time.UtcNow,
                        UpdatedAt        = _time.UtcNow,
                        DeliveryAddressId = deliveryAddressId,
                        SubscriptionId   = subscription.SubscriptionId,
                        Ingredients      = resolvedIngredients,
                        MealId           = subscription.MealId,
                        MealImageUrl     = orderMealImageUrl,
                        NutritionalSummary = nutritionalSummaryJson
                    };

                    scheduledOrdersToCreate.Add(scheduledOrder);

                    // 10. Advance NextScheduledDate
                    subscription.NextScheduledDate = CalculateNextScheduledDate(subscription, deliveryDay);
                    subscription.UpdatedAt         = _time.UtcNow;
                    subscriptionsToUpdate.Add(subscription);

                    generated++;
                    _logger.LogInformation(
                        "[SUB-JOB] ✅ Created order for subscription #{Id} ({Meal}) → delivery {Date}, qty {Qty}, next {Next:yyyy-MM-dd}",
                        subscription.SubscriptionId, orderMealName, deliveryDay,
                        quantity, subscription.NextScheduledDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[SUB-JOB] ❌ Unhandled exception for subscription #{Id}",
                        subscription.SubscriptionId);
                    failed++;
                }
            }

            if (subscriptionsToUpdate.Any())
            {
                await _subscriptionRepo.UpdateBatchAsync(subscriptionsToUpdate);
            }

            if (scheduledOrdersToCreate.Any())
            {
                await _scheduledOrderRepo.CreateBatchAsync(scheduledOrdersToCreate);
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "[JOB-METRICS] {@Metrics}", new
                {
                    Job = "subscription-order-generation",
                    Date = deliveryDay.ToString("yyyy-MM-dd"),
                    Generated = generated,
                    Skipped = skipped,
                    Failed = failed,
                    DurationMs = stopwatch.ElapsedMilliseconds
                });
        }

        // ─────────────────────────────────────────────────────────────────────
        // REAL-TIME — called immediately when user subscribes or resumes
        // ─────────────────────────────────────────────────────────────────────
        public async Task GenerateOrderForSubscriptionAsync(int subscriptionId, int userId, Guid authId)
        {
            var subscription = await _subscriptionRepo.GetByIdAsync(subscriptionId);
            if (subscription == null)
                throw new InvalidOperationException($"Subscription #{subscriptionId} not found");

            if (!subscription.IsActive)
            {
                _logger.LogInformation(
                    "[REALTIME] Subscription #{Id} is inactive, skipping", subscriptionId);
                return;
            }

            var today       = _time.TodayIst;
            var deliveryDay = today.AddDays(1);

            // For weekly: if tomorrow isn't a scheduled day, find the next one
            if (!IsDueOnDate(subscription, deliveryDay))
            {
                if (subscription.Frequency == SubscriptionFrequency.Weekly)
                {
                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    deliveryDay = FindNextWeeklyDate(today, scheduledDays);
                }
                else
                {
                    _logger.LogInformation(
                        "[REALTIME] Subscription #{Id} not due on {Date}", subscriptionId, deliveryDay);
                    return;
                }
            }

            // Duplicate guard
            var existing = await _scheduledOrderRepo.GetBySubscriptionIdAndDateAsync(
                subscriptionId, deliveryDay);
            if (existing != null)
            {
                _logger.LogInformation(
                    "[REALTIME] Order already exists for subscription #{Id} on {Date}",
                    subscriptionId, deliveryDay);
                return;
            }

            int quantity   = GetQuantityForDate(subscription, deliveryDay);
            
            string orderMealName;
            string? orderMealImageUrl = null;
            decimal totalPrice = subscription.AgreedPrice;
            List<ScheduledOrderIngredient>? resolvedIngredients = null;
            string? nutritionalSummaryJson = null;

            if (subscription.UserMealId.HasValue)
            {
                var userMeal = await _userMealRepo.GetByIdAsync(subscription.UserMealId.Value)
                                 ?? throw new InvalidOperationException("UserMeal not found");
                
                orderMealName = $"{userMeal.MealName} (Subscription)";
                var ingredients = await _userMealIngredientRepo.GetByUserMealIdAsync(subscription.UserMealId.Value);
                var ingredientIds = ingredients.Select(i => i.IngredientId).ToList();
                var prices = await _ingredientRepo.GetByIdsAsync(ingredientIds);

                var result = ResolveCustomIngredients(subscription.SubscriptionId, userMeal, 
                    new Dictionary<int, List<UserMealIngredient>> { { userMeal.UserMealId, ingredients.ToList() } }, quantity, prices);
                resolvedIngredients = result.Ingredients;
                nutritionalSummaryJson = result.NutritionalSummary;
            }
            else if (subscription.MealId.HasValue)
            {
                var masterMeal = await _mealRepo.GetByIdWithOptionsAsync(subscription.MealId.Value)
                                 ?? throw new InvalidOperationException("Master Meal not found");
                
                // Real-time price protection
                if (masterMeal.BasePrice > subscription.AgreedPrice)
                {
                    subscription.IsActive = false;
                    subscription.PauseReason = $"Price increased from {subscription.AgreedPrice:C} to {masterMeal.BasePrice:C}";
                    await _subscriptionRepo.UpdateAsync(subscription);
                    _logger.LogInformation("[REALTIME] Price Protection Paused Subscription #{Id}", subscriptionId);
                    return;
                }
                else if (masterMeal.BasePrice < subscription.AgreedPrice)
                {
                    subscription.AgreedPrice = masterMeal.BasePrice;
                    await _subscriptionRepo.UpdateAsync(subscription);
                }

                orderMealName = $"{masterMeal.MealName} (Subscription)";
                orderMealImageUrl = masterMeal.ImageUrl;

                var ingredientIds = masterMeal.MealOptions?.FirstOrDefault()?.MealOptionIngredients.Select(i => i.IngredientId).ToList() ?? new List<int>();
                var prices = await _ingredientRepo.GetByIdsAsync(ingredientIds);

                var result = ResolveFixedIngredients(subscriptionId, masterMeal, quantity, prices);
                resolvedIngredients = result.Ingredients;
                nutritionalSummaryJson = result.NutritionalSummary;
            }
            else
            {
                throw new InvalidOperationException("Subscription missing both MealId and UserMealId");
            }

            var user       = await _userRepo.GetByIdAsync(userId)
                             ?? throw new InvalidOperationException("User not found");

            if (user.AuthMapping?.AuthId == null)
                throw new InvalidOperationException("User AuthMapping missing");

            if (resolvedIngredients == null)
                throw new InvalidOperationException($"No ingredients found for Subscription #{subscriptionId}");

            int? deliveryAddressId = subscription.DeliveryAddressId
                ?? (await _userAddressRepo.GetPrimaryAddressAsync(userId))?.Id
                ?? throw new AddressNotFoundException(userId);

            var scheduledOrder = new ScheduledOrder
            {
                UserId           = subscription.UserId,
                AuthId           = user.AuthMapping.AuthId,
                MealName         = orderMealName,
                ScheduledFor     = deliveryDay,
                DeliveryTimeSlot = DeliveryConstants.DefaultTimeSlot,
                TotalPrice       = resolvedIngredients.Sum(i => i.TotalPrice),
                OrderStatus      = ScheduledOrderStatus.Scheduled,
                CanModify         = true,
                ExpiresAt         = _time.ToUtc(
                                        deliveryDay.AddDays(1)
                                                   .ToDateTime(TimeOnly.MinValue)),
                CreatedAt         = _time.UtcNow,
                UpdatedAt         = _time.UtcNow,
                DeliveryAddressId = deliveryAddressId,
                SubscriptionId    = subscriptionId,
                Ingredients       = resolvedIngredients,
                MealId           = subscription.MealId,
                MealImageUrl     = orderMealImageUrl,
                NutritionalSummary = nutritionalSummaryJson
            };

            await _scheduledOrderRepo.CreateAsync(scheduledOrder);

            _logger.LogInformation(
                "[REALTIME] ✅ Created order for subscription #{Id} → delivery {Date}",
                subscriptionId, deliveryDay);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CANCEL — called when user pauses or deletes subscription
        // ─────────────────────────────────────────────────────────────────────
        public async Task CancelOrderForSubscriptionAsync(int subscriptionId, int userId, Guid authId)
        {
            var tomorrow = _time.TodayIst.AddDays(1);

            var orders = await _scheduledOrderRepo.GetBySubscriptionIdAsync(subscriptionId);
            var toDelete = orders
                .Where(o => o.ScheduledFor >= tomorrow 
                         && !o.IsProcessedToOrder)
                .ToList();

            if (!toDelete.Any())
            {
                _logger.LogInformation("[CANCEL] No future orders to delete for subscription #{SubscriptionId}", subscriptionId);
                return;
            }

            _logger.LogInformation("[CANCEL] Deleting {Count} future orders for subscription #{SubscriptionId} starting {Date}",
                toDelete.Count, subscriptionId, tomorrow);

            await _scheduledOrderRepo.DeleteBatchAsync(toDelete.Select(o => o.ScheduledOrderId));
            _logger.LogInformation("[CANCEL] ✅ Successfully deleted {Count} future orders for subscription #{SubscriptionId}", toDelete.Count, subscriptionId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines if a subscription should generate an order for the given date.
        /// </summary>
        private bool IsDueOnDate(Subscription subscription, DateOnly date)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    return true;

                case SubscriptionFrequency.Weekly:
                    int dow = (int)date.DayOfWeek;
                    return subscription.WeeklySchedule.Any(s => s.DayOfWeek == dow);

                case SubscriptionFrequency.Alternate:
                    if (subscription.NextScheduledDate == null)
                    {
                        // Fallback: use StartDate parity
                        int diff = date.DayNumber - subscription.StartDate.DayNumber;
                        return diff >= 0 && diff % 2 == 0;
                    }
                    return subscription.NextScheduledDate == date;

                case SubscriptionFrequency.Monthly:
                    if (subscription.NextScheduledDate == null)
                        return date.Day == subscription.StartDate.Day;
                    return subscription.NextScheduledDate == date;

                default:
                    return false;
            }
        }

        private int GetQuantityForDate(Subscription subscription, DateOnly date)
        {
            if (subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                int dow = (int)date.DayOfWeek;
                return subscription.WeeklySchedule
                    .FirstOrDefault(s => s.DayOfWeek == dow)?.Quantity ?? 1;
            }
            return 1;
        }

        internal DateOnly CalculateNextScheduledDate(Subscription subscription, DateOnly deliveredOn)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    return deliveredOn.AddDays(1);

                case SubscriptionFrequency.Weekly:
                    var days = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    return FindNextWeeklyDate(deliveredOn, days);

                case SubscriptionFrequency.Monthly:
                    return deliveredOn.AddMonths(1);

                case SubscriptionFrequency.Alternate:
                    return deliveredOn.AddDays(2);

                default:
                    return deliveredOn.AddDays(1);
            }
        }

        /// <summary>
        /// Finds next weekly delivery date. Uses nullable int to avoid Sunday (0) = default(int) bug.
        /// </summary>
        internal static DateOnly FindNextWeeklyDate(DateOnly fromDate, List<int> scheduledDays)
        {
            if (!scheduledDays.Any())
                return fromDate.AddDays(7);

            var ordered = scheduledDays.OrderBy(d => d).ToList();
            int current = (int)fromDate.DayOfWeek;

            var next = ordered.Cast<int?>().FirstOrDefault(d => d > current);
            if (next.HasValue)
                return fromDate.AddDays(next.Value - current);

            int first = ordered.First();
            return fromDate.AddDays((7 - current) + first);
        }

        /// <summary>
        /// Resolves ingredient list for Custom Meal subscription.
        /// </summary>
        private (List<ScheduledOrderIngredient>? Ingredients, string? NutritionalSummary) ResolveCustomIngredients(
            int subscriptionId,
            UserMeal userMeal,
            Dictionary<int, List<UserMealIngredient>> userMealIngredientsMap,
            int quantity,
            IReadOnlyDictionary<int, Ingredient> prices)
        {
            if (!userMealIngredientsMap.TryGetValue(userMeal.UserMealId, out var umi) || !umi.Any())
            {
                _logger.LogWarning("[INGREDIENTS] Empty UserMealIngredients for UserMeal #{Id}, subscription #{SubId}",
                    userMeal.UserMealId, subscriptionId);
                return (null, null);
            }

            var result = new List<ScheduledOrderIngredient>();
            int totalCalories = 0;
            decimal totalProtein = 0m;
            int itemCount = 0;

            foreach (var item in umi)
            {
                prices.TryGetValue(item.IngredientId, out var ing);
                decimal unitPrice = ing?.Price ?? 0m;
                int     qty       = item.Quantity * quantity;

                result.Add(new ScheduledOrderIngredient
                {
                    IngredientId = item.IngredientId,
                    Quantity     = qty,
                    UnitPrice    = unitPrice,
                    TotalPrice   = unitPrice * qty,
                    CreatedAt    = _time.UtcNow
                });

                if (ing != null)
                {
                    totalCalories += ing.Calories * qty;
                    totalProtein += ing.Protein * qty;
                    itemCount += qty;
                }
            }

            var nutritionalSummary = new Sovva.Application.DTOs.NutritionalSummaryDto
            {
                TotalCalories = totalCalories,
                TotalProtein = totalProtein,
                ItemCount = itemCount
            };

            return (result, System.Text.Json.JsonSerializer.Serialize(nutritionalSummary));
        }

        /// <summary>
        /// Resolves ingredient list for Fixed Meal subscription.
        /// </summary>
        private (List<ScheduledOrderIngredient>? Ingredients, string? NutritionalSummary) ResolveFixedIngredients(
            int subscriptionId,
            Meal masterMeal,
            int quantity,
            IReadOnlyDictionary<int, Ingredient> ingredientPrices)
        {
            var defaultOption = masterMeal.MealOptions?.FirstOrDefault();

            if (defaultOption == null || !defaultOption.MealOptionIngredients.Any())
            {
                _logger.LogWarning("[SUB-JOB] No ingredients resolvable for Master Meal #{MealId}, subscription #{SubId}",
                    masterMeal.MealId, subscriptionId);
                return (null, null);
            }

            var result = new List<ScheduledOrderIngredient>();
            int totalCalories = 0;
            decimal totalProtein = 0m;
            int itemCount = 0;

            foreach (var moi in defaultOption.MealOptionIngredients)
            {
                int qty = 1 * quantity;
                ingredientPrices.TryGetValue(moi.IngredientId, out var ing);
                decimal unitPrice = ing?.Price ?? 0m;

                result.Add(new ScheduledOrderIngredient
                {
                    IngredientId = moi.IngredientId,
                    Quantity     = qty,
                    UnitPrice    = unitPrice,
                    TotalPrice   = unitPrice * qty,
                    CreatedAt    = _time.UtcNow
                });

                if (ing != null)
                {
                    totalCalories += ing.Calories * qty;
                    totalProtein += ing.Protein * qty;
                    itemCount += qty;
                }
            }

            var nutritionalSummary = new Sovva.Application.DTOs.NutritionalSummaryDto
            {
                TotalCalories = totalCalories,
                TotalProtein = totalProtein,
                ItemCount = itemCount
            };

            return (result, System.Text.Json.JsonSerializer.Serialize(nutritionalSummary));
        }
    }
}