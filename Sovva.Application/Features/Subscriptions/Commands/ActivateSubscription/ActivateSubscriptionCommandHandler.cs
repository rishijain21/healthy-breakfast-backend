using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Commands.ActivateSubscription
{
    public class ActivateSubscriptionCommandHandler : IRequestHandler<ActivateSubscriptionCommand, bool>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IMealRepository _mealRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ActivateSubscriptionCommandHandler> _logger;

        public ActivateSubscriptionCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IMealRepository mealRepository,
            ICacheService cacheService,
            ILogger<ActivateSubscriptionCommandHandler> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _mealRepository = mealRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<bool> Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
                return false;

            if (subscription.IsActive)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already active - no action needed", request.SubscriptionId);
                return true;
            }

            if (subscription.MealId.HasValue)
            {
                var meal = await _mealRepository.GetByIdAsync(subscription.MealId.Value);
                if (meal != null)
                    subscription.AgreedPrice = meal.BasePrice;
            }

            subscription.IsActive = true;
            subscription.PauseReason = null;
            await _subscriptionRepository.UpdateAsync(subscription);

            await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(subscription.UserId));

            return true;
        }
    }
}
