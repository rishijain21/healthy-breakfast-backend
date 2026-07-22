using MediatR;

namespace Sovva.Application.Features.Subscriptions.Commands.DeleteSubscription
{
    public class DeleteSubscriptionCommand : IRequest<bool>
    {
        public int SubscriptionId { get; set; }

        public DeleteSubscriptionCommand(int subscriptionId)
        {
            SubscriptionId = subscriptionId;
        }
    }
}
