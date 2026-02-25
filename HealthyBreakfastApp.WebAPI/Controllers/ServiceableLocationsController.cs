using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceableLocationsController : ControllerBase
    {
        private readonly IServiceableLocationService _service;
        private readonly ILogger<ServiceableLocationsController> _logger;

        public ServiceableLocationsController(IServiceableLocationService service, ILogger<ServiceableLocationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Get all active serviceable locations
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> GetAll()
        {
            var locations = await _service.GetActiveLocationsAsync();
            return Ok(locations);
        }

        /// <summary>
        /// Get serviceable location by ID
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ServiceableLocationDto>> GetById(int id)
        {
            var location = await _service.GetByIdAsync(id);
            if (location == null)
                return NotFound(new { message = "Serviceable location not found" });

            return Ok(location);
        }

        /// <summary>
        /// Search locations by pincode
        /// </summary>
        [HttpGet("search/pincode/{pincode}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> SearchByPincode(string pincode)
        {
            var locations = await _service.SearchByPincodeAsync(pincode);
            return Ok(locations);
        }

        /// <summary>
        /// Search locations by city
        /// </summary>
        [HttpGet("search/city/{city}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> SearchByCity(string city)
        {
            var locations = await _service.SearchByCityAsync(city);
            return Ok(locations);
        }

        /// <summary>
        /// Search locations by city and area
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> SearchByArea(
            [FromQuery] string city, 
            [FromQuery] string area)
        {
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(area))
                return BadRequest(new { message = "City and area are required" });

            var locations = await _service.SearchByAreaAsync(city, area);
            return Ok(locations);
        }

        /// <summary>
        /// Validate if a location is serviceable
        /// </summary>
        [HttpGet("validate/{locationId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ValidateAddressDto>> ValidateLocation(int locationId)
        {
            var result = await _service.ValidateLocationAsync(locationId);
            return Ok(result);
        }

        // ========================================
        // ADMIN ENDPOINTS
        // ========================================

        /// <summary>
        /// Create new serviceable location (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ServiceableLocationDto>> Create(
            [FromBody] CreateServiceableLocationDto dto)
        {
            try
            {
                _logger.LogInformation("Admin creating serviceable location: {City} {Pincode}", dto.City, dto.Pincode);
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update serviceable location (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ServiceableLocationDto>> Update(
            int id, 
            [FromBody] UpdateServiceableLocationDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete serviceable location (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFound(new { message = "Serviceable location not found" });

            _logger.LogWarning("Admin deleting serviceable location: {Id}", id);
            return Ok(new { message = "Serviceable location deleted successfully" });
        }
    }
}
