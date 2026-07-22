using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.ScheduledOrders.Commands.CancelScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.ConfirmAllScheduledOrders;
using Sovva.Application.Features.ScheduledOrders.Commands.CreateScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.DuplicateScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.ModifyScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.ProcessSingleScheduledOrder;
using Sovva.Application.Features.ScheduledOrders.Commands.RetryFailedOrders;
using Sovva.Application.Features.ScheduledOrders.Queries.CheckWalletBalance;
using Sovva.Application.Features.ScheduledOrders.Queries.GetScheduledOrdersForDate;
using Sovva.Application.Features.ScheduledOrders.Queries.GetTimeUntilMidnightMinutes;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class ScheduledOrderService : IScheduledOrderService
    {
        private readonly ISender _sender;

        public ScheduledOrderService(ISender sender)
        {
            _sender = sender;
        }

        public async Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(int userId, Guid authId, CreateScheduledOrderDto dto, bool skipWalletCheck = false)
        {
            return await _sender.Send(new CreateScheduledOrderCommand(userId, authId, dto, skipWalletCheck));
        }

        public async Task<ScheduledOrderResponseDto> DuplicateScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId)
        {
            return await _sender.Send(new DuplicateScheduledOrderCommand(userId, authId, scheduledOrderId));
        }

        public async Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(int userId, Guid authId, DateTime date)
        {
            return await _sender.Send(new GetScheduledOrdersForDateQuery(userId, authId, date));
        }

        public async Task ModifyScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto)
        {
            await _sender.Send(new ModifyScheduledOrderCommand(userId, authId, scheduledOrderId, dto));
        }

        public async Task CancelScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId)
        {
            await _sender.Send(new CancelScheduledOrderCommand(userId, authId, scheduledOrderId));
        }

        public async Task<bool> CheckWalletBalanceAsync(int userId, decimal amount)
        {
            return await _sender.Send(new CheckWalletBalanceQuery(userId, amount));
        }

        public async Task<ProcessOrdersResponseDto> ConfirmAllScheduledOrdersAsync(DateOnly? targetDate = null)
        {
            return await _sender.Send(new ConfirmAllScheduledOrdersCommand(targetDate));
        }

        public async Task<ProcessOrdersResponseDto> RetryFailedOrdersAsync(DateOnly? targetDate = null, string? correlationId = null)
        {
            return await _sender.Send(new RetryFailedOrdersCommand(targetDate, correlationId));
        }

        public async Task<bool> ProcessSingleScheduledOrderAsync(
            ScheduledOrder scheduledOrder,
            IReadOnlyDictionary<Guid, User> usersByAuthId,
            IReadOnlyDictionary<int, Order> existingOrders,
            IReadOnlyDictionary<int, WalletTransaction> existingTransactions,
            IReadOnlyDictionary<int, UserAddress> addressesMap)
        {
            return await _sender.Send(new ProcessSingleScheduledOrderCommand(
                scheduledOrder,
                usersByAuthId,
                existingOrders,
                existingTransactions,
                addressesMap
            ));
        }

        public async Task<int> GetTimeUntilMidnightMinutesAsync()
        {
            return await _sender.Send(new GetTimeUntilMidnightMinutesQuery());
        }
    }
}
