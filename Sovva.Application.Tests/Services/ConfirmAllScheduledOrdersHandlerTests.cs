using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.DTOs;
using Sovva.Application.Features.ScheduledOrders.Commands.ConfirmAllScheduledOrders;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class ConfirmAllScheduledOrdersHandlerTests
    {
        private readonly Mock<IScheduledOrderRepository> _mockScheduledOrderRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly Mock<IWalletTransactionRepository> _mockWalletTxRepo;
        private readonly Mock<IUserAddressRepository> _mockAddressRepo;
        private readonly Mock<IWalletTransactionService> _mockWalletService;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<IAppTimeProvider> _mockTime;
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<ILogger<ConfirmAllScheduledOrdersCommandHandler>> _mockLogger;

        public ConfirmAllScheduledOrdersHandlerTests()
        {
            _mockScheduledOrderRepo = new Mock<IScheduledOrderRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockOrderRepo = new Mock<IOrderRepository>();
            _mockWalletTxRepo = new Mock<IWalletTransactionRepository>();
            _mockAddressRepo = new Mock<IUserAddressRepository>();
            _mockWalletService = new Mock<IWalletTransactionService>();
            _mockOrderService = new Mock<IOrderService>();
            _mockTime = new Mock<IAppTimeProvider>();
            _mockUow = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<ConfirmAllScheduledOrdersCommandHandler>>();

            var tomorrow = new DateOnly(2026, 7, 13);
            _mockTime.Setup(t => t.TomorrowIst).Returns(tomorrow);
            _mockTime.Setup(t => t.TodayIst).Returns(new DateOnly(2026, 7, 12));
            _mockTime.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 18, 30, 0, DateTimeKind.Utc));
            _mockTime.Setup(t => t.ToIst(It.IsAny<DateTime>())).Returns(new DateTime(2026, 7, 13, 0, 0, 0));

            _mockUow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());
        }

        [Fact]
        public async Task Handle_NoPendingOrders_ReturnsZeroCounts()
        {
            // Arrange
            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(It.IsAny<DateOnly>()))
                .ReturnsAsync(new List<ScheduledOrder>());

            var handler = new ConfirmAllScheduledOrdersCommandHandler(
                _mockScheduledOrderRepo.Object,
                _mockUserRepo.Object,
                _mockOrderRepo.Object,
                _mockWalletTxRepo.Object,
                _mockAddressRepo.Object,
                _mockWalletService.Object,
                _mockOrderService.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockLogger.Object);

            // Act
            var result = await handler.Handle(new ConfirmAllScheduledOrdersCommand(new DateOnly(2026, 7, 13)), CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.OrdersConfirmed);
            Assert.Equal(0, result.OrdersFailed);
        }

        [Fact]
        public async Task Handle_ValidPendingOrders_BatchProcessesAndReturnsResponse()
        {
            // Arrange
            var targetDate = new DateOnly(2026, 7, 13);
            var authGuid = Guid.NewGuid();
            var orderSuccess = new ScheduledOrder
            {
                ScheduledOrderId = 10,
                UserId = 1,
                AuthId = authGuid,
                DeliveryAddressId = 100,
                ScheduledFor = targetDate,
                TotalPrice = 150m,
                OrderStatus = ScheduledOrderStatus.Scheduled,
                MealName = "Healthy Oatmeal"
            };

            var scheduledOrders = new List<ScheduledOrder> { orderSuccess };

            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(targetDate))
                .ReturnsAsync(scheduledOrders);

            _mockUserRepo.Setup(r => r.GetByAuthIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>
                {
                    new User 
                    { 
                        UserId = 1, 
                        AccountStatus = AccountStatus.Active, 
                        Email = "user1@example.com",
                        AuthMapping = new UserAuthMapping { AuthId = authGuid, UserId = 1 }
                    }
                });

            _mockAddressRepo.Setup(r => r.GetByIdsWithDetailsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<UserAddress>
                {
                    new UserAddress 
                    { 
                        Id = 100, 
                        UserId = 1, 
                        FlatNumber = "101", 
                        ServiceableLocationId = 5,
                        ServiceableLocation = new ServiceableLocation { Id = 5, Area = "Downtown", IsActive = true }
                    }
                });

            _mockOrderRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Order>());

            _mockWalletTxRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, WalletTransaction>());

            _mockWalletService.Setup(w => w.AtomicDebitAsync(1, 150m, It.IsAny<string>(), 10))
                .ReturnsAsync((true, 999L));

            _mockOrderService.Setup(o => o.ConfirmScheduledOrderAsync(orderSuccess, It.IsAny<Order?>()))
                .ReturnsAsync(1001);

            _mockScheduledOrderRepo.Setup(r => r.MarkAsProcessedAsync(10, 1001, It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);

            var handler = new ConfirmAllScheduledOrdersCommandHandler(
                _mockScheduledOrderRepo.Object,
                _mockUserRepo.Object,
                _mockOrderRepo.Object,
                _mockWalletTxRepo.Object,
                _mockAddressRepo.Object,
                _mockWalletService.Object,
                _mockOrderService.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockLogger.Object);

            // Act
            var result = await handler.Handle(new ConfirmAllScheduledOrdersCommand(targetDate), CancellationToken.None);

            // Assert
            Assert.Equal(1, result.OrdersConfirmed);
            Assert.Equal(0, result.OrdersFailed);
            _mockOrderService.Verify(o => o.ConfirmScheduledOrderAsync(orderSuccess, It.IsAny<Order?>()), Times.Once);
            _mockScheduledOrderRepo.Verify(r => r.MarkAsProcessedAsync(10, 1001, It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task Handle_InsufficientBalance_MarksOrderAsFailed_AndLogsAttempt()
        {
            // Arrange
            var targetDate = new DateOnly(2026, 7, 13);
            var authGuid = Guid.NewGuid();
            var orderFail = new ScheduledOrder
            {
                ScheduledOrderId = 11, UserId = 1, AuthId = authGuid, DeliveryAddressId = 100,
                ScheduledFor = targetDate, TotalPrice = 150m, OrderStatus = ScheduledOrderStatus.Scheduled
            };

            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(targetDate))
                .ReturnsAsync(new List<ScheduledOrder> { orderFail });
            _mockUserRepo.Setup(r => r.GetByAuthIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User> { new User { UserId = 1, AccountStatus = AccountStatus.Active, AuthMapping = new UserAuthMapping { AuthId = authGuid, UserId = 1 } } });
            _mockAddressRepo.Setup(r => r.GetByIdsWithDetailsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<UserAddress> { new UserAddress { Id = 100, UserId = 1, ServiceableLocation = new ServiceableLocation { IsActive = true } } });
            _mockOrderRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Order>());
            _mockWalletTxRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, WalletTransaction>());

            // Return false for AtomicDebitAsync to simulate insufficient balance
            _mockWalletService.Setup(w => w.AtomicDebitAsync(1, 150m, It.IsAny<string>(), 11))
                .ReturnsAsync((false, 0L));

            var handler = new ConfirmAllScheduledOrdersCommandHandler(_mockScheduledOrderRepo.Object, _mockUserRepo.Object, _mockOrderRepo.Object, _mockWalletTxRepo.Object, _mockAddressRepo.Object, _mockWalletService.Object, _mockOrderService.Object, _mockTime.Object, _mockUow.Object, _mockLogger.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(new ConfirmAllScheduledOrdersCommand(targetDate), CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(act);
            _mockScheduledOrderRepo.Verify(r => r.MarkAsAsync(11, "Failed"), Times.Once);
        }

        [Fact]
        public async Task Handle_AlreadyProcessedOrder_IsSkipped_NoDoubleCharge()
        {
            // Arrange
            var targetDate = new DateOnly(2026, 7, 13);
            var orderAlreadyProcessed = new ScheduledOrder
            {
                ScheduledOrderId = 12, UserId = 1, OrderStatus = ScheduledOrderStatus.Processed
            };

            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(targetDate))
                .ReturnsAsync(new List<ScheduledOrder> { orderAlreadyProcessed });

            var handler = new ConfirmAllScheduledOrdersCommandHandler(_mockScheduledOrderRepo.Object, _mockUserRepo.Object, _mockOrderRepo.Object, _mockWalletTxRepo.Object, _mockAddressRepo.Object, _mockWalletService.Object, _mockOrderService.Object, _mockTime.Object, _mockUow.Object, _mockLogger.Object);

            // Act
            var result = await handler.Handle(new ConfirmAllScheduledOrdersCommand(targetDate), CancellationToken.None);

            // Assert
            Assert.Equal(0, result.OrdersConfirmed);
            Assert.Equal(0, result.OrdersFailed);
            Assert.Equal(1, result.OrdersAlreadyConfirmed);
            _mockWalletService.Verify(w => w.AtomicDebitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Handle_MissingDeliveryAddress_MarksOrderAsFailed()
        {
            // Arrange
            var targetDate = new DateOnly(2026, 7, 13);
            var authGuid = Guid.NewGuid();
            var orderNoAddress = new ScheduledOrder
            {
                ScheduledOrderId = 13, UserId = 1, AuthId = authGuid, DeliveryAddressId = 999, // Missing address
                ScheduledFor = targetDate, TotalPrice = 150m, OrderStatus = ScheduledOrderStatus.Scheduled
            };

            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(targetDate))
                .ReturnsAsync(new List<ScheduledOrder> { orderNoAddress });
            _mockUserRepo.Setup(r => r.GetByAuthIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User> { new User { UserId = 1, AccountStatus = AccountStatus.Active, AuthMapping = new UserAuthMapping { AuthId = authGuid, UserId = 1 } } });
            
            // Return empty list for addresses
            _mockAddressRepo.Setup(r => r.GetByIdsWithDetailsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<UserAddress>());
                
            _mockOrderRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Order>());

            var handler = new ConfirmAllScheduledOrdersCommandHandler(_mockScheduledOrderRepo.Object, _mockUserRepo.Object, _mockOrderRepo.Object, _mockWalletTxRepo.Object, _mockAddressRepo.Object, _mockWalletService.Object, _mockOrderService.Object, _mockTime.Object, _mockUow.Object, _mockLogger.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(new ConfirmAllScheduledOrdersCommand(targetDate), CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(act);
            _mockScheduledOrderRepo.Verify(r => r.MarkAsAsync(13, "Failed"), Times.Once);
        }

        [Fact]
        public async Task Handle_MixedOrders_SomePass_SomeFail_AllProcessed()
        {
            // Arrange
            var targetDate = new DateOnly(2026, 7, 13);
            var authGuid = Guid.NewGuid();
            var orderSuccess = new ScheduledOrder { ScheduledOrderId = 14, UserId = 1, AuthId = authGuid, DeliveryAddressId = 100, ScheduledFor = targetDate, TotalPrice = 150m, OrderStatus = ScheduledOrderStatus.Scheduled, MealName = "Meal 1" };
            var orderFailWallet = new ScheduledOrder { ScheduledOrderId = 15, UserId = 1, AuthId = authGuid, DeliveryAddressId = 100, ScheduledFor = targetDate, TotalPrice = 200m, OrderStatus = ScheduledOrderStatus.Scheduled, MealName = "Meal 2" };
            var orderFailAddress = new ScheduledOrder { ScheduledOrderId = 16, UserId = 1, AuthId = authGuid, DeliveryAddressId = 999, ScheduledFor = targetDate, TotalPrice = 150m, OrderStatus = ScheduledOrderStatus.Scheduled, MealName = "Meal 3" };
            
            _mockScheduledOrderRepo.Setup(r => r.GetScheduledOrdersForDateAsync(targetDate))
                .ReturnsAsync(new List<ScheduledOrder> { orderSuccess, orderFailWallet, orderFailAddress });
            
            _mockUserRepo.Setup(r => r.GetByAuthIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User> { new User { UserId = 1, AccountStatus = AccountStatus.Active, AuthMapping = new UserAuthMapping { AuthId = authGuid, UserId = 1 } } });
                
            _mockAddressRepo.Setup(r => r.GetByIdsWithDetailsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<UserAddress> { new UserAddress { Id = 100, UserId = 1, ServiceableLocation = new ServiceableLocation { IsActive = true } } });
                
            _mockOrderRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Order>());
            _mockWalletTxRepo.Setup(r => r.GetByScheduledOrderIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, WalletTransaction>());

            _mockWalletService.Setup(w => w.AtomicDebitAsync(1, 150m, It.IsAny<string>(), 14))
                .ReturnsAsync((true, 999L));
            _mockWalletService.Setup(w => w.AtomicDebitAsync(1, 200m, It.IsAny<string>(), 15))
                .ReturnsAsync((false, 0L)); // Fails
                
            _mockOrderService.Setup(o => o.ConfirmScheduledOrderAsync(orderSuccess, It.IsAny<Order?>()))
                .ReturnsAsync(1001);

            var handler = new ConfirmAllScheduledOrdersCommandHandler(_mockScheduledOrderRepo.Object, _mockUserRepo.Object, _mockOrderRepo.Object, _mockWalletTxRepo.Object, _mockAddressRepo.Object, _mockWalletService.Object, _mockOrderService.Object, _mockTime.Object, _mockUow.Object, _mockLogger.Object);

            // Act
            var result = await handler.Handle(new ConfirmAllScheduledOrdersCommand(targetDate), CancellationToken.None);

            // Assert
            Assert.Equal(3, result.OrdersFound);
            Assert.Equal(1, result.OrdersConfirmed);
            Assert.Equal(2, result.OrdersFailed); // Wallet fail + Address fail
        }
    }
}
