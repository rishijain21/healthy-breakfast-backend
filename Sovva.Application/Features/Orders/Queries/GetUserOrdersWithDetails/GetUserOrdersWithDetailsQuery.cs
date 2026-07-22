using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetUserOrdersWithDetails;

public record GetUserOrdersWithDetailsQuery(int UserId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<EnhancedOrderHistoryDto>>;
