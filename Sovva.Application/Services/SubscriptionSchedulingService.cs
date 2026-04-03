// Sovva.Application/Services/SubscriptionSchedulingService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

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
        public async Task GenerateScheduledOrdersFromSubscriptionsAsync()
        {
            var istNow      = _time.ToIst(_time.UtcNow);
            var today       = _time.TodayIst;           // April 3 (job runs at 12:01 AM April 3)
            var deliveryDay = today.AddDays(1);          // April 4 — the day we're scheduling for

            _logger.LogInformation(
                "[SUB-JOB] Started at {Now:yyyy-MM-dd HH:mm:ss} IST. Generating orders for {DeliveryDay:yyyy-MM-dd}",
                istNow, deliveryDay);

            var allSubscriptions = await _subscriptionRepo.GetActiveSubscriptionsAsync();
            _logger.LogInformation("[SUB-JOB] Active subscriptions: {Count}", allSubscriptions.Count());

            // ── BATCH LOAD — 5 queries regardless of subscription count ──────
            var userMealIds = allSubscriptions.Select(s => s.UserMealId).Distinct().ToList();
            var userIds     = allSubscriptions.Select(s => s.UserId).Distinct().ToList();

            var userMealsMap  = (await _userMealRepo.GetByIdsAsync(userMealIds))
                                .ToDictionary(m => m.UserMealId);

            var userMealIngredientsMap = (await _userMealIngredientRepo.GetByUserMealIdsAsync(userMealIds))
                                         .GroupBy(i => i.UserMealId)
                                         .ToDictionary(g => g.Key, g => g.ToList());

            var usersMap      = (await _userRepo.GetByIdsWithAuthMappingAsync(userIds))
                                .ToDictionary(u => u.UserId);

            var addressesMap  = (await _userAddressRepo.GetPrimaryAddressesByUserIdsAsync(userIds))
                                .ToDictionary(a => a.UserId);
            // ─────────────────────────────────────────────────────────────────

            int generated = 0, skipped = 0, failed = 0;

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
                    if (subscription.EndDate < deliveryDay)
                    {
                        _logger.LogInformation(
                            "[SUB-JOB] Subscription #{Id} expired on {End}, skipping",
                            subscription.SubscriptionId, subscription.EndDate);
                        skipped++;
                        continue;
                    }

                    // 3. Duplicate guard — DB unique index also enforces this, but check first
                    //    to avoid noisy constraint violations in logs
                    var existing = await _scheduledOrderRepo.GetBySubscriptionIdAndDateAsync(
                        subscription.SubscriptionId, deliveryDay);
                    if (existing != null)
                    {
                        _logger.LogInformation(
                            "[SUB-JOB] Order already exists for subscription #{Id} on {Date}, skipping",
                            subscription.SubscriptionId, deliveryDay);
                        skipped++;
                        continue;
                    }

                    // 4. Resolve quantity (weekly subscriptions can have per-day quantities)
                    int quantity = GetQuantityForDate(subscription, deliveryDay);

                    // 5. Resolve UserMeal
                    if (!userMealsMap.TryGetValue(subscription.UserMealId, out var userMeal))
                    {
                        _logger.LogWarning(
                            "[SUB-JOB] UserMeal {UserMealId} not found for subscription #{Id}",
                            subscription.UserMealId, subscription.SubscriptionId);
                        failed++;
                        continue;
                    }

                    // 6. Resolve ingredients — custom meal first, then catalogue fallback
                    var resolvedIngredients = await ResolveIngredientsAsync(
                        subscription.SubscriptionId, userMeal, userMealIngredientsMap, quantity);

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
                        MealName         = $"{userMeal.MealName} (Subscription)",
                        ScheduledFor     = deliveryDay,
                        DeliveryTimeSlot = "7:00 AM",
                        TotalPrice       = userMeal.TotalPrice * quantity,
                        OrderStatus      = ScheduledOrderStatus.Scheduled,
                        CanModify        = true,
                        ExpiresAt        = _time.ToUtc(
                                               deliveryDay.AddDays(1)
                                                          .ToDateTime(TimeOnly.MinValue)),
                        CreatedAt        = _time.UtcNow,
                        UpdatedAt        = _time.UtcNow,
                        DeliveryAddressId = deliveryAddressId,
                        SubscriptionId   = subscription.SubscriptionId,
                        Ingredients      = resolvedIngredients
                    };

                    await _scheduledOrderRepo.CreateAsync(scheduledOrder);

                    // 10. Advance NextScheduledDate
                    subscription.NextScheduledDate = CalculateNextScheduledDate(subscription, deliveryDay);
                    subscription.UpdatedAt         = _time.UtcNow;
                    await _subscriptionRepo.UpdateAsync(subscription);

                    generated++;
                    _logger.LogInformation(
                        "[SUB-JOB] ✅ Created order for subscription #{Id} ({Meal}) → delivery {Date}, qty {Qty}, next {Next:yyyy-MM-dd}",
                        subscription.SubscriptionId, userMeal.MealName, deliveryDay,
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

            _logger.LogInformation(
                "[SUB-JOB] Complete — generated: {G}, skipped: {S}, failed: {F}",
                generated, skipped, failed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // REAL-TIME — called immediately when user subscribes or resumes
        // ─────────────────────────────────────────────────────────────────────
        public async Task GenerateOrderForSubscriptionAsync(int subscriptionId, int userId, Guid authId)
        {
            var subscription = await _subscriptionRepo.GetByIdAsync(subscriptionId);
            if (subscription == null)
                throw new InvalidOperationException($"Subscription #{subscriptionId} not found");

            if (!subscription.Active)
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
            var userMeal   = await _userMealRepo.GetByIdAsync(subscription.UserMealId)
                             ?? throw new InvalidOperationException("UserMeal not found");
            var user       = await _userRepo.GetByIdAsync(userId)
                             ?? throw new InvalidOperationException("User not found");

            if (user.AuthMapping?.AuthId == null)
                throw new InvalidOperationException("User AuthMapping missing");

            var ingredients = await _userMealIngredientRepo.GetByUserMealIdAsync(subscription.UserMealId);
            var resolvedIngredients = await BuildIngredientListAsync(
                subscription.SubscriptionId, userMeal, ingredients.ToList(), quantity);

            if (resolvedIngredients == null)
                throw new InvalidOperationException(
                    $"No ingredients found for UserMeal #{subscription.UserMealId}");

            int? deliveryAddressId = subscription.DeliveryAddressId
                ?? (await _userAddressRepo.GetPrimaryAddressAsync(userId))?.Id
                ?? throw new InvalidOperationException("No delivery address found");

            var scheduledOrder = new ScheduledOrder
            {
                UserId            = userId,
                AuthId            = user.AuthMapping.AuthId,
                MealName          = $"{userMeal.MealName} (Subscription)",
                ScheduledFor      = deliveryDay,
                DeliveryTimeSlot  = "7:00 AM",
                TotalPrice        = userMeal.TotalPrice * quantity,
                OrderStatus       = ScheduledOrderStatus.Scheduled,
                CanModify         = true,
                ExpiresAt         = _time.ToUtc(
                                        deliveryDay.AddDays(1)
                                                   .ToDateTime(TimeOnly.MinValue)),
                CreatedAt         = _time.UtcNow,
                UpdatedAt         = _time.UtcNow,
                DeliveryAddressId = deliveryAddressId,
                SubscriptionId    = subscriptionId,
                Ingredients       = resolvedIngredients
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
            var toCancel = orders
                .Where(o => o.ScheduledFor == tomorrow && o.OrderStatus == ScheduledOrderStatus.Scheduled)
                .ToList();

            _logger.LogInformation("[CANCEL] Found {Count} orders to cancel for subscription #{Id} on {Date}",
                toCancel.Count, subscriptionId, tomorrow);

            foreach (var order in toCancel)
            {
                try
                {
                    await _scheduledOrderRepo.DeleteAsync(order.ScheduledOrderId);
                    _logger.LogInformation("[CANCEL] ✅ Deleted order #{OrderId}", order.ScheduledOrderId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[CANCEL] ⚠️ Failed to delete order #{OrderId}", order.ScheduledOrderId);
                }
            }
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

        private DateOnly CalculateNextScheduledDate(Subscription subscription, DateOnly deliveredOn)
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
        private static DateOnly FindNextWeeklyDate(DateOnly fromDate, List<int> scheduledDays)
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
        /// Resolves ingredient list for nightly batch job (uses pre-loaded maps).
        /// Returns null if ingredients cannot be resolved — caller should increment failedCount.
        /// </summary>
        private async Task<List<ScheduledOrderIngredient>?> ResolveIngredientsAsync(
            int subscriptionId,
            UserMeal userMeal,
            Dictionary<int, List<UserMealIngredient>> userMealIngredientsMap,
            int quantity)
        {
            // Path A: custom meal — UserMealIngredients exist
            if (userMealIngredientsMap.TryGetValue(userMeal.UserMealId, out var umi) && umi.Any())
            {
                return await BuildIngredientListAsync(subscriptionId, userMeal, umi, quantity);
            }

            // Path B: catalogue meal — fall back to MealOptions default option
            var meal = await _mealRepo.GetByIdWithOptionsAsync(userMeal.MealId);
            var defaultOption = meal?.MealOptions?.FirstOrDefault();

            if (defaultOption == null || !defaultOption.MealOptionIngredients.Any())
            {
                _logger.LogWarning(
                    "[SUB-JOB] No ingredients resolvable for UserMeal #{UserMealId} " +
                    "(MealId: {MealId}), subscription #{SubId}",
                    userMeal.UserMealId, userMeal.MealId, subscriptionId);
                return null;
            }

            // Batch load ingredient prices for the catalogue path
            var ingredientIds = defaultOption.MealOptionIngredients
                .Select(i => i.IngredientId).ToList();
            var ingredientPrices = (await _ingredientRepo.GetByIdsAsync(ingredientIds))
                .ToDictionary(i => i.IngredientId);

            var result = new List<ScheduledOrderIngredient>();
            foreach (var moi in defaultOption.MealOptionIngredients)
            {
                // MealOptionIngredient has no Quantity — default to 1 per ingredient per serving
                int qty = 1 * quantity;
                ingredientPrices.TryGetValue(moi.IngredientId, out var ing);
                decimal unitPrice = ing?.Price ?? 0m;

                result.Add(new ScheduledOrderIngredient
                {
                    IngredientId = moi.IngredientId,
                    Quantity     = qty,
                    UnitPrice    = unitPrice,
                    TotalPrice   = unitPrice * qty,
                    CreatedAt    = DateTime.UtcNow
                });
            }
            return result;
        }

        /// <summary>
        /// Builds ScheduledOrderIngredient list from UserMealIngredients.
        /// Used by both batch job and real-time path.
        /// Returns null if ingredients list is empty.
        /// </summary>
        private async Task<List<ScheduledOrderIngredient>?> BuildIngredientListAsync(
            int subscriptionId,
            UserMeal userMeal,
            List<UserMealIngredient> umi,
            int quantity)
        {
            if (!umi.Any())
            {
                _logger.LogWarning(
                    "[INGREDIENTS] Empty UserMealIngredients for UserMeal #{Id}, subscription #{SubId}",
                    userMeal.UserMealId, subscriptionId);
                return null;
            }

            var ingredientIds = umi.Select(i => i.IngredientId).ToList();
            var prices = (await _ingredientRepo.GetByIdsAsync(ingredientIds))
                .ToDictionary(i => i.IngredientId);

            var result = new List<ScheduledOrderIngredient>();
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
                    CreatedAt    = DateTime.UtcNow
                });
            }
            return result;
        }
    }
}