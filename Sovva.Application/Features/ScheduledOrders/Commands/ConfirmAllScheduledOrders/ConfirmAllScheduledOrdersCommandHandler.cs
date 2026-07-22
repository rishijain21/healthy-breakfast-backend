using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ConfirmAllScheduledOrders
{
    public class ConfirmAllScheduledOrdersCommandHandler : IRequestHandler<ConfirmAllScheduledOrdersCommand, ProcessOrdersResponseDto>
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
        private readonly ILogger<ConfirmAllScheduledOrdersCommandHandler> _logger;

        public ConfirmAllScheduledOrdersCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IOrderRepository orderRepository,
            IWalletTransactionRepository walletTransactionRepository,
            IUserAddressRepository userAddressRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ILogger<ConfirmAllScheduledOrdersCommandHandler> logger)
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

        public async Task<ProcessOrdersResponseDto> Handle(ConfirmAllScheduledOrdersCommand request, CancellationToken cancellationToken)
        {
            var deliveryDate = request.TargetDate ?? _time.TomorrowIst;
            var istNow = _time.ToIst(_time.UtcNow);

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("[MIDNIGHT JOB] Starting confirmation for Date: {Date} IST (System Today: {Today} IST)",
                deliveryDate, _time.TodayIst);

            _logger.LogInformation("[MIDNIGHT JOB] Started at {IstNow} IST", istNow.ToString("yyyy-MM-dd HH:mm:ss"));
            _logger.LogInformation("Confirming orders for delivery on: {DeliveryDate}", deliveryDate);
            _logger.LogInformation("UTC: {UtcNow} | IST: {IstNow}", _time.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), istNow.ToString("yyyy-MM-dd HH:mm:ss"));

            var scheduledOrders = await _scheduledOrderRepository.GetScheduledOrdersForDateAsync(deliveryDate);

            _logger.LogInformation("Found {TotalOrders} total orders for {DeliveryDate}", scheduledOrders.Count, deliveryDate);

            var pendingOrders = scheduledOrders
                .Where(o => o.OrderStatus == ScheduledOrderStatus.Scheduled
                         || o.OrderStatus == ScheduledOrderStatus.Processing
                         || o.OrderStatus == ScheduledOrderStatus.Failed)
                .ToList();

            _logger.LogInformation("{PendingCount} orders pending confirmation", pendingOrders.Count);

            if (pendingOrders.Count == 0)
            {
                var alreadyProcessed = scheduledOrders.Count(o => o.OrderStatus == ScheduledOrderStatus.Processed);
                return new ProcessOrdersResponseDto
                {
                    Success               = true,
                    Message               = $"No pending orders for {deliveryDate:yyyy-MM-dd}",
                    DeliveryDate          = deliveryDate.ToDateTime(TimeOnly.MinValue),
                    OrdersFound           = scheduledOrders.Count,
                    OrdersPending         = 0,
                    OrdersAlreadyConfirmed = alreadyProcessed,
                    OrdersConfirmed       = 0,
                    OrdersFailed          = 0,
                    Timestamp             = _time.UtcNow,
                    Note                  = "Safe to call multiple times — idempotent"
                };
            }

            var authIds = pendingOrders.Select(o => o.AuthId).Distinct().ToList();
            var users = await _userRepository.GetByAuthIdsAsync(authIds);
            var usersByAuthId = users
                .Where(u => u.AuthMapping != null)
                .ToDictionary(u => u.AuthMapping!.AuthId);

            var scheduledOrderIds = pendingOrders.Select(o => o.ScheduledOrderId).ToList();
            var existingOrdersByScheduledOrderId = await _orderRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);
            var existingTransactionsByScheduledOrderId = await _walletTransactionRepository.GetByScheduledOrderIdsAsync(scheduledOrderIds);

            var addressIds = pendingOrders
                .Where(o => o.DeliveryAddressId.HasValue)
                .Select(o => o.DeliveryAddressId!.Value)
                .Distinct().ToList();
            var addressesMap = (await _userAddressRepository.GetByIdsWithDetailsAsync(addressIds))
                .ToDictionary(a => a.Id);

            int confirmedCount = 0;
            int failedCount = 0;

            foreach (var scheduledOrder in pendingOrders)
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
            stopwatch.Stop();

            _logger.LogInformation(
                "[JOB-METRICS] {@Metrics}", new
                {
                    Job = "scheduled-order-confirmation",
                    Date = deliveryDate.ToString("yyyy-MM-dd"),
                    Found = scheduledOrders.Count,
                    Pending = pendingOrders.Count,
                    Confirmed = confirmedCount,
                    Failed = failedCount,
                    DurationMs = stopwatch.ElapsedMilliseconds
                });

            _logger.LogInformation("[MIDNIGHT JOB] Complete! Confirmed: {Confirmed}, Failed: {Failed}, Already processed: {AlreadyProcessed}",
                confirmedCount, failedCount, scheduledOrders.Count - pendingOrders.Count);

            if (failedCount > 0 && confirmedCount == 0 && pendingOrders.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[MIDNIGHT JOB] All {failedCount} orders failed to confirm. " +
                    $"Check logs for {deliveryDate:yyyy-MM-dd}. " +
                    $"Common causes: wallet balance, inactive delivery location, missing address.");
            }

            return new ProcessOrdersResponseDto
            {
                Success               = confirmedCount > 0 || failedCount == 0,
                Message               = $"Processed {confirmedCount} orders for {deliveryDate:yyyy-MM-dd}",
                DeliveryDate          = deliveryDate.ToDateTime(TimeOnly.MinValue),
                OrdersFound           = scheduledOrders.Count,
                OrdersPending         = pendingOrders.Count,
                OrdersAlreadyConfirmed = scheduledOrders.Count - pendingOrders.Count,
                OrdersConfirmed       = confirmedCount,
                OrdersFailed          = failedCount,
                Timestamp             = _time.UtcNow,
                Note                  = "Safe to call multiple times — idempotent"
            };
        }
    }
}
