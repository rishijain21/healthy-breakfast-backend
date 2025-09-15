using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        // ✅ YOUR EXISTING METHODS (unchanged)
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByAuthIdAsync(Guid authId)
        {
            var userAuthMapping = await _context.UserAuthMappings
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.AuthId == authId);
            
            return userAuthMapping?.User;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // ✅ ADD THESE NEW METHODS
        public async Task<User?> GetUserByAuthIdAsync(Guid authId)
        {
            return await _context.UserAuthMappings
                .Where(mapping => mapping.AuthId == authId)
                .Include(mapping => mapping.User)
                .Select(mapping => mapping.User)
                .FirstOrDefaultAsync();
        }

        public async Task<UserAuthMapping?> GetAuthMappingAsync(Guid authId)
        {
            return await _context.UserAuthMappings
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.AuthId == authId);
        }

        public async Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Add user
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                // Create auth mapping
                var authMapping = new UserAuthMapping
                {
                    AuthId = authId,
                    UserId = user.UserId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.UserAuthMappings.AddAsync(authMapping);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return user;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
