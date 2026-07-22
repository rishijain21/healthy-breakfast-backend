using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Subscriptions
{
    public static class SubscriptionHelper
    {
        public static SubscriptionDto MapToDto(Subscription subscription)
        {
            return new SubscriptionDto
            {
                SubscriptionId = subscription.SubscriptionId,
                UserId = subscription.UserId,
                UserMealId = subscription.UserMealId,
                MealId = subscription.MealId ?? subscription.UserMeal?.MealId,
                AgreedPrice = subscription.AgreedPrice,
                PauseReason = subscription.PauseReason,
                Frequency = subscription.Frequency,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                NextScheduledDate = subscription.NextScheduledDate,
                CreatedAt = subscription.CreatedAt,
                UpdatedAt = subscription.UpdatedAt,
                UserName = subscription.User?.Name ?? string.Empty,
                MealName = subscription.MealId.HasValue ? (subscription.Meal?.MealName ?? string.Empty) : (subscription.UserMeal?.MealName ?? string.Empty),
                MealPrice = subscription.MealId.HasValue ? (subscription.Meal?.BasePrice ?? 0) : (subscription.UserMeal?.TotalPrice ?? 0),
                MealImageUrl = subscription.MealId.HasValue ? subscription.Meal?.ImageUrl : subscription.UserMeal?.Meal?.ImageUrl,
                WeeklySchedule = subscription.WeeklySchedule
                    .Select(s => new WeeklyScheduleDto
                    {
                        DayOfWeek = s.DayOfWeek,
                        Quantity = s.Quantity
                    })
                    .OrderBy(s => s.DayOfWeek)
                    .ToList()
            };
        }

        public static DateOnly CalculateInitialNextDeliveryDate(
            DateOnly startDate,
            SubscriptionFrequency frequency,
            List<WeeklyScheduleDto>? weeklySchedule,
            IAppTimeProvider time)
        {
            var today = time.TodayIst;

            if (startDate > today)
                return startDate;

            switch (frequency)
            {
                case SubscriptionFrequency.Daily:
                    return today.AddDays(1);

                case SubscriptionFrequency.Weekly:
                    if (weeklySchedule == null || !weeklySchedule.Any())
                        return today.AddDays(7);

                    return FindNextWeeklyDate(today, weeklySchedule.Select(s => s.DayOfWeek).ToList());

                case SubscriptionFrequency.Monthly:
                    return startDate.AddMonths(1);

                default:
                    return today.AddDays(1);
            }
        }

        public static DateOnly CalculateNextDeliveryDate(Subscription subscription, DateOnly fromDate)
        {
            switch (subscription.Frequency)
            {
                case SubscriptionFrequency.Daily:
                    if (subscription.StartDate <= fromDate)
                    {
                        return fromDate.AddDays(1); // Tomorrow (IST)
                    }
                    return subscription.StartDate;

                case SubscriptionFrequency.Weekly:
                    if (!subscription.WeeklySchedule.Any())
                        return fromDate.AddDays(7);

                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    return FindNextWeeklyDate(fromDate, scheduledDays);

                case SubscriptionFrequency.Monthly:
                    if (subscription.NextScheduledDate == null || subscription.NextScheduledDate <= fromDate)
                    {
                        var startDay = subscription.StartDate.Day;
                        var nextMonth = fromDate.AddMonths(1);
                        var maxDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var day = Math.Min(startDay, maxDay);
                        return new DateOnly(nextMonth.Year, nextMonth.Month, day);
                    }
                    return subscription.NextScheduledDate.Value;

                default:
                    return fromDate.AddDays(1);
            }
        }

        public static DateOnly FindNextWeeklyDate(DateOnly currentDate, List<int> scheduledDays)
        {
            if (!scheduledDays.Any())
                return currentDate.AddDays(7);

            var orderedDays = scheduledDays.OrderBy(d => d).ToList();
            int currentDayOfWeek = (int)currentDate.DayOfWeek;

            var nextDayInWeek = orderedDays.Cast<int?>().FirstOrDefault(d => d > currentDayOfWeek);

            if (nextDayInWeek.HasValue)
            {
                int daysUntilNext = nextDayInWeek.Value - currentDayOfWeek;
                return currentDate.AddDays(daysUntilNext);
            }
            else
            {
                int firstDay = orderedDays.First();
                int daysUntilNext = (7 - currentDayOfWeek) + firstDay;
                return currentDate.AddDays(daysUntilNext);
            }
        }

        public static DateOnly CalculateFirstDeliveryDate(Subscription subscription, IAppTimeProvider time, ILogger logger)
        {
            var today = time.TodayIst;

            if (subscription.StartDate > today)
            {
                return subscription.StartDate;
            }

            var firstDeliveryDate = today.AddDays(1);

            if (subscription.Frequency == SubscriptionFrequency.Weekly)
            {
                var tomorrowDayOfWeek = (int)firstDeliveryDate.DayOfWeek;
                var isScheduledDay = subscription.WeeklySchedule.Any(ws => ws.DayOfWeek == tomorrowDayOfWeek);

                if (!isScheduledDay)
                {
                    logger.LogInformation("Tomorrow ({TomorrowDate}) is not a scheduled day, finding next delivery date", firstDeliveryDate);

                    var scheduledDays = subscription.WeeklySchedule.Select(s => s.DayOfWeek).ToList();
                    firstDeliveryDate = FindNextWeeklyDate(today, scheduledDays);
                }
            }

            return firstDeliveryDate;
        }
    }
}
