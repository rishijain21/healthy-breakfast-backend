using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Your existing methods (keep these unchanged)
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // ✅ ADD THESE NEW METHODS
        public async Task<User?> GetUserByAuthIdAsync(Guid authId)
        {
            return await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId);
        }

        public async Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create user
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create auth mapping
                var authMapping = new UserAuthMapping
                {
                    AuthId = authId,
                    UserId = user.UserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserAuthMappings.Add(authMapping);
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
