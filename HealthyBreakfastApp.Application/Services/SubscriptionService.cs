// HealthyBreakfastApp.Application/Services/SubscriptionService.cs

using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserMealRepository _userMealRepository;

        // ✅ FIX: Use IST timezone for consistent date calculations
        private static readonly TimeZoneInfo IstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IUserRepository userRepository,
            IUserMealRepository userMealRepository)
        {
            _subscriptionRepository = subscriptionRepository;
            _userRepository = userRepository;
            _userMealRepository = userMealRepository;
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
            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            var userMeal = await _userMealRepository.GetByIdAsync(dto.UserMealId);
            if (userMeal == null)
                throw new ArgumentException("User meal not found");

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

            var result = await _subscriptionRepository.GetByIdAsync(createdSubscription.SubscriptionId);
            return MapToDto(result!);
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
