using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IWalletTransactionService
    {
        Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync();
        Task<WalletTransactionDto?> GetTransactionByIdAsync(int transactionId);
        Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsAsync(int userId);
        Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type);
        Task<decimal> GetUserBalanceAsync(int userId);
        Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId);
        Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto createTransactionDto);
        Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto);
        Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description);
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);
    }
}
