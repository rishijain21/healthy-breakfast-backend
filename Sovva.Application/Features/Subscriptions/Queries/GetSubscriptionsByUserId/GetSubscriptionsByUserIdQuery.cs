using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionsByUserId
{
    public class GetSubscriptionsByUserIdQuery : IRequest<IEnumerable<SubscriptionDto>>
    {
        public int UserId { get; set; }

        public GetSubscriptionsByUserIdQuery(int userId)
        {
            UserId = userId;
        }
    }
}
