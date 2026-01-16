using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserRepository
    {
        // ✅ EXISTING METHODS
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<List<User>> GetAllAsync();
        Task AddUserAsync(User user);
        Task SaveChangesAsync();

        // ✅ ADD THESE TWO NEW METHODS
        Task<User?> GetByAuthIdAsync(Guid authId);
        Task<User?> GetUserByAuthIdAsync(Guid authId);
        Task UpdateUserAsync(User user);

        Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId);
    }
}
