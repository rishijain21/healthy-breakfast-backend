using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Commands.RateOrder;

public class RateOrderCommandHandler : IRequestHandler<RateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppTimeProvider _time;

    public RateOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IAppTimeProvider time)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<bool> Handle(RateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId);
        if (order == null || order.UserId != request.UserId)
            throw new InvalidOperationException("Order not found or access denied.");

        if (!order.IsPrepared)
            throw new InvalidOperationException("Cannot rate an order that hasn't been prepared/delivered yet.");

        order.Rating = request.Rating;
        order.Review = request.Review;
        order.UpdatedAt = _time.UtcNow;

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}
