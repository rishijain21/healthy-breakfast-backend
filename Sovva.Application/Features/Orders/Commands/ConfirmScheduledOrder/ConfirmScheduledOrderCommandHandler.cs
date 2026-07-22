using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Orders.Commands.ConfirmScheduledOrder;

public class ConfirmScheduledOrderCommandHandler : IRequestHandler<ConfirmScheduledOrderCommand, int>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppTimeProvider _time;

    public ConfirmScheduledOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IAppTimeProvider time)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<int> Handle(ConfirmScheduledOrderCommand request, CancellationToken cancellationToken)
    {
        var scheduledOrder = request.ScheduledOrder;
        var existingOrder = request.ExistingOrder ?? await _orderRepository.GetByScheduledOrderIdAsync(scheduledOrder.ScheduledOrderId);

        if (existingOrder != null)
        {
            return existingOrder.OrderId;
        }

        if (scheduledOrder.DeliveryAddressId == null)
        {
            throw new InvalidOperationException(
                $"ScheduledOrder #{scheduledOrder.ScheduledOrderId} has no DeliveryAddressId. " +
                "Cannot create Order without a delivery address.");
        }

        var order = new Order
        {
            UserId = scheduledOrder.UserId,
            UserMealId = null,
            ScheduledOrderId = scheduledOrder.ScheduledOrderId,
            DeliveryAddressId = scheduledOrder.DeliveryAddressId.Value,
            OrderStatus = OrderStatus.Confirmed,
            TotalPrice = scheduledOrder.TotalPrice,
            ScheduledFor = _time.ToUtc(scheduledOrder.ScheduledFor.ToDateTime(TimeOnly.MinValue)),
            OrderDate = _time.UtcNow,
            CreatedAt = _time.UtcNow,
            UpdatedAt = _time.UtcNow
        };

        await _orderRepository.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return order.OrderId;
    }
}
