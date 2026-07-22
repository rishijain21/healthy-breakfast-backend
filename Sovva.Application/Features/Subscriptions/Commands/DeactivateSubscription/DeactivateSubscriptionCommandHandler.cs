using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Commands.DeactivateSubscription
{
    public class DeactivateSubscriptionCommandHandler : IRequestHandler<DeactivateSubscriptionCommand, bool>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DeactivateSubscriptionCommandHandler> _logger;

        public DeactivateSubscriptionCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            ICacheService cacheService,
            ILogger<DeactivateSubscriptionCommandHandler> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<bool> Handle(DeactivateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId);
            if (subscription == null)
                return false;

            if (!subscription.IsActive)
            {
                _logger.LogInformation("Subscription #{SubscriptionId} is already inactive - no action needed", request.SubscriptionId);
                return true;
            }

            subscription.IsActive = false;
            await _subscriptionRepository.UpdateAsync(subscription);

            await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(subscription.UserId));

            return true;
        }
    }
}
