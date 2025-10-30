using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? GetAuthId();
        Task<UserDto?> GetCurrentUserAsync();
        Task<int?> GetCurrentUserIdAsync();
    }
}
