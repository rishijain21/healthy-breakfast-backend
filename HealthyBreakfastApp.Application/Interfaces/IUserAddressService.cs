using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IUserAddressService
    {
        Task<UserAddressDetailDto?> GetByIdAsync(int id);
        Task<IEnumerable<UserAddressDetailDto>> GetUserAddressesAsync(int userId);
        Task<IEnumerable<UserAddressDetailDto>> GetActiveUserAddressesAsync(int userId);
        Task<UserAddressDetailDto?> GetPrimaryAddressAsync(int userId);
        Task<UserAddressDetailDto> CreateAsync(int userId, CreateUserAddressDto dto);
        Task<UserAddressDetailDto> UpdateAsync(int userId, int addressId, UpdateUserAddressDto dto);
        Task<bool> DeleteAsync(int userId, int addressId);
        Task<bool> SetPrimaryAddressAsync(int userId, int addressId);
        Task<ValidateAddressDto> ValidateAddressChangeAsync(int userId, int newAddressId);
    }
}
