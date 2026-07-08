using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    internal class WalletTransactionRepository : IWalletTransactionRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public WalletTransactionRepository(AppDbContext context, IAppTimeProvider time) 
        { 
            _context = context; 
            _time = time;
        }

        // ✅ BOUNDED: Added safety limit of 500 to prevent OOM
        public async Task<IEnumerable<WalletTransaction>> GetAllAsync()
        {
            // WARNING: GetAllAsync called — this is unbounded and should not be used in production flows
            // Safety limit applied for now.
            return await _context.WalletTransactions
                        .AsNoTracking()
                        .OrderByDescending(wt => wt.CreatedAt)
                        .Take(500)
                        .ToListAsync();
        }

        public async Task<(IEnumerable<WalletTransaction> Items, int TotalCount)> GetAllPagedAsync(int page, int pageSize)
        {
            var query = _context.WalletTransactions
                        .AsNoTracking()
                        .OrderByDescending(wt => wt.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

            return (items, totalCount);
        }

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<WalletTransaction?> GetByIdAsync(long transactionId)
            => await _context.WalletTransactions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(wt => wt.TransactionId == transactionId);

        // ✅ OPTIMIZED: Added pagination to prevent unbounded queries
        public async Task<(IEnumerable<WalletTransaction> Items, int TotalCount)> GetByUserIdAsync(int userId, int page, int pageSize)
        {
            var query = _context.WalletTransactions
                        .AsNoTracking()
                        .Where(wt => wt.UserId == userId)
                        .OrderByDescending(wt => wt.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

            return (items, totalCount);
        }

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<IEnumerable<WalletTransaction>> GetByUserIdAndTypeAsync(int userId, string type)
            => await _context.WalletTransactions
                        .AsNoTracking()
                        .Where(wt => wt.UserId == userId && wt.Type == type)
                        .OrderByDescending(wt => wt.CreatedAt)
                        .Take(100)
                        .ToListAsync();

        public async Task<decimal> GetUserBalanceAsync(int userId)
        {
            return await _context.WalletTransactions
                .Where(wt => wt.UserId == userId)
                .SumAsync(wt => wt.Type == WalletConstants.Credit ? wt.Amount : -wt.Amount);
        }

        public async Task<WalletTransaction> CreateAsync(WalletTransaction transaction)
        {
            try
            {
                // CreatedAt handled by TimestampInterceptor
                _context.WalletTransactions.Add(transaction);
                await _context.SaveChangesAsync();
                return transaction;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Wallet balance was updated by another request. Please retry the transaction.", ex);
            }
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await GetUserBalanceAsync(userId) >= amount;

        // ✅ FIX 8: Optimized to use targeted EF Core aggregates instead of loading all transactions into memory or manual ADO.NET
        public async Task<(decimal totalCredits, decimal totalDebits, int transactionCount, DateTime? lastTransactionDate)> GetUserWalletSummaryAsync(int userId)
        {
            var summary = await _context.WalletTransactions
                .Where(wt => wt.UserId == userId)
                .GroupBy(wt => wt.UserId)
                .Select(g => new
                {
                    TotalCredits = g.Sum(wt => wt.Type == WalletConstants.Credit ? wt.Amount : 0),
                    TotalDebits = g.Sum(wt => wt.Type == WalletConstants.Debit ? wt.Amount : 0),
                    TransactionCount = g.Count(),
                    LastTransactionDate = g.Max(wt => (DateTime?)wt.CreatedAt)
                })
                .FirstOrDefaultAsync();

            if (summary == null)
            {
                return (0m, 0m, 0, null);
            }

            return (summary.TotalCredits, summary.TotalDebits, summary.TransactionCount, summary.LastTransactionDate);
        }

        // ✅ FIX 4: PostgreSQL advisory lock to prevent race conditions on wallet operations
        public async Task AcquireUserWalletLockAsync(int userId)
        {
            // PostgreSQL advisory lock — scoped to transaction, auto-released on commit/rollback.
            // Using userId as the lock key ensures only one wallet operation runs per user at a time.
            // Callers must be inside a database transaction for this to be meaningful.
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", userId);
        }



        /// <summary>
        /// ✅ NEW: Write ledger record ONLY — no wallet balance update
        /// Used when balance is already deducted atomically upstream (midnight confirm job)
        /// </summary>
        public async Task WriteRecordOnlyAsync(WalletTransaction transaction)
        {
            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            // ❌ Do NOT call UpdateUserWalletBalance — balance already correct
        }

        /// <summary>
        /// ✅ NEW: Check if a wallet transaction exists for a scheduled order
        /// </summary>
        public async Task<bool> ExistsForScheduledOrderAsync(int scheduledOrderId)
        {
            // ✅ FIX [W4]: Query directly by the explicit ScheduledOrderId column
            return await _context.WalletTransactions
                .AnyAsync(t => t.Type == WalletConstants.Debit && t.ScheduledOrderId == scheduledOrderId);
        }

        // ✅ FIX 13: Batch idempotency check
        public async Task<Dictionary<int, WalletTransaction>> GetByScheduledOrderIdsAsync(IEnumerable<int> scheduledOrderIds)
        {
            return await _context.WalletTransactions
                .AsNoTracking()
                .Where(wt => wt.Type == WalletConstants.Debit && wt.ScheduledOrderId.HasValue && scheduledOrderIds.Contains(wt.ScheduledOrderId.Value))
                .ToDictionaryAsync(wt => wt.ScheduledOrderId!.Value);
        }

        /// <summary>
        /// Atomically checks the ledger balance and inserts a Debit record in a single SQL statement.
        /// Returns true if the debit was recorded (balance was sufficient), false if insufficient.
        ///
        /// HOW IT WORKS:
        /// The INSERT...SELECT WHERE pattern is a single SQL statement.
        /// PostgreSQL guarantees single-statement atomicity — the balance check and
        /// insert happen as one indivisible operation. No advisory locks needed.
        /// If two requests race, only one can see sufficient balance; the other
        /// will see the first's INSERT in its snapshot (same statement = same snapshot).
        /// </summary>
        public async Task<(bool Success, long? TransactionId)> AtomicDebitAsync(int userId, decimal amount, string description, int? scheduledOrderId = null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = _context.Database.CurrentTransaction == null 
                    ? await _context.Database.BeginTransactionAsync() 
                    : null;

                await AcquireUserWalletLockAsync(userId);

                var ids = await _context.Database
                    .SqlQueryRaw<long>(
                        @"INSERT INTO ""WalletTransactions"" (""UserId"", ""Amount"", ""Type"", ""Description"", ""ScheduledOrderId"", ""CreatedAt"", ""UpdatedAt"")
                          SELECT {0}, {1}, 'Debit', {2}, {3}, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'
                          WHERE (
                              SELECT COALESCE(SUM(CASE WHEN ""Type"" = 'Credit' THEN ""Amount"" ELSE -""Amount"" END), 0)
                              FROM ""WalletTransactions""
                              WHERE ""UserId"" = {0}
                          ) >= {1}
                          AND ({3} IS NULL OR NOT EXISTS (
                              SELECT 1 FROM ""WalletTransactions""
                              WHERE ""ScheduledOrderId"" = {3} AND ""Type"" = 'Debit'
                          ))
                          RETURNING ""TransactionId""",
                        userId, amount, description, scheduledOrderId)
                    .ToListAsync();

                if (ids.Count == 1)
                {
                    if (transaction != null) await transaction.CommitAsync();
                    return (true, ids[0]);
                }

                if (transaction != null) await transaction.RollbackAsync();
                return (false, (long?)null);
            });
        }

        /// <summary>
        /// Atomically inserts a Credit record.
        /// Uses INSERT...SELECT WHERE to enforce max balance guard in a single statement.
        /// </summary>
        public async Task<bool> AtomicCreditAsync(int userId, decimal amount, string description, int? scheduledOrderId = null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = _context.Database.CurrentTransaction == null 
                    ? await _context.Database.BeginTransactionAsync() 
                    : null;

                await AcquireUserWalletLockAsync(userId);

                var maxBalance = WalletConstants.MaxWalletBalance;

                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO ""WalletTransactions"" (""UserId"", ""Amount"", ""Type"", ""Description"", ""ScheduledOrderId"", ""CreatedAt"", ""UpdatedAt"")
                      SELECT {0}, {1}, 'Credit', {2}, {3}, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'
                      WHERE (
                          SELECT COALESCE(SUM(CASE WHEN ""Type"" = 'Credit' THEN ""Amount"" ELSE -""Amount"" END), 0)
                          FROM ""WalletTransactions""
                          WHERE ""UserId"" = {0}
                      ) + {1} <= {4}",
                    userId, amount, description, scheduledOrderId, maxBalance);

                if (transaction != null)
                {
                    if (rowsAffected == 1) await transaction.CommitAsync();
                    else await transaction.RollbackAsync();
                }

                return rowsAffected == 1;
            });
        }
    }
}
