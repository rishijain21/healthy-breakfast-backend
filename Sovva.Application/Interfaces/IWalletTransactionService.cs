using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IWalletTransactionService
    {
        Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync();
        Task<PagedResult<WalletTransactionDto>> GetAllTransactionsPagedAsync(int page, int pageSize);
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
        Task<WalletTransactionDto> AdminCreditWalletAsync(long userId, decimal amount, string description, int adminUserId);

        // ✅ NEW: Write transaction record without balance check (balance already deducted atomically)
        Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description, int? scheduledOrderId = null);

        // ✅ NEW: Check if wallet transaction exists for a scheduled order
        Task<bool> TransactionExistsForScheduledOrderAsync(int scheduledOrderId);

        /// <summary>
        /// Atomically checks the ledger balance and inserts a Debit in one SQL statement.
        /// CALLER CONTRACT: This method MUST be called inside an active DB transaction
        /// (IUnitOfWork.ExecuteInTransactionAsync) to prevent overdraft under concurrent load.
        /// Using Read Committed isolation, the INSERT...SELECT WHERE is safe only when
        /// the surrounding transaction prevents another connection from interleaving.
        /// Returns true if the debit was recorded (balance sufficient), false otherwise.
        /// </summary>
        Task<(bool Success, long? TransactionId)> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null);
    }
}
