using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserAddressesController : ControllerBase
    {
        private readonly IUserAddressService _addressService;
        private readonly ICurrentUserService _currentUserService;

        public UserAddressesController(
            IUserAddressService addressService,
            ICurrentUserService currentUserService)
        {
            _addressService = addressService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Get all addresses for current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserAddressDetailDto>>> GetMyAddresses()
        {
            var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
            if (!userIdResult.HasValue)
                return Unauthorized(new { message = "User not authenticated" });

            var userId = userIdResult.Value;
            var addresses = await _addressService.GetActiveUserAddressesAsync(userId);
            return Ok(addresses);
        }

        /// <summary>
        /// Get specific address by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> GetById(int id)
        {
            var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
            if (!userIdResult.HasValue)
                return Unauthorized(new { message = "User not authenticated" });

            var userId = userIdResult.Value;
            var address = await _addressService.GetByIdAsync(id);
            
            if (address == null)
                return NotFound(new { message = "Address not found" });

            if (address.UserId != userId)
                return Forbid();

            return Ok(address);
        }

        /// <summary>
        /// Get primary address for current user
        /// </summary>
        [HttpGet("primary")]
        public async Task<ActionResult<UserAddressDetailDto>> GetPrimaryAddress()
        {
            var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
            if (!userIdResult.HasValue)
                return Unauthorized(new { message = "User not authenticated" });

            var userId = userIdResult.Value;
            var address = await _addressService.GetPrimaryAddressAsync(userId);
            
            if (address == null)
                return NotFound(new { message = "No primary address found. Please add an address." });

            return Ok(address);
        }

        /// <summary>
        /// Create new address
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UserAddressDetailDto>> Create(
            [FromBody] CreateUserAddressDto dto)
        {
            try
            {
                var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
                if (!userIdResult.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                var userId = userIdResult.Value;
                var created = await _addressService.CreateAsync(userId, dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update existing address
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> Update(
            int id, 
            [FromBody] UpdateUserAddressDto dto)
        {
            try
            {
                var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
                if (!userIdResult.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                var userId = userIdResult.Value;
                var updated = await _addressService.UpdateAsync(userId, id, dto);
                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Set address as primary
        /// </summary>
        [HttpPut("{id}/set-primary")]
        public async Task<ActionResult<UserAddressDetailDto>> SetPrimary(int id)
        {
            try
            {
                var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
                if (!userIdResult.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                var userId = userIdResult.Value;
                var result = await _addressService.SetPrimaryAddressAsync(userId, id);
                
                if (!result)
                    return NotFound(new { message = "Address not found" });

                // Return the full updated address so frontend can sync state
                var updated = await _addressService.GetByIdAsync(id);
                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Delete address
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
                if (!userIdResult.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                var userId = userIdResult.Value;
                var result = await _addressService.DeleteAsync(userId, id);
                
                if (!result)
                    return NotFound(new { message = "Address not found" });

                return Ok(new { message = "Address deleted successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Validate address for delivery
        /// </summary>
        [HttpGet("{id}/validate")]
        public async Task<ActionResult<ValidateAddressDto>> ValidateAddress(int id)
        {
            var userIdResult = await _currentUserService.GetCurrentUserIdAsync();
            if (!userIdResult.HasValue)
                return Unauthorized(new { message = "User not authenticated" });

            var userId = userIdResult.Value;
            var result = await _addressService.ValidateAddressChangeAsync(userId, id);
            return Ok(result);
        }
    }
}
