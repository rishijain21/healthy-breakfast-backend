using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IServiceableLocationService
    {
        Task<ServiceableLocationDto?> GetByIdAsync(int id);
        Task<IEnumerable<ServiceableLocationDto>> GetAllAsync();
        Task<IEnumerable<ServiceableLocationDto>> GetActiveLocationsAsync();
        Task<IEnumerable<ServiceableLocationDto>> SearchByPincodeAsync(string pincode);
        Task<IEnumerable<ServiceableLocationDto>> SearchByCityAsync(string city);
        Task<IEnumerable<ServiceableLocationDto>> SearchByAreaAsync(string city, string area);
        Task<ServiceableLocationDto> CreateAsync(CreateServiceableLocationDto dto);
        Task<ServiceableLocationDto> UpdateAsync(int id, UpdateServiceableLocationDto dto);
        Task<bool> DeleteAsync(int id);
        Task<ValidateAddressDto> ValidateLocationAsync(int locationId);
    }
}
