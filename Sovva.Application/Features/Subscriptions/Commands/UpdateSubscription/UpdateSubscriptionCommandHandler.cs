using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Subscriptions.Commands.UpdateSubscription
{
    public class UpdateSubscriptionCommandHandler : IRequestHandler<UpdateSubscriptionCommand, SubscriptionDto?>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAppTimeProvider _time;
        private readonly ICacheService _cacheService;

        public UpdateSubscriptionCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IUnitOfWork unitOfWork,
            IAppTimeProvider time,
            ICacheService cacheService)
        {
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _time = time;
            _cacheService = cacheService;
        }

        public async Task<SubscriptionDto?> Handle(UpdateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            var subscriptionId = request.SubscriptionId;
            var dto = request.Dto;

            var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
                if (subscription == null)
                    return null;

                if (dto.Frequency.HasValue)
                    subscription.Frequency = dto.Frequency.Value;

                if (dto.StartDate.HasValue)
                    subscription.StartDate = dto.StartDate.Value;

                if (dto.EndDate.HasValue)
                    subscription.EndDate = dto.EndDate.Value;

                if (dto.IsActive.HasValue)
                    subscription.IsActive = dto.IsActive.Value;

                if (subscription.StartDate >= subscription.EndDate)
                    throw new ArgumentException("Start date must be before end date");

                if (dto.WeeklySchedule != null && subscription.Frequency == SubscriptionFrequency.Weekly)
                {
                    if (dto.WeeklySchedule.Any(s => s.DayOfWeek < 0 || s.DayOfWeek > 6))
                        throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday)");

                    if (dto.WeeklySchedule.Any(s => s.Quantity <= 0))
                        throw new ArgumentException("Quantity must be greater than 0");

                    var duplicateDays = dto.WeeklySchedule
                        .GroupBy(s => s.DayOfWeek)
                        .Where(g => g.Count() > 1)
                        .Select(g => ((DayOfWeek)g.Key).ToString());

                    if (duplicateDays.Any())
                        throw new ArgumentException($"Duplicate days found: {string.Join(", ", duplicateDays)}");

                    await _subscriptionRepository.RemoveSchedulesAsync(subscriptionId);

                    if (dto.WeeklySchedule.Any())
                    {
                        var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                        {
                            SubscriptionId = subscriptionId,
                            DayOfWeek = s.DayOfWeek,
                            Quantity = s.Quantity
                        });

                        await _subscriptionRepository.AddSchedulesAsync(subscriptionId, schedules);
                    }
                }

                var today = _time.TodayIst;
                subscription.NextScheduledDate = SubscriptionHelper.CalculateNextDeliveryDate(subscription, today);

                await _subscriptionRepository.UpdateAsync(subscription);

                return SubscriptionHelper.MapToDto(subscription);
            });

            if (result != null)
            {
                await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(result.UserId));
            }

            return result;
        }
    }
}
