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
            // Validate user exists
            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Validate user meal exists
            var userMeal = await _userMealRepository.GetByIdAsync(dto.UserMealId);
            if (userMeal == null)
                throw new ArgumentException("User meal not found");

            // Validate dates
            if (dto.StartDate >= dto.EndDate)
                throw new ArgumentException("Start date must be before end date");

            // ✅ Validate weekly schedule for Weekly frequency
            if (dto.Frequency == SubscriptionFrequency.Weekly)
            {
                if (dto.WeeklySchedule == null || !dto.WeeklySchedule.Any())
                {
                    throw new ArgumentException("Weekly schedule is required for Weekly subscriptions");
                }

                // Validate day of week values
                if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                {
                    throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");
                }

                // Validate quantities
                if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                {
                    throw new ArgumentException("Quantity must be greater than 0");
                }

                // Check for duplicate days
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
                NextScheduledDate = dto.StartDate
            };

            var createdSubscription = await _subscriptionRepository.CreateAsync(subscription);

            // ✅ Add weekly schedule if provided WITH TIMESTAMPS
            if (dto.Frequency == SubscriptionFrequency.Weekly && dto.WeeklySchedule != null)
            {
                var now = DateTime.UtcNow;
                var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                {
                    SubscriptionId = createdSubscription.SubscriptionId,
                    DayOfWeek = s.DayOfWeek,
                    Quantity = s.Quantity,
                    CreatedAt = now,    // ✅ ADDED
                    UpdatedAt = now     // ✅ ADDED
                });

                await _subscriptionRepository.AddSchedulesAsync(createdSubscription.SubscriptionId, schedules);
            }

            // Reload with all navigation properties
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

            // Validate dates
            if (subscription.StartDate >= subscription.EndDate)
                throw new ArgumentException("Start date must be before end date");

            // ✅ Update weekly schedule if provided
            if (dto.WeeklySchedule != null && subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                // Validate new schedule before removing old one
                if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                {
                    throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");
                }

                if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                {
                    throw new ArgumentException("Quantity must be greater than 0");
                }

                // Check for duplicate days
                var duplicateDays = dto.WeeklySchedule
                    .GroupBy(s => s.DayOfWeek)
                    .Where(g => g.Count() > 1)
                    .Select(g => ((DayOfWeek)g.Key).ToString());
                    
                if (duplicateDays.Any())
                {
                    throw new ArgumentException($"Duplicate days found: {string.Join(", ", duplicateDays)}");
                }

                // Remove existing schedules
                await _subscriptionRepository.RemoveSchedulesAsync(subscriptionId);

                // Add new schedules WITH TIMESTAMPS
                if (dto.WeeklySchedule.Any())
                {
                    var now = DateTime.UtcNow;
                    var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                    {
                        SubscriptionId = subscriptionId,
                        DayOfWeek = s.DayOfWeek,
                        Quantity = s.Quantity,
                        CreatedAt = now,    // ✅ ADDED
                        UpdatedAt = now     // ✅ ADDED
                    });

                    await _subscriptionRepository.AddSchedulesAsync(subscriptionId, schedules);
                }
            }

            var updatedSubscription = await _subscriptionRepository.UpdateAsync(subscription);
            
            // Reload with schedules
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
                
                // ✅ Map weekly schedule
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
