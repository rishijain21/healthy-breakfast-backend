using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptions
{
    public class GetActiveSubscriptionsQuery : IRequest<IEnumerable<SubscriptionDto>>
    {
    }
}
