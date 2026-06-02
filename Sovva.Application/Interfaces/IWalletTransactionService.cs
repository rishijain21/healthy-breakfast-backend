using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IWalletTransactionService
    {
        Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync();
        Task<WalletTransactionDto?> GetTransactionByIdAsync(long transactionId);
        Task<PagedResult<WalletTransactionDto>> GetUserTransactionsAsync(int userId, int page, int pageSize);
        Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type);
        Task<decimal> GetUserBalanceAsync(int userId);
        Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up");
Task<decimal> GetWalletBalanceAsync(int userId);

        Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId);
        Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto createTransactionDto);
        Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto);
        Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description);
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);
        Task<WalletTransactionDto> AdminCreditWalletAsync(long userId, decimal amount, string description);

        // ✅ NEW: Write transaction record without balance check (balance already deducted atomically)
        Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description, int? scheduledOrderId = null);

        // ✅ NEW: Check if wallet transaction exists for a scheduled order
        Task<bool> TransactionExistsForScheduledOrderAsync(int scheduledOrderId);

        /// <summary>
        /// Atomically checks ledger balance and inserts a Debit record in a single SQL statement.
        /// Returns true if the debit was recorded (balance sufficient), false otherwise.
        /// Replaces the broken DeductWalletBalanceAtomicAsync + WriteTransactionRecordAsync combo.
        /// </summary>
        Task<bool> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null);
    }
}
