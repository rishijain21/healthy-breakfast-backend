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

            // Default mock setups for GenerateScheduledOrdersFromSubscriptionsAsync
            _userMealRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<UserMeal>());
            _userMealIngredientRepoMock.Setup(r => r.GetByUserMealIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<UserMealIngredient>());
            _userRepoMock.Setup(r => r.GetByIdsWithAuthMappingAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<User>());
            _userAddressRepoMock.Setup(r => r.GetPrimaryAddressesByUserIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<UserAddress>());
            _mealRepoMock.Setup(r => r.GetByIdsWithOptionsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<Meal>());
            _ingredientRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Ingredient>());
            _scheduledOrderRepoMock.Setup(r => r.GetExistingSubscriptionOrdersForDateAsync(It.IsAny<List<int>>(), It.IsAny<DateOnly>()))
                .ReturnsAsync(new List<int>());
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

        // ─────────────────────────────────────────────────────────────────────
        // TASK-8.3: EndDate boundary tests for GenerateScheduledOrdersFromSubscriptionsAsync
        // ─────────────────────────────────────────────────────────────────────

        // Helper: builds a fully-wired subscription + dependencies for EndDate tests.
        // Uses UserMealId path (avoids MealOptions complexity) with pre-populated ingredients.
        private void SetupEndDateTestDependencies(DateOnly today, DateOnly deliveryDay, int userId = 1)
        {
            var authId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                AuthMapping = new UserAuthMapping { AuthId = authId, UserId = userId }
            };
            var userMeal = new UserMeal { UserMealId = 99, UserId = userId, MealName = "Test Meal" };
            var userMealIngredient = new UserMealIngredient
            {
                UserMealIngredientId = 1, UserMealId = 99, IngredientId = 1, Quantity = 1
            };

            _timeProviderMock.Setup(t => t.TodayIst).Returns(today);
            _timeProviderMock.Setup(t => t.TomorrowIst).Returns(deliveryDay);
            _timeProviderMock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc));
            _timeProviderMock.Setup(t => t.ToUtc(It.IsAny<DateTime>())).Returns<DateTime>(dt => dt);

            _userRepoMock.Setup(r => r.GetByIdsWithAuthMappingAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<User> { user });

            _userMealRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<UserMeal> { userMeal });

            _userMealIngredientRepoMock.Setup(r => r.GetByUserMealIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<UserMealIngredient> { userMealIngredient });

            _subscriptionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Subscription>()))
                .ReturnsAsync((Subscription sub) => sub);
        }

        [Fact]
        public async Task GenerateScheduledOrders_EndDateEqualToDeliveryDay_ShouldGenerateOrder()
        {
            // Arrange — EndDate == deliveryDay means the subscription is still valid for this order
            var today = new DateOnly(2026, 7, 12);
            var deliveryDay = today.AddDays(1); // 2026-07-13

            SetupEndDateTestDependencies(today, deliveryDay);

            var subscription = new Subscription
            {
                SubscriptionId = 1, UserId = 1, UserMealId = 99,
                DeliveryAddressId = 10,
                NextScheduledDate = deliveryDay,
                EndDate = deliveryDay,          // ← boundary: exactly on delivery day
                Frequency = SubscriptionFrequency.Daily,
                AgreedPrice = 100m
            };

            _subscriptionRepoMock.Setup(r => r.GetActiveSubscriptionsAsync())
                .ReturnsAsync(new List<Subscription> { subscription });

            // Act
            await _service.GenerateScheduledOrdersFromSubscriptionsAsync("test");

            // Assert
            _scheduledOrderRepoMock.Verify(
                r => r.CreateBatchAsync(It.IsAny<IEnumerable<ScheduledOrder>>()),
                Times.Once,
                "Order should be generated: EndDate == DeliveryDay means subscription covers this delivery");
        }

        [Fact]
        public async Task GenerateScheduledOrders_EndDateBeforeDeliveryDay_ShouldSkip()
        {
            // Arrange — EndDate < deliveryDay means the subscription expired before this order
            var today = new DateOnly(2026, 7, 12);
            var deliveryDay = today.AddDays(1); // 2026-07-13

            _timeProviderMock.Setup(t => t.TodayIst).Returns(today);
            _timeProviderMock.Setup(t => t.TomorrowIst).Returns(deliveryDay);

            var subscription = new Subscription
            {
                SubscriptionId = 1, UserId = 1, UserMealId = 99,
                NextScheduledDate = deliveryDay,
                EndDate = today,                // ← expired TODAY, before tomorrow's delivery
                Frequency = SubscriptionFrequency.Daily
            };

            _subscriptionRepoMock.Setup(r => r.GetActiveSubscriptionsAsync())
                .ReturnsAsync(new List<Subscription> { subscription });

            // Act
            await _service.GenerateScheduledOrdersFromSubscriptionsAsync("test");

            // Assert
            _scheduledOrderRepoMock.Verify(
                r => r.CreateBatchAsync(It.IsAny<IEnumerable<ScheduledOrder>>()),
                Times.Never,
                "Order should NOT be generated: EndDate < DeliveryDay means subscription expired");
        }

        [Fact]
        public async Task GenerateScheduledOrders_EndDateAfterDeliveryDay_ShouldGenerateOrder()
        {
            // Arrange — EndDate > deliveryDay means the subscription is well within its window
            var today = new DateOnly(2026, 7, 12);
            var deliveryDay = today.AddDays(1); // 2026-07-13

            SetupEndDateTestDependencies(today, deliveryDay);

            var subscription = new Subscription
            {
                SubscriptionId = 1, UserId = 1, UserMealId = 99,
                DeliveryAddressId = 10,
                NextScheduledDate = deliveryDay,
                EndDate = deliveryDay.AddDays(30), // ← still plenty of time left
                Frequency = SubscriptionFrequency.Daily,
                AgreedPrice = 100m
            };

            _subscriptionRepoMock.Setup(r => r.GetActiveSubscriptionsAsync())
                .ReturnsAsync(new List<Subscription> { subscription });

            // Act
            await _service.GenerateScheduledOrdersFromSubscriptionsAsync("test");

            // Assert
            _scheduledOrderRepoMock.Verify(
                r => r.CreateBatchAsync(It.Is<IEnumerable<ScheduledOrder>>(
                    orders => orders.Single().SubscriptionId == 1)),
                Times.Once,
                "Order should be generated: EndDate > DeliveryDay");
        }
    }
}
