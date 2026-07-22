using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.ScheduledOrders.Commands.RetryFailedOrders
{
    public class RetryFailedOrdersCommandHandler : IRequestHandler<RetryFailedOrdersCommand, ProcessOrdersResponseDto>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IOrderService _orderService;
        private readonly IAppTimeProvider _time;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RetryFailedOrdersCommandHandler> _logger;

        public RetryFailedOrdersCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IOrderRepository orderRepository,
            IWalletTransactionRepository walletTransactionRepository,
            IUserAddressRepository userAddressRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ILogger<RetryFailedOrdersCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _orderRepository = orderRepository;
            _walletTransactionRepository = walletTransactionRepository;
            _userAddressRepository = userAddressRepository;
            _walletService = walletService;
            _orderService = orderService;
            _time = time;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ProcessOrdersResponseDto> Handle(RetryFailedOrdersCommand request, CancellationToken cancellationToken)
        {
            var targetDate = request.TargetDate;
            var correlationId = request.CorrelationId;

            var cid = correlationId ?? Guid.NewGuid().ToString("N")[..8];
            using var scope = _logger.BeginScope(new Dictionary<string, object> { { "CorrelationId", cid } });

            var failedOrders = await _scheduledOrderRepository.GetFailedScheduledOrdersAsync(targetDate);

            if (failedOrders.Count == 0)
            {
                return new ProcessOrdersResponseDto
                {
                    Success = true,
                    Message = "No failed orders found to retry.",
                    DeliveryDate = targetDate?.ToDateTime(TimeOnly.MinValue) ?? _time.UtcNow,
                    OrdersFound = 0,
                    OrdersPending = 0,
                    OrdersAlreadyConfirmed = 0,
                    OrdersConfirmed = 0,
                    OrdersFailed = 0,
                    Timestamp = _time.UtcNow
                };
            }

            var authIds = failedOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            var scheduledOrderIds = failedOrders.Select(o => o.ScheduledOrderId).ToList();
            var existingOrdersByScheduledOrderId = await _orderRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);
            var existingTransactionsByScheduledOrderId = await _walletTransactionRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);

            var addressIds = failedOrders
                .Where(o => o.DeliveryAddressId.HasValue)
                .Select(o => o.DeliveryAddressId!.Value)
                .Distinct().ToList();
            var addressesMap = (await _userAddressRepository.GetByIdsWithDetailsAsync(addressIds))
                .ToDictionary(a => a.Id);

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in failedOrders)
            {
                var success = await ScheduledOrderHelper.ProcessSingleScheduledOrderAsync(
                    scheduledOrder,
                    usersByAuthId,
                    existingOrdersByScheduledOrderId,
                    existingTransactionsByScheduledOrderId,
                    addressesMap,
                    _scheduledOrderRepository,
                    _walletService,
                    _orderService,
                    _time,
                    _unitOfWork,
                    _logger);

                if (success) confirmedCount++;
                else failedCount++;
            }

            return new ProcessOrdersResponseDto
            {
                Success = true,
                Message = $"Retry complete. {confirmedCount} succeeded, {failedCount} failed.",
                DeliveryDate = targetDate?.ToDateTime(TimeOnly.MinValue) ?? _time.UtcNow,
                OrdersFound = failedOrders.Count,
                OrdersPending = failedOrders.Count,
                OrdersAlreadyConfirmed = 0,
                OrdersConfirmed = confirmedCount,
                OrdersFailed = failedCount,
                Timestamp = _time.UtcNow
            };
        }
    }
}
