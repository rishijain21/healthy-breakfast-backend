using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Commands.ExpireSubscriptions
{
    public class ExpireSubscriptionsCommandHandler : IRequestHandler<ExpireSubscriptionsCommand>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IAppTimeProvider _time;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ExpireSubscriptionsCommandHandler> _logger;

        public ExpireSubscriptionsCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IAppTimeProvider time,
            ICacheService cacheService,
            ILogger<ExpireSubscriptionsCommandHandler> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _time = time;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task Handle(ExpireSubscriptionsCommand request, CancellationToken cancellationToken)
        {
            var today = _time.TodayIst;
            var expired = (await _subscriptionRepository.GetExpiredActiveSubscriptionsAsync(today)).ToList();

            if (!expired.Any())
            {
                _logger.LogInformation("Expiry job: 0 subscriptions to expire on {Date}", today);
                return;
            }

            try
            {
                foreach (var sub in expired)
                {
                    sub.IsActive = false;
                    _logger.LogInformation(
                        "Subscription #{Id} (User {UserId}) expired on {EndDate} — deactivating",
                        sub.SubscriptionId, sub.UserId, sub.EndDate);
                }

                await _subscriptionRepository.UpdateBatchAsync(expired);

                foreach (var sub in expired)
                {
                    await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(sub.UserId));
                }

                _logger.LogInformation(
                    "Expiry job complete — {Count} subscriptions deactivated on {Date}",
                    expired.Count, today);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "CRITICAL: Failed to persist {Count} expired subscriptions. These will remain active until the next successful run.", expired.Count);
                throw;
            }
        }
    }
}
