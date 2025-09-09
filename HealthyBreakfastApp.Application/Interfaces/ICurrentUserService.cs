using HealthyBreakfastApp.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Task<UserDto?> GetCurrentUserAsync(Guid authId);
        Task<int?> GetCurrentUserIdAsync(Guid authId);
    }
}
