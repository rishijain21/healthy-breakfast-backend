using MediatR;

namespace Sovva.Application.Features.Subscriptions.Commands.ActivateSubscription
{
    public class ActivateSubscriptionCommand : IRequest<bool>
    {
        public int SubscriptionId { get; set; }

        public ActivateSubscriptionCommand(int subscriptionId)
        {
            SubscriptionId = subscriptionId;
        }
    }
}
