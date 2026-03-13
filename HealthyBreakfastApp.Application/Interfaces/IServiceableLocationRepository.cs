using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IServiceableLocationRepository
    {
        Task<ServiceableLocation?> GetByIdAsync(int id);
        Task<IEnumerable<ServiceableLocation>> GetAllAsync();
        Task<IEnumerable<ServiceableLocation>> GetActiveLocationsAsync();
        Task<IEnumerable<ServiceableLocation>> SearchByPincodeAsync(string pincode);
        Task<IEnumerable<ServiceableLocation>> SearchByCityAsync(string city);
        Task<IEnumerable<ServiceableLocation>> SearchByAreaAsync(string city, string area);
        Task<IEnumerable<ServiceableLocation>> SearchByQueryAsync(string query);
        Task<ServiceableLocation> CreateAsync(ServiceableLocation location);
        Task<ServiceableLocation> UpdateAsync(ServiceableLocation location);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<bool> IsLocationServiceableAsync(int locationId);
    }
}