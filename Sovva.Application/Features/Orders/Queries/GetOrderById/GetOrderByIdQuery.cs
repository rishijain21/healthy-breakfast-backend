using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(long Id) : IRequest<OrderDto?>;
