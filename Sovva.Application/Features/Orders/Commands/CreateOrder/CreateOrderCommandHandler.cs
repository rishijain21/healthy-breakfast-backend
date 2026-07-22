using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Orders.Commands.CreateOrder;

[Obsolete("Do not use. Relies on client-trusted TotalPrice. Use ConfirmScheduledOrderAsync or MealBuilder paths.")]
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, long>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppTimeProvider _time;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IAppTimeProvider time)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<long> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var entity = new Order
        {
            UserId = request.UserId,
            OrderStatus = OrderStatus.Pending,
            TotalPrice = request.Dto.TotalPrice,
            OrderDate = _time.UtcNow,
            ScheduledFor = _time.UtcNow.AddHours(2),
            CreatedAt = _time.UtcNow,
            UpdatedAt = _time.UtcNow
        };

        await _orderRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return entity.OrderId;
    }
}
