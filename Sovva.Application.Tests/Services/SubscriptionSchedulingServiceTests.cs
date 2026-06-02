using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class SubscriptionSchedulingServiceTests
    {
        private readonly Mock<ISubscriptionRepository> _subscriptionRepoMock;
        private readonly Mock<IScheduledOrderRepository> _scheduledOrderRepoMock;
        private readonly Mock<IScheduledOrderService> _scheduledOrderServiceMock;
        private readonly Mock<IUserMealRepository> _userMealRepoMock;
        private readonly Mock<IUserMealIngredientRepository> _userMealIngredientRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IUserAddressRepository> _userAddressRepoMock;
        private readonly Mock<IMealRepository> _mealRepoMock;
        private readonly Mock<IIngredientRepository> _ingredientRepoMock;
        private readonly Mock<IAppTimeProvider> _timeProviderMock;
        private readonly Mock<ILogger<SubscriptionSchedulingService>> _loggerMock;
        private readonly SubscriptionSchedulingService _service;

        public SubscriptionSchedulingServiceTests()
        {
            _subscriptionRepoMock = new Mock<ISubscriptionRepository>();
            _scheduledOrderRepoMock = new Mock<IScheduledOrderRepository>();
            _scheduledOrderServiceMock = new Mock<IScheduledOrderService>();
            _userMealRepoMock = new Mock<IUserMealRepository>();
            _userMealIngredientRepoMock = new Mock<IUserMealIngredientRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _userAddressRepoMock = new Mock<IUserAddressRepository>();
            _mealRepoMock = new Mock<IMealRepository>();
            _ingredientRepoMock = new Mock<IIngredientRepository>();
            _timeProviderMock = new Mock<IAppTimeProvider>();
            _loggerMock = new Mock<ILogger<SubscriptionSchedulingService>>();

            _service = new SubscriptionSchedulingService(
                _subscriptionRepoMock.Object,
                _scheduledOrderRepoMock.Object,
                _scheduledOrderServiceMock.Object,
                _userMealRepoMock.Object,
                _userMealIngredientRepoMock.Object,
                _userRepoMock.Object,
                _userAddressRepoMock.Object,
                _mealRepoMock.Object,
                _ingredientRepoMock.Object,
                _timeProviderMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public void FindNextWeeklyDate_ShouldReturnSevenDaysLater_WhenScheduledDaysIsEmpty()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21); // Thursday
            var scheduledDays = new List<int>();

            // Act
            var nextDate = SubscriptionSchedulingService.FindNextWeeklyDate(fromDate, scheduledDays);

            // Assert
            nextDate.Should().Be(fromDate.AddDays(7));
        }

        [Fact]
        public void FindNextWeeklyDate_ShouldReturnNextDay_WhenScheduleContainsNextDayInSameWeek()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21); // Thursday (DayOfWeek = 4)
            var scheduledDays = new List<int> { 5 }; // Friday (DayOfWeek = 5)

            // Act
            var nextDate = SubscriptionSchedulingService.FindNextWeeklyDate(fromDate, scheduledDays);

            // Assert
            nextDate.Should().Be(new DateOnly(2026, 5, 22)); // Friday
        }

        [Fact]
        public void FindNextWeeklyDate_ShouldReturnFirstScheduledDayOfNextWeek_WhenNoMoreScheduledDaysInCurrentWeek()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21); // Thursday (DayOfWeek = 4)
            var scheduledDays = new List<int> { 1, 3 }; // Monday, Wednesday (DayOfWeek = 1, 3)

            // Act
            var nextDate = SubscriptionSchedulingService.FindNextWeeklyDate(fromDate, scheduledDays);

            // Assert
            // Thursday (4) -> next Monday (1) of next week.
            // Days to add: (7 - 4) + 1 = 4 days -> 2026-05-25 (Monday)
            nextDate.Should().Be(new DateOnly(2026, 5, 25));
        }

        [Fact]
        public void FindNextWeeklyDate_ShouldCorrectlyHandleSundayEdgeCase_WhenSundayIsNextScheduledDay()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 22); // Friday (DayOfWeek = 5)
            var scheduledDays = new List<int> { 0, 3 }; // Sunday (0), Wednesday (3)

            // Act
            var nextDate = SubscriptionSchedulingService.FindNextWeeklyDate(fromDate, scheduledDays);

            // Assert
            // Friday (5) -> Next scheduled day is Sunday (0).
            // Days to add: (7 - 5) + 0 = 2 days -> 2026-05-24 (Sunday)
            nextDate.Should().Be(new DateOnly(2026, 5, 24));
        }

        [Fact]
        public void CalculateNextScheduledDate_Daily_ShouldReturnNextDay()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21);
            var subscription = new Subscription
            {
                Frequency = SubscriptionFrequency.Daily
            };

            // Act
            var nextDate = _service.CalculateNextScheduledDate(subscription, fromDate);

            // Assert
            nextDate.Should().Be(fromDate.AddDays(1));
        }

        [Fact]
        public void CalculateNextScheduledDate_Monthly_ShouldReturnNextMonth()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21);
            var subscription = new Subscription
            {
                Frequency = SubscriptionFrequency.Monthly
            };

            // Act
            var nextDate = _service.CalculateNextScheduledDate(subscription, fromDate);

            // Assert
            nextDate.Should().Be(fromDate.AddMonths(1));
        }

        [Fact]
        public void CalculateNextScheduledDate_Alternate_ShouldReturnTwoDaysLater()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21);
            var subscription = new Subscription
            {
                Frequency = SubscriptionFrequency.Alternate
            };

            // Act
            var nextDate = _service.CalculateNextScheduledDate(subscription, fromDate);

            // Assert
            nextDate.Should().Be(fromDate.AddDays(2));
        }

        [Fact]
        public void CalculateNextScheduledDate_Weekly_ShouldCallFindNextWeeklyDate()
        {
            // Arrange
            var fromDate = new DateOnly(2026, 5, 21); // Thursday (4)
            var subscription = new Subscription
            {
                Frequency = SubscriptionFrequency.Weekly,
                WeeklySchedule = new List<SubscriptionSchedule>
                {
                    new SubscriptionSchedule { DayOfWeek = 5, Quantity = 1 } // Friday
                }
            };

            // Act
            var nextDate = _service.CalculateNextScheduledDate(subscription, fromDate);

            // Assert
            nextDate.Should().Be(new DateOnly(2026, 5, 22)); // Friday
        }
    }
}
