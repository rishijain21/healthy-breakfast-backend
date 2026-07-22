using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Queries.GetAllOrderHistoryWithDetails;

public class GetAllOrderHistoryWithDetailsQueryHandler : IRequestHandler<GetAllOrderHistoryWithDetailsQuery, PagedResult<EnhancedOrderHistoryDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrderHistoryWithDetailsQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResult<EnhancedOrderHistoryDto>> Handle(GetAllOrderHistoryWithDetailsQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllOrdersWithDetailsAsync(request.Page, request.PageSize);
        var totalCount = await _orderRepository.CountAsync();

        return new PagedResult<EnhancedOrderHistoryDto>
        {
            Items = OrdersHelper.MapToEnhancedDto(orders).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
