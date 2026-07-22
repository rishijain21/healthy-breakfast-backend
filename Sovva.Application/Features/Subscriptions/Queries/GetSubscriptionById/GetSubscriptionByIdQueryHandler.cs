using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionById
{
    public class GetSubscriptionByIdQueryHandler : IRequestHandler<GetSubscriptionByIdQuery, SubscriptionDto?>
    {
        private readonly ISubscriptionRepository _repository;

        public GetSubscriptionByIdQueryHandler(ISubscriptionRepository repository)
        {
            _repository = repository;
        }

        public async Task<SubscriptionDto?> Handle(GetSubscriptionByIdQuery request, CancellationToken cancellationToken)
        {
            var subscription = request.UserId.HasValue
                ? await _repository.GetByIdAndUserIdAsync(request.SubscriptionId, request.UserId.Value)
                : await _repository.GetByIdAsync(request.SubscriptionId);

            return subscription != null ? SubscriptionHelper.MapToDto(subscription) : null;
        }
    }
}
