using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        // ✅ FIXED: Now includes AuthMapping
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        public Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            // SaveChangesAsync will be called by the service
            return Task.CompletedTask;
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

        // ✅ NEW METHOD: Get user by Supabase AuthId (used by ScheduledOrderService)
        public async Task<User?> GetByAuthIdAsync(Guid authId)
        {
            return await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId);
        }

        // ✅ NEW METHOD: Get user by Supabase AuthId (alternative name for clarity)
        public async Task<User?> GetUserByAuthIdAsync(Guid authId)
        {
            return await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId);
        }

        // ✅ NEW METHOD: Batch load users by auth IDs (used in midnight job)
        public async Task<List<User>> GetByAuthIdsAsync(List<Guid> authIds)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.AuthMapping)
                .Where(u => u.AuthMapping != null && authIds.Contains(u.AuthMapping.AuthId))
                .ToListAsync();
        }

        // ✅ NEW METHOD: Create user with auth mapping in transaction
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

        // ✅ NEW: Batch get users with AuthMapping by user IDs (for generation job optimization)
        public async Task<List<User>> GetByIdsWithAuthMappingAsync(List<int> userIds) =>
            await _context.Users
                .AsNoTracking()
                .Include(u => u.AuthMapping)
                .Where(u => userIds.Contains(u.UserId))
                .ToListAsync();

        // ✅ NEW: Atomic wallet deduction with balance check (prevents race conditions)
        // Returns the new balance if successful, or null if insufficient funds
        public async Task<bool> DeductWalletBalanceAtomicAsync(int userId, decimal amount)
        {
            // Use raw SQL for atomic UPDATE with condition
            // This ensures the check-and-deduct is a single atomic operation
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE users 
                  SET wallet_balance = wallet_balance - {0}, updated_at = {1}
                  WHERE user_id = {2} AND wallet_balance >= {0}",
                amount, DateTime.UtcNow, userId);

            return rowsAffected == 1;
        }
    }
}
