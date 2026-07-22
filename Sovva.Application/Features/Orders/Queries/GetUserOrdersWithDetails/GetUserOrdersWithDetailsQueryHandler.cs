using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Queries.GetUserOrdersWithDetails;

public class GetUserOrdersWithDetailsQueryHandler : IRequestHandler<GetUserOrdersWithDetailsQuery, PagedResult<EnhancedOrderHistoryDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetUserOrdersWithDetailsQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResult<EnhancedOrderHistoryDto>> Handle(GetUserOrdersWithDetailsQuery request, CancellationToken cancellationToken)
    {
        var (orders, totalCount) = await _orderRepository.GetUserOrdersWithDetailsPagedAsync(request.UserId, request.Page, request.PageSize);

        return new PagedResult<EnhancedOrderHistoryDto>
        {
            Items = OrdersHelper.MapToEnhancedDto(orders).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
