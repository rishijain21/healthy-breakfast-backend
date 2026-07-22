using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Commands.RetryFailedOrders
{
    public record RetryFailedOrdersCommand(
        DateOnly? TargetDate = null,
        string? CorrelationId = null
    ) : IRequest<ProcessOrdersResponseDto>;
}
