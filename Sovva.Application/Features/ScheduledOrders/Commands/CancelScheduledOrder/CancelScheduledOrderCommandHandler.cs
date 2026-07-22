using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Exceptions;
using Sovva.Application.Interfaces;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders.Commands.CancelScheduledOrder
{
    public class CancelScheduledOrderCommandHandler : IRequestHandler<CancelScheduledOrderCommand, bool>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly ILogger<CancelScheduledOrderCommandHandler> _logger;

        public CancelScheduledOrderCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            ILogger<CancelScheduledOrderCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _logger = logger;
        }

        public async Task<bool> Handle(CancelScheduledOrderCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            var authId = request.AuthId;
            var scheduledOrderId = request.ScheduledOrderId;

            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
            {
                _logger.LogInformation("Order {OrderId} not found during cancellation (likely already deleted) - treating as success", scheduledOrderId);
                return true;
            }

            if (scheduledOrder.UserId != userId)
                throw new UnauthorizedAccessException("Order does not belong to this user");

            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                throw new Sovva.Domain.Exceptions.BusinessRuleException("Order can no longer be cancelled");

            _logger.LogInformation("User cancelled order {OrderId} - deleting from cart", scheduledOrderId);

            await _scheduledOrderRepository.DeleteAsync(scheduledOrderId);

            _logger.LogInformation("Order {OrderId} successfully removed from cart", scheduledOrderId);
            return true;
        }
    }
}
