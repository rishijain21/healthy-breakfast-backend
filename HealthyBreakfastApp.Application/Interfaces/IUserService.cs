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

        // ✅ ADD THESE NEW METHODS
        Task<bool> UserExistsAsync(string email);
        Task<UserDto> RegisterUserAsync(RegisterUserRequest request);
    }
}
