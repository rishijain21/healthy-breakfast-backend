using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sovva.Application.Services
{
    public class WalletTransactionService : IWalletTransactionService
    {
        private readonly IWalletTransactionRepository _walletTransactionRepository;
        private readonly IUserRepository _userRepository;

        // ✅ UPDATED: Removed MAX_TOPUP_AMOUNT
        private const decimal MAX_WALLET_BALANCE = 50000m;
        private const decimal MIN_TOPUP_AMOUNT = 50m;

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

        // ✅ FIX 4: Updated to use advisory lock to prevent race conditions
        public async Task<WalletTransactionDto> CreateTransactionAsync(CreateWalletTransactionDto dto)
        {
            var user = await _userRepository.GetByIdAsync(dto.UserId) ?? throw new ArgumentException("User not found");
            
            if (dto.Type != "Credit" && dto.Type != "Debit") 
                throw new ArgumentException("Transaction type must be 'Credit' or 'Debit'");

            // ✅ Use advisory lock to prevent race conditions on the same userId
            // pg_advisory_xact_lock is scoped to the transaction and auto-releases on commit/rollback
            await _walletTransactionRepository.AcquireUserWalletLockAsync(dto.UserId);

            // ✅ Re-read balance AFTER acquiring the lock (pre-lock read is stale)
            var currentBalance = await GetUserBalanceAsync(dto.UserId);

            // ✅ Validate wallet limit for Credit transactions
            if (dto.Type == "Credit")
            {
                var newBalance = currentBalance + dto.Amount;

                if (dto.Amount < MIN_TOPUP_AMOUNT)
                    throw new InvalidOperationException($"Minimum top-up amount is ₹{MIN_TOPUP_AMOUNT}");

                if (newBalance > MAX_WALLET_BALANCE)
                {
                    var remaining = MAX_WALLET_BALANCE - currentBalance;
                    throw new InvalidOperationException(
                        $"Cannot add ₹{dto.Amount}. Maximum wallet balance is ₹{MAX_WALLET_BALANCE}. " +
                        $"Current balance: ₹{currentBalance}. You can add up to ₹{remaining}."
                    );
                }
            }

            // ✅ Validate sufficient balance for Debit transactions
            if (dto.Type == "Debit" && currentBalance < dto.Amount)
                throw new InvalidOperationException(
                    $"Insufficient wallet balance. Required: ₹{dto.Amount}, Available: ₹{currentBalance}");

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

        // ✅ UPDATED: Removed MAX_TOPUP_AMOUNT validation
        public async Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up")
        {
            // Validate minimum amount
            if (amount < MIN_TOPUP_AMOUNT)
                throw new InvalidOperationException($"Minimum top-up amount is ₹{MIN_TOPUP_AMOUNT}");

            var currentBalance = await GetUserBalanceAsync(userId);
            var newBalance = currentBalance + amount;

            if (newBalance > MAX_WALLET_BALANCE)
            {
                var remaining = MAX_WALLET_BALANCE - currentBalance;
                throw new InvalidOperationException(
                    $"Cannot add ₹{amount}. Maximum wallet balance is ₹{MAX_WALLET_BALANCE}. " +
                    $"Current balance: ₹{currentBalance}. You can add up to ₹{remaining}."
                );
            }

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
                Description = topUpDto.Description ?? $"Wallet top-up of ₹{topUpDto.Amount}"
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

        // ✅ NEW: Write transaction record without balance check (balance already deducted atomically)
        public async Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description)
        {
            // No balance check — balance already adjusted atomically upstream
            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description
            };
            // ✅ Use WriteRecordOnlyAsync — balance already deducted atomically upstream
            await _walletTransactionRepository.WriteRecordOnlyAsync(transaction);
        }

        private static WalletTransactionDto MapToDto(WalletTransaction t)
            => new WalletTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                Amount = t.Amount,
                Type = t.Type,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            };
    }
}
