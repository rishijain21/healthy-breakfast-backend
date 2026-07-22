using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetAllOrderHistoryWithDetails;

public record GetAllOrderHistoryWithDetailsQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResult<EnhancedOrderHistoryDto>>;
