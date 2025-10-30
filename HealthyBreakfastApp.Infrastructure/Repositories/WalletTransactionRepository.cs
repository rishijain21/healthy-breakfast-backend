using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class WalletTransactionRepository : IWalletTransactionRepository
    {
        private readonly AppDbContext _context;

        public WalletTransactionRepository(AppDbContext context) => _context = context;

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
            transaction.CreatedAt = DateTime.UtcNow;
            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            await UpdateUserWalletBalance(transaction.UserId);
            return transaction;
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
            => await GetUserBalanceAsync(userId) >= amount;

        public async Task<(decimal totalCredits, decimal totalDebits, int transactionCount, DateTime? lastTransactionDate)> GetUserWalletSummaryAsync(int userId)
        {
            var transactions = await _context.WalletTransactions.Where(wt => wt.UserId == userId).ToListAsync();
            return (
                transactions.Where(t => t.Type == "Credit").Sum(t => t.Amount),
                transactions.Where(t => t.Type == "Debit").Sum(t => t.Amount),
                transactions.Count,
                transactions.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.CreatedAt
            );
        }

        private async Task UpdateUserWalletBalance(int userId)
        {
            var balance = await GetUserBalanceAsync(userId);
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.WalletBalance = balance;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
