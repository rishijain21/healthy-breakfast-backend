using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionByUserMealId
{
    public class GetActiveSubscriptionByUserMealIdQueryHandler : IRequestHandler<GetActiveSubscriptionByUserMealIdQuery, SubscriptionDto?>
    {
        private readonly ISubscriptionRepository _repository;

        public GetActiveSubscriptionByUserMealIdQueryHandler(ISubscriptionRepository repository)
        {
            _repository = repository;
        }

        public async Task<SubscriptionDto?> Handle(GetActiveSubscriptionByUserMealIdQuery request, CancellationToken cancellationToken)
        {
            var subscription = await _repository.GetActiveSubscriptionByUserMealIdAsync(request.UserId, request.UserMealId);
            return subscription != null ? SubscriptionHelper.MapToDto(subscription) : null;
        }
    }
}
