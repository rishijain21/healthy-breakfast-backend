// Sovva.Application/Services/SubscriptionService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.Exceptions;
using Sovva.Domain.Constants;
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
        private readonly IUserMealRepository _userMealRepository;
        private readonly IMealRepository _mealRepository;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IUserMealIngredientRepository _userMealIngredientRepository;
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly IUserLoader _userLoader;
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IUserMealRepository userMealRepository,
            IMealRepository mealRepository,
            IUserAddressRepository userAddressRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IIngredientRepository ingredientRepository,
            IUserMealIngredientRepository userMealIngredientRepository,
            IWalletTransactionService walletTransactionService,
            IAppTimeProvider time,
            ILogger<SubscriptionService> logger,
            IUserLoader userLoader,
            IUnitOfWork unitOfWork)
        {
            _subscriptionRepository = subscriptionRepository;
            _userMealRepository = userMealRepository;
            _mealRepository = mealRepository;
            _userAddressRepository = userAddressRepository;
            _scheduledOrderRepository = scheduledOrderRepository;
            _ingredientRepository = ingredientRepository;
            _userMealIngredientRepository = userMealIngredientRepository;
            _walletTransactionService = walletTransactionService;
            _time = time;
            _logger = logger;
            _userLoader = userLoader;
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<SubscriptionDto>> GetAllSubscriptionsAsync(int page = 1, int pageSize = 50)
        {
            var (subscriptions, totalCount) = await _subscriptionRepository.GetAllWithCountAsync(page, pageSize);
            
            return new PagedResult<SubscriptionDto>
            {
                Items = subscriptions.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
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
            var user = await _userLoader.GetUserWithAuthMappingAsync(dto.UserId);
            if (user == null)
                throw new UserNotFoundException(dto.UserId);

            if (user.UserId != dto.UserId)
            {
                _logger.LogWarning("Security violation: User {UserId} attempted to subscribe with invalid user account", dto.UserId);
                throw new UnauthorizedAccessException("Invalid user account");
            }

            if (!dto.MealId.HasValue && !dto.UserMealId.HasValue)
                throw new ArgumentException("Either MealId or UserMealId must be provided");
            if (dto.MealId.HasValue && dto.UserMealId.HasValue)
                throw new ArgumentException("Cannot provide both MealId and UserMealId");

            Meal? meal = null;
            UserMeal? userMeal = null;

            if (dto.MealId.HasValue)
            {
                meal = await _mealRepository.GetByIdAsync(dto.MealId.Value);
                if (meal == null) throw new ArgumentException("Meal not found");
            }
            else
            {
                userMeal = await _userMealRepository.GetByIdAsync(dto.UserMealId!.Value);
                if (userMeal == null || userMeal.UserId != dto.UserId) 
                    throw new ArgumentException("Custom meal not found or unauthorized");
                meal = await _mealRepository.GetByIdAsync(userMeal.MealId);
                if (meal == null) throw new ArgumentException("Base meal not found for custom meal");
            }

            var finalSubscription = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    // 1. Check for duplicate subscription
                    Subscription? existingSubscription = null;
                    if (dto.MealId.HasValue)
                    {
                        existingSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByMealIdAsync(dto.UserId, dto.MealId.Value);
                    }
                    else
                    {
                        existingSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByUserMealIdAsync(dto.UserId, dto.UserMealId!.Value);
                    }
                    
                    if (existingSubscription != null)
                    {
                        throw new DuplicateSubscriptionException();
                    }

                    // 2. Get primary address
                    var primaryAddress = await _userAddressRepository.GetPrimaryAddressAsync(dto.UserId);
                    if (primaryAddress == null)
                    {
                        throw new AddressNotFoundException(dto.UserId, "Please set a default delivery address before creating a subscription");
                    }

                    // 4. Validate dates
                    if (dto.StartDate >= dto.EndDate)
                        throw new ArgumentException("Start date must be before end date");

                    // 5. Frequency validation
                    if (dto.Frequency == SubscriptionFrequency.Weekly)
                    {
                        if (dto.WeeklySchedule == null || !dto.WeeklySchedule.Any())
                            throw new ArgumentException("Weekly schedule is required for Weekly subscriptions");
                    }

                    // 6. Create Subscription
                    var subscription = new Subscription
                    {
                        UserId = dto.UserId,
                        MealId = dto.MealId,
                        UserMealId = dto.UserMealId,
                        AgreedPrice = dto.MealId.HasValue ? meal.BasePrice : userMeal!.TotalPrice,
                        Frequency = dto.Frequency,
                        StartDate = dto.StartDate,
                        EndDate = dto.EndDate,
                        IsActive = dto.IsActive,
                        DeliveryAddressId = primaryAddress.Id,
                        NextScheduledDate = CalculateInitialNextDeliveryDate(dto.StartDate, dto.Frequency, dto.WeeklySchedule)
                    };

                    var createdSubscription = await _subscriptionRepository.CreateAsync(subscription);

                    // 7. Add Schedules
                    if (dto.Frequency == SubscriptionFrequency.Weekly && dto.WeeklySchedule != null)
                    {
                        var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                        {
                            SubscriptionId = createdSubscription.SubscriptionId,
                            DayOfWeek = s.DayOfWeek,
                            Quantity = s.Quantity
                        });

                        await _subscriptionRepository.AddSchedulesAsync(createdSubscription.SubscriptionId, schedules);
                    }

                    // 8. Create first scheduled order
                    var firstOrderResult = await CreateFirstScheduledOrderAsync(
                        createdSubscription, 
                        user, 
                        userMeal, 
                        meal,
                        primaryAddress
                    );

                    string? firstOrderWarning = null;
                    if (!firstOrderResult.Success)
                    {
                        _logger.LogWarning("Subscription created but first order failed: {Error}", firstOrderResult.Error);
                        if (firstOrderResult.Error?.Contains("No ingredients") == true)
                        {
                            firstOrderWarning = "Subscription created. First delivery will be scheduled once meal ingredients are configured by admin.";
                        }
                    }
                    
                    // ✅ FIX: Attach navigation properties so MapToDto has data to populate MealName, MealPrice, etc.
                    createdSubscription.User = user;
                    createdSubscription.Meal = meal;
                    createdSubscription.UserMeal = userMeal;

                    var dtoResult = MapToDto(createdSubscription);
                    dtoResult.Warning = firstOrderWarning;
                    return dtoResult;
                }
                catch (DuplicateSubscriptionException)
                {
                    // ✅ SOLID-2 FIX: Re-throw domain exception from Infrastructure layer
                    // The duplicate key catch now lives in SubscriptionRepository.CreateAsync
                    throw;
                }
            });

            return finalSubscription;
        }

        // ✅ Return result object instead of throwing
        private async Task<(bool Success, string? Error)> CreateFirstScheduledOrderAsync(
            Subscription subscription,
            User user,
            UserMeal? userMeal,
            Meal meal,
            UserAddress deliveryAddress)
        {
            // FIX Bug 1: userMeal is null for fixed-meal subscriptions — guard against NullReferenceException
            _logger.LogInformation(
                "CreateFirstScheduledOrderAsync called - SubscriptionId: {SubscriptionId}, UserId: {UserId}, UserMealId: {UserMealId}, MealName: {MealName}",
                subscription.SubscriptionId,
                user.UserId,
                subscription.UserMealId?.ToString() ?? "(fixed meal)",
                subscription.MealId.HasValue ? meal.MealName : userMeal?.MealName ?? "(unknown)");
            
            try
            {
                _logger.LogInformation($"Creating first order for subscription #{subscription.SubscriptionId}");

                var scheduledOrderIngredients = new List<ScheduledOrderIngredient>();
                decimal totalPrice = subscription.AgreedPrice; // Base price

                if (subscription.UserMealId.HasValue)
                {
                    var ingredients = await _userMealIngredientRepository.GetByUserMealIdAsync(subscription.UserMealId.Value);
                    if (!ingredients.Any()) return (false, $"No ingredients found for UserMeal #{subscription.UserMealId.Value}");
                    
                    var ingredientIds = ingredients.Select(i => i.IngredientId).ToList();
                    var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

                    foreach (var umi in ingredients)
                    {
                        if (ingredientMap.TryGetValue(umi.IngredientId, out var ing))
                        {
                            var itemTotal = ing.Price * umi.Quantity;
                            totalPrice += itemTotal;
                            scheduledOrderIngredients.Add(new ScheduledOrderIngredient
                            {
                                IngredientId = ing.IngredientId,
                                Quantity = umi.Quantity,
                                UnitPrice = ing.Price,
                                TotalPrice = itemTotal
                            });
                        }
                    }
                }
                else
                {
                    // Fixed meal
                    var mealWithDetails = await _mealRepository.GetByIdWithOptionsAsync(subscription.MealId!.Value);
                    var defaultOption = mealWithDetails?.MealOptions?.FirstOrDefault();
                    if (defaultOption != null && defaultOption.MealOptionIngredients.Any())
                    {
                        var ingredientIds = defaultOption.MealOptionIngredients.Select(i => i.IngredientId).ToList();
                        var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

                        foreach (var moi in defaultOption.MealOptionIngredients)
                        {
                            if (ingredientMap.TryGetValue(moi.IngredientId, out var ing))
                            {
                                var itemTotal = ing.Price * 1;
                                totalPrice += itemTotal;
                                scheduledOrderIngredients.Add(new ScheduledOrderIngredient
                                {
                                    IngredientId = ing.IngredientId,
                                    Quantity = 1,
                                    UnitPrice = ing.Price,
                                    TotalPrice = itemTotal
                                });
                            }
                        }
                    }
                    else
                    {
                        return (false, $"No ingredients configured for master meal #{subscription.MealId.Value}");
                    }
                }

                // Calculate first delivery date
                var firstDeliveryDate = CalculateFirstDeliveryDate(subscription);
                _logger.LogInformation($"First delivery: {firstDeliveryDate:yyyy-MM-dd}");

                // Build scheduled order
                var deliveryDateTimeUtc = _time.ToUtc(firstDeliveryDate.ToDateTime(TimeOnly.MinValue));
                
                var scheduledOrder = new ScheduledOrder
                {
                    UserId = subscription.UserId,
                    AuthId = user.AuthMapping?.AuthId ?? throw new Sovva.Domain.Exceptions.BusinessRuleException("User has no AuthMapping"),
                    MealName = subscription.MealId.HasValue ? meal.MealName : userMeal!.MealName,
                    ScheduledFor = DateOnly.FromDateTime(deliveryDateTimeUtc),
                    DeliveryTimeSlot = DeliveryConstants.DefaultTimeSlot,
                    TotalPrice = totalPrice,
                    OrderStatus = ScheduledOrderStatus.Scheduled,
                    CanModify = true,
                    ExpiresAt = deliveryDateTimeUtc.AddDays(1),
                    DeliveryAddressId = deliveryAddress.Id,
                    SubscriptionId = subscription.SubscriptionId,
                    Ingredients = scheduledOrderIngredients
                };

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
                    _logger.LogInformation($"Tomorrow is not a scheduled day, finding next delivery date");
                    
                    // Find next scheduled day
                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    firstDeliveryDate = FindNextWeeklyDate(today, scheduledDays);
                }
            }

            return firstDeliveryDate;
        }



        public async Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto dto)
        {
            var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
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

                if (dto.IsActive.HasValue)
                    subscription.IsActive = dto.IsActive.Value;

                if (subscription.StartDate >= subscription.EndDate)
                    throw new ArgumentException("Start date must be before end date");

                if (dto.WeeklySchedule != null && subscription.Frequency == SubscriptionFrequency.Weekly)
                {
                    if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                        throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");

                    if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                        throw new ArgumentException("Quantity must be greater than 0");

                    var duplicateDays = dto.WeeklySchedule
                        .GroupBy(s => s.DayOfWeek)
                        .Where(g => g.Count() > 1)
                        .Select(g => ((DayOfWeek)g.Key).ToString());
                        
                    if (duplicateDays.Any())
                        throw new ArgumentException($"Duplicate days found: {string.Join(", ", duplicateDays)}");

                    await _subscriptionRepository.RemoveSchedulesAsync(subscriptionId);

                    if (dto.WeeklySchedule.Any())
                    {
                        var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                        {
                            SubscriptionId = subscriptionId,
                            DayOfWeek = s.DayOfWeek,
                            Quantity = s.Quantity
                        });

                        await _subscriptionRepository.AddSchedulesAsync(subscriptionId, schedules);
                    }
                }

                var today = _time.TodayIst;
                subscription.NextScheduledDate = CalculateNextDeliveryDate(subscription, today);

                await _subscriptionRepository.UpdateAsync(subscription);

                // FIX Bug 3: UpdateAsync already returns the saved entity.
                // Re-fetching it from DB is a redundant round-trip.
                return MapToDto(subscription);
            });

            return result;
        }

        public async Task<bool> DeleteSubscriptionAsync(int subscriptionId)
        {
            bool success = false;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // ── Step 1: Deactivate immediately ────────────────────────────
                // Set IsActive = false BEFORE the soft delete so the nightly
                // Hangfire job cannot accidentally process this subscription
                // if it runs between now and midnight.
                var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
                if (subscription == null)
                {
                    success = false;
                    return;
                }

                if (subscription.IsActive)
                {
                    subscription.IsActive = false;
                    await _subscriptionRepository.UpdateAsync(subscription);
                }

                // ── Step 2: Refund pending ScheduledOrders ────────────────────
                // Only delete non-processed orders to preserve FK integrity with Orders table.
                var scheduledOrders = await _scheduledOrderRepository.GetBySubscriptionIdAsync(subscriptionId);
                var pendingOrders = scheduledOrders.Where(so => !so.IsProcessedToOrder).ToList();

                _logger.LogInformation("Deleting {PendingCount} pending ScheduledOrders (keeping {ProcessedCount} processed)",
                    pendingOrders.Count, scheduledOrders.Count - pendingOrders.Count);

                foreach (var order in pendingOrders)
                {
                    bool hasDebit = await _walletTransactionService.TransactionExistsForScheduledOrderAsync(order.ScheduledOrderId);
                    if (hasDebit)
                    {
                        await _walletTransactionService.WriteTransactionRecordAsync(
                            order.UserId,
                            order.TotalPrice,
                            "Credit",
                            $"Refund: Subscription cancelled for scheduled order #{order.ScheduledOrderId}",
                            order.ScheduledOrderId);

                        _logger.LogInformation("Refunded {Amount} to User {UserId} for deleted ScheduledOrder #{OrderId}",
                            order.TotalPrice, order.UserId, order.ScheduledOrderId);
                    }

                    await _scheduledOrderRepository.DeleteAsync(order.ScheduledOrderId);
                }

                // ── Step 3: Soft delete the subscription ──────────────────────
                // DeleteAsync calls _context.Remove() which the TimestampInterceptor
                // converts to: subscription.DeletedAt = now (soft delete).
                // The row is retained for win-back analytics.
                success = await _subscriptionRepository.DeleteAsync(subscriptionId);
            });

            return success;
        }

        public async Task<bool> ActivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

            // ✅ FIX: Idempotency guard - prevent double activation
            if (subscription.IsActive)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already active - no action needed", subscriptionId);
                return true;
            }

            // ✅ NEW: If it's a fixed meal, update the AgreedPrice to current master price
            if (subscription.MealId.HasValue)
            {
                var meal = await _mealRepository.GetByIdAsync(subscription.MealId.Value);
                if (meal != null)
                    subscription.AgreedPrice = meal.BasePrice; // Accept new price
            }

            subscription.IsActive = true;
            subscription.PauseReason = null; // ✅ NEW: Clear the reason
            await _subscriptionRepository.UpdateAsync(subscription);
            return true;
        }

        public async Task<bool> DeactivateSubscriptionAsync(int subscriptionId)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return false;

            // ✅ FIX: Idempotency guard - prevent double deactivation
            if (!subscription.IsActive)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already inactive - no action needed", subscriptionId);
                return true;
            }

            subscription.IsActive = false;
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
            
            _logger.LogInformation("=== Subscription date sync started - IST Today: {Today}", today);
            
            // ✅ NEW: Collect updates in memory, then batch update
            var subscriptionsToUpdate = new List<Subscription>();
            int updatedCount = 0;
            int skippedCount = 0;
            
            foreach (var subscription in activeSubscriptions)
            {
                var oldNextDate = subscription.NextScheduledDate;
                var newNextDate = CalculateNextDeliveryDate(subscription, today);
                
                _logger.LogDebug(
                    "Subscription #{SubscriptionId} sync - Frequency: {Frequency}, StartDate: {StartDate}, OldNextDate: {OldNextDate}, NewNextDate: {NewNextDate}",
                    subscription.SubscriptionId, subscription.Frequency, subscription.StartDate, oldNextDate, newNextDate);

                if (subscription.NextScheduledDate != newNextDate)
                {
                    subscription.NextScheduledDate = newNextDate;
                    subscriptionsToUpdate.Add(subscription);
                    _logger.LogDebug("Subscription #{SubscriptionId} next delivery date updated to {NewDate}", subscription.SubscriptionId, newNextDate);
                    updatedCount++;
                }
                else
                {
                    _logger.LogDebug("Subscription #{SubscriptionId} next delivery date already correct ({Date})", subscription.SubscriptionId, subscription.NextScheduledDate);
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
                    return today.AddDays(1);
                
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
                MealId = subscription.MealId ?? subscription.UserMeal?.MealId,
                AgreedPrice = subscription.AgreedPrice,
                PauseReason = subscription.PauseReason,
                Frequency = subscription.Frequency,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                NextScheduledDate = subscription.NextScheduledDate,
                CreatedAt = subscription.CreatedAt,
                UpdatedAt = subscription.UpdatedAt,
                UserName = subscription.User?.Name ?? string.Empty,
                MealName = subscription.MealId.HasValue ? (subscription.Meal?.MealName ?? string.Empty) : (subscription.UserMeal?.MealName ?? string.Empty),
                MealPrice = subscription.MealId.HasValue ? (subscription.Meal?.BasePrice ?? 0) : (subscription.UserMeal?.TotalPrice ?? 0),

                MealImageUrl = subscription.MealId.HasValue ? subscription.Meal?.ImageUrl : subscription.UserMeal?.Meal?.ImageUrl,
                
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
                .Where(s => s.EndDate <= today)
                .ToList();

            if (!expired.Any())
            {
                _logger.LogInformation("Expiry job: 0 subscriptions to expire on {Date}", today);
                return;
            }

            foreach (var sub in expired)
            {
                sub.IsActive = false;
                // UpdatedAt is handled by TimestampInterceptor automatically
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
