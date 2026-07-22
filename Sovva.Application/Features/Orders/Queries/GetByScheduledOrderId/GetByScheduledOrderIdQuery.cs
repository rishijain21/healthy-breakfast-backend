using MediatR;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Orders.Queries.GetByScheduledOrderId;

public record GetByScheduledOrderIdQuery(int ScheduledOrderId) : IRequest<Order?>;
