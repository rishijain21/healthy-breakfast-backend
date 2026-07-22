using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetAllOrderHistory;

public record GetAllOrderHistoryQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResult<OrderDto>>;
