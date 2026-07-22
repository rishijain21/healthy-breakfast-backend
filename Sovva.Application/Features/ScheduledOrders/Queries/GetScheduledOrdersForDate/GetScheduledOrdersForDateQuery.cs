using System;
using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.ScheduledOrders.Queries.GetScheduledOrdersForDate
{
    public record GetScheduledOrdersForDateQuery(
        int UserId,
        Guid AuthId,
        DateTime Date
    ) : IRequest<List<ScheduledOrderResponseDto>>;
}
