using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserService
    {
        // ✅ EXISTING METHODS (keep these)
        Task<int> CreateUserAsync(CreateUserDto dto);
        Task<UserDto?> GetUserByIdAsync(int id);
        Task<List<UserDto>> GetAllUsersAsync();
        Task<bool> UserExistsAsync(string email);
        Task<UserDto> RegisterUserAsync(RegisterUserRequest request);

        // ✅ ADD THESE NEW METHODS FOR AUTH
        Task<UserDto?> GetUserByAuthIdAsync(Guid authId);
        Task<UserDto> FindOrCreateUserByAuthIdAsync(Guid authId, string? name = null, string? email = null);
    }
}
