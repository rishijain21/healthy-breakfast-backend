using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IScheduledOrderService
    {
        // ✅ UPDATED: Now accepts userId directly (from JWT claim) - zero DB hit
        Task<ScheduledOrderResponseDto> DuplicateScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId);
        Task<ScheduledOrderResponseDto> CreateScheduledOrderAsync(int userId, Guid authId, CreateScheduledOrderDto dto, bool skipWalletCheck = false);
        Task<List<ScheduledOrderResponseDto>> GetScheduledOrdersForDateAsync(int userId, Guid authId, DateTime date);
        Task ModifyScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId, ModifyScheduledOrderDto dto);
        Task CancelScheduledOrderAsync(int userId, Guid authId, int scheduledOrderId);
        // ✅ UPDATED: Uses userId directly - PK lookup instead of authId join
        Task<bool> CheckWalletBalanceAsync(int userId, decimal amount);
        Task ConfirmAllScheduledOrdersAsync();
        Task<int> GetTimeUntilMidnightMinutesAsync();
    }
}
