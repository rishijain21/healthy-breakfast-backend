using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Sovva.Infrastructure.Repositories
{
    public class UserAddressRepository : IUserAddressRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public UserAddressRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<UserAddress?> GetByIdAsync(int id)
        {
            return await _context.UserAddresses
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<UserAddress?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.UserAddresses
                .Include(x => x.ServiceableLocation)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        /// <summary>
        /// Get user's primary address with serviceable location details
        /// </summary>
        public async Task<UserAddress?> GetPrimaryAddressByUserIdAsync(int userId)
        {
            return await _context.UserAddresses
                .Include(x => x.ServiceableLocation)
                .FirstOrDefaultAsync(x => 
                    x.UserId == userId && 
                    x.IsPrimary == true && 
                    x.IsActive == true
                );
        }

        public async Task<IEnumerable<UserAddress>> GetByUserIdAsync(int userId)
        {
            return await _context.UserAddresses
                .Include(x => x.ServiceableLocation)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserAddress>> GetActiveByUserIdAsync(int userId)
        {
            return await _context.UserAddresses
                .Include(x => x.ServiceableLocation)
                .Where(x => x.UserId == userId && x.IsActive)
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserAddress?> GetPrimaryAddressAsync(int userId)
        {
            return await _context.UserAddresses
                .Include(x => x.ServiceableLocation)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.IsPrimary && x.IsActive);
        }

        public async Task<UserAddress> CreateAsync(UserAddress address)
        {
            _context.UserAddresses.Add(address);
            await _context.SaveChangesAsync();
            return address;
        }

        public async Task<UserAddress> UpdateAsync(UserAddress address)
        {
            _context.UserAddresses.Update(address);
            await _context.SaveChangesAsync();
            return address;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var address = await GetByIdAsync(id);
            if (address == null)
                return false;

            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var address = await GetByIdAsync(id);
            if (address == null)
                return false;

            address.IsActive = false;
            // UpdatedAt handled by TimestampInterceptor
            await UpdateAsync(address);
            return true;
        }

        public async Task<bool> SetPrimaryAddressAsync(int userId, int addressId)
        {
            try
            {
                Console.WriteLine($"🔍 SetPrimaryAddressAsync called - UserId: {userId}, AddressId: {addressId}");

                // ✅ Use raw SQL to avoid EF Core unique constraint issues
                // First, clear all primary flags for this user
                var clearSql = @"UPDATE ""UserAddresses"" 
                               SET ""IsPrimary"" = false, ""UpdatedAt"" = @now 
                               WHERE ""UserId"" = @userId AND ""IsPrimary"" = true";
                
                await _context.Database.ExecuteSqlRawAsync(clearSql, 
                    new Npgsql.NpgsqlParameter("@now", _time.UtcNow),
                    new Npgsql.NpgsqlParameter("@userId", userId));

                // Then set the target address as primary
                var setSql = @"UPDATE ""UserAddresses"" 
                              SET ""IsPrimary"" = true, ""UpdatedAt"" = @now 
                              WHERE ""Id"" = @addressId AND ""UserId"" = @userId";
                
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(setSql,
                    new Npgsql.NpgsqlParameter("@now", _time.UtcNow),
                    new Npgsql.NpgsqlParameter("@addressId", addressId),
                    new Npgsql.NpgsqlParameter("@userId", userId));

                Console.WriteLine($"✅ SetPrimaryAddressAsync: {rowsAffected} row(s) affected");
                
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in SetPrimaryAddressAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.UserAddresses.AnyAsync(x => x.Id == id);
        }

        public async Task<bool> IsUserAddressAsync(int addressId, int userId)
        {
            return await _context.UserAddresses
                .AnyAsync(x => x.Id == addressId && x.UserId == userId);
        }

        public async Task<bool> HasActiveSubscriptionsAsync(int addressId)
        {
            return await _context.Subscriptions
                .AnyAsync(x => x.DeliveryAddressId == addressId 
                            && x.Active 
                            && x.EndDate >= _time.TodayIst);
        }

        // ✅ NEW METHOD: Batch load addresses by IDs (used in midnight job)
        public async Task<List<UserAddress>> GetByIdsAsync(List<int> ids)
        {
            return await _context.UserAddresses
                .AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .ToListAsync();
        }

        // ✅ NEW: Batch get primary addresses by user IDs (for generation job optimization)
        public async Task<List<UserAddress>> GetPrimaryAddressesByUserIdsAsync(List<int> userIds) =>
            await _context.UserAddresses
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId) && a.IsPrimary)
                .ToListAsync();
    }
}
