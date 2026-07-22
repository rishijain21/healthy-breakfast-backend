using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Subscriptions.Commands.CreateSubscription
{
    public class CreateSubscriptionCommand : IRequest<SubscriptionDto>
    {
        public CreateSubscriptionInternalDto Dto { get; set; }

        public CreateSubscriptionCommand(CreateSubscriptionInternalDto dto)
        {
            Dto = dto;
        }
    }
}
