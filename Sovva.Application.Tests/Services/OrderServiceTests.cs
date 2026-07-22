using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Features.Orders.Commands.ConfirmScheduledOrder;
using Sovva.Application.Features.Orders.Commands.CreateOrder;
using Sovva.Application.Features.Orders.Commands.CreateOrderFromMealBuilder;
using Sovva.Application.Features.Orders.Commands.RateOrder;
using Sovva.Application.Features.Orders.Commands.Reorder;
using Sovva.Application.Features.Orders.Queries.GetAllOrderHistory;
using Sovva.Application.Features.Orders.Queries.GetAllOrderHistoryWithDetails;
using Sovva.Application.Features.Orders.Queries.GetByScheduledOrderId;
using Sovva.Application.Features.Orders.Queries.GetOrderById;
using Sovva.Application.Features.Orders.Queries.GetOrdersByStatus;
using Sovva.Application.Features.Orders.Queries.GetUserOrders;
using Sovva.Application.Features.Orders.Queries.GetUserOrdersWithDetails;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Application.Tests.Helpers;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IMealService> _mealServiceMock = new();
    private readonly Mock<IWalletTransactionService> _walletServiceMock = new();
    private readonly Mock<IUserMealService> _userMealServiceMock = new();
    private readonly Mock<IUserMealIngredientService> _userMealIngredientServiceMock = new();
    private readonly Mock<IUserAddressRepository> _addressRepoMock = new();
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IAppTimeProvider> _timeMock = new();
    private readonly Mock<ILogger<ReorderCommandHandler>> _reorderLoggerMock = new();
    private readonly TestMediatRSender _sender = new();
    private readonly IOrderService _orderService;

    public OrderServiceTests()
    {
        _timeMock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc));
        _timeMock.Setup(t => t.ToUtc(It.IsAny<DateTime>())).Returns((DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc));
        _timeMock.Setup(t => t.TomorrowIst).Returns(new DateOnly(2026, 7, 13));

        _uowMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async func => await func());

        // Register all Command Handlers
        _sender.Register(new CreateOrderCommandHandler(_orderRepoMock.Object, _uowMock.Object, _timeMock.Object));
        _sender.Register(new CreateOrderFromMealBuilderCommandHandler(
            _mealServiceMock.Object,
            _walletServiceMock.Object,
            _userMealServiceMock.Object,
            _userMealIngredientServiceMock.Object,
            _addressRepoMock.Object,
            _orderRepoMock.Object,
            _uowMock.Object,
            _timeMock.Object));
        _sender.Register(new ConfirmScheduledOrderCommandHandler(_orderRepoMock.Object, _uowMock.Object, _timeMock.Object));
        _sender.Register(new RateOrderCommandHandler(_orderRepoMock.Object, _uowMock.Object, _timeMock.Object));
        _sender.Register(new ReorderCommandHandler(_orderRepoMock.Object, _walletServiceMock.Object, _uowMock.Object, _timeMock.Object, _reorderLoggerMock.Object));

        // Register all Query Handlers
        _sender.Register(new GetOrderByIdQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetByScheduledOrderIdQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetAllOrderHistoryQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetOrdersByStatusQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetUserOrdersQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetUserOrdersWithDetailsQueryHandler(_orderRepoMock.Object));
        _sender.Register(new GetAllOrderHistoryWithDetailsQueryHandler(_orderRepoMock.Object));

        _orderService = new OrderService(_sender);
    }

    [Fact]
    public async Task CreateOrderFromMealBuilderAsync_WithValidPrimaryAddress_CreatesOrderSuccessfully()
    {
        // Arrange
        var userId = 101;
        var dto = new CreateOrderFromMealBuilderDto
        {
            MealId = 1,
            MealName = "Oats Bowl",
            SelectedIngredients = new List<SelectedIngredientDto>
            {
                new SelectedIngredientDto { IngredientId = 10, Quantity = 2 }
            }
        };

        _mealServiceMock.Setup(m => m.GetMealByIdAsync(1))
            .ReturnsAsync(new MealDto { MealId = 1, MealName = "Oats Bowl" });

        _addressRepoMock.Setup(a => a.GetPrimaryAddressByUserIdAsync(userId))
            .ReturnsAsync(new UserAddress
            {
                Id = 5,
                UserId = userId,
                ServiceableLocation = new ServiceableLocation { IsActive = true, Area = "Downtown" }
            });

        _mealServiceMock.Setup(m => m.CalculateMealPriceAsync(It.IsAny<MealPriceCalculationDto>()))
            .ReturnsAsync(new MealPriceResponseDto
            {
                MealName = "Oats Bowl",
                TotalPrice = 150m,
                IngredientBreakdown = new List<IngredientBreakdownDto>()
            });

        _walletServiceMock.Setup(w => w.GetUserBalanceAsync(userId)).ReturnsAsync(300m);
        _walletServiceMock.Setup(w => w.HasSufficientBalanceAsync(userId, 150m)).ReturnsAsync(true);
        _userMealServiceMock.Setup(u => u.CreateUserMealAsync(It.IsAny<CreateUserMealDto>(), userId)).ReturnsAsync(500);
        _walletServiceMock.Setup(w => w.DebitWalletAsync(userId, 150m, It.IsAny<string>()))
            .ReturnsAsync(new WalletTransactionDto { TransactionId = 999 });

        // Act
        var result = await _orderService.CreateOrderFromMealBuilderAsync(dto, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Confirmed", result.OrderStatus);
        Assert.Equal(150m, result.TotalPrice);
        _orderRepoMock.Verify(o => o.AddAsync(It.IsAny<Order>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeast(2));
    }

    [Fact]
    public async Task CreateOrderFromMealBuilderAsync_WhenNoPrimaryAddress_ThrowsAddressNotFoundException()
    {
        // Arrange
        var userId = 101;
        var dto = new CreateOrderFromMealBuilderDto
        {
            MealId = 1,
            SelectedIngredients = new List<SelectedIngredientDto> { new() { IngredientId = 10, Quantity = 1 } }
        };

        _mealServiceMock.Setup(m => m.GetMealByIdAsync(1)).ReturnsAsync(new MealDto { MealId = 1 });
        _addressRepoMock.Setup(a => a.GetPrimaryAddressByUserIdAsync(userId)).ReturnsAsync((UserAddress?)null);

        // Act & Assert
        await Assert.ThrowsAsync<AddressNotFoundException>(() => _orderService.CreateOrderFromMealBuilderAsync(dto, userId));
    }

    [Fact]
    public async Task CreateOrderFromMealBuilderAsync_WhenInsufficientBalance_ThrowsInsufficientBalanceException()
    {
        // Arrange
        var userId = 101;
        var dto = new CreateOrderFromMealBuilderDto
        {
            MealId = 1,
            SelectedIngredients = new List<SelectedIngredientDto> { new() { IngredientId = 10, Quantity = 1 } }
        };

        _mealServiceMock.Setup(m => m.GetMealByIdAsync(1)).ReturnsAsync(new MealDto { MealId = 1 });
        _addressRepoMock.Setup(a => a.GetPrimaryAddressByUserIdAsync(userId)).ReturnsAsync(new UserAddress
        {
            Id = 5,
            UserId = userId,
            ServiceableLocation = new ServiceableLocation { IsActive = true }
        });

        _mealServiceMock.Setup(m => m.CalculateMealPriceAsync(It.IsAny<MealPriceCalculationDto>()))
            .ReturnsAsync(new MealPriceResponseDto { TotalPrice = 250m });

        _walletServiceMock.Setup(w => w.GetUserBalanceAsync(userId)).ReturnsAsync(50m);
        _walletServiceMock.Setup(w => w.HasSufficientBalanceAsync(userId, 250m)).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientBalanceException>(() => _orderService.CreateOrderFromMealBuilderAsync(dto, userId));
    }

    [Fact]
    public async Task ConfirmScheduledOrderAsync_WhenOrderAlreadyExists_ReturnsExistingOrderIdWithoutDuplicateInsert()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder { ScheduledOrderId = 77, UserId = 101, TotalPrice = 120m };
        var existingOrder = new Order { OrderId = 888, ScheduledOrderId = 77 };

        _orderRepoMock.Setup(o => o.GetByScheduledOrderIdAsync(77)).ReturnsAsync(existingOrder);

        // Act
        var orderId = await _orderService.ConfirmScheduledOrderAsync(scheduledOrder);

        // Assert
        Assert.Equal(888, orderId);
        _orderRepoMock.Verify(o => o.AddAsync(It.IsAny<Order>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmScheduledOrderAsync_WhenDeliveryAddressIdNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder { ScheduledOrderId = 77, UserId = 101, DeliveryAddressId = null };
        _orderRepoMock.Setup(o => o.GetByScheduledOrderIdAsync(77)).ReturnsAsync((Order?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _orderService.ConfirmScheduledOrderAsync(scheduledOrder));
    }

    [Fact]
    public async Task ConfirmScheduledOrderAsync_WhenNewOrder_CreatesAndReturnsOrderId()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder
        {
            ScheduledOrderId = 77,
            UserId = 101,
            DeliveryAddressId = 15,
            TotalPrice = 180m,
            ScheduledFor = new DateOnly(2026, 7, 13)
        };

        _orderRepoMock.Setup(o => o.GetByScheduledOrderIdAsync(77)).ReturnsAsync((Order?)null);
        _orderRepoMock.Setup(o => o.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => o.OrderId = 999)
            .Returns(Task.CompletedTask);

        // Act
        var orderId = await _orderService.ConfirmScheduledOrderAsync(scheduledOrder);

        // Assert
        Assert.Equal(999, orderId);
        _orderRepoMock.Verify(o => o.AddAsync(It.IsAny<Order>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RateOrderAsync_WhenOrderPrepared_UpdatesRatingAndReview()
    {
        // Arrange
        var order = new Order { OrderId = 10, UserId = 101, IsPrepared = true };
        _orderRepoMock.Setup(o => o.GetByIdAsync(10L)).ReturnsAsync(order);

        // Act
        var success = await _orderService.RateOrderAsync(10L, 101, 5, "Great meal!");

        // Assert
        Assert.True(success);
        Assert.Equal(5, order.Rating);
        Assert.Equal("Great meal!", order.Review);
        _orderRepoMock.Verify(o => o.Update(order), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RateOrderAsync_WhenOrderNotPrepared_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = new Order { OrderId = 10, UserId = 101, IsPrepared = false };
        _orderRepoMock.Setup(o => o.GetByIdAsync(10L)).ReturnsAsync(order);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _orderService.RateOrderAsync(10L, 101, 5, null));
    }

    [Fact]
    public async Task ReorderAsync_WhenRecentDuplicateWithin30Seconds_ReturnsExistingRecentOrder()
    {
        // Arrange
        var pastOrder = new Order { OrderId = 100, UserId = 101, UserMealId = 50, TotalPrice = 140m };
        var recentDuplicate = new Order { OrderId = 200, UserId = 101, UserMealId = 50, TotalPrice = 140m, OrderStatus = OrderStatus.Confirmed };

        _orderRepoMock.Setup(o => o.GetByIdAsync(100L)).ReturnsAsync(pastOrder);
        _walletServiceMock.Setup(w => w.GetUserBalanceAsync(101)).ReturnsAsync(300m);
        _orderRepoMock.Setup(o => o.GetRecentOrderByUserMealIdAsync(50, 101, 30)).ReturnsAsync(recentDuplicate);

        // Act
        var result = await _orderService.ReorderAsync(100L, 101);

        // Assert
        Assert.Equal(200, result.OrderId);
        Assert.Contains("Reorder (Duplicate Prevention)", result.MealName);
        _walletServiceMock.Verify(w => w.DebitWalletAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReorderAsync_WhenValidAndSufficientBalance_DebitsAndCreatesNewOrder()
    {
        // Arrange
        var pastOrder = new Order { OrderId = 100, UserId = 101, UserMealId = 50, TotalPrice = 140m, DeliveryAddressId = 5 };
        _orderRepoMock.Setup(o => o.GetByIdAsync(100L)).ReturnsAsync(pastOrder);
        _walletServiceMock.Setup(w => w.GetUserBalanceAsync(101)).ReturnsAsync(300m);
        _orderRepoMock.Setup(o => o.GetRecentOrderByUserMealIdAsync(50, 101, 30)).ReturnsAsync((Order?)null);

        _orderRepoMock.Setup(o => o.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => o.OrderId = 300)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _orderService.ReorderAsync(100L, 101);

        // Assert
        Assert.Equal(300, result.OrderId);
        Assert.Equal("Confirmed", result.OrderStatus);
        _walletServiceMock.Verify(w => w.DebitWalletAsync(101, 140m, "Reorder of past order #100"), Times.Once);
        _orderRepoMock.Verify(o => o.AddAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenFound_ReturnsDto()
    {
        // Arrange
        var order = new Order { OrderId = 10, UserId = 101, TotalPrice = 99m, OrderStatus = OrderStatus.Confirmed };
        _orderRepoMock.Setup(o => o.GetByIdAsync(10L)).ReturnsAsync(order);

        // Act
        var result = await _orderService.GetOrderByIdAsync(10L);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.OrderId);
        Assert.Equal(99m, result.TotalPrice);
    }

    [Fact]
    public async Task GetAllOrderHistoryAsync_ReturnsPagedOrders()
    {
        // Arrange
        var orders = new List<Order> { new Order { OrderId = 1, TotalPrice = 100m }, new Order { OrderId = 2, TotalPrice = 200m } };
        _orderRepoMock.Setup(o => o.GetAllAsync(1, 50)).ReturnsAsync(orders);
        _orderRepoMock.Setup(o => o.CountAsync()).ReturnsAsync(2);

        // Act
        var result = await _orderService.GetAllOrderHistoryAsync(1, 50);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }
}
