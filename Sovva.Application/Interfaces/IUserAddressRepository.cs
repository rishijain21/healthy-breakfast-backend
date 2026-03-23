using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IUserAddressRepository
    {
        Task<UserAddress?> GetByIdAsync(int id);
        Task<UserAddress?> GetByIdWithDetailsAsync(int id);
        Task<UserAddress?> GetPrimaryAddressByUserIdAsync(int userId);
        Task<IEnumerable<UserAddress>> GetByUserIdAsync(int userId);
        Task<IEnumerable<UserAddress>> GetActiveByUserIdAsync(int userId);
        Task<UserAddress?> GetPrimaryAddressAsync(int userId);
        Task<UserAddress> CreateAsync(UserAddress address);
        Task<UserAddress> UpdateAsync(UserAddress address);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeactivateAsync(int id);
        Task<bool> SetPrimaryAddressAsync(int userId, int addressId);
        Task<bool> ExistsAsync(int id);
        Task<bool> IsUserAddressAsync(int addressId, int userId);
        Task<bool> HasActiveSubscriptionsAsync(int addressId);
        Task<List<UserAddress>> GetByIdsAsync(List<int> ids);
    }
}
