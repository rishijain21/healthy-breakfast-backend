// Sovva.Application/Services/SubscriptionService.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
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
        private readonly ILogger<SubscriptionService> _logger;
        private readonly IUserLoader _userLoader;

        // ✅ FIX: Use IST timezone for consistent date calculations
        private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IUserRepository userRepository,
            IUserMealRepository userMealRepository,
            IMealRepository mealRepository,  // ✅ ADD: For auto-find-or-create
            IUserAddressRepository userAddressRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IIngredientRepository ingredientRepository,
            IUserMealIngredientRepository userMealIngredientRepository,
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

            // ✅ NEW: Auto-find or create UserMeal by MealId
            var userMeal = await _userMealRepository.GetByUserIdAndMealIdAsync(dto.UserId, dto.MealId);

            if (userMeal == null)
            {
                // Fetch meal details for UserMeal record
                var meal = await _mealRepository.GetByIdAsync(dto.MealId);
                if (meal == null)
                    throw new ArgumentException("Meal not found");

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

            // ✅ ADD: Check for existing active subscription for this UserMeal
            _logger.LogInformation("🔍 Checking for existing subscription: UserId={UserId}, UserMealId={UserMealId}", dto.UserId, dto.UserMealId);
            
            var existingSubscription = await _subscriptionRepository.GetActiveSubscriptionByUserMealIdAsync(
                dto.UserId, 
                dto.UserMealId
            );
            
            if (existingSubscription != null)
            {
                _logger.LogWarning(
                    "❌ Duplicate subscription attempt: User {UserId} tried to subscribe to UserMeal {UserMealId} again. Existing subscription ID: {ExistingSubId}",
                    dto.UserId, dto.UserMealId, existingSubscription.SubscriptionId);
                    
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
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🟢 BEFORE CreateFirstScheduledOrderAsync call");
            Console.WriteLine($"🟢 Subscription: {createdSubscription.SubscriptionId}");
            Console.WriteLine($"🟢 User: {user.UserId}");
            Console.WriteLine($"🟢 UserMeal: {userMeal.UserMealId}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var firstOrderResult = await CreateFirstScheduledOrderAsync(
                createdSubscription, 
                user,              // ✅ Pass explicitly
                userMeal,          // ✅ Pass explicitly
                primaryAddress     // ✅ Pass explicitly
            );

            Console.WriteLine("🟢 AFTER CreateFirstScheduledOrderAsync call");
            Console.WriteLine($"🟢 FirstOrderResult: Success={firstOrderResult.Success}, Error={firstOrderResult.Error ?? "null"}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

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
            // 🔴 ADD AGGRESSIVE DEBUG LOGGING
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔴 CreateFirstScheduledOrderAsync CALLED!");
            Console.WriteLine($"🔴 SubscriptionId: {subscription.SubscriptionId}");
            Console.WriteLine($"🔴 UserId: {user.UserId}");
            Console.WriteLine($"🔴 UserMealId: {userMeal.UserMealId}");
            Console.WriteLine($"🔴 UserMeal Name: {userMeal.MealName}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            try
            {
                _logger.LogInformation($"📦 Creating first order for subscription #{subscription.SubscriptionId}");

                // Load ingredients from UserMeal
                var ingredients = await _userMealIngredientRepository.GetByUserMealIdAsync(userMeal.UserMealId);
                
                Console.WriteLine($"🔴 Loaded {ingredients.Count()} ingredients from UserMeal #{userMeal.UserMealId}");
                
                if (!ingredients.Any())
                {
                    var error = $"No ingredients found for UserMeal #{userMeal.UserMealId}";
                    _logger.LogWarning($"⚠️ {error}");
                    Console.WriteLine($"🔴 ERROR: {error}");
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
                
                Console.WriteLine($"🔴 SUCCESS: ScheduledOrder #{created.ScheduledOrderId} created for {firstDeliveryDate:yyyy-MM-dd}");
                _logger.LogInformation($"✅ ScheduledOrder #{created.ScheduledOrderId} created successfully for {firstDeliveryDate:yyyy-MM-dd}");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to create first order for subscription #{subscription.SubscriptionId}");
                Console.WriteLine($"🔴 ERROR: Failed to create first order - {ex.Message}");
                Console.WriteLine($"🔴 StackTrace: {ex.StackTrace}");
                return (false, ex.Message);
            }
        }

        // ✅ Helper method to calculate first delivery date
        private DateOnly CalculateFirstDeliveryDate(Subscription subscription)
        {
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);
            var today = DateOnly.FromDateTime(istNow);
            
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

            foreach (var userMealIngredient in ingredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(userMealIngredient.IngredientId);
                if (ingredient == null) continue;

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
                ScheduledFor = deliveryDateTimeUtc,
                DeliveryTimeSlot = "8:00 AM",
                TotalPrice = totalPrice,
                OrderStatus = "scheduled",
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

            var today = GetTodayInIST();
            subscription.NextScheduledDate = CalculateNextDeliveryDate(subscription, today);

            var updatedSubscription = await _subscriptionRepository.UpdateAsync(subscription);
            
            var result = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            return MapToDto(result!);
        }

        public async Task<bool> DeleteSubscriptionAsync(int subscriptionId)
        {
            return await _subscriptionRepository.DeleteAsync(subscriptionId);
        }

        public async Task<bool> ActivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

            subscription.Active = true;
            await _subscriptionRepository.UpdateAsync(subscription);
            return true;
        }

        public async Task<bool> DeactivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

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
            var today = GetTodayInIST();
            
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);
            
            Console.WriteLine($"[SYNC] ========================================");
            Console.WriteLine($"[SYNC] Starting subscription date sync");
            Console.WriteLine($"[SYNC] UTC Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"[SYNC] IST Time: {istNow:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"[SYNC] Today (IST): {today:yyyy-MM-dd}");
            Console.WriteLine($"[SYNC] Active subscriptions found: {activeSubscriptions.Count()}");
            Console.WriteLine($"[SYNC] ========================================");
            
            int updatedCount = 0;
            int skippedCount = 0;
            
            foreach (var subscription in activeSubscriptions)
            {
                var oldNextDate = subscription.NextScheduledDate;
                var newNextDate = CalculateNextDeliveryDate(subscription, today);
                
                Console.WriteLine($"[SYNC] Sub #{subscription.SubscriptionId}: " +
                                 $"Freq={subscription.Frequency}, " +
                                 $"Start={subscription.StartDate:yyyy-MM-dd}, " +
                                 $"OldNext={oldNextDate?.ToString("yyyy-MM-dd") ?? "NULL"}, " +
                                 $"NewNext={newNextDate:yyyy-MM-dd}");
                
                if (subscription.NextScheduledDate != newNextDate)
                {
                    subscription.NextScheduledDate = newNextDate;
                    await _subscriptionRepository.UpdateAsync(subscription);
                    updatedCount++;
                    Console.WriteLine($"[SYNC]   ✅ UPDATED to {newNextDate:yyyy-MM-dd}");
                }
                else
                {
                    skippedCount++;
                    Console.WriteLine($"[SYNC]   ⏭️ SKIPPED (already correct)");
                }
            }
            
            Console.WriteLine($"[SYNC] ========================================");
            Console.WriteLine($"[SYNC] Sync complete: {updatedCount} updated, {skippedCount} skipped");
            Console.WriteLine($"[SYNC] ========================================");
        }

        /// <summary>
        /// ✅ NEW: Get today's date in IST timezone
        /// </summary>
        private static DateOnly GetTodayInIST()
        {
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstTimeZone);
            return DateOnly.FromDateTime(istNow);
        }

        private static DateOnly CalculateInitialNextDeliveryDate(
            DateOnly startDate, 
            SubscriptionFrequency frequency,
            List<WeeklyScheduleDto>? weeklySchedule)
        {
            var today = GetTodayInIST();
            
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
            
            var nextDayInWeek = orderedDays.FirstOrDefault(d => d > currentDayOfWeek);
            
            if (nextDayInWeek > 0)
            {
                int daysUntilNext = nextDayInWeek - currentDayOfWeek;
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
    }
}
