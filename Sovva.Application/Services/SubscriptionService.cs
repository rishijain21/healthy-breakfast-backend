// Sovva.Application/Services/SubscriptionService.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserMealRepository _userMealRepository;
        private readonly IMealRepository _mealRepository;  // ✅ ADD: For auto-find-or-create
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IUserMealIngredientRepository _userMealIngredientRepository;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly IUserLoader _userLoader;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IUserRepository userRepository,
            IUserMealRepository userMealRepository,
            IMealRepository mealRepository,  // ✅ ADD: For auto-find-or-create
            IUserAddressRepository userAddressRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IIngredientRepository ingredientRepository,
            IUserMealIngredientRepository userMealIngredientRepository,
            IAppTimeProvider time,
            ILogger<SubscriptionService> logger,
            IUserLoader userLoader)
        {
            _subscriptionRepository = subscriptionRepository;
            _userRepository = userRepository;
            _userMealRepository = userMealRepository;
            _mealRepository = mealRepository;  // ✅ ADD
            _userAddressRepository = userAddressRepository;
            _scheduledOrderRepository = scheduledOrderRepository;
            _ingredientRepository = ingredientRepository;
            _userMealIngredientRepository = userMealIngredientRepository;
            _time = time;
            _logger = logger;
            _userLoader = userLoader;
        }

        public async Task<IEnumerable<SubscriptionDto>> GetAllSubscriptionsAsync()
        {
            var subscriptions = await _subscriptionRepository.GetAllAsync();
            return subscriptions.Select(MapToDto);
        }

        public async Task<SubscriptionDto?> GetSubscriptionByIdAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            return subscription != null ? MapToDto(subscription) : null;
        }

        public async Task<IEnumerable<SubscriptionDto>> GetSubscriptionsByUserIdAsync(int userId)
        {
            var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
            return subscriptions.Select(MapToDto);
        }

        public async Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsAsync()
        {
            var subscriptions = await _subscriptionRepository.GetActiveSubscriptionsAsync();
            return subscriptions.Select(MapToDto);
        }

        public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionInternalDto dto)
        {
            // ✅ FIXED: Load user WITH AuthMapping eagerly
            var user = await _userLoader.GetUserWithAuthMappingAsync(dto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // ✅ FIX BUG 5: Security check - validate user owns their account (fail fast)
            if (user.UserId != dto.UserId)
            {
                _logger.LogWarning(
                    "❌ Security violation: User {UserId} attempted to subscribe with invalid user account",
                    dto.UserId);
                
                throw new UnauthorizedAccessException(
                    "Invalid user account");
            }

            // ✅ FIX BUG 2: Validate meal exists BEFORE duplicate check (fail fast)
            var meal = await _mealRepository.GetByIdAsync(dto.MealId);
            if (meal == null)
                throw new ArgumentException("Meal not found");

            // ✅ FIX BUG 1: Check for duplicate subscription BEFORE creating UserMeal
            // Uses MealId to check any active subscription for this meal (not just current date range)
            _logger.LogInformation("🔍 Checking for existing subscription: UserId={UserId}, MealId={MealId}", dto.UserId, dto.MealId);
            
            var existingSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByMealIdAsync(
                dto.UserId, 
                dto.MealId
            );
            
            if (existingSubscription != null)
            {
                _logger.LogWarning(
                    "❌ Duplicate subscription attempt: User {UserId} tried to subscribe to Meal {MealId} again. Existing subscription ID: {ExistingSubId}",
                    dto.UserId, dto.MealId, existingSubscription.SubscriptionId);
                
                throw new InvalidOperationException(
                    $"You already have an active subscription for '{meal.MealName}'. " +
                    "Please edit your existing subscription instead of creating a new one."
                );
            }

            // ✅ FIX BUG 1: Now safe to look up or create UserMeal AFTER duplicate check passes
            var userMeal = await _userMealRepository.GetByUserIdAndMealIdAsync(dto.UserId, dto.MealId);

            if (userMeal == null)
            {
                userMeal = new UserMeal
                {
                    UserId = dto.UserId,
                    MealId = dto.MealId,
                    MealName = meal.MealName,
                    TotalPrice = meal.BasePrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userMealRepository.AddAsync(userMeal);
                await _userMealRepository.SaveChangesAsync();
            }

            // Update the UserMealId in the DTO for downstream usage
            dto.UserMealId = userMeal.UserMealId;

            // ✅ Check for existing active subscription using the new simpler method (already validated but safe to check)
            _logger.LogInformation("🔍 Final check: UserId={UserId}, UserMealId={UserMealId}", dto.UserId, dto.UserMealId);
            
            var checkSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByUserMealIdAsync(
                dto.UserId, 
                dto.UserMealId
            );
            
            if (checkSubscription != null)
            {
                _logger.LogWarning(
                    "❌ Duplicate subscription attempt: User {UserId} tried to subscribe to UserMeal {UserMealId} again. Existing subscription ID: {ExistingSubId}",
                    dto.UserId, dto.UserMealId, checkSubscription.SubscriptionId);
                    
                throw new InvalidOperationException(
                    $"You already have an active subscription for '{userMeal.MealName}'. " +
                    "Please edit your existing subscription instead of creating a new one."
                );
            }

            // ✅ ADD: Security check - ensure user owns this meal
            if (userMeal.UserId != dto.UserId)
            {
                _logger.LogWarning(
                    "❌ Security violation: User {UserId} attempted to subscribe to UserMeal {UserMealId} owned by {OwnerId}",
                    dto.UserId, dto.UserMealId, userMeal.UserId);
                    
                throw new UnauthorizedAccessException(
                    "You can only subscribe to your own meals"
                );
            }

            _logger.LogInformation($"✅ Validation passed. Creating new subscription for UserMeal {dto.UserMealId}");

            // ✅ ADD: Get user's primary address
            var primaryAddress = await _userAddressRepository.GetPrimaryAddressAsync(dto.UserId);
            if (primaryAddress == null)
            {
                throw new InvalidOperationException(
                    "Please set a default delivery address before creating a subscription"
                );
            }

            if (dto.StartDate >= dto.EndDate)
                throw new ArgumentException("Start date must be before end date");

            if (dto.Frequency == SubscriptionFrequency.Weekly)
            {
                if (dto.WeeklySchedule == null || !dto.WeeklySchedule.Any())
                {
                    throw new ArgumentException("Weekly schedule is required for Weekly subscriptions");
                }

                if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                {
                    throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");
                }

                if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                {
                    throw new ArgumentException("Quantity must be greater than 0");
                }

                var duplicateDays = dto.WeeklySchedule
                    .GroupBy(s => s.DayOfWeek)
                    .Where(g => g.Count() > 1)
                    .Select(g => ((DayOfWeek)g.Key).ToString());
                    
                if (duplicateDays.Any())
                {
                    throw new ArgumentException($"Duplicate days found: {string.Join(", ", duplicateDays)}");
                }
            }

            var subscription = new Subscription
            {
                UserId = dto.UserId,
                UserMealId = dto.UserMealId,
                Frequency = dto.Frequency,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Active = dto.Active,
                DeliveryAddressId = primaryAddress.Id, // ✅ ADD: Link to primary address
                NextScheduledDate = CalculateInitialNextDeliveryDate(dto.StartDate, dto.Frequency, dto.WeeklySchedule)
            };

            var createdSubscription = await _subscriptionRepository.CreateAsync(subscription);

            if (dto.Frequency == SubscriptionFrequency.Weekly && dto.WeeklySchedule != null)
            {
                var now = DateTime.UtcNow;
                var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                {
                    SubscriptionId = createdSubscription.SubscriptionId,
                    DayOfWeek = s.DayOfWeek,
                    Quantity = s.Quantity,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                await _subscriptionRepository.AddSchedulesAsync(createdSubscription.SubscriptionId, schedules);
            }

            // ✅ NEW: Create first scheduled order for immediate visibility
            _logger.LogInformation("Creating first scheduled order - Subscription: {SubscriptionId}, User: {UserId}, UserMeal: {UserMealId}",
                createdSubscription.SubscriptionId, user.UserId, userMeal.UserMealId);

            var firstOrderResult = await CreateFirstScheduledOrderAsync(
                createdSubscription, 
                user,              // ✅ Pass explicitly
                userMeal,          // ✅ Pass explicitly
                primaryAddress     // ✅ Pass explicitly
            );

            _logger.LogInformation("First scheduled order created - Success: {Success}, Error: {Error}",
                firstOrderResult.Success, firstOrderResult.Error ?? "null");

            // ✅ Log result but don't fail subscription creation
            if (!firstOrderResult.Success)
            {
                _logger.LogWarning($"⚠️ Subscription created but first order failed: {firstOrderResult.Error}");
            }

            var result = await _subscriptionRepository.GetByIdAsync(createdSubscription.SubscriptionId);
            return MapToDto(result!);
        }

        // ✅ Return result object instead of throwing
        private async Task<(bool Success, string? Error)> CreateFirstScheduledOrderAsync(
            Subscription subscription,
            User user,
            UserMeal userMeal,
            UserAddress deliveryAddress)
        {
            // ✅ FIX 10: Remove debug logging - use proper logging
            _logger.LogInformation("CreateFirstScheduledOrderAsync called - SubscriptionId: {SubscriptionId}, UserId: {UserId}, UserMealId: {UserMealId}, MealName: {MealName}",
                subscription.SubscriptionId, user.UserId, userMeal.UserMealId, userMeal.MealName);
            
            try
            {
                _logger.LogInformation($"📦 Creating first order for subscription #{subscription.SubscriptionId}");

                // Load ingredients from UserMeal
                var ingredients = await _userMealIngredientRepository.GetByUserMealIdAsync(userMeal.UserMealId);
                
                _logger.LogInformation("Loaded {IngredientCount} ingredients from UserMeal #{UserMealId}", ingredients.Count(), userMeal.UserMealId);
                
                if (!ingredients.Any())
                {
                    var error = $"No ingredients found for UserMeal #{userMeal.UserMealId}";
                    _logger.LogWarning("Error in CreateFirstScheduledOrderAsync for subscription {SubscriptionId}: {Error}", subscription.SubscriptionId, error);
                    return (false, error);
                }

                _logger.LogInformation($"✅ Found {ingredients.Count()} ingredients");

                // Calculate first delivery date
                var firstDeliveryDate = CalculateFirstDeliveryDate(subscription);
                _logger.LogInformation($"📅 First delivery: {firstDeliveryDate:yyyy-MM-dd}");

                // Build scheduled order (async)
                var scheduledOrder = await BuildScheduledOrder(
                    subscription, 
                    user, 
                    userMeal, 
                    deliveryAddress, 
                    ingredients,
                    firstDeliveryDate
                );

                // Save to database
                var created = await _scheduledOrderRepository.CreateAsync(scheduledOrder);
                
                _logger.LogInformation("ScheduledOrder #{ScheduledOrderId} created successfully for delivery date {DeliveryDate}", created.ScheduledOrderId, firstDeliveryDate);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create first order for subscription #{SubscriptionId}", subscription.SubscriptionId);
                return (false, ex.Message);
            }
        }

        // ✅ Helper method to calculate first delivery date
        private DateOnly CalculateFirstDeliveryDate(Subscription subscription)
        {
            var today = _time.TodayIst;
            
            // If subscription starts in the future, use start date
            if (subscription.StartDate > today)
            {
                return subscription.StartDate;
            }

            // Subscription starts today or in past - first delivery is tomorrow
            var firstDeliveryDate = today.AddDays(1);
            
            // For weekly subscriptions, check if tomorrow is a scheduled day
            if (subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                var tomorrowDayOfWeek = (int)firstDeliveryDate.DayOfWeek;
                var isScheduledDay = subscription.WeeklySchedule.Any(ws => ws.DayOfWeek == tomorrowDayOfWeek);
                
                if (!isScheduledDay)
                {
                    _logger.LogInformation($"⏭️ Tomorrow is not a scheduled day, finding next delivery date");
                    
                    // Find next scheduled day
                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    firstDeliveryDate = FindNextWeeklyDate(today, scheduledDays);
                }
            }

            return firstDeliveryDate;
        }

        // ✅ Helper method to build scheduled order
        private async Task<ScheduledOrder> BuildScheduledOrder(
            Subscription subscription,
            User user,
            UserMeal userMeal,
            UserAddress deliveryAddress,
            IEnumerable<UserMealIngredient> ingredients,
            DateOnly firstDeliveryDate)
        {
            // Calculate total price
            decimal totalPrice = 0;
            var scheduledOrderIngredients = new List<ScheduledOrderIngredient>();

            // ✅ OPTIMIZED: Batch load all ingredients in single query to kill N+1
            var ingredientList = ingredients.ToList();
            var ingredientIds = ingredientList.Select(i => i.IngredientId).ToList();
            var allIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
            var ingredientMap = allIngredients.ToDictionary(i => i.IngredientId);

            foreach (var userMealIngredient in ingredientList)
            {
                if (!ingredientMap.TryGetValue(userMealIngredient.IngredientId, out var ingredient))
                    continue;

                var quantity = userMealIngredient.Quantity;
                var unitPrice = ingredient.Price;
                var itemTotal = unitPrice * quantity;
                
                totalPrice += itemTotal;

                scheduledOrderIngredients.Add(new ScheduledOrderIngredient
                {
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = itemTotal,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var deliveryDateTime = firstDeliveryDate.ToDateTime(TimeOnly.MinValue);
            var deliveryDateTimeUtc = DateTime.SpecifyKind(deliveryDateTime, DateTimeKind.Utc);

            return new ScheduledOrder
            {
                UserId = subscription.UserId,
                AuthId = user.AuthMapping?.AuthId ?? Guid.Empty, // ✅ Get AuthId from UserAuthMapping
                MealName = userMeal.MealName,
                ScheduledFor = DateOnly.FromDateTime(deliveryDateTimeUtc),  // DateTime → DateOnly
                DeliveryTimeSlot = "8:00 AM",
                TotalPrice = totalPrice,
                OrderStatus = ScheduledOrderStatus.Scheduled,
                CanModify = true,
                ExpiresAt = deliveryDateTimeUtc.AddDays(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeliveryAddressId = deliveryAddress.Id,
                SubscriptionId = subscription.SubscriptionId, // ✅ Link to subscription
                Ingredients = scheduledOrderIngredients
            };
        }

        public async Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto dto)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return null;

            if (dto.Frequency.HasValue)
                subscription.Frequency = dto.Frequency.Value;

            if (dto.StartDate.HasValue)
                subscription.StartDate = dto.StartDate.Value;

            if (dto.EndDate.HasValue)
                subscription.EndDate = dto.EndDate.Value;

            if (dto.Active.HasValue)
                subscription.Active = dto.Active.Value;

            if (subscription.StartDate >= subscription.EndDate)
                throw new ArgumentException("Start date must be before end date");

            if (dto.WeeklySchedule != null && subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                {
                    throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");
                }

                if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                {
                    throw new ArgumentException("Quantity must be greater than 0");
                }

                var duplicateDays = dto.WeeklySchedule
                    .GroupBy(s => s.DayOfWeek)
                    .Where(g => g.Count() > 1)
                    .Select(g => ((DayOfWeek)g.Key).ToString());
                    
                if (duplicateDays.Any())
                {
                    throw new ArgumentException($"Duplicate days found: {string.Join(", ", duplicateDays)}");
                }

                await _subscriptionRepository.RemoveSchedulesAsync(subscriptionId);

                if (dto.WeeklySchedule.Any())
                {
                    var now = DateTime.UtcNow;
                    var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                    {
                        SubscriptionId = subscriptionId,
                        DayOfWeek = s.DayOfWeek,
                        Quantity = s.Quantity,
                        CreatedAt = now,
                        UpdatedAt = now
                    });

                    await _subscriptionRepository.AddSchedulesAsync(subscriptionId, schedules);
                }
            }

            var today = _time.TodayIst;
            subscription.NextScheduledDate = CalculateNextDeliveryDate(subscription, today);

            var updatedSubscription = await _subscriptionRepository.UpdateAsync(subscription);
            
            var result = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            return MapToDto(result!);
        }

        public async Task<bool> DeleteSubscriptionAsync(int subscriptionId)
        {
            // ✅ FIX: Only delete non-processed ScheduledOrders to prevent FK break
            // Processed orders have IsProcessedToOrder = true and are linked to actual Orders
            var scheduledOrders = await _scheduledOrderRepository.GetBySubscriptionIdAsync(subscriptionId);
            
            var pendingOrders = scheduledOrders.Where(so => !so.IsProcessedToOrder).ToList();
            _logger.LogInformation("Deleting {PendingCount} pending ScheduledOrders (keeping {ProcessedCount} processed)",
                pendingOrders.Count, scheduledOrders.Count - pendingOrders.Count);
            
            foreach (var order in pendingOrders)
            {
                await _scheduledOrderRepository.DeleteAsync(order.ScheduledOrderId);
            }
            
            return await _subscriptionRepository.DeleteAsync(subscriptionId);
        }

        public async Task<bool> ActivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

            // ✅ FIX: Idempotency guard - prevent double activation
            if (subscription.Active)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already active - no action needed", subscriptionId);
                return true;
            }

            subscription.Active = true;
            await _subscriptionRepository.UpdateAsync(subscription);
            return true;
        }

        public async Task<bool> DeactivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

            // ✅ FIX: Idempotency guard - prevent double deactivation
            if (!subscription.Active)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already inactive - no action needed", subscriptionId);
                return true;
            }

            subscription.Active = false;
            await _subscriptionRepository.UpdateAsync(subscription);
            return true;
        }

        /// <summary>
        /// ✅ FIXED: Updates NextScheduledDate for all active subscriptions using IST timezone
        /// </summary>
        public async Task UpdateNextScheduledDatesAsync()
        {
            var activeSubscriptions = await _subscriptionRepository.GetActiveSubscriptionsAsync();
            var today = _time.TodayIst;
            
            var istNow = _time.ToIst(_time.UtcNow);
            
            _logger.LogInformation("=== Subscription date sync started - UTC: {UtcTime}, IST: {IstTime}, Today: {Today}",
                DateTime.UtcNow, istNow, today);
            
            // ✅ NEW: Collect updates in memory, then batch update
            var subscriptionsToUpdate = new List<Subscription>();
            int updatedCount = 0;
            int skippedCount = 0;
            
            foreach (var subscription in activeSubscriptions)
            {
                var oldNextDate = subscription.NextScheduledDate;
                var newNextDate = CalculateNextDeliveryDate(subscription, today);
                
                _logger.LogInformation("Subscription #{SubscriptionId} sync - Frequency: {Frequency}, StartDate: {StartDate}, OldNextDate: {OldNextDate}, NewNextDate: {NewNextDate}",
                    subscription.SubscriptionId, subscription.Frequency, subscription.StartDate, oldNextDate, newNextDate);

                if (subscription.NextScheduledDate != newNextDate)
                {
                    subscription.NextScheduledDate = newNextDate;
                    subscriptionsToUpdate.Add(subscription);
                    _logger.LogInformation("Subscription #{SubscriptionId} next delivery date updated to {NewDate}", subscription.SubscriptionId, newNextDate);
                    updatedCount++;
                }
                else
                {
                    _logger.LogInformation("Subscription #{SubscriptionId} next delivery date already correct ({Date})", subscription.SubscriptionId, subscription.NextScheduledDate);
                    skippedCount++;
                }
            }
            
            // ✅ NEW: Batch update all changed subscriptions in one DB call
            if (subscriptionsToUpdate.Count > 0)
            {
                await _subscriptionRepository.UpdateBatchAsync(subscriptionsToUpdate);
                _logger.LogInformation("Batch updated {Count} subscriptions in single DB call", subscriptionsToUpdate.Count);
            }
            
            _logger.LogInformation("=== Subscription sync complete - Updated: {UpdatedCount}, Skipped: {SkippedCount}", updatedCount, skippedCount);
        }



        private DateOnly CalculateInitialNextDeliveryDate(
            DateOnly startDate, 
            SubscriptionFrequency frequency,
            List<WeeklyScheduleDto>? weeklySchedule)
        {
            var today = _time.TodayIst;
            
            if (startDate > today)
                return startDate;
            
            switch (frequency)
            {
                case SubscriptionFrequency.Daily:
                    return today >= startDate ? today.AddDays(1) : startDate;
                
                case SubscriptionFrequency.Weekly:
                    if (weeklySchedule == null || !weeklySchedule.Any())
                        return today.AddDays(7);
                    
                    return FindNextWeeklyDate(today, weeklySchedule.Select(s => s.DayOfWeek).ToList());
                
                case SubscriptionFrequency.Monthly:
                    return startDate.AddMonths(1);
                
                default:
                    return today.AddDays(1);
            }
        }

        /// <summary>
        /// ✅ FIXED: Always recalculate next delivery date based on IST timezone
        /// </summary>
        private static DateOnly CalculateNextDeliveryDate(Subscription subscription, DateOnly fromDate)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    // ✅ FIX: For daily subscriptions started in the past or today, next delivery is tomorrow
                    if (subscription.StartDate <= fromDate)
                    {
                        return fromDate.AddDays(1); // Tomorrow (IST)
                    }
                    // If subscription starts in the future, next delivery is start date
                    return subscription.StartDate;
                
                case SubscriptionFrequency.Weekly:
                    if (!subscription.WeeklySchedule.Any())
                        return fromDate.AddDays(7);
                    
                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    return FindNextWeeklyDate(fromDate, scheduledDays);
                
                case SubscriptionFrequency.Monthly:
                    if (subscription.NextScheduledDate == null || subscription.NextScheduledDate <= fromDate)
                    {
                        var startDay = subscription.StartDate.Day;
                        var nextMonth = fromDate.AddMonths(1);
                        var maxDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var day = Math.Min(startDay, maxDay);
                        return new DateOnly(nextMonth.Year, nextMonth.Month, day);
                    }
                    return subscription.NextScheduledDate.Value;
                
                default:
                    return fromDate.AddDays(1);
            }
        }

        private static DateOnly FindNextWeeklyDate(DateOnly currentDate, List<int> scheduledDays)
        {
            if (!scheduledDays.Any())
                return currentDate.AddDays(7);
            
            var orderedDays = scheduledDays.OrderBy(d => d).ToList();
            int currentDayOfWeek = (int)currentDate.DayOfWeek;
            
            var nextDayInWeek = orderedDays.Cast<int?>().FirstOrDefault(d => d > currentDayOfWeek);

            if (nextDayInWeek.HasValue)
            {
                int daysUntilNext = nextDayInWeek.Value - currentDayOfWeek;
                return currentDate.AddDays(daysUntilNext);
            }
            else
            {
                int firstDay = orderedDays.First();
                int daysUntilNext = (7 - currentDayOfWeek) + firstDay;
                return currentDate.AddDays(daysUntilNext);
            }
        }

        private static SubscriptionDto MapToDto(Subscription subscription)
        {
            return new SubscriptionDto
            {
                SubscriptionId = subscription.SubscriptionId,
                UserId = subscription.UserId,
                UserMealId = subscription.UserMealId,
                Frequency = subscription.Frequency,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Active = subscription.Active,
                NextScheduledDate = subscription.NextScheduledDate,
                CreatedAt = subscription.CreatedAt,
                UpdatedAt = subscription.UpdatedAt,
                UserName = subscription.User?.Name ?? string.Empty,
                MealName = subscription.UserMeal?.MealName ?? string.Empty,
                MealPrice = subscription.UserMeal?.TotalPrice ?? 0,
                
                WeeklySchedule = subscription.WeeklySchedule
                    .Select(s => new WeeklyScheduleDto
                    {
                        DayOfWeek = s.DayOfWeek,
                        Quantity = s.Quantity
                    })
                    .OrderBy(s => s.DayOfWeek)
                    .ToList()
            };
        }

        /// <summary>
        /// Runs nightly at 11:50 PM IST via Hangfire.
        /// Deactivates any subscription whose EndDate has passed.
        /// Runs before sync-subscription-dates (11:55 PM).
        /// </summary>
        public async Task ExpireSubscriptionsAsync()
        {
            var today = _time.TodayIst;
            var activeSubscriptions = await _subscriptionRepository.GetActiveSubscriptionsAsync();

            var expired = activeSubscriptions
                .Where(s => s.EndDate < today)
                .ToList();

            if (!expired.Any())
            {
                _logger.LogInformation("Expiry job: 0 subscriptions to expire on {Date}", today);
                return;
            }

            foreach (var sub in expired)
            {
                sub.Active = false;
                sub.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Subscription #{Id} (User {UserId}) expired on {EndDate} — deactivating",
                    sub.SubscriptionId, sub.UserId, sub.EndDate);
            }

            // Uses the existing UpdateBatchAsync — already wired up
            await _subscriptionRepository.UpdateBatchAsync(expired);

            _logger.LogInformation(
                "Expiry job complete — {Count} subscriptions deactivated on {Date}",
                expired.Count, today);
        }
    }
}
