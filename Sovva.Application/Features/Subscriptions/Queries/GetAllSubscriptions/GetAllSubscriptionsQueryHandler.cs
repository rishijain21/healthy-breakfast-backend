using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Queries.GetAllSubscriptions
{
    public class GetAllSubscriptionsQueryHandler : IRequestHandler<GetAllSubscriptionsQuery, PagedResult<SubscriptionDto>>
    {
        private readonly ISubscriptionRepository _repository;

        public GetAllSubscriptionsQueryHandler(ISubscriptionRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<SubscriptionDto>> Handle(GetAllSubscriptionsQuery request, CancellationToken cancellationToken)
        {
            var (subscriptions, totalCount) = await _repository.GetAllWithCountAsync(request.Page, request.PageSize);

            return new PagedResult<SubscriptionDto>
            {
                Items = subscriptions.Select(SubscriptionHelper.MapToDto).ToList(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}
