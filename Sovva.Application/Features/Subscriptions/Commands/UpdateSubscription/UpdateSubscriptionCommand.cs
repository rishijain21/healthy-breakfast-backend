using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Commands.UpdateSubscription
{
    public class UpdateSubscriptionCommand : IRequest<SubscriptionDto?>
    {
        public int SubscriptionId { get; set; }
        public UpdateSubscriptionDto Dto { get; set; }

        public UpdateSubscriptionCommand(int subscriptionId, UpdateSubscriptionDto dto)
        {
            SubscriptionId = subscriptionId;
            Dto = dto;
        }
    }
}
