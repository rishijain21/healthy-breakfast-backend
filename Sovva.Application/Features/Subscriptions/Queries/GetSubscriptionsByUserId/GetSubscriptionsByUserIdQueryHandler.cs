using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionsByUserId
{
    public class GetSubscriptionsByUserIdQueryHandler : IRequestHandler<GetSubscriptionsByUserIdQuery, IEnumerable<SubscriptionDto>>
    {
        private readonly ISubscriptionRepository _repository;
        private readonly ICacheService _cacheService;

        public GetSubscriptionsByUserIdQueryHandler(ISubscriptionRepository repository, ICacheService cacheService)
        {
            _repository = repository;
            _cacheService = cacheService;
        }

        public async Task<IEnumerable<SubscriptionDto>> Handle(GetSubscriptionsByUserIdQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = CacheKeys.SubscriptionsByUser(request.UserId);
            var cached = await _cacheService.GetAsync<IEnumerable<SubscriptionDto>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var subscriptions = await _repository.GetByUserIdAsync(request.UserId);
            var result = subscriptions.Select(SubscriptionHelper.MapToDto).ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }
    }
}
