using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.ScheduledOrders.Queries.GetTimeUntilMidnightMinutes
{
    public class GetTimeUntilMidnightMinutesQueryHandler : IRequestHandler<GetTimeUntilMidnightMinutesQuery, int>
    {
        private readonly IAppTimeProvider _time;

        public GetTimeUntilMidnightMinutesQueryHandler(IAppTimeProvider time)
        {
            _time = time;
        }

        public Task<int> Handle(GetTimeUntilMidnightMinutesQuery request, CancellationToken cancellationToken)
        {
            var istNow = _time.ToIst(_time.UtcNow);
            var midnight = istNow.Date.AddDays(1);
            var timeTillMidnight = midnight - istNow;
            return Task.FromResult((int)timeTillMidnight.TotalMinutes);
        }
    }
}
