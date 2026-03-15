using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? GetAuthId();
        Task<UserDto?> GetCurrentUserAsync();
        Task<int?> GetCurrentUserIdAsync();
    }
}
