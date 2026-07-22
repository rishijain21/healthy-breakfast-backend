using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;

namespace Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionsByUserId
{
    public class GetActiveSubscriptionsByUserIdQueryHandler : IRequestHandler<GetActiveSubscriptionsByUserIdQuery, IEnumerable<SubscriptionDto>>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IAppTimeProvider _timeProvider;

        public GetActiveSubscriptionsByUserIdQueryHandler(
            ISubscriptionRepository subscriptionRepository,
            IAppTimeProvider timeProvider)
        {
            _subscriptionRepository = subscriptionRepository;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<SubscriptionDto>> Handle(GetActiveSubscriptionsByUserIdQuery request, CancellationToken cancellationToken)
        {
            var targetDate = _timeProvider.TodayIst;
            var subscriptions = await _subscriptionRepository.GetActiveSubscriptionsByUserIdAsync(request.UserId, targetDate);

            return subscriptions.Select(s => new SubscriptionDto
            {
                SubscriptionId = s.SubscriptionId,
                UserId = s.UserId,
                MealId = s.MealId,
                MealName = s.Meal?.MealName ?? string.Empty,
                UserMealId = s.UserMealId,
                AgreedPrice = s.AgreedPrice,
                PauseReason = s.PauseReason,
                Frequency = s.Frequency,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                NextScheduledDate = s.NextScheduledDate,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                UserName = s.User?.Name ?? string.Empty,
                MealPrice = s.Meal?.BasePrice ?? s.UserMeal?.Meal?.BasePrice ?? 0,
                MealImageUrl = s.Meal?.ImageUrl ?? s.UserMeal?.Meal?.ImageUrl,
                WeeklySchedule = s.WeeklySchedule.Select(ws => new WeeklyScheduleDto
                {
                    DayOfWeek = ws.DayOfWeek,
                    Quantity = ws.Quantity
                }).ToList()
            });
        }
    }
}
