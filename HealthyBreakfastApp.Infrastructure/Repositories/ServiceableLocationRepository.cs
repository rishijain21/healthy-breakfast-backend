using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.Infrastructure.Repositories
{
    public class ServiceableLocationRepository : IServiceableLocationRepository
    {
        private readonly AppDbContext _context;

        public ServiceableLocationRepository(AppDbContext context)
        {
            _context = context;
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
                location.UpdatedAt = DateTime.UtcNow;
                await UpdateAsync(location);
                return true;
            }

            _context.ServiceableLocations.Remove(location);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ServiceableLocations
                .AnyAsync(x => x.Id == id);
        }

        public async Task<bool> IsLocationServiceableAsync(int locationId)
        {
            return await _context.ServiceableLocations
                .AnyAsync(x => x.Id == locationId && x.IsActive);
        }
    }
}
