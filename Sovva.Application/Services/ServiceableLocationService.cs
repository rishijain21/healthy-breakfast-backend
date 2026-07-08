using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class ServiceableLocationService : IServiceableLocationService
    {
        private readonly IServiceableLocationRepository _repository;
        private readonly ICacheService _cacheService;

        private const string ActiveLocationsCacheKey = "locations:all:active";
        private const string LocationByIdCacheKeyPrefix = "locations:id:";

        public ServiceableLocationService(IServiceableLocationRepository repository, ICacheService cacheService)
        {
            _repository = repository;
            _cacheService = cacheService;
        }

        public async Task<ServiceableLocationDto?> GetByIdAsync(int id)
        {
            var cacheKey = LocationByIdCacheKeyPrefix + id;
            var cached = await _cacheService.GetAsync<ServiceableLocationDto>(cacheKey);
            if (cached != null) return cached;

            var location = await _repository.GetByIdAsync(id);
            var result = location == null ? null : MapToDto(location);

            if (result != null)
            {
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60));
            }

            return result;
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
            var cached = await _cacheService.GetAsync<IEnumerable<ServiceableLocationDto>>(ActiveLocationsCacheKey);
            if (cached != null) return cached;

            var locations = await _repository.GetActiveLocationsAsync();
            var result = locations.Select(MapToDto).ToList();

            await _cacheService.SetAsync(ActiveLocationsCacheKey, result, TimeSpan.FromMinutes(15));
            return result;
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
                IsActive          = true
            };

            var created = await _repository.CreateAsync(location);

            await _cacheService.RemoveAsync(ActiveLocationsCacheKey);
            await _cacheService.RemoveAsync(LocationByIdCacheKeyPrefix + created.Id);

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



            var updated = await _repository.UpdateAsync(location);

            await _cacheService.RemoveAsync(ActiveLocationsCacheKey);
            await _cacheService.RemoveAsync(LocationByIdCacheKeyPrefix + updated.Id);

            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var result = await _repository.DeleteAsync(id);
            if (result)
            {
                await _cacheService.RemoveAsync(ActiveLocationsCacheKey);
                await _cacheService.RemoveAsync(LocationByIdCacheKeyPrefix + id);
            }
            return result;
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