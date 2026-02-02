// HealthyBreakfastApp.Application/Services/SubscriptionSchedulingService.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Application.Services
{
    public class SubscriptionSchedulingService : ISubscriptionSchedulingService
    {
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IUserMealRepository _userMealRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUserMealIngredientRepository _userMealIngredientRepo;
        private readonly ILogger<SubscriptionSchedulingService> _logger;

        public SubscriptionSchedulingService(
            ISubscriptionRepository subscriptionRepo,
            IScheduledOrderService scheduledOrderService,
            IUserMealRepository userMealRepo,
            IUserRepository userRepo,
            IUserMealIngredientRepository userMealIngredientRepo,
            ILogger<SubscriptionSchedulingService> logger)
        {
            _subscriptionRepo = subscriptionRepo;
            _scheduledOrderService = scheduledOrderService;
            _userMealRepo = userMealRepo;
            _userRepo = userRepo;
            _userMealIngredientRepo = userMealIngredientRepo;
            _logger = logger;
        }

        /// <summary>
        /// ✅ MILKBASKET STYLE: Called by Hangfire daily at 12:01 AM IST 
        /// Creates scheduled orders for TOMORROW's delivery from active subscriptions
        /// Supports Daily, Weekly (with specific days & quantities), and Monthly frequencies
        /// </summary>
        public async Task GenerateScheduledOrdersFromSubscriptionsAsync()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var today = DateOnly.FromDateTime(istNow);
            var tomorrow = today.AddDays(1);
            
            _logger.LogInformation($"🥛 [MILKBASKET SUBSCRIPTION JOB] Starting at {istNow:yyyy-MM-dd HH:mm:ss} IST");
            _logger.LogInformation($"📦 Creating scheduled orders for TOMORROW's delivery: {tomorrow:yyyy-MM-dd} ({tomorrow.DayOfWeek})");

            var allSubscriptions = await _subscriptionRepo.GetActiveSubscriptionsAsync();
            
            _logger.LogInformation($"📋 Found {allSubscriptions.Count()} total active subscriptions");

            int generatedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            foreach (var subscription in allSubscriptions)
            {
                try
                {
                    // ✅ Check if this subscription should generate an order for tomorrow
                    if (!ShouldGenerateOrderForDate(subscription, tomorrow))
                    {
                        skippedCount++;
                        continue;
                    }

                    // ✅ Get quantity for tomorrow (especially important for weekly subscriptions)
                    int quantity = GetQuantityForDate(subscription, tomorrow);
                    
                    _logger.LogInformation(
                        $"🔄 Processing subscription #{subscription.SubscriptionId} " +
                        $"(Frequency: {subscription.Frequency}, Quantity: {quantity})");

                    // Get UserMeal details
                    var userMeal = await _userMealRepo.GetByIdAsync(subscription.UserMealId);
                    if (userMeal == null)
                    {
                        _logger.LogWarning($"❌ UserMeal {subscription.UserMealId} not found for subscription {subscription.SubscriptionId}");
                        failedCount++;
                        continue;
                    }

                    // Get UserMealIngredients
                    var userMealIngredients = await _userMealIngredientRepo.GetByUserMealIdAsync(subscription.UserMealId);
                    if (userMealIngredients == null || !userMealIngredients.Any())
                    {
                        _logger.LogWarning($"❌ UserMeal {subscription.UserMealId} has no ingredients");
                        failedCount++;
                        continue;
                    }

                    // Get user with auth mapping
                    var user = await _userRepo.GetByIdAsync(subscription.UserId);
                    if (user?.AuthMapping?.AuthId == null)
                    {
                        _logger.LogWarning($"❌ User {subscription.UserId} has no AuthId mapping");
                        failedCount++;
                        continue;
                    }

                    // ✅ Create scheduled order for TOMORROW with adjusted quantities
                    var tomorrowDateTime = istNow.Date.AddDays(1);

                    var scheduledOrderDto = new CreateScheduledOrderDto
                    {
                        MealName = $"{userMeal.MealName} (Subscription)",
                        MealPrice = userMeal.TotalPrice * quantity, // ✅ Multiply by quantity
                        SelectedIngredients = userMealIngredients.Select(i => new ScheduledOrderIngredientDto
                        {
                            IngredientId = i.IngredientId,
                            Quantity = i.Quantity * quantity  // ✅ Multiply by quantity
                        }).ToList(),
                        ScheduledFor = DateTime.SpecifyKind(tomorrowDateTime, DateTimeKind.Utc),
                        DeliveryTimeSlot = "7:00 AM",
                        NutritionalSummary = null
                    };

                    await _scheduledOrderService.CreateScheduledOrderAsync(user.AuthMapping.AuthId, scheduledOrderDto);

                    // ✅ Update NextScheduledDate
                    subscription.NextScheduledDate = CalculateNextScheduledDate(subscription, tomorrow);
                    subscription.UpdatedAt = DateTime.UtcNow;
                    await _subscriptionRepo.UpdateAsync(subscription);

                    generatedCount++;
                    _logger.LogInformation(
                        $"✅ Generated order for subscription #{subscription.SubscriptionId} " +
                        $"({userMeal.MealName}) - Delivery: {tomorrow:yyyy-MM-dd}, Qty: {quantity}, Next: {subscription.NextScheduledDate:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Failed to generate order for subscription {subscription.SubscriptionId}");
                    failedCount++;
                }
            }

            _logger.LogInformation(
                $"🎉 [SUBSCRIPTION JOB] Complete: {generatedCount} generated, {skippedCount} skipped, {failedCount} failed");
        }

        /// <summary>
        /// ✅ Determines if an order should be generated for a specific date
        /// </summary>
        private bool ShouldGenerateOrderForDate(Domain.Entities.Subscription subscription, DateOnly date)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    // Daily subscriptions run every day
                    return true;

                case SubscriptionFrequency.Weekly:
                    // Check if tomorrow's day is in the weekly schedule
                    int dayOfWeek = (int)date.DayOfWeek;
                    return subscription.WeeklySchedule.Any(s => s.DayOfWeek == dayOfWeek);

                case SubscriptionFrequency.Monthly:
                    // Monthly subscriptions run once per month on the same day
                    return subscription.NextScheduledDate == date;

                default:
                    return false;
            }
        }

        /// <summary>
        /// ✅ Gets the quantity for a specific date (important for weekly subscriptions)
        /// </summary>
        private int GetQuantityForDate(Domain.Entities.Subscription subscription, DateOnly date)
        {
            if (subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                int dayOfWeek = (int)date.DayOfWeek;
                var schedule = subscription.WeeklySchedule.FirstOrDefault(s => s.DayOfWeek == dayOfWeek);
                return schedule?.Quantity ?? 1;
            }

            // Daily and Monthly always return 1
            return 1;
        }

        /// <summary>
        /// ✅ Calculates the next scheduled date based on frequency and current date
        /// </summary>
        private DateOnly CalculateNextScheduledDate(Domain.Entities.Subscription subscription, DateOnly currentDate)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    return currentDate.AddDays(1);

                case SubscriptionFrequency.Weekly:
                    // Find the next day in the weekly schedule
                    return FindNextWeeklyDate(subscription, currentDate);

                case SubscriptionFrequency.Monthly:
                    return currentDate.AddMonths(1);

                default:
                    return currentDate.AddDays(1);
            }
        }

        /// <summary>
        /// ✅ Finds the next scheduled date for weekly subscriptions
        /// Example: If today is Monday and schedule is [Mon, Wed, Fri], next is Wednesday
        /// </summary>
        private DateOnly FindNextWeeklyDate(Domain.Entities.Subscription subscription, DateOnly currentDate)
        {
            if (!subscription.WeeklySchedule.Any())
                return currentDate.AddDays(7);

            var scheduledDays = subscription.WeeklySchedule
                .Select(s => s.DayOfWeek)
                .OrderBy(d => d)
                .ToList();

            int currentDayOfWeek = (int)currentDate.DayOfWeek;

            // Find the next day in the same week
            var nextDayInWeek = scheduledDays.FirstOrDefault(d => d > currentDayOfWeek);
            
            if (nextDayInWeek > 0)
            {
                // Next delivery is later this week
                int daysUntilNext = nextDayInWeek - currentDayOfWeek;
                return currentDate.AddDays(daysUntilNext);
            }
            else
            {
                // Next delivery is next week (first scheduled day)
                int firstDay = scheduledDays.First();
                int daysUntilNext = (7 - currentDayOfWeek) + firstDay;
                return currentDate.AddDays(daysUntilNext);
            }
        }
    }
}
