using System;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Orders.Commands.ConfirmScheduledOrder;
using Sovva.Application.Features.Orders.Commands.CreateOrder;
using Sovva.Application.Features.Orders.Commands.CreateOrderFromMealBuilder;
using Sovva.Application.Features.Orders.Commands.RateOrder;
using Sovva.Application.Features.Orders.Commands.Reorder;
using Sovva.Application.Features.Orders.Queries.GetAllOrderHistory;
using Sovva.Application.Features.Orders.Queries.GetAllOrderHistoryWithDetails;
using Sovva.Application.Features.Orders.Queries.GetByScheduledOrderId;
using Sovva.Application.Features.Orders.Queries.GetOrderById;
using Sovva.Application.Features.Orders.Queries.GetOrderDetailsById;
using Sovva.Application.Features.Orders.Queries.GetOrdersByStatus;
using Sovva.Application.Features.Orders.Queries.GetUserOrders;
using Sovva.Application.Features.Orders.Queries.GetUserOrdersWithDetails;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Services;

/// <summary>
/// CQRS Facade for Order Operations. Delegates 100% of calls to MediatR handlers
/// under Sovva.Application/Features/Orders/ while maintaining exact IOrderService compatibility.
/// </summary>
public class OrderService : IOrderService
{
    private readonly ISender _sender;

    public OrderService(ISender sender)
    {
        _sender = sender;
    }

    [Obsolete("Do not use. Relies on client-trusted TotalPrice. Use ConfirmScheduledOrderAsync or MealBuilder paths.")]
    public Task<long> CreateOrderAsync(CreateOrderDto dto, int userId)
        => _sender.Send(new CreateOrderCommand(dto, userId));

    public Task<OrderDto?> GetOrderByIdAsync(long id)
        => _sender.Send(new GetOrderByIdQuery(id));

    public Task<EnhancedOrderHistoryDto?> GetOrderDetailsByIdAsync(long id)
        => _sender.Send(new GetOrderDetailsByIdQuery(id));

    public Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId)
        => _sender.Send(new CreateOrderFromMealBuilderCommand(dto, userId, null));

    public Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId, int? deliveryAddressId)
        => _sender.Send(new CreateOrderFromMealBuilderCommand(dto, userId, deliveryAddressId));

    public Task<PagedResult<OrderDto>> GetUserOrdersAsync(int userId, int page = 1, int pageSize = 20)
        => _sender.Send(new GetUserOrdersQuery(userId, page, pageSize));

    public Task<PagedResult<OrderDto>> GetAllOrderHistoryAsync(int page = 1, int pageSize = 50)
        => _sender.Send(new GetAllOrderHistoryQuery(page, pageSize));

    public Task<PagedResult<OrderDto>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 50)
        => _sender.Send(new GetOrdersByStatusQuery(status, page, pageSize));

    public Task<PagedResult<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId, int page = 1, int pageSize = 20)
        => _sender.Send(new GetUserOrdersWithDetailsQuery(userId, page, pageSize));

    public Task<PagedResult<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync(int page = 1, int pageSize = 50)
        => _sender.Send(new GetAllOrderHistoryWithDetailsQuery(page, pageSize));

    public Task<int> ConfirmScheduledOrderAsync(ScheduledOrder scheduledOrder, Order? existingOrder = null)
        => _sender.Send(new ConfirmScheduledOrderCommand(scheduledOrder, existingOrder));

    public Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId)
        => _sender.Send(new GetByScheduledOrderIdQuery(scheduledOrderId));

    public Task<bool> RateOrderAsync(long orderId, int userId, int rating, string? review)
        => _sender.Send(new RateOrderCommand(orderId, userId, rating, review));

    public Task<OrderCreationResponseDto> ReorderAsync(long orderId, int userId)
        => _sender.Send(new ReorderCommand(orderId, userId));
}
