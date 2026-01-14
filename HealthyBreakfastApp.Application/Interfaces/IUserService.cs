using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserService
    {
        // ✅ EXISTING METHODS
        Task<int> CreateUserAsync(CreateUserDto dto);
        Task<UserDto?> GetUserByIdAsync(int id);
        Task<List<UserDto>> GetAllUsersAsync();
        Task<bool> UserExistsAsync(string email);

        // ✅ AUTH METHODS
        Task<UserDto?> GetUserByEmailAsync(string email);
        Task<UserDto> RegisterUserAsync(RegisterUserRequest request);
        Task<UserDto?> GetUserByAuthIdAsync(Guid authId);
        
        // ❌ REMOVED: FindOrCreateUserByAuthIdAsync() - Causes auto-creation bug
    }
}
