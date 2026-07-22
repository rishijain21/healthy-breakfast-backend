using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Commands.CreateScheduledOrder
{
    public record CreateScheduledOrderCommand(
        int UserId,
        Guid AuthId,
        CreateScheduledOrderDto Dto,
        bool SkipWalletCheck = false
    ) : IRequest<ScheduledOrderResponseDto>;
}
