using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserRepository  
    {
        // ✅ Your existing methods (keep these)
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<List<User>> GetAllAsync();
        Task AddUserAsync(User user);
        Task SaveChangesAsync();

        // ✅ ADD THESE MISSING METHODS
        Task<User?> GetUserByAuthIdAsync(Guid authId);
        Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId);
        
        // ✅ ADD: Method that scheduled order service needs
        Task<User?> GetByAuthIdAsync(Guid authId);
    }
}
