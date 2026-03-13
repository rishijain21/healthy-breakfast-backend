using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class UserAddressService : IUserAddressService
    {
        private readonly IUserAddressRepository _addressRepository;
        private readonly IServiceableLocationRepository _locationRepository;

        public UserAddressService(
            IUserAddressRepository addressRepository,
            IServiceableLocationRepository locationRepository)
        {
            _addressRepository = addressRepository;
            _locationRepository = locationRepository;
        }

        public async Task<UserAddressDetailDto?> GetByIdAsync(int id)
        {
            var address = await _addressRepository.GetByIdWithDetailsAsync(id);
            return address == null ? null : MapToDetailDto(address);
        }

        public async Task<IEnumerable<UserAddressDetailDto>> GetUserAddressesAsync(int userId)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);
            return addresses.Select(MapToDetailDto);
        }

        public async Task<IEnumerable<UserAddressDetailDto>> GetActiveUserAddressesAsync(int userId)
        {
            var addresses = await _addressRepository.GetActiveByUserIdAsync(userId);
            return addresses.Select(MapToDetailDto);
        }

        public async Task<UserAddressDetailDto?> GetPrimaryAddressAsync(int userId)
        {
            var address = await _addressRepository.GetPrimaryAddressAsync(userId);
            return address == null ? null : MapToDetailDto(address);
        }

        public async Task<UserAddressDetailDto> CreateAsync(int userId, CreateUserAddressDto dto)
        {
            var location = await _locationRepository.GetByIdAsync(dto.ServiceableLocationId);
            if (location == null || !location.IsActive)
                throw new InvalidOperationException("Selected location is not serviceable");

            var address = new UserAddress
            {
                UserId = userId,
                ServiceableLocationId = dto.ServiceableLocationId,
                Wing = dto.Wing,
                Block = dto.Block,
                FlatNumber = dto.FlatNumber,
                Floor = dto.Floor,
                AdditionalInstructions = dto.AdditionalInstructions,
                Label = dto.Label,
                IsPrimary = dto.SetAsPrimary,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.SetAsPrimary)
            {
                var existingAddresses = await _addressRepository.GetByUserIdAsync(userId);
                foreach (var existing in existingAddresses)
                {
                    if (existing.IsPrimary)
                    {
                        existing.IsPrimary = false;
                        await _addressRepository.UpdateAsync(existing);
                    }
                }
            }

            var created = await _addressRepository.CreateAsync(address);
            
            // Reload with details
            var createdWithDetails = await _addressRepository.GetByIdWithDetailsAsync(created.Id);
            return MapToDetailDto(createdWithDetails!);
        }

        public async Task<UserAddressDetailDto> UpdateAsync(int userId, int addressId, UpdateUserAddressDto dto)
        {
            var address = await _addressRepository.GetByIdWithDetailsAsync(addressId);
            if (address == null)
                throw new KeyNotFoundException($"Address with ID {addressId} not found");

            if (address.UserId != userId)
                throw new UnauthorizedAccessException("You can only update your own addresses");

            if (!string.IsNullOrEmpty(dto.Wing))
                address.Wing = dto.Wing;
            
            if (!string.IsNullOrEmpty(dto.Block))
                address.Block = dto.Block;
            
            if (!string.IsNullOrEmpty(dto.FlatNumber))
                address.FlatNumber = dto.FlatNumber;
            
            if (!string.IsNullOrEmpty(dto.Floor))
                address.Floor = dto.Floor;
            
            if (dto.AdditionalInstructions != null)
                address.AdditionalInstructions = dto.AdditionalInstructions;
            
            if (dto.Label != null)
                address.Label = dto.Label;

            // Handle IsPrimary flag - set this address as primary if IsPrimary is true
            if (dto.IsPrimary.HasValue && dto.IsPrimary.Value)
            {
                await _addressRepository.SetPrimaryAddressAsync(userId, addressId);
            }

            address.UpdatedAt = DateTime.UtcNow;

            var updated = await _addressRepository.UpdateAsync(address);
            
            // Reload with details
            var updatedWithDetails = await _addressRepository.GetByIdWithDetailsAsync(updated.Id);
            return MapToDetailDto(updatedWithDetails!);
        }

        public async Task<bool> DeleteAsync(int userId, int addressId)
        {
            var address = await _addressRepository.GetByIdAsync(addressId);
            if (address == null)
                return false;

            if (address.UserId != userId)
                throw new UnauthorizedAccessException("You can only delete your own addresses");

            var hasActiveSubscriptions = await _addressRepository.HasActiveSubscriptionsAsync(addressId);
            if (hasActiveSubscriptions)
                throw new InvalidOperationException("Cannot delete address with active subscriptions");

            return await _addressRepository.DeleteAsync(addressId);
        }

        public async Task<bool> SetPrimaryAddressAsync(int userId, int addressId)
        {
            var address = await _addressRepository.GetByIdAsync(addressId);
            if (address == null)
                throw new KeyNotFoundException($"Address with ID {addressId} not found");

            if (address.UserId != userId)
                throw new UnauthorizedAccessException("You can only modify your own addresses");

            return await _addressRepository.SetPrimaryAddressAsync(userId, addressId);
        }

        public async Task<ValidateAddressDto> ValidateAddressChangeAsync(int userId, int newAddressId)
        {
            var address = await _addressRepository.GetByIdWithDetailsAsync(newAddressId);
            
            if (address == null)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "Address not found"
                };
            }

            if (address.UserId != userId)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "Unauthorized access"
                };
            }

            if (!address.IsActive)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "Address is inactive"
                };
            }

            if (!address.ServiceableLocation.IsActive)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "Location is no longer serviceable"
                };
            }

            return new ValidateAddressDto
            {
                IsServiceable = true,
                Message = "Address is valid and serviceable"
            };
        }

        private UserAddressDetailDto MapToDetailDto(UserAddress address)
        {
            return new UserAddressDetailDto
            {
                Id = address.Id,
                UserId = address.UserId,
                Wing = address.Wing,
                Block = address.Block,
                FlatNumber = address.FlatNumber,
                Floor = address.Floor,
                AdditionalInstructions = address.AdditionalInstructions,
                Label = address.Label,
                IsPrimary = address.IsPrimary,
                IsActive = address.IsActive,
                CompleteAddress = address.CompleteAddress,
                ServiceableLocation = MapLocationToDto(address.ServiceableLocation),
                CreatedAt = address.CreatedAt,
                UpdatedAt = address.UpdatedAt
            };
        }

        private ServiceableLocationDto MapLocationToDto(ServiceableLocation location)
        {
            return new ServiceableLocationDto
            {
                Id = location.Id,
                City = location.City,
                Area = location.Area,
                Locality = location.Locality,
                LandmarkOrSociety = location.LandmarkOrSociety,
                Pincode = location.Pincode,
                IsActive = location.IsActive,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                DeliveryTimeSlot = location.DeliveryTimeSlot,
                FullAddress = location.FullAddress,
                CreatedAt = location.CreatedAt
            };
        }
    }
}
