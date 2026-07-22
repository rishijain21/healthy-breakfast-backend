using MediatR;

namespace Sovva.Application.Features.ScheduledOrders.Queries.GetTimeUntilMidnightMinutes
{
    public record GetTimeUntilMidnightMinutesQuery : IRequest<int>;
}
