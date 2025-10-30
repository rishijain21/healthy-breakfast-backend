using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            => (await _walletTransactionRepository.GetAllAsync()).Select(t => MapToDto(t!));

        public async Task<WalletTransactionDto?> GetTransactionByIdAsync(int transactionId)
        {
            var transaction = await _walletTransactionRepository.GetByIdAsync(transactionId);
            if (transaction == null) return null;
            return MapToDto(transaction);
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsAsync(int userId)
            => (await _walletTransactionRepository.GetByUserIdAsync(userId)).Select(t => MapToDto(t!));

        public async Task<IEnumerable<WalletTransactionDto>> GetUserTransactionsByTypeAsync(int userId, string type)
            => (await _walletTransactionRepository.GetByUserIdAndTypeAsync(userId, type)).Select(t => MapToDto(t!));

        public async Task<decimal> GetUserBalanceAsync(int userId)
            => await _walletTransactionRepository.GetUserBalanceAsync(userId);

        public async Task<UserWalletSummaryDto?> GetUserWalletSummaryAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var summary = await _walletTransactionRepository.GetUserWalletSummaryAsync(userId);
            var balance = await GetUserBalanceAsync(userId);

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

        public async Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto dto)
        {
            var user = await _userRepository.GetByIdAsync(dto.UserId) ?? throw new ArgumentException("User not found");
            if (dto.Type != "Credit" && dto.Type != "Debit") throw new ArgumentException("Transaction type must be 'Credit' or 'Debit'");
            if (dto.Type == "Debit" && !await HasSufficientBalanceAsync(dto.UserId, dto.Amount))
                throw new InvalidOperationException("Insufficient wallet balance");

            var transaction = new WalletTransaction
            {
                UserId = dto.UserId,
                Amount = dto.Amount,
                Type = dto.Type,
                Description = dto.Description
            };

            var created = await _walletTransactionRepository.CreateAsync(transaction);
            var transactionFromDb = await _walletTransactionRepository.GetByIdAsync(created.TransactionId);
            if (transactionFromDb == null) throw new InvalidOperationException("Transaction creation failed.");

            return MapToDto(transactionFromDb);
        }

        public async Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up")
        {
            var transactionDto = await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = amount,
                Type = "Credit",
                Description = description
            });

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                WalletBalance = await GetUserBalanceAsync(userId)
            };
        }

        public async Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto)
            => await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = topUpDto.Amount,
                Type = "Credit",
                Description = topUpDto.Description ?? $"Wallet top-up of {topUpDto.Amount}"
            });

        public async Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description)
            => await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = amount,
                Type = "Debit",
                Description = description
            });

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await _walletTransactionRepository.HasSufficientBalanceAsync(userId, amount);

        public async Task<decimal> GetWalletBalanceAsync(int userId)
            => await GetUserBalanceAsync(userId);

        // ✅ OPTIMIZED: Removed UserName and UserEmail mapping
        private static WalletTransactionDto MapToDto(WalletTransaction t)
            => new WalletTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                Amount = t.Amount,
                Type = t.Type,
                Description = t.Description,
                CreatedAt = t.CreatedAt
                // ✅ REMOVED: UserName and UserEmail (no longer needed)
            };
    }
}
