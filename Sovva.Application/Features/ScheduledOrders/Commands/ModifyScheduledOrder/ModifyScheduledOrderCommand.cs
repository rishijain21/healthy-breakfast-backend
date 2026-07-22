using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ModifyScheduledOrder
{
    public record ModifyScheduledOrderCommand(
        int UserId,
        Guid AuthId,
        int ScheduledOrderId,
        ModifyScheduledOrderDto Dto
    ) : IRequest<bool>;
}
