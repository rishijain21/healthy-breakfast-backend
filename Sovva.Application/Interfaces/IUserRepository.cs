using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IUserRepository
    {
        // ✅ EXISTING METHODS
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 50);
        Task AddUserAsync(User user);

        // Auth-based lookups (single canonical method — GetByAuthIdAsync removed as duplicate)
        Task<(int UserId, string Role, string AccountStatus)?> GetAuthInfoByAuthIdAsync(Guid authId);
        Task<User?> GetUserByAuthIdAsync(Guid authId);
        Task<User?> GetUserByAuthIdIncludingDeletedAsync(Guid authId);
        Task<List<User>> GetByAuthIdsAsync(List<Guid> authIds);
        Task UpdateUserAsync(User user);

        Task<User> CreateUserWithAuthMappingAsync(User user, Guid authId);

        // Batch get users with AuthMapping by user IDs (for generation job optimization)
        Task<List<User>> GetByIdsWithAuthMappingAsync(List<int> userIds);

        // Wallet operations moved to IWalletTransactionRepository (AtomicDebitAsync / AtomicCreditAsync)

        Task<int> CountAsync();
    }
}
