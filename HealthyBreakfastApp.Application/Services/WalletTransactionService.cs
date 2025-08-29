using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class WalletTransactionService : IWalletTransactionService
    {
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly IUserRepository _userRepository;

        public WalletTransactionService(
            IWalletTransactionRepository walletTransactionRepository,
            IUserRepository userRepository)
        {
            _walletTransactionRepository = walletTransactionRepository;
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync()
        {
            var transactions = await _walletTransactionRepository.GetAllAsync();
            return transactions.Select(MapToDto);
        }

        public async Task<WalletTransactionDto?> GetTransactionByIdAsync(int transactionId)
        {
            var transaction = await _walletTransactionRepository.GetByIdAsync(transactionId);
            return transaction != null ? MapToDto(transaction) : null;
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsAsync(int userId)
        {
            var transactions = await _walletTransactionRepository.GetByUserIdAsync(userId);
            return transactions.Select(MapToDto);
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type)
        {
            var transactions = await _walletTransactionRepository.GetByUserIdAndTypeAsync(userId, type);
            return transactions.Select(MapToDto);
        }

        public async Task<decimal> GetUserBalanceAsync(int userId)
        {
            return await _walletTransactionRepository.GetUserBalanceAsync(userId);
        }

        public async Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return null;

            var summary = await _walletTransactionRepository.GetUserWalletSummaryAsync(userId);
            var balance = await _walletTransactionRepository.GetUserBalanceAsync(userId);

            return new UserWalletSummaryDto
            {
                UserId = userId,
                UserName = user.Name,
                UserEmail = user.Email,
                CurrentBalance = balance,
                TotalCredits = summary.totalCredits,
                TotalDebits = summary.totalDebits,
                TransactionCount = summary.transactionCount,
                LastTransactionDate = summary.lastTransactionDate ?? DateTime.MinValue
            };
        }

        public async Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto createTransactionDto)
        {
            // Validate user exists
            var user = await _userRepository.GetByIdAsync(createTransactionDto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Validate transaction type
            if (createTransactionDto.Type != "Credit" && createTransactionDto.Type != "Debit")
                throw new ArgumentException("Transaction type must be 'Credit' or 'Debit'");

            // For debit transactions, check sufficient balance
            if (createTransactionDto.Type == "Debit")
            {
                var hasSufficientBalance = await _walletTransactionRepository.HasSufficientBalanceAsync(
                    createTransactionDto.UserId, createTransactionDto.Amount);
                
                if (!hasSufficientBalance)
                    throw new InvalidOperationException("Insufficient wallet balance");
            }

            var transaction = new WalletTransaction
            {
                UserId = createTransactionDto.UserId,
                Amount = createTransactionDto.Amount,
                Type = createTransactionDto.Type,
                Description = createTransactionDto.Description
            };

            var createdTransaction = await _walletTransactionRepository.CreateAsync(transaction);
            
            // Reload with navigation properties
            var result = await _walletTransactionRepository.GetByIdAsync(createdTransaction.TransactionId);
            return MapToDto(result!);
        }

        public async Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto)
        {
            var createDto = new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = topUpDto.Amount,
                Type = "Credit",
                Description = topUpDto.Description ?? $"Wallet top-up of ${topUpDto.Amount}"
            };

            return await CreateTransactionAsync(createDto);
        }

        public async Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description)
        {
            var createDto = new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = amount,
                Type = "Debit",
                Description = description
            };

            return await CreateTransactionAsync(createDto);
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
        {
            return await _walletTransactionRepository.HasSufficientBalanceAsync(userId, amount);
        }

        private static WalletTransactionDto MapToDto(WalletTransaction transaction)
        {
            return new WalletTransactionDto
            {
                TransactionId = transaction.TransactionId,
                UserId = transaction.UserId,
                Amount = transaction.Amount,
                Type = transaction.Type,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt,
                UserName = transaction.User?.Name ?? string.Empty,
                UserEmail = transaction.User?.Email ?? string.Empty
            };
        }
    }
}
