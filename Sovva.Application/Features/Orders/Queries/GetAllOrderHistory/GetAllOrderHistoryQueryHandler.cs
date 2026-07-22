using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Queries.GetAllOrderHistory;

public class GetAllOrderHistoryQueryHandler : IRequestHandler<GetAllOrderHistoryQuery, PagedResult<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrderHistoryQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResult<OrderDto>> Handle(GetAllOrderHistoryQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllAsync(request.Page, request.PageSize);
        var totalCount = await _orderRepository.CountAsync();

        var items = orders.Select(order => new OrderDto
        {
            OrderId = order.OrderId,
            UserId = order.UserId,
            OrderStatus = order.OrderStatus,
            TotalPrice = order.TotalPrice,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        }).ToList();

        return new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
