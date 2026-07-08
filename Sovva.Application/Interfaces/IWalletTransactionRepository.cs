
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IWalletTransactionRepository
    {
        Task<IEnumerable<WalletTransaction>> GetAllAsync();
        Task<(IEnumerable<WalletTransaction> Items, int TotalCount)> GetAllPagedAsync(int page, int pageSize);
        Task<WalletTransaction?> GetByIdAsync(long transactionId);
        Task<(IEnumerable<WalletTransaction> Items, int TotalCount)> GetByUserIdAsync(int userId, int page, int pageSize);
        Task<IEnumerable<WalletTransaction>> GetByUserIdAndTypeAsync(int userId, string type);
        Task<decimal> GetUserBalanceAsync(int userId);
        Task<WalletTransaction> CreateAsync(WalletTransaction transaction);
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);
        Task<(decimal totalCredits, decimal totalDebits, int transactionCount, DateTime? lastTransactionDate)> GetUserWalletSummaryAsync(int userId);
        Task AcquireUserWalletLockAsync(int userId);



        /// <summary>
        /// ✅ NEW: Write ledger record ONLY — no wallet balance update
        /// Used when balance is already deducted atomically upstream (midnight confirm job)
        /// </summary>
        Task WriteRecordOnlyAsync(WalletTransaction transaction);

        /// <summary>
        /// ✅ NEW: Check if a wallet transaction exists for a scheduled order
        /// </summary>
        Task<bool> ExistsForScheduledOrderAsync(int scheduledOrderId);

        // ✅ FIX 13: Batch idempotency check
        Task<Dictionary<int, WalletTransaction>> GetByScheduledOrderIdsAsync(IEnumerable<int> scheduledOrderIds);

        /// <summary>
        /// Atomically checks the ledger balance and inserts a Debit record in a single SQL statement.
        /// Returns true if the debit was recorded (balance was sufficient), false if insufficient.
        /// This is the ONLY safe way to deduct wallet balance — eliminates race conditions.
        /// </summary>
        Task<(bool Success, long? TransactionId)> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null);

        /// <summary>
        /// Atomically inserts a Credit record. Returns true if successful.
        /// </summary>
        Task<bool> AtomicCreditAsync(int userId, decimal amount, string description, int? scheduledOrderId = null);
    }
}
