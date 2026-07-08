using Microsoft.EntityFrameworkCore;
using Sovva.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Sovva.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<UnitOfWork> _logger;
        private IDbContextTransaction? _transaction;
        private bool _disposed;

        public UnitOfWork(AppDbContext context, Microsoft.Extensions.Logging.ILogger<UnitOfWork> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> SaveChangesAsync()
            => await _context.SaveChangesAsync();

        public async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            if (_context.Database.CurrentTransaction != null)
            {
                await operation();
                return;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await operation();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return await operation();
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_transaction != null)
            {
                _logger.LogWarning("UnitOfWork disposed with open transaction. Rolling back automatically.");
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }
            _context.Dispose();
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            if (_transaction != null)
            {
                _logger.LogWarning("UnitOfWork disposed with open transaction. Rolling back automatically.");
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
            await _context.DisposeAsync();
            _disposed = true;
        }
    }
}
