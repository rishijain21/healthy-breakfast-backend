using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IWalletTransactionService
    {
        Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync();
        Task<WalletTransactionDto?> GetTransactionByIdAsync(long transactionId);
        Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsAsync(int userId);
        Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type);
        Task<decimal> GetUserBalanceAsync(int userId);
        Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up");
Task<decimal> GetWalletBalanceAsync(int userId);

        Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId);
        Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto createTransactionDto);
        Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto);
        Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description);
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);

        // ✅ NEW: Write transaction record without balance check (balance already deducted atomically)
        Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description);
    }
}
