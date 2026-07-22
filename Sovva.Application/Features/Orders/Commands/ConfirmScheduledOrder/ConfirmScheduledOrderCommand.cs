using MediatR;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Orders.Commands.ConfirmScheduledOrder;

public record ConfirmScheduledOrderCommand(ScheduledOrder ScheduledOrder, Order? ExistingOrder = null) : IRequest<int>;
