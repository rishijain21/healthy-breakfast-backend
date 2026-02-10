using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class ServiceableLocationService : IServiceableLocationService
    {
        private readonly IServiceableLocationRepository _repository;

        public ServiceableLocationService(IServiceableLocationRepository repository)
        {
            _repository = repository;
        }

        public async Task<ServiceableLocationDto?> GetByIdAsync(int id)
        {
            var location = await _repository.GetByIdAsync(id);
            return location == null ? null : MapToDto(location);
        }

        public async Task<IEnumerable<ServiceableLocationDto>> GetAllAsync()
        {
            var locations = await _repository.GetAllAsync();
            return locations.Select(MapToDto);
        }

        public async Task<IEnumerable<ServiceableLocationDto>> GetActiveLocationsAsync()
        {
            var locations = await _repository.GetActiveLocationsAsync();
            return locations.Select(MapToDto);
        }

        public async Task<IEnumerable<ServiceableLocationDto>> SearchByPincodeAsync(string pincode)
        {
            var locations = await _repository.SearchByPincodeAsync(pincode);
            return locations.Select(MapToDto);
        }

        public async Task<IEnumerable<ServiceableLocationDto>> SearchByCityAsync(string city)
        {
            var locations = await _repository.SearchByCityAsync(city);
            return locations.Select(MapToDto);
        }

        public async Task<IEnumerable<ServiceableLocationDto>> SearchByAreaAsync(string city, string area)
        {
            var locations = await _repository.SearchByAreaAsync(city, area);
            return locations.Select(MapToDto);
        }

        public async Task<ServiceableLocationDto> CreateAsync(CreateServiceableLocationDto dto)
        {
            var location = new ServiceableLocation
            {
                City = dto.City,
                Area = dto.Area,
                Locality = dto.Locality ?? string.Empty,
                LandmarkOrSociety = dto.LandmarkOrSociety ?? string.Empty,
                Pincode = dto.Pincode,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                DeliveryTimeSlot = dto.DeliveryTimeSlot,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(location);
            return MapToDto(created);
        }

        public async Task<ServiceableLocationDto> UpdateAsync(int id, UpdateServiceableLocationDto dto)
        {
            var location = await _repository.GetByIdAsync(id);
            if (location == null)
                throw new KeyNotFoundException($"Serviceable location with ID {id} not found");

            if (!string.IsNullOrEmpty(dto.City))
                location.City = dto.City;
            
            if (!string.IsNullOrEmpty(dto.Area))
                location.Area = dto.Area;
            
            if (dto.Locality != null)
                location.Locality = dto.Locality;
            
            if (dto.LandmarkOrSociety != null)
                location.LandmarkOrSociety = dto.LandmarkOrSociety;
            
            if (!string.IsNullOrEmpty(dto.Pincode))
                location.Pincode = dto.Pincode;
            
            if (dto.IsActive.HasValue)
                location.IsActive = dto.IsActive.Value;
            
            if (dto.Latitude.HasValue)
                location.Latitude = dto.Latitude;
            
            if (dto.Longitude.HasValue)
                location.Longitude = dto.Longitude;
            
            if (dto.DeliveryTimeSlot != null)
                location.DeliveryTimeSlot = dto.DeliveryTimeSlot;

            location.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(location);
            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<ValidateAddressDto> ValidateLocationAsync(int locationId)
        {
            var location = await _repository.GetByIdAsync(locationId);
            
            if (location == null)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "Location not found"
                };
            }

            if (!location.IsActive)
            {
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "This location is currently not serviceable"
                };
            }

            return new ValidateAddressDto
            {
                IsServiceable = true,
                Message = "Location is serviceable",
                ServiceableLocation = MapToDto(location)
            };
        }

        private ServiceableLocationDto MapToDto(ServiceableLocation location)
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
