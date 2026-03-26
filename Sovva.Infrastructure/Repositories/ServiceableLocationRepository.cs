using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class ServiceableLocationRepository : IServiceableLocationRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public ServiceableLocationRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<ServiceableLocation?> GetByIdAsync(int id)
        {
            return await _context.ServiceableLocations
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<ServiceableLocation>> GetAllAsync()
        {
            return await _context.ServiceableLocations
                .OrderBy(x => x.City)
                .ThenBy(x => x.Area)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceableLocation>> GetActiveLocationsAsync()
        {
            return await _context.ServiceableLocations
                .Where(x => x.IsActive)
                .OrderBy(x => x.City)
                .ThenBy(x => x.Area)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceableLocation>> SearchByPincodeAsync(string pincode)
        {
            return await _context.ServiceableLocations
                .Where(x => x.Pincode == pincode && x.IsActive)
                .OrderBy(x => x.City)
                .ThenBy(x => x.Area)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceableLocation>> SearchByCityAsync(string city)
        {
            return await _context.ServiceableLocations
                .Where(x => x.City.ToLower().Contains(city.ToLower()) && x.IsActive)
                .OrderBy(x => x.Area)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceableLocation>> SearchByAreaAsync(string city, string area)
        {
            return await _context.ServiceableLocations
                .Where(x => x.City.ToLower().Contains(city.ToLower())
                         && x.Area.ToLower().Contains(area.ToLower())
                         && x.IsActive)
                .OrderBy(x => x.Locality)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceableLocation>> SearchByQueryAsync(string query)
        {
            var q = query.Trim().ToLower();

            return await _context.ServiceableLocations
                .Where(x => x.IsActive && (
                    x.City.ToLower().Contains(q) ||
                    x.Area.ToLower().Contains(q) ||
                    x.Locality.ToLower().Contains(q) ||
                    x.LandmarkOrSociety.ToLower().Contains(q) ||
                    x.Pincode.Contains(q)
                ))
                .OrderBy(x => x.City)
                .ThenBy(x => x.Area)
                .Take(20)
                .ToListAsync();
        }

        public async Task<ServiceableLocation> CreateAsync(ServiceableLocation location)
        {
            _context.ServiceableLocations.Add(location);
            await _context.SaveChangesAsync();
            return location;
        }

        public async Task<ServiceableLocation> UpdateAsync(ServiceableLocation location)
        {
            _context.ServiceableLocations.Update(location);
            await _context.SaveChangesAsync();
            return location;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var location = await GetByIdAsync(id);
            if (location == null)
                return false;

            var addressCount = await _context.UserAddresses
                .CountAsync(x => x.ServiceableLocationId == id);

            if (addressCount > 0)
            {
                location.IsActive = false;
                // UpdatedAt handled by TimestampInterceptor
                await UpdateAsync(location);
                return true;
            }

            _context.ServiceableLocations.Remove(location);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ServiceableLocations.AnyAsync(x => x.Id == id);
        }

        public async Task<bool> IsLocationServiceableAsync(int locationId)
        {
            return await _context.ServiceableLocations
                .AnyAsync(x => x.Id == locationId && x.IsActive);
        }
    }
}