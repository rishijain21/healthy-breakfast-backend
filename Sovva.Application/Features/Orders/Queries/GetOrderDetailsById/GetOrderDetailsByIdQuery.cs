using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetOrderDetailsById;

public record GetOrderDetailsByIdQuery(long Id) : IRequest<EnhancedOrderHistoryDto?>;
