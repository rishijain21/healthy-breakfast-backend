using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptions
{
    public class GetActiveSubscriptionsQueryHandler : IRequestHandler<GetActiveSubscriptionsQuery, IEnumerable<SubscriptionDto>>
    {
        private readonly ISubscriptionRepository _repository;

        public GetActiveSubscriptionsQueryHandler(ISubscriptionRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<SubscriptionDto>> Handle(GetActiveSubscriptionsQuery request, CancellationToken cancellationToken)
        {
            var subscriptions = await _repository.GetActiveSubscriptionsAsync();
            return subscriptions.Select(SubscriptionHelper.MapToDto);
        }
    }
}
