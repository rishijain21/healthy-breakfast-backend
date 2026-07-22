using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Commands.DuplicateScheduledOrder
{
    public record DuplicateScheduledOrderCommand(
        int UserId,
        Guid AuthId,
        int ScheduledOrderId
    ) : IRequest<ScheduledOrderResponseDto>;
}
