using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IScheduledOrderService
    {
        Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(Guid authId, CreateScheduledOrderDto dto);
        Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(Guid authId, DateTime date);
        Task ModifyScheduledOrderAsync(Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto);
        Task CancelScheduledOrderAsync(Guid authId, int scheduledOrderId);
        Task<bool> CheckWalletBalanceAsync(Guid authId, decimal amount);
        Task ConfirmAllScheduledOrdersAsync();
        Task<int> GetTimeUntilMidnightMinutesAsync();
    }
}
