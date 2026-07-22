using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Commands.DeleteSubscription
{
    public class DeleteSubscriptionCommandHandler : IRequestHandler<DeleteSubscriptionCommand, bool>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DeleteSubscriptionCommandHandler> _logger;

        public DeleteSubscriptionCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IWalletTransactionService walletTransactionService,
            IUnitOfWork unitOfWork,
            ICacheService cacheService,
            ILogger<DeleteSubscriptionCommandHandler> logger)
        {
            _subscriptionRepository = subscriptionRepository;
            _scheduledOrderRepository = scheduledOrderRepository;
            _walletTransactionService = walletTransactionService;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<bool> Handle(DeleteSubscriptionCommand request, CancellationToken cancellationToken)
        {
            bool success = false;
            int userIdToInvalidate = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId);
                if (subscription == null)
                {
                    success = false;
                    return;
                }

                userIdToInvalidate = subscription.UserId;

                if (subscription.IsActive)
                {
                    subscription.IsActive = false;
                    await _subscriptionRepository.UpdateAsync(subscription);
                }

                var scheduledOrders = await _scheduledOrderRepository.GetBySubscriptionIdAsync(request.SubscriptionId);
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

                success = await _subscriptionRepository.DeleteAsync(request.SubscriptionId);
            });

            if (success && userIdToInvalidate != 0)
            {
                await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(userIdToInvalidate));
            }

            return success;
        }
    }
}
