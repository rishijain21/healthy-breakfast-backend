using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionByUserMealId
{
    public class GetActiveSubscriptionByUserMealIdQuery : IRequest<SubscriptionDto?>
    {
        public int UserId { get; set; }
        public int UserMealId { get; set; }

        public GetActiveSubscriptionByUserMealIdQuery(int userId, int userMealId)
        {
            UserId = userId;
            UserMealId = userMealId;
        }
    }
}
