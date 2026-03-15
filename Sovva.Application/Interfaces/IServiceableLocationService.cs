using Sovva.Application.DTOs;

namespace Sovva.Application.Interfaces
{
    public interface IServiceableLocationService
    {
        Task<ServiceableLocationDto?> GetByIdAsync(int id);
        Task<IEnumerable<ServiceableLocationDto>> GetAllAsync();           // all (admin)
        Task<IEnumerable<ServiceableLocationDto>> GetActiveLocationsAsync(); // active only (users)
        Task<IEnumerable<ServiceableLocationDto>> SearchByPincodeAsync(string pincode);
        Task<IEnumerable<ServiceableLocationDto>> SearchByCityAsync(string city);
        Task<IEnumerable<ServiceableLocationDto>> SearchByAreaAsync(string city, string area);
        Task<IEnumerable<ServiceableLocationDto>> SearchByQueryAsync(string query); // FIX: free-text
        Task<ServiceableLocationDto> CreateAsync(CreateServiceableLocationDto dto);
        Task<ServiceableLocationDto> UpdateAsync(int id, UpdateServiceableLocationDto dto);
        Task<bool> DeleteAsync(int id);
        Task<ValidateAddressDto> ValidateLocationAsync(int locationId);
    }
}