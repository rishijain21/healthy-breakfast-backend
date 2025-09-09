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
        
        // ✅ ADD THIS METHOD
        Task<List<User>> GetAllAsync();
    }
}
