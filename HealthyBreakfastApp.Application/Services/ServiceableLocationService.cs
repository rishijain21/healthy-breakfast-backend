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

        /// <summary>
        /// Returns ALL locations (active + inactive) — for admin panel
        /// </summary>
        public async Task<IEnumerable<ServiceableLocationDto>> GetAllAsync()
        {
            var locations = await _repository.GetAllAsync();
            return locations.Select(MapToDto);
        }

        /// <summary>
        /// Returns only active locations — for user-facing address selection
        /// </summary>
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

        /// <summary>
        /// FIX: Free-text search used by frontend LocationService.searchServiceableLocations(query)
        /// Searches across city, area, locality, landmark, pincode
        /// </summary>
        public async Task<IEnumerable<ServiceableLocationDto>> SearchByQueryAsync(string query)
        {
            var locations = await _repository.SearchByQueryAsync(query);
            return locations.Select(MapToDto);
        }

        public async Task<ServiceableLocationDto> CreateAsync(CreateServiceableLocationDto dto)
        {
            var location = new ServiceableLocation
            {
                City              = dto.City.Trim(),
                Area              = dto.Area.Trim(),
                Locality          = dto.Locality?.Trim() ?? string.Empty,
                LandmarkOrSociety = dto.LandmarkOrSociety?.Trim() ?? string.Empty,
                Pincode           = dto.Pincode.Trim(),
                Latitude          = dto.Latitude,
                Longitude         = dto.Longitude,
                DeliveryTimeSlot  = dto.DeliveryTimeSlot?.Trim(),
                IsActive          = true,
                CreatedAt         = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(location);
            return MapToDto(created);
        }

        public async Task<ServiceableLocationDto> UpdateAsync(int id, UpdateServiceableLocationDto dto)
        {
            var location = await _repository.GetByIdAsync(id);
            if (location == null)
                throw new KeyNotFoundException($"Serviceable location with ID {id} not found");

            // Patch only provided fields
            if (!string.IsNullOrWhiteSpace(dto.City))
                location.City = dto.City.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Area))
                location.Area = dto.Area.Trim();

            if (dto.Locality != null)
                location.Locality = dto.Locality.Trim();

            if (dto.LandmarkOrSociety != null)
                location.LandmarkOrSociety = dto.LandmarkOrSociety.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Pincode))
                location.Pincode = dto.Pincode.Trim();

            // IsActive — explicitly nullable so false is honoured (toggle fix)
            if (dto.IsActive.HasValue)
                location.IsActive = dto.IsActive.Value;

            if (dto.Latitude.HasValue)
                location.Latitude = dto.Latitude;

            if (dto.Longitude.HasValue)
                location.Longitude = dto.Longitude;

            if (dto.DeliveryTimeSlot != null)
                location.DeliveryTimeSlot = dto.DeliveryTimeSlot.Trim();

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
                return new ValidateAddressDto { IsServiceable = false, Message = "Location not found" };

            if (!location.IsActive)
                return new ValidateAddressDto
                {
                    IsServiceable = false,
                    Message = "This location is currently not serviceable"
                };

            return new ValidateAddressDto
            {
                IsServiceable       = true,
                Message             = "Location is serviceable",
                ServiceableLocation = MapToDto(location)
            };
        }

        private static ServiceableLocationDto MapToDto(ServiceableLocation location) =>
            new()
            {
                Id                = location.Id,
                City              = location.City,
                Area              = location.Area,
                Locality          = location.Locality,
                LandmarkOrSociety = location.LandmarkOrSociety,
                Pincode           = location.Pincode,
                IsActive          = location.IsActive,
                Latitude          = location.Latitude,
                Longitude         = location.Longitude,
                DeliveryTimeSlot  = location.DeliveryTimeSlot,
                FullAddress       = location.FullAddress,
                CreatedAt         = location.CreatedAt
            };
    }
}