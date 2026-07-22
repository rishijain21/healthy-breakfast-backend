using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Subscriptions.Commands.ActivateSubscription;
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
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Tests.Helpers;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class SubscriptionCQRSHandlerTests
    {
        private readonly Mock<ISubscriptionRepository> _subRepoMock = new();
        private readonly Mock<IScheduledOrderRepository> _scheduledOrderRepoMock = new();
        private readonly Mock<IMealRepository> _mealRepoMock = new();
        private readonly Mock<IWalletTransactionService> _walletServiceMock = new();
        private readonly Mock<IAppTimeProvider> _timeProviderMock = new();
        private readonly Mock<ICacheService> _cacheServiceMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

        public SubscriptionCQRSHandlerTests()
        {
            var today = new DateOnly(2026, 7, 12);
            _timeProviderMock.Setup(t => t.TodayIst).Returns(today);

            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns((Func<Task> action) => action());
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<SubscriptionDto>>>()))
                .Returns((Func<Task<SubscriptionDto>> action) => action());
        }

        [Fact]
        public async Task GetAllSubscriptionsQueryHandler_ShouldReturnPagedSubscriptions()
        {
            // Arrange
            var list = new List<Subscription>
            {
                new Subscription { SubscriptionId = 1, UserId = 10, Frequency = SubscriptionFrequency.Daily }
            };
            _subRepoMock.Setup(r => r.GetAllWithCountAsync(1, 10)).ReturnsAsync((list, 1));
            var handler = new GetAllSubscriptionsQueryHandler(_subRepoMock.Object);

            // Act
            var result = await handler.Handle(new GetAllSubscriptionsQuery(1, 10), CancellationToken.None);

            // Assert
            result.TotalCount.Should().Be(1);
            result.Items.Should().HaveCount(1);
            result.Items.First().SubscriptionId.Should().Be(1);
        }

        [Fact]
        public async Task GetSubscriptionByIdQueryHandler_ShouldReturnDto_WhenFound()
        {
            // Arrange
            var sub = new Subscription { SubscriptionId = 5, UserId = 2, Frequency = SubscriptionFrequency.Weekly };
            _subRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(sub);
            var handler = new GetSubscriptionByIdQueryHandler(_subRepoMock.Object);

            // Act
            var result = await handler.Handle(new GetSubscriptionByIdQuery(5), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.SubscriptionId.Should().Be(5);
        }

        [Fact]
        public async Task GetSubscriptionsByUserIdQueryHandler_ShouldUseCacheIfAvailable()
        {
            // Arrange
            var cachedList = new List<SubscriptionDto> { new SubscriptionDto { SubscriptionId = 20, UserId = 3 } };
            _cacheServiceMock.Setup(c => c.GetAsync<IEnumerable<SubscriptionDto>>(CacheKeys.SubscriptionsByUser(3)))
                .ReturnsAsync(cachedList);
            var handler = new GetSubscriptionsByUserIdQueryHandler(_subRepoMock.Object, _cacheServiceMock.Object);

            // Act
            var result = await handler.Handle(new GetSubscriptionsByUserIdQuery(3), CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(cachedList);
            _subRepoMock.Verify(r => r.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActivateSubscriptionCommandHandler_ShouldActivateAndClearCache()
        {
            // Arrange
            var sub = new Subscription { SubscriptionId = 12, UserId = 4, IsActive = false, MealId = 100 };
            _subRepoMock.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(sub);
            _mealRepoMock.Setup(m => m.GetByIdAsync(100)).ReturnsAsync(new Meal { MealId = 100, BasePrice = 150m });
            var handler = new ActivateSubscriptionCommandHandler(_subRepoMock.Object, _mealRepoMock.Object, _cacheServiceMock.Object, new Mock<ILogger<ActivateSubscriptionCommandHandler>>().Object);

            // Act
            var result = await handler.Handle(new ActivateSubscriptionCommand(12), CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            sub.IsActive.Should().BeTrue();
            sub.AgreedPrice.Should().Be(150m);
            _subRepoMock.Verify(r => r.UpdateAsync(sub), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync(CacheKeys.SubscriptionsByUser(4)), Times.Once);
        }

        [Fact]
        public async Task DeleteSubscriptionCommandHandler_ShouldRefundPendingOrdersAndSoftDelete()
        {
            // Arrange
            var sub = new Subscription { SubscriptionId = 50, UserId = 7, IsActive = true };
            _subRepoMock.Setup(r => r.GetByIdAsync(50)).ReturnsAsync(sub);
            _subRepoMock.Setup(r => r.DeleteAsync(50)).ReturnsAsync(true);

            var pendingOrder = new ScheduledOrder { ScheduledOrderId = 1001, UserId = 7, TotalPrice = 120m, IsProcessedToOrder = false };
            _scheduledOrderRepoMock.Setup(r => r.GetBySubscriptionIdAsync(50)).ReturnsAsync(new List<ScheduledOrder> { pendingOrder });
            _walletServiceMock.Setup(w => w.TransactionExistsForScheduledOrderAsync(1001)).ReturnsAsync(true);

            var handler = new DeleteSubscriptionCommandHandler(_subRepoMock.Object, _scheduledOrderRepoMock.Object, _walletServiceMock.Object, _unitOfWorkMock.Object, _cacheServiceMock.Object, new Mock<ILogger<DeleteSubscriptionCommandHandler>>().Object);

            // Act
            var result = await handler.Handle(new DeleteSubscriptionCommand(50), CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            sub.IsActive.Should().BeFalse();
            _walletServiceMock.Verify(w => w.WriteTransactionRecordAsync(7, 120m, "Credit", It.IsAny<string>(), 1001), Times.Once);
            _scheduledOrderRepoMock.Verify(r => r.DeleteAsync(1001), Times.Once);
            _subRepoMock.Verify(r => r.DeleteAsync(50), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync(CacheKeys.SubscriptionsByUser(7)), Times.Once);
        }
    }
}
