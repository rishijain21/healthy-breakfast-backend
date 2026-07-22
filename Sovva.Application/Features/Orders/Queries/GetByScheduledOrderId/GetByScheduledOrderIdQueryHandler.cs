using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Orders.Queries.GetByScheduledOrderId;

public class GetByScheduledOrderIdQueryHandler : IRequestHandler<GetByScheduledOrderIdQuery, Order?>
{
    private readonly IOrderRepository _orderRepository;

    public GetByScheduledOrderIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order?> Handle(GetByScheduledOrderIdQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetByScheduledOrderIdAsync(request.ScheduledOrderId);
    }
}
