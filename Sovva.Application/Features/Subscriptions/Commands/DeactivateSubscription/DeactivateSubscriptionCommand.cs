using MediatR;

namespace Sovva.Application.Features.Subscriptions.Commands.DeactivateSubscription
{
    public class DeactivateSubscriptionCommand : IRequest<bool>
    {
        public int SubscriptionId { get; set; }

        public DeactivateSubscriptionCommand(int subscriptionId)
        {
            SubscriptionId = subscriptionId;
        }
    }
}
