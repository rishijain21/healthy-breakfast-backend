using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetUserOrders;

public record GetUserOrdersQuery(int UserId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<OrderDto>>;
