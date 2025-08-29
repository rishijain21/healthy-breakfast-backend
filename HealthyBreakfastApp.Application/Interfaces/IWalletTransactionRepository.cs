
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IWalletTransactionRepository
    {
        Task<IEnumerable<WalletTransaction>> GetAllAsync();
        Task<WalletTransaction?> GetByIdAsync(int transactionId);
        Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId);
        Task<IEnumerable<WalletTransaction>> GetByUserIdAndTypeAsync(int userId, string type);
        Task<decimal> GetUserBalanceAsync(int userId);
        Task<WalletTransaction> CreateAsync(WalletTransaction transaction);
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);
        Task<(decimal totalCredits, decimal totalDebits, int transactionCount, DateTime? lastTransactionDate)> GetUserWalletSummaryAsync(int userId);
    }
}
