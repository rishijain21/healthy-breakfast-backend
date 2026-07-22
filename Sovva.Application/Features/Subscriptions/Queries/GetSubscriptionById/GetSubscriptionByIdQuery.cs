using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionById
{
    public class GetSubscriptionByIdQuery : IRequest<SubscriptionDto?>
    {
        public int SubscriptionId { get; set; }
        public int? UserId { get; set; }

        public GetSubscriptionByIdQuery(int subscriptionId, int? userId = null)
        {
            SubscriptionId = subscriptionId;
            UserId = userId;
        }
    }
}
