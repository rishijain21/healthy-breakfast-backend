using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IScheduledOrderService
    {// Sovva.Application/Interfaces/IScheduledOrderService.cs

Task<ScheduledOrderResponseDto> DuplicateScheduledOrderAsync(Guid authId, int scheduledOrderId);

        Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(Guid authId, CreateScheduledOrderDto dto);
        Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(Guid authId, DateTime date);
        Task ModifyScheduledOrderAsync(Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto);
        Task CancelScheduledOrderAsync(Guid authId, int scheduledOrderId);
        Task<bool> CheckWalletBalanceAsync(Guid authId, decimal amount);
        Task ConfirmAllScheduledOrdersAsync();
        Task<int> GetTimeUntilMidnightMinutesAsync();
    }
}
