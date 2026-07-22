using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Subscriptions.Commands.UpdateNextScheduledDates
{
    public class UpdateNextScheduledDatesCommandHandler : IRequestHandler<UpdateNextScheduledDatesCommand>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<UpdateNextScheduledDatesCommandHandler> _logger;

        public UpdateNextScheduledDatesCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IAppTimeProvider time,
            ILogger<UpdateNextScheduledDatesCommandHandler> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _time = time;
            _logger = logger;
        }

        public async Task Handle(UpdateNextScheduledDatesCommand request, CancellationToken cancellationToken)
        {
            var activeSubscriptions = await _subscriptionRepository.GetActiveSubscriptionsAsync();
            var today = _time.TodayIst;

            _logger.LogInformation("=== Subscription date sync started - IST Today: {Today}", today);

            var subscriptionsToUpdate = new List<Subscription>();
            int updatedCount = 0;
            int skippedCount = 0;

            foreach (var subscription in activeSubscriptions)
            {
                var oldNextDate = subscription.NextScheduledDate;
                var newNextDate = SubscriptionHelper.CalculateNextDeliveryDate(subscription, today);

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

            if (subscriptionsToUpdate.Count > 0)
            {
                await _subscriptionRepository.UpdateBatchAsync(subscriptionsToUpdate);
                _logger.LogInformation("Batch updated {Count} subscriptions in single DB call", subscriptionsToUpdate.Count);
            }

            _logger.LogInformation("=== Subscription sync complete - Updated: {UpdatedCount}, Skipped: {SkippedCount}", updatedCount, skippedCount);
        }
    }
}
