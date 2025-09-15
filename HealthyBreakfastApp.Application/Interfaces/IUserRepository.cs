using HealthyBreakfastApp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserRepository
    {
        // Your existing methods
        Task<User?> GetByIdAsync(int id);
        Task AddUserAsync(User user);
        Task SaveChangesAsync();
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByAuthIdAsync(Guid authId);
        Task<List<User>> GetAllAsync();
        
        // ✅ ADD THESE NEW METHODS
        Task<User?> GetUserByAuthIdAsync(Guid authId);
        Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId);
        Task<UserAuthMapping?> GetAuthMappingAsync(Guid authId);
    }
}
