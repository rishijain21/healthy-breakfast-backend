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

        public ServiceableLocationsController(
            IServiceableLocationService service,
            ILogger<ServiceableLocationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ══════════════════════════════════════════════════════
        // PUBLIC ENDPOINTS (users + admin)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Get all ACTIVE serviceable locations (for users selecting delivery address)
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
        /// Search locations by pincode (active only)
        /// </summary>
        [HttpGet("search/pincode/{pincode}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> SearchByPincode(string pincode)
        {
            var locations = await _service.SearchByPincodeAsync(pincode);
            return Ok(locations);
        }

        /// <summary>
        /// Search locations by city (active only)
        /// </summary>
        [HttpGet("search/city/{city}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> SearchByCity(string city)
        {
            var locations = await _service.SearchByCityAsync(city);
            return Ok(locations);
        }

        /// <summary>
        /// Search locations by free-text query across city, area, locality, landmark, pincode (active only)
        /// FIX: Frontend LocationService hits GET /search?query=... — this replaces the broken city+area version
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> Search(
            [FromQuery] string? query,
            [FromQuery] string? city,
            [FromQuery] string? area)
        {
            // Free-text search from frontend LocationService.searchServiceableLocations()
            if (!string.IsNullOrWhiteSpace(query))
            {
                var results = await _service.SearchByQueryAsync(query);
                return Ok(results);
            }

            // Legacy city+area search (keep for backwards compatibility)
            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(area))
            {
                var results = await _service.SearchByAreaAsync(city, area);
                return Ok(results);
            }

            // No params — return all active
            var all = await _service.GetActiveLocationsAsync();
            return Ok(all);
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

        // ══════════════════════════════════════════════════════
        // ADMIN ENDPOINTS
        // BUG FIX: Removed [Authorize(Roles = "Admin")] — Supabase JWT
        // role claim is "authenticated", not "Admin". Role check is handled
        // by AuthMiddleware which injects the app-level role.
        // Use [Authorize] + manual role check via HttpContext.Items["UserRole"].
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Get ALL locations including inactive (Admin only)
        /// BUG FIX: Old GetAll() only returned active — admin needs all zones
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ServiceableLocationDto>>> GetAllForAdmin()
        {
            if (!IsAdmin())
                return Forbid();

            var locations = await _service.GetAllAsync();
            return Ok(locations);
        }

        /// <summary>
        /// Create new serviceable location (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ServiceableLocationDto>> Create(
            [FromBody] CreateServiceableLocationDto dto)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                _logger.LogInformation(
                    "Admin creating serviceable location: {City} {Pincode}", dto.City, dto.Pincode);

                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating serviceable location");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update serviceable location (Admin only)
        /// Handles both full updates AND toggle-status (IsActive field)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<ServiceableLocationDto>> Update(
            int id,
            [FromBody] UpdateServiceableLocationDto dto)
        {
            if (!IsAdmin())
                return Forbid();

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
                _logger.LogError(ex, "Error updating serviceable location {Id}", id);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete serviceable location (Admin only)
        /// Note: If location has user addresses linked, it soft-deletes (sets IsActive=false)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (!IsAdmin())
                return Forbid();

            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFound(new { message = "Serviceable location not found" });

            _logger.LogWarning("Admin deleted serviceable location: {Id}", id);
            return Ok(new { message = "Serviceable location deleted successfully" });
        }

        // ══════════════════════════════════════════════════════
        // HELPER: Read role from UserDto set by AuthMiddleware
        // AuthMiddleware sets HttpContext.Items["User"] to UserDto before
        // calling _next(context), so it's available at action execution time.
        // ══════════════════════════════════════════════════════
        private bool IsAdmin()
        {
            var user = HttpContext.Items["User"] as HealthyBreakfastApp.Application.DTOs.UserDto;
            return string.Equals(user?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}