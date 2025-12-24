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
        
        // ✅ ADD THIS METHOD (used by WalletController, OrdersController, AuthMiddleware)
        Task<UserDto> FindOrCreateUserByAuthIdAsync(Guid authId, string? name = null, string? email = null);
    }
}
