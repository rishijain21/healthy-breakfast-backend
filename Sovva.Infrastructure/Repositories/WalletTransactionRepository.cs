using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class WalletTransactionRepository : IWalletTransactionRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public WalletTransactionRepository(AppDbContext context, IAppTimeProvider time) 
        { 
            _context = context; 
            _time = time;
        }

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<IEnumerable<WalletTransaction>> GetAllAsync()
            => await _context.WalletTransactions
                        .OrderByDescending(wt => wt.CreatedAt).ToListAsync();

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<WalletTransaction?> GetByIdAsync(int transactionId)
            => await _context.WalletTransactions
                        .FirstOrDefaultAsync(wt => wt.TransactionId == transactionId);

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId)
            => await _context.WalletTransactions
                        .Where(wt => wt.UserId == userId).OrderByDescending(wt => wt.CreatedAt).ToListAsync();

        // ✅ OPTIMIZED: Removed .Include(wt => wt.User) for faster queries
        public async Task<IEnumerable<WalletTransaction>> GetByUserIdAndTypeAsync(int userId, string type)
            => await _context.WalletTransactions
                        .Where(wt => wt.UserId == userId && wt.Type == type)
                        .OrderByDescending(wt => wt.CreatedAt).ToListAsync();

        public async Task<decimal> GetUserBalanceAsync(int userId)
        {
            var credits = await _context.WalletTransactions.Where(wt => wt.UserId == userId && wt.Type == "Credit").SumAsync(wt => wt.Amount);
            var debits = await _context.WalletTransactions.Where(wt => wt.UserId == userId && wt.Type == "Debit").SumAsync(wt => wt.Amount);
            return credits - debits;
        }

        public async Task<WalletTransaction> CreateAsync(WalletTransaction transaction)
        {
            try
            {
                // CreatedAt handled by TimestampInterceptor
                _context.WalletTransactions.Add(transaction);
                await _context.SaveChangesAsync();
                await UpdateUserWalletBalance(transaction.UserId);
                return transaction;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Wallet balance was updated by another request. Please retry the transaction.", ex);
            }
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await GetUserBalanceAsync(userId) >= amount;

        // ✅ FIX 8: Optimized to use targeted SQL aggregates instead of loading all transactions into memory
        public async Task<(decimal totalCredits, decimal totalDebits, int transactionCount, DateTime? lastTransactionDate)> GetUserWalletSummaryAsync(int userId)
        {
            var totalCredits = await _context.WalletTransactions
                .Where(wt => wt.UserId == userId && wt.Type == "Credit")
                .SumAsync(wt => wt.Amount);

            var totalDebits = await _context.WalletTransactions
                .Where(wt => wt.UserId == userId && wt.Type == "Debit")
                .SumAsync(wt => wt.Amount);

            var count = await _context.WalletTransactions
                .Where(wt => wt.UserId == userId)
                .CountAsync();

            var lastDate = await _context.WalletTransactions
                .Where(wt => wt.UserId == userId)
                .OrderByDescending(wt => wt.CreatedAt)
                .Select(wt => (DateTime?)wt.CreatedAt)
                .FirstOrDefaultAsync();

            return (totalCredits, totalDebits, count, lastDate);
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

        private async Task UpdateUserWalletBalance(int userId)
        {
            var balance = await GetUserBalanceAsync(userId);
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.WalletBalance = balance;
                // UpdatedAt handled by TimestampInterceptor
                await _context.SaveChangesAsync();
            }
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
    }
}
