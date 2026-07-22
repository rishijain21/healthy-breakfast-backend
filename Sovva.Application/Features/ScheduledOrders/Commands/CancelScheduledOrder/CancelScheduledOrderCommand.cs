using System;
using MediatR;

namespace Sovva.Application.Features.ScheduledOrders.Commands.CancelScheduledOrder
{
    public record CancelScheduledOrderCommand(
        int UserId,
        Guid AuthId,
        int ScheduledOrderId
    ) : IRequest<bool>;
}
