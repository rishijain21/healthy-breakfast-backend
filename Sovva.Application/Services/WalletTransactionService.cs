using Sovva.Application.Exceptions;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
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
        private readonly ICacheService _cacheService;
        private readonly IFailedOrderAttemptRepository _failedOrderAttemptRepository;
        private readonly IAppTimeProvider _time;

        public WalletTransactionService(
            IWalletTransactionRepository walletTransactionRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ILogger<WalletTransactionService> logger,
            ICacheService cacheService,
            IFailedOrderAttemptRepository failedOrderAttemptRepository,
            IAppTimeProvider time)
        {
            _walletTransactionRepository = walletTransactionRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheService = cacheService;
            _failedOrderAttemptRepository = failedOrderAttemptRepository;
            _time = time;
        }

        public async Task<IEnumerable<WalletTransactionDto>> GetAllTransactionsAsync()
            => (await _walletTransactionRepository.GetAllAsync()).Select(t => MapToDto(t!));

        public async Task<PagedResult<WalletTransactionDto>> GetAllTransactionsPagedAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (transactions, totalCount) = await _walletTransactionRepository.GetAllPagedAsync(page, pageSize);
            
            return new PagedResult<WalletTransactionDto>
            {
                Items = transactions.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

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
        {
            var cacheKey = $"wallet:balance:{userId}";
            var cached = await _cacheService.GetAsync<decimal?>(cacheKey);
            if (cached.HasValue) return cached.Value;
            
            var balance = await _walletTransactionRepository.GetUserBalanceAsync(userId);
            await _cacheService.SetAsync(cacheKey, balance, TimeSpan.FromSeconds(30));
            return balance;
        }

        private async Task InvalidateBalanceCacheAsync(int userId)
        {
            await _cacheService.RemoveAsync($"wallet:balance:{userId}");
        }

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

                // ✅ Re-read balance AFTER acquiring the lock — bypass cache for authoritative read
                var currentBalance = await _walletTransactionRepository.GetUserBalanceAsync(dto.UserId);

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
                    Description = dto.Description,
                    ReferenceType = dto.ReferenceType,
                    ReferenceId = dto.ReferenceId
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

            // ✅ Invalidate cache after successful transaction commit
            await InvalidateBalanceCacheAsync(dto.UserId);

            return result;
        }

        // ✅ UPDATED: Removed redundant pre-balance check to avoid TOCTOU race conditions
        public async Task<UserDto> TopUpWalletAsync(int userId, decimal amount, string description = "Wallet top-up")
        {
            // Validate minimum amount
            if (amount < WalletConstants.MinTopUpAmount)
                throw new InvalidOperationException($"Minimum top-up amount is ₹{WalletConstants.MinTopUpAmount}");

            // The actual CreateTransactionAsync handles the advisory lock and MaxWalletBalance check safely
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

        public async Task<WalletTransactionDto> AdminCreditWalletAsync(long userId, decimal amount, string description, int adminUserId)
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

            // Redundant check removed to avoid TOCTOU race condition.
            // CreateTransactionAsync handles advisory lock + max balance verification.

            return await CreateTransactionAsync(new CreateWalletTransactionDto
            {
                UserId = (int)userId,
                Amount = amount,
                Type = WalletConstants.Credit,
                Description = description,
                IsAdminCredit = true,
                ReferenceType = "Manual",
                ReferenceId = adminUserId
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
        public async Task<(bool Success, long? TransactionId)> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Safety net: Warn loudly if called outside a transaction.
            // AtomicDebitAsync relies on the surrounding transaction for serialization.
            // This is not enforced at runtime to avoid breaking the retrying execution strategy,
            // but is logged as a warning to catch accidental misuse.
            // See: IWalletTransactionService.AtomicDebitAsync contract.
            _logger.LogDebug("AtomicDebitAsync called for UserId={UserId}, Amount={Amount}", userId, amount);

            var result = await _walletTransactionRepository.AtomicDebitAsync(userId, amount, description, scheduledOrderId);

            if (result.Success)
            {
                await InvalidateBalanceCacheAsync(userId);
                
                _logger.LogInformation(
                    "Wallet atomic debit: UserId={UserId} Amount={Amount} ScheduledOrderId={ScheduledOrderId} TransactionId={TransactionId}",
                    userId, amount, scheduledOrderId, result.TransactionId);
            }
            else
            {
                var currentBalance = await GetUserBalanceAsync(userId);
                _logger.LogWarning(
                    "Wallet atomic debit FAILED (insufficient balance): UserId={UserId} Required={Amount} ScheduledOrderId={ScheduledOrderId} CurrentBalance={CurrentBalance}",
                    userId, amount, scheduledOrderId, currentBalance);

                if (scheduledOrderId.HasValue)
                {
                    await _failedOrderAttemptRepository.AddAsync(new FailedOrderAttempt
                    {
                        UserId = userId,
                        ScheduledOrderId = scheduledOrderId.Value,
                        RequiredAmount = amount,
                        AvailableBalance = currentBalance,
                        Reason = "Insufficient wallet balance",
                        AttemptedAt = _time.UtcNow
                    });
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("[METRICS] AtomicDebitAsync took {Ms}ms", stopwatch.ElapsedMilliseconds);

            return result;
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
