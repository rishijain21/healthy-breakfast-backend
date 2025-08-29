using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

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

        public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionDto createSubscriptionDto)
        {
            // Validate user exists
            var user = await _userRepository.GetByIdAsync(createSubscriptionDto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Validate user meal exists
            var userMeal = await _userMealRepository.GetByIdAsync(createSubscriptionDto.UserMealId);
            if (userMeal == null)
                throw new ArgumentException("User meal not found");

            // Validate dates
            if (createSubscriptionDto.StartDate >= createSubscriptionDto.EndDate)
                throw new ArgumentException("Start date must be before end date");

            var subscription = new Subscription
            {
                UserId = createSubscriptionDto.UserId,
                UserMealId = createSubscriptionDto.UserMealId,
                Frequency = createSubscriptionDto.Frequency,
                StartDate = createSubscriptionDto.StartDate,
                EndDate = createSubscriptionDto.EndDate,
                Active = createSubscriptionDto.Active
            };

            var createdSubscription = await _subscriptionRepository.CreateAsync(subscription);
            
            // Reload with navigation properties
            var result = await _subscriptionRepository.GetByIdAsync(createdSubscription.SubscriptionId);
            return MapToDto(result!);
        }

        public async Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto updateSubscriptionDto)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                return null;

            if (!string.IsNullOrEmpty(updateSubscriptionDto.Frequency))
                subscription.Frequency = updateSubscriptionDto.Frequency;

            if (updateSubscriptionDto.StartDate.HasValue)
                subscription.StartDate = updateSubscriptionDto.StartDate.Value;

            if (updateSubscriptionDto.EndDate.HasValue)
                subscription.EndDate = updateSubscriptionDto.EndDate.Value;

            if (updateSubscriptionDto.Active.HasValue)
                subscription.Active = updateSubscriptionDto.Active.Value;

            // Validate dates if both are set
            if (subscription.StartDate >= subscription.EndDate)
                throw new ArgumentException("Start date must be before end date");

            var updatedSubscription = await _subscriptionRepository.UpdateAsync(subscription);
            return MapToDto(updatedSubscription);
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
                CreatedAt = subscription.CreatedAt,
                UpdatedAt = subscription.UpdatedAt,
                UserName = subscription.User?.Name ?? string.Empty,
                MealName = subscription.UserMeal?.MealName ?? string.Empty,
                MealPrice = subscription.UserMeal?.TotalPrice ?? 0
            };
        }
    }
}
