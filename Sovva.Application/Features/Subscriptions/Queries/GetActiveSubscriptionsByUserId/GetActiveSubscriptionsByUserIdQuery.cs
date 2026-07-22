using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionsByUserId
{
    public class GetActiveSubscriptionsByUserIdQuery : IRequest<IEnumerable<SubscriptionDto>>
    {
        public int UserId { get; set; }

        public GetActiveSubscriptionsByUserIdQuery(int userId)
        {
            UserId = userId;
        }
    }
}
