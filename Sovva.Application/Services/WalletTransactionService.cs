using Sovva.Application.Exceptions;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Microsoft.Extensions.Logging;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WalletTransactionService> _logger;

        public WalletTransactionService(
            IWalletTransactionRepository walletTransactionRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ILogger<WalletTransactionService> logger)
        {
            _walletTransactionRepository = walletTransactionRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync()
            => (await _walletTransactionRepository.GetAllAsync()).Select(t => MapToDto(t!));

        public async Task<WalletTransactionDto?> GetTransactionByIdAsync(long transactionId)
        {
            var transaction = await _walletTransactionRepository.GetByIdAsync(transactionId);
            if (transaction == null) return null;
            return MapToDto(transaction);
        }

        public async Task<PagedResult<WalletTransactionDto>> GetUserTransactionsAsync(int userId, int page, int pageSize)
        {
            // Clamp inputs
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100); // Higher cap for transactions

            var (transactions, totalCount) = await _walletTransactionRepository.GetByUserIdAsync(userId, page, pageSize);
            
            return new PagedResult<WalletTransactionDto>
            {
                Items = transactions.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

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
            WalletTransactionDto result = null!;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var user = await _userRepository.GetByIdAsync(dto.UserId) ?? throw new ArgumentException("User not found");
                
                if (dto.Type != WalletConstants.Credit && dto.Type != WalletConstants.Debit) 
                    throw new ArgumentException("Transaction type must be 'Credit' or 'Debit'");

                // ✅ Use advisory lock to prevent race conditions on the same userId
                // pg_advisory_xact_lock is scoped to the transaction and auto-releases on commit/rollback
                await _walletTransactionRepository.AcquireUserWalletLockAsync(dto.UserId);

                // ✅ Re-read balance AFTER acquiring the lock (pre-lock read is stale)
                var currentBalance = await GetUserBalanceAsync(dto.UserId);

                // ✅ Validate wallet limit for Credit transactions
                if (dto.Type == WalletConstants.Credit)
                {
                    var newBalance = currentBalance + dto.Amount;

                    if (!dto.IsAdminCredit && dto.Amount < WalletConstants.MinTopUpAmount)
                        throw new InvalidOperationException($"Minimum top-up amount is ₹{WalletConstants.MinTopUpAmount}");

                    if (newBalance > WalletConstants.MaxWalletBalance)
                    {
                        var remaining = WalletConstants.MaxWalletBalance - currentBalance;
                        throw new InvalidOperationException(
                            $"Cannot add ₹{dto.Amount}. Maximum wallet balance is ₹{WalletConstants.MaxWalletBalance}. " +
                            $"Current balance: ₹{currentBalance}. You can add up to ₹{remaining}."
                        );
                    }
                }

                // ✅ Validate sufficient balance for Debit transactions
                if (dto.Type == WalletConstants.Debit && currentBalance < dto.Amount)
                {
                    _logger.LogWarning(
                        "Wallet debit failed - insufficient funds: UserId={UserId} Required={Required} Available={Available}",
                        dto.UserId, dto.Amount, currentBalance);

                    throw new InsufficientBalanceException(dto.Amount, currentBalance);
                }

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

                result = MapToDto(transactionFromDb);

                // ✅ LOG SUCCESS
                if (dto.Type == WalletConstants.Debit)
                {
                    _logger.LogInformation(
                        "Wallet debit: UserId={UserId} Amount={Amount} OrderId={OrderId} NewBalance={NewBalance}",
                        dto.UserId, dto.Amount, dto.ScheduledOrderId, currentBalance - dto.Amount);
                }
                else
                {
                    _logger.LogInformation(
                        "Wallet credit: UserId={UserId} Amount={Amount} Description={Description}",
                        dto.UserId, dto.Amount, dto.Description);
                }
            });

            return result;
        }

        // ✅ UPDATED: Removed MAX_TOPUP_AMOUNT validation
        public async Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up")
        {
            // Validate minimum amount
            if (amount < WalletConstants.MinTopUpAmount)
                throw new InvalidOperationException($"Minimum top-up amount is ₹{WalletConstants.MinTopUpAmount}");

            var currentBalance = await GetUserBalanceAsync(userId);
            var newBalance = currentBalance + amount;

            if (newBalance > WalletConstants.MaxWalletBalance)
            {
                var remaining = WalletConstants.MaxWalletBalance - currentBalance;
                throw new InvalidOperationException(
                    $"Cannot add ₹{amount}. Maximum wallet balance is ₹{WalletConstants.MaxWalletBalance}. " +
                    $"Current balance: ₹{currentBalance}. You can add up to ₹{remaining}."
                );
            }

            var transactionDto = await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = amount,
                Type = WalletConstants.Credit,
                Description = description
            });

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            var newLedgerBalance = await GetUserBalanceAsync(userId);

            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                AccountStatus = user.AccountStatus.ToString(),
                Role = user.Role.ToString(),
                IsProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                                  !string.IsNullOrWhiteSpace(user.Phone)
                // WalletBalance omitted — caller should use GET /api/WalletTransactions/my-balance
            };
        }

        public async Task<WalletTransactionDto> TopUpWalletAsync(int userId, WalletTopUpDto topUpDto)
            => await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = topUpDto.Amount,
                Type = WalletConstants.Credit,
                Description = topUpDto.Description ?? $"Wallet top-up of ₹{topUpDto.Amount}"
            });

        public async Task<WalletTransactionDto> AdminCreditWalletAsync(long userId, decimal amount, string description)
        {
            var user = await _userRepository.GetByIdAsync((int)userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero");
            }

            var currentBalance = await GetUserBalanceAsync((int)userId);
            if (currentBalance + amount > WalletConstants.MaxWalletBalance)
            {
                throw new InvalidOperationException($"Maximum wallet balance is ₹{WalletConstants.MaxWalletBalance}. Current balance: ₹{currentBalance}");
            }

            return await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = (int)userId,
                Amount = amount,
                Type = WalletConstants.Credit,
                Description = description,
                IsAdminCredit = true
            });
        }

        public async Task<WalletTransactionDto> DebitWalletAsync(int userId, decimal amount, string description)
            => await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = userId,
                Amount = amount,
                Type = WalletConstants.Debit,
                Description = description
            });

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await _walletTransactionRepository.HasSufficientBalanceAsync(userId, amount);

        public async Task<decimal> GetWalletBalanceAsync(int userId)
            => await GetUserBalanceAsync(userId);

        // ✅ NEW: Write transaction record without balance check (balance already deducted atomically)
        public async Task WriteTransactionRecordAsync(int userId, decimal amount, string type, string description, int? scheduledOrderId = null)
        {
            // No balance check — balance already adjusted atomically upstream
            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description,
                ScheduledOrderId = scheduledOrderId
            };
            // ✅ Use WriteRecordOnlyAsync — balance already deducted atomically upstream
            await _walletTransactionRepository.WriteRecordOnlyAsync(transaction);
        }

        // ✅ NEW: Check if wallet transaction exists for a scheduled order
        public async Task<bool> TransactionExistsForScheduledOrderAsync(int scheduledOrderId)
        {
            // Check if a Debit transaction exists with description containing the scheduled order ID
            return await _walletTransactionRepository.ExistsForScheduledOrderAsync(scheduledOrderId);
        }

        /// <summary>
        /// Atomically checks ledger balance and inserts a Debit record in a single SQL statement.
        /// Replaces the broken two-step DeductWalletBalanceAtomicAsync + WriteTransactionRecordAsync flow.
        /// </summary>
        public async Task<bool> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null)
        {
            var success = await _walletTransactionRepository.AtomicDebitAsync(userId, amount, description, scheduledOrderId);

            if (success)
            {
                _logger.LogInformation(
                    "Wallet atomic debit: UserId={UserId} Amount={Amount} ScheduledOrderId={ScheduledOrderId}",
                    userId, amount, scheduledOrderId);
            }
            else
            {
                _logger.LogWarning(
                    "Wallet atomic debit FAILED (insufficient balance): UserId={UserId} Required={Amount} ScheduledOrderId={ScheduledOrderId}",
                    userId, amount, scheduledOrderId);
            }

            return success;
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
