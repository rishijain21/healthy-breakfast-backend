using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.ScheduledOrders.Queries.GetScheduledOrdersForDate
{
    public class GetScheduledOrdersForDateQueryHandler : IRequestHandler<GetScheduledOrdersForDateQuery, List<ScheduledOrderResponseDto>>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;

        public GetScheduledOrdersForDateQueryHandler(IScheduledOrderRepository scheduledOrderRepository)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
        }

        public async Task<List<ScheduledOrderResponseDto>> Handle(GetScheduledOrdersForDateQuery request, CancellationToken cancellationToken)
        {
            var orders = await _scheduledOrderRepository.GetByAuthIdAndDateAsync(request.AuthId, request.Date);
            var result = new List<ScheduledOrderResponseDto>();

            foreach (var order in orders)
            {
                result.Add(ScheduledOrderHelper.MapToResponseDto(order));
            }

            return result;
        }
    }
}
