// Sovva.Application/Services/SubscriptionService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Subscriptions.Commands.ActivateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.CreateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.DeactivateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.DeleteSubscription;
using Sovva.Application.Features.Subscriptions.Commands.ExpireSubscriptions;
using Sovva.Application.Features.Subscriptions.Commands.UpdateNextScheduledDates;
using Sovva.Application.Features.Subscriptions.Commands.UpdateSubscription;
using Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionByUserMealId;
using Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptions;
using Sovva.Application.Features.Subscriptions.Queries.GetAllSubscriptions;
using Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionById;
using Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionsByUserId;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISender _sender;

        public SubscriptionService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<PagedResult<SubscriptionDto>> GetAllSubscriptionsAsync(int page = 1, int pageSize = 50)
        {
            return await _sender.Send(new GetAllSubscriptionsQuery(page, pageSize));
        }

        public async Task<SubscriptionDto?> GetSubscriptionByIdAsync(int subscriptionId)
        {
            return await _sender.Send(new GetSubscriptionByIdQuery(subscriptionId));
        }

        public async Task<SubscriptionDto?> GetSubscriptionByIdAndUserIdAsync(int subscriptionId, int userId)
        {
            return await _sender.Send(new GetSubscriptionByIdQuery(subscriptionId, userId));
        }

        public async Task<IEnumerable<SubscriptionDto>> GetSubscriptionsByUserIdAsync(int userId)
        {
            return await _sender.Send(new GetSubscriptionsByUserIdQuery(userId));
        }

        public async Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsByUserIdAsync(int userId)
        {
            return await _sender.Send(new Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionsByUserId.GetActiveSubscriptionsByUserIdQuery(userId));
        }

        public async Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsAsync()
        {
            return await _sender.Send(new GetActiveSubscriptionsQuery());
        }

        public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionInternalDto dto)
        {
            return await _sender.Send(new CreateSubscriptionCommand(dto));
        }

        public async Task<SubscriptionDto?> UpdateSubscriptionAsync(int subscriptionId, UpdateSubscriptionDto updateSubscriptionDto)
        {
            return await _sender.Send(new UpdateSubscriptionCommand(subscriptionId, updateSubscriptionDto));
        }

        public async Task<bool> DeleteSubscriptionAsync(int subscriptionId)
        {
            return await _sender.Send(new DeleteSubscriptionCommand(subscriptionId));
        }

        public async Task<bool> ActivateSubscriptionAsync(int subscriptionId)
        {
            return await _sender.Send(new ActivateSubscriptionCommand(subscriptionId));
        }

        public async Task<bool> DeactivateSubscriptionAsync(int subscriptionId)
        {
            return await _sender.Send(new DeactivateSubscriptionCommand(subscriptionId));
        }

        public async Task<SubscriptionDto?> GetActiveSubscriptionByUserMealIdAsync(int userId, int userMealId)
        {
            return await _sender.Send(new GetActiveSubscriptionByUserMealIdQuery(userId, userMealId));
        }

        public async Task UpdateNextScheduledDatesAsync()
        {
            await _sender.Send(new UpdateNextScheduledDatesCommand());
        }

        public async Task ExpireSubscriptionsAsync()
        {
            await _sender.Send(new ExpireSubscriptionsCommand());
        }
    }
}
