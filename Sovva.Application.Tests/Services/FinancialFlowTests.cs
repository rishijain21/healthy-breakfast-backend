using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Features.ScheduledOrders.Commands.ProcessSingleScheduledOrder;
using Sovva.Application.Features.Subscriptions.Commands.CreateSubscription;
using Sovva.Application.Features.Wallet.Commands.AtomicDebit;
using Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Application.Tests.Helpers;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class FinancialFlowTests
    {
        // ── 1. WalletDebitIsAtomicAsync ──────────────────────────────────────────
        [Fact]
        public async Task WalletDebitIsAtomicAsync_ShouldReturnFalseAndNotCreateTransaction_WhenBalanceInsufficient()
        {
            // Arrange
            var walletTxRepoMock = new Mock<IWalletTransactionRepository>();
            var userRepoMock = new Mock<IUserRepository>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var loggerMock = new Mock<ILogger<WalletTransactionService>>();
            var cacheMock = new Mock<ICacheService>();
            var failedAttemptRepoMock = new Mock<IFailedOrderAttemptRepository>();
            var timeMock = new Mock<IAppTimeProvider>();

            timeMock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));
            walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(1)).ReturnsAsync(50m);
            walletTxRepoMock.Setup(r => r.AtomicDebitAsync(1, 200m, "Scheduled Order #101", 101))
                .ReturnsAsync((false, null));

            var sender = new TestMediatRSender();
            var atomicDebitHandler = new AtomicDebitCommandHandler(
                walletTxRepoMock.Object, failedAttemptRepoMock.Object, cacheMock.Object, timeMock.Object, new Mock<ILogger<AtomicDebitCommandHandler>>().Object);
            sender.Register<AtomicDebitCommand, (bool Success, long? TransactionId)>(atomicDebitHandler);

            var sut = new WalletTransactionService(sender);

            // Act
            var result = await sut.AtomicDebitAsync(1, 200m, "Scheduled Order #101", 101);

            // Assert
            result.Success.Should().BeFalse();
            result.TransactionId.Should().BeNull();
            failedAttemptRepoMock.Verify(r => r.AddAsync(It.Is<FailedOrderAttempt>(f =>
                f.UserId == 1 &&
                f.ScheduledOrderId == 101 &&
                f.RequiredAmount == 200m &&
                f.AvailableBalance == 50m &&
                f.Reason == "Insufficient wallet balance"
            )), Times.Once);

            cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        // ── 2. SubscriptionCreateRollsBackOnFailure ──────────────────────────────
        [Fact]
        public async Task SubscriptionCreateRollsBackOnFailure_ShouldThrowAndPropagateError_WhenRepoCreateFails()
        {
            // Arrange
            var subRepoMock = new Mock<ISubscriptionRepository>();
            var userMealRepoMock = new Mock<IUserMealRepository>();
            var mealRepoMock = new Mock<IMealRepository>();
            var addressRepoMock = new Mock<IUserAddressRepository>();
            var scheduledOrderRepoMock = new Mock<IScheduledOrderRepository>();
            var ingredientRepoMock = new Mock<IIngredientRepository>();
            var userMealIngRepoMock = new Mock<IUserMealIngredientRepository>();
            var walletServiceMock = new Mock<IWalletTransactionService>();
            var timeMock = new Mock<IAppTimeProvider>();
            var loggerMock = new Mock<ILogger<SubscriptionService>>();
            var userLoaderMock = new Mock<IUserLoader>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var cacheMock = new Mock<ICacheService>();

            var today = new DateOnly(2026, 7, 12);
            timeMock.Setup(t => t.TodayIst).Returns(today);
            timeMock.Setup(t => t.ToIst(It.IsAny<DateTime>())).Returns(new DateTime(2026, 7, 12, 10, 0, 0));

            // ExecuteInTransactionAsync immediately executes the passed action
            unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<SubscriptionDto>>>()))
                .Returns<Func<Task<SubscriptionDto>>>(action => action());

            userLoaderMock.Setup(u => u.GetUserWithAuthMappingAsync(1))
                .ReturnsAsync(new User 
                { 
                    UserId = 1, 
                    Name = "Test User",
                    AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() }
                });

            userMealRepoMock.Setup(u => u.GetByIdAsync(5))
                .ReturnsAsync(new UserMeal { UserMealId = 5, UserId = 1, MealId = 10 });

            mealRepoMock.Setup(m => m.GetByIdAsync(10))
                .ReturnsAsync(new Meal { MealId = 10, BasePrice = 100m });

            addressRepoMock.Setup(a => a.GetPrimaryAddressAsync(1))
                .ReturnsAsync(new UserAddress { Id = 20, UserId = 1, ServiceableLocation = new ServiceableLocation { IsActive = true, Area = "Area 1" } });

            subRepoMock.Setup(s => s.GetAnyActiveSubscriptionByMealIdAsync(1, 10))
                .ReturnsAsync((Subscription?)null);

            // Simulate database error during subscription creation inside unit of work transaction
            subRepoMock.Setup(s => s.CreateAsync(It.IsAny<Subscription>()))
                .ThrowsAsync(new InvalidOperationException("Database constraint violation during insert"));

            var subSender = new TestMediatRSender();
            var createSubHandler = new CreateSubscriptionCommandHandler(
                subRepoMock.Object,
                userMealRepoMock.Object,
                mealRepoMock.Object,
                addressRepoMock.Object,
                scheduledOrderRepoMock.Object,
                ingredientRepoMock.Object,
                userMealIngRepoMock.Object,
                timeMock.Object,
                new Mock<ILogger<CreateSubscriptionCommandHandler>>().Object,
                userLoaderMock.Object,
                unitOfWorkMock.Object,
                cacheMock.Object
            );
            subSender.Register(createSubHandler);

            var sut = new SubscriptionService(subSender);

            var dto = new CreateSubscriptionInternalDto
            {
                UserId = 1,
                UserMealId = 5,
                StartDate = today.AddDays(1),
                EndDate = today.AddDays(10),
                Frequency = SubscriptionFrequency.Daily
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateSubscriptionAsync(dto));
            ex.Message.Should().Be("Database constraint violation during insert");

            // Verify UnitOfWork executed in transaction and nothing committed outside the failed transaction
            unitOfWorkMock.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<SubscriptionDto>>>()), Times.Once);
        }

        // ── 3. MidnightJobIsIdempotent ───────────────────────────────────────────
        [Fact]
        public async Task MidnightJobIsIdempotent_ShouldNotDebitOrRecreateOrder_WhenOrderAlreadyExists()
        {
            // Arrange
            var scheduledOrderRepoMock = new Mock<IScheduledOrderRepository>();
            var userRepoMock = new Mock<IUserRepository>();
            var ingredientRepoMock = new Mock<IIngredientRepository>();
            var walletServiceMock = new Mock<IWalletTransactionService>();
            var orderServiceMock = new Mock<IOrderService>();
            var timeMock = new Mock<IAppTimeProvider>();
            var loggerMock = new Mock<ILogger<ScheduledOrderService>>();
            var addressRepoMock = new Mock<IUserAddressRepository>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var mealRepoMock = new Mock<IMealRepository>();
            var orderRepoMock = new Mock<IOrderRepository>();
            var walletTxRepoMock = new Mock<IWalletTransactionRepository>();

            var utcNow = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
            timeMock.Setup(t => t.UtcNow).Returns(utcNow);

            var scheduledOrderSender = new TestMediatRSender();
            var processSingleHandler = new ProcessSingleScheduledOrderCommandHandler(
                scheduledOrderRepoMock.Object,
                walletServiceMock.Object,
                orderServiceMock.Object,
                timeMock.Object,
                unitOfWorkMock.Object,
                new Mock<ILogger<ProcessSingleScheduledOrderCommandHandler>>().Object
            );
            scheduledOrderSender.Register(processSingleHandler);

            var sut = new ScheduledOrderService(scheduledOrderSender);

            var authId = Guid.NewGuid();
            var scheduledOrder = new ScheduledOrder
            {
                ScheduledOrderId = 501,
                UserId = 1,
                AuthId = authId,
                DeliveryAddressId = 10,
                MealName = "Oat Bowl",
                TotalPrice = 150m,
                OrderStatus = ScheduledOrderStatus.Confirmed
            };

            var user = new User 
            { 
                UserId = 1, 
                Name = "Test User",
                AuthMapping = new UserAuthMapping { AuthId = authId }
            };
            var usersByAuthId = new Dictionary<Guid, User> { { authId, user } };

            var existingOrder = new Order { OrderId = 999, ScheduledOrderId = 501, TotalPrice = 150m };
            var existingOrders = new Dictionary<int, Order> { { 501, existingOrder } };

            var existingTx = new WalletTransaction { TransactionId = 888, ScheduledOrderId = 501, Amount = 150m, Type = WalletConstants.Debit };
            var existingTransactions = new Dictionary<int, WalletTransaction> { { 501, existingTx } };

            var address = new UserAddress { Id = 10, UserId = 1, ServiceableLocation = new ServiceableLocation { IsActive = true, Area = "Downtown" } };
            var addressesMap = new Dictionary<int, UserAddress> { { 10, address } };

            // Act
            var success = await sut.ProcessSingleScheduledOrderAsync(
                scheduledOrder,
                usersByAuthId,
                existingOrders,
                existingTransactions,
                addressesMap
            );

            // Assert
            success.Should().BeTrue();
            scheduledOrderRepoMock.Verify(r => r.MarkAsProcessedAsync(501, 999, utcNow), Times.Once);

            // Crucially: verify neither AtomicDebitAsync nor ConfirmScheduledOrderAsync were invoked again
            walletServiceMock.Verify(w => w.AtomicDebitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
            orderServiceMock.Verify(o => o.ConfirmScheduledOrderAsync(It.IsAny<ScheduledOrder>(), It.IsAny<Order?>()), Times.Never);
        }

        // ── 4. WalletBalanceNeverGoesNegative ────────────────────────────────────
        [Fact]
        public async Task WalletBalanceNeverGoesNegative_ShouldThrowInsufficientBalanceException_WhenDebitExceedsAvailable()
        {
            // Arrange
            var walletTxRepoMock = new Mock<IWalletTransactionRepository>();
            var userRepoMock = new Mock<IUserRepository>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            var loggerMock = new Mock<ILogger<WalletTransactionService>>();
            var cacheMock = new Mock<ICacheService>();
            var failedAttemptRepoMock = new Mock<IFailedOrderAttemptRepository>();
            var timeMock = new Mock<IAppTimeProvider>();

            var sender = new TestMediatRSender();
            var createHandler = new CreateWalletTransactionCommandHandler(
                walletTxRepoMock.Object, userRepoMock.Object, unitOfWorkMock.Object, new Mock<ILogger<CreateWalletTransactionCommandHandler>>().Object, cacheMock.Object);
            sender.Register<CreateWalletTransactionCommand, WalletTransactionDto>(createHandler);

            var sut = new WalletTransactionService(sender);

            userRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(new User { UserId = 1, Name = "Test User" });

            // User has ₹100 balance
            walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(1))
                .ReturnsAsync(100m);

            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 500m, // Attempting to debit ₹500 when only ₹100 is available
                Type = WalletConstants.Debit,
                Description = "High cost meal"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InsufficientBalanceException>(() => sut.CreateTransactionAsync(dto));
            ex.Required.Should().Be(500m);
            ex.Available.Should().Be(100m);

            // Verify repository CreateAsync was NEVER called for a negative balance debit
            walletTxRepoMock.Verify(r => r.CreateAsync(It.IsAny<WalletTransaction>()), Times.Never);
        }
    }
}
