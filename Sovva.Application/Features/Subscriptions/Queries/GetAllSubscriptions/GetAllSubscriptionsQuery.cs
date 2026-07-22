using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetAllSubscriptions
{
    public class GetAllSubscriptionsQuery : IRequest<PagedResult<SubscriptionDto>>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public GetAllSubscriptionsQuery(int page = 1, int pageSize = 50)
        {
            Page = page;
            PageSize = pageSize;
        }
    }
}
