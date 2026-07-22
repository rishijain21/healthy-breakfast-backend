using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Features.ScheduledOrders.Commands.CancelScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.ConfirmAllScheduledOrders;
using Sovva.Application.Features.ScheduledOrders.Commands.CreateScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.DuplicateScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.ModifyScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Queries.CheckWalletBalance;
using Sovva.Application.Features.ScheduledOrders.Queries.GetScheduledOrdersForDate;
using Sovva.Application.Features.ScheduledOrders.Queries.GetTimeUntilMidnightMinutes;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class ScheduledOrderCQRSHandlerTests
    {
        private readonly Mock<IScheduledOrderRepository> _scheduledOrderRepoMock = new();
        private readonly Mock<IUserRepository> _userRepoMock = new();
        private readonly Mock<IIngredientRepository> _ingredientRepoMock = new();
        private readonly Mock<IWalletTransactionService> _walletServiceMock = new();
        private readonly Mock<IOrderService> _orderServiceMock = new();
        private readonly Mock<IAppTimeProvider> _timeProviderMock = new();
        private readonly Mock<IUserAddressRepository> _userAddressRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<IMealRepository> _mealRepoMock = new();
        private readonly Mock<IOrderRepository> _orderRepoMock = new();
        private readonly Mock<IWalletTransactionRepository> _walletTxRepoMock = new();

        public ScheduledOrderCQRSHandlerTests()
        {
            var today = new DateOnly(2026, 7, 12);
            _timeProviderMock.Setup(t => t.TodayIst).Returns(today);
            _timeProviderMock.Setup(t => t.TomorrowIst).Returns(today.AddDays(1));
            _timeProviderMock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc));
            _timeProviderMock.Setup(t => t.ToIst(It.IsAny<DateTime>())).Returns((DateTime d) => d.AddHours(5).AddMinutes(30));

            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns((Func<Task> action) => action());
        }

        [Fact]
        public async Task CreateScheduledOrderCommandHandler_ShouldCreateOrder_WhenBalanceAndAddressAreValid()
        {
            // Arrange
            var authId = Guid.NewGuid();
            var user = new User { UserId = 1, Name = "Rishi", AuthMapping = new UserAuthMapping { AuthId = authId } };
            _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var address = new UserAddress { Id = 10, UserId = 1, ServiceableLocation = new ServiceableLocation { IsActive = true, Area = "Powai", City = "Mumbai" } };
            _userAddressRepoMock.Setup(r => r.GetPrimaryAddressByUserIdAsync(1)).ReturnsAsync(address);

            var ing = new Ingredient { IngredientId = 5, Price = 50m, IngredientName = "Almond Milk" };
            _ingredientRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, Ingredient> { { 5, ing } });

            _mealRepoMock.Setup(m => m.GetByIdAsync(2)).ReturnsAsync(new Meal { MealId = 2, BasePrice = 100m, MealName = "Custom Bowl" });
            _walletServiceMock.Setup(w => w.HasSufficientBalanceAsync(1, 150m)).ReturnsAsync(true);

            _scheduledOrderRepoMock.Setup(r => r.CreateAsync(It.IsAny<ScheduledOrder>()))
                .ReturnsAsync((ScheduledOrder o) => { o.ScheduledOrderId = 1001; return o; });

            var handler = new CreateScheduledOrderCommandHandler(
                _scheduledOrderRepoMock.Object, _userRepoMock.Object, _ingredientRepoMock.Object,
                _walletServiceMock.Object, _timeProviderMock.Object, _userAddressRepoMock.Object,
                _mealRepoMock.Object, new Mock<ILogger<CreateScheduledOrderCommandHandler>>().Object);

            var dto = new CreateScheduledOrderDto
            {
                MealId = 2,
                SelectedIngredients = new List<ScheduledOrderIngredientDto> { new ScheduledOrderIngredientDto { IngredientId = 5, Quantity = 1 } }
            };

            // Act
            var result = await handler.Handle(new CreateScheduledOrderCommand(1, authId, dto), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ScheduledOrderId.Should().Be(1001);
            result.TotalPrice.Should().Be(150m);
            _scheduledOrderRepoMock.Verify(r => r.CreateAsync(It.Is<ScheduledOrder>(o => o.TotalPrice == 150m && o.UserId == 1)), Times.Once);
        }

        [Fact]
        public async Task CancelScheduledOrderCommandHandler_ShouldDeleteOrder_WhenUserIsOwner()
        {
            // Arrange
            var authId = Guid.NewGuid();
            var order = new ScheduledOrder { ScheduledOrderId = 500, UserId = 3, AuthId = authId, CanModify = true, OrderStatus = ScheduledOrderStatus.Scheduled };
            _scheduledOrderRepoMock.Setup(r => r.GetByIdAndAuthIdAsync(500, authId)).ReturnsAsync(order);
            _scheduledOrderRepoMock.Setup(r => r.DeleteAsync(500)).Returns(Task.CompletedTask);

            var handler = new CancelScheduledOrderCommandHandler(_scheduledOrderRepoMock.Object, new Mock<ILogger<CancelScheduledOrderCommandHandler>>().Object);

            // Act
            var result = await handler.Handle(new CancelScheduledOrderCommand(3, authId, 500), CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            _scheduledOrderRepoMock.Verify(r => r.DeleteAsync(500), Times.Once);
        }

        [Fact]
        public async Task CheckWalletBalanceQueryHandler_ShouldReturnBalanceCheckResult()
        {
            // Arrange
            _walletServiceMock.Setup(w => w.HasSufficientBalanceAsync(10, 250m)).ReturnsAsync(true);
            var handler = new CheckWalletBalanceQueryHandler(_walletServiceMock.Object);

            // Act
            var result = await handler.Handle(new CheckWalletBalanceQuery(10, 250m), CancellationToken.None);

            // Assert
            result.Should().BeTrue();
        }
    }
}
