using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Queries.GetOrderDetailsById;

public class GetOrderDetailsByIdQueryHandler : IRequestHandler<GetOrderDetailsByIdQuery, EnhancedOrderHistoryDto?>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderDetailsByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<EnhancedOrderHistoryDto?> Handle(GetOrderDetailsByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _orderRepository.GetOrderDetailsByIdAsync(request.Id);
        if (entity == null) return null;

        return OrdersHelper.MapToEnhancedDto(new[] { entity }).FirstOrDefault();
    }
}
