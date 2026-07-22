using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ConfirmAllScheduledOrders
{
    public record ConfirmAllScheduledOrdersCommand(
        DateOnly? TargetDate = null
    ) : IRequest<ProcessOrdersResponseDto>;
}
