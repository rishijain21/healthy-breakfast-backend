using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
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
        Task<List<User>> GetByAuthIdsAsync(List<Guid> authIds);
        Task UpdateUserAsync(User user);

        Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId);

        // ✅ NEW: Batch get users with AuthMapping by user IDs (for generation job optimization)
        Task<List<User>> GetByIdsWithAuthMappingAsync(List<int> userIds);

        // ✅ NEW: Atomic wallet deduction with balance check (prevents race conditions)
        Task<bool> DeductWalletBalanceAtomicAsync(int userId, decimal amount);
    }
}
