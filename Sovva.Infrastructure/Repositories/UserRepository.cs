using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    internal class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public UserRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            var user = await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.UserId == id);

            return user;
        }

        public Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            // SaveChangesAsync will be called by the service
            return Task.CompletedTask;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var lowerEmail = email.ToLower();
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == lowerEmail);
        }

        public async Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 50)
        {
            return await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // P1-1 FIX: Removed duplicate GetByAuthIdAsync. This is the single canonical method.
        public async Task<(int UserId, string Role, string AccountStatus)?> GetAuthInfoByAuthIdAsync(Guid authId)
        {
            var authInfo = await _context.Users
                .AsNoTracking()
                .Where(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId)
                .Select(u => new { u.UserId, u.Role, u.AccountStatus })
                .FirstOrDefaultAsync();

            if (authInfo == null)
                return null;

            return (authInfo.UserId, authInfo.Role.ToString(), authInfo.AccountStatus.ToString());
        }

        public async Task<User?> GetUserByAuthIdAsync(Guid authId)
        {
            var user = await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId);

            if (user != null)
            {
                user.WalletBalance = await _context.WalletTransactions
                    .Where(wt => wt.UserId == user.UserId)
                    .SumAsync(wt => (decimal?)(wt.Type == WalletConstants.Credit ? wt.Amount : -wt.Amount)) ?? 0m;
            }

            return user;
        }

        public async Task<User?> GetUserByAuthIdIncludingDeletedAsync(Guid authId)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.AuthMapping != null && u.AuthMapping.AuthId == authId);

            if (user != null)
            {
                user.WalletBalance = await _context.WalletTransactions
                    .Where(wt => wt.UserId == user.UserId)
                    .SumAsync(wt => (decimal?)(wt.Type == WalletConstants.Credit ? wt.Amount : -wt.Amount)) ?? 0m;
            }

            return user;
        }

        // Batch load users by auth IDs (used in midnight job)
        public async Task<List<User>> GetByAuthIdsAsync(List<Guid> authIds)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.AuthMapping)
                .Where(u => u.AuthMapping != null && authIds.Contains(u.AuthMapping.AuthId))
                .ToListAsync();
        }

        // Create user with auth mapping in transaction
        public async Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Create user
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Create auth mapping
                    var authMapping = new UserAuthMapping
                    {
                        AuthId = authId,
                        UserId = user.UserId
                        // CreatedAt handled by TimestampInterceptor
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
            });
        }

        // Batch get users with AuthMapping by user IDs (for generation job optimization)
        public async Task<List<User>> GetByIdsWithAuthMappingAsync(List<int> userIds) =>
            await _context.Users
                .AsNoTracking()
                .Include(u => u.AuthMapping)
                .Where(u => userIds.Contains(u.UserId))
                .ToListAsync();

        // P0-3 FIX: DeductWalletBalanceAtomicAsync and CreditWalletBalanceAsync REMOVED.
        // Wallet operations now use IWalletTransactionRepository.AtomicDebitAsync / AtomicCreditAsync
        // which do a single INSERT INTO WalletTransactions with a balance check in the WHERE clause.

        public async Task<int> CountAsync()
        {
            return await _context.Users.CountAsync();
        }
    }
}
