using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Orders.Commands.Reorder;

public class ReorderCommandHandler : IRequestHandler<ReorderCommand, OrderCreationResponseDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IWalletTransactionService _walletService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppTimeProvider _time;
    private readonly ILogger<ReorderCommandHandler> _logger;

    public ReorderCommandHandler(
        IOrderRepository orderRepository,
        IWalletTransactionService walletService,
        IUnitOfWork unitOfWork,
        IAppTimeProvider time,
        ILogger<ReorderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _walletService = walletService;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    public async Task<OrderCreationResponseDto> Handle(ReorderCommand request, CancellationToken cancellationToken)
    {
        var pastOrder = await _orderRepository.GetByIdAsync(request.OrderId);
        if (pastOrder == null || pastOrder.UserId != request.UserId)
            throw new InvalidOperationException("Order not found or access denied.");

        if (pastOrder.UserMealId == null)
            throw new InvalidOperationException("Cannot reorder this meal as its components are no longer available.");

        var price = pastOrder.TotalPrice;
        var currentBalance = await _walletService.GetUserBalanceAsync(request.UserId);

        var recentOrder = await _orderRepository.GetRecentOrderByUserMealIdAsync(pastOrder.UserMealId.Value, request.UserId, 30);
        if (recentOrder != null)
        {
            _logger.LogInformation("Reorder duplicate detected for User {UserId}, returning existing Order {OrderId}", request.UserId, recentOrder.OrderId);
            return new OrderCreationResponseDto
            {
                OrderId = recentOrder.OrderId,
                MealName = "Reorder (Duplicate Prevention)",
                OrderStatus = recentOrder.OrderStatus.ToString(),
                UserMealId = recentOrder.UserMealId ?? 0,
                TotalPrice = recentOrder.TotalPrice,
                WalletBalanceBefore = currentBalance,
                WalletBalanceAfter = currentBalance,
                OrderDate = recentOrder.OrderDate,
                ScheduledFor = recentOrder.ScheduledFor
            };
        }

        if (currentBalance < price)
            throw new InsufficientBalanceException(price, currentBalance);

        OrderCreationResponseDto response = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var transaction = await _walletService.DebitWalletAsync(
                request.UserId,
                price,
                $"Reorder of past order #{request.OrderId}"
            );

            var tomorrowIst = _time.TomorrowIst;
            var localDeliveryTime = tomorrowIst.ToDateTime(new TimeOnly(7, 0));
            var scheduledDeliveryTime = _time.ToUtc(localDeliveryTime);

            var newOrder = new Order
            {
                UserId = request.UserId,
                UserMealId = pastOrder.UserMealId,
                DeliveryAddressId = pastOrder.DeliveryAddressId,
                IsPrepared = false,
                OrderStatus = OrderStatus.Confirmed,
                TotalPrice = price,
                OrderDate = _time.UtcNow,
                ScheduledFor = scheduledDeliveryTime,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _orderRepository.AddAsync(newOrder);
            await _unitOfWork.SaveChangesAsync();

            var walletBalanceAfter = await _walletService.GetUserBalanceAsync(request.UserId);

            response = new OrderCreationResponseDto
            {
                OrderId = newOrder.OrderId,
                MealName = "Reorder",
                OrderStatus = "Confirmed",
                UserMealId = newOrder.UserMealId ?? 0,
                TotalPrice = price,
                WalletBalanceBefore = currentBalance,
                WalletBalanceAfter = walletBalanceAfter,
                OrderDate = newOrder.OrderDate,
                ScheduledFor = newOrder.ScheduledFor
            };
        });

        return response;
    }
}
