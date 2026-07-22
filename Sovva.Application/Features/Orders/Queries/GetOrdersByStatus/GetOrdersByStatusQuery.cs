using MediatR;
using Sovva.Application.DTOs;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Orders.Queries.GetOrdersByStatus;

public record GetOrdersByStatusQuery(OrderStatus Status, int Page = 1, int PageSize = 50) : IRequest<PagedResult<OrderDto>>;
