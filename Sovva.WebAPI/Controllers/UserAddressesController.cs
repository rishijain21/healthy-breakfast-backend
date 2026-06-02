using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;          // ✅ ADD
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserAddressDetailDto>>> GetMyAddresses()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var addresses = await _addressService.GetActiveUserAddressesAsync(userId.Value);
            return Ok(ApiResponse.Ok(addresses));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> GetById(int id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var address = await _addressService.GetByIdAsync(id);
            if (address == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Address not found"));

            if (address.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            return Ok(ApiResponse.Ok(address));
        }

        [HttpGet("primary")]
        public async Task<ActionResult<UserAddressDetailDto>> GetPrimaryAddress()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var address = await _addressService.GetPrimaryAddressAsync(userId.Value);
            if (address == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "No primary address found. Please add an address."));

            return Ok(ApiResponse.Ok(address));
        }

        [HttpPost]
        public async Task<ActionResult<UserAddressDetailDto>> Create([FromBody] CreateUserAddressDto dto)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var created = await _addressService.CreateAsync(userId.Value, dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse.Ok(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> Update(int id, [FromBody] UpdateUserAddressDto dto)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var updated = await _addressService.UpdateAsync(userId.Value, id, dto);
                return Ok(ApiResponse.Ok(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", ex.Message));
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));
            }
        }

        [HttpPut("{id}/set-primary")]
        public async Task<ActionResult<UserAddressDetailDto>> SetPrimary(int id)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var result = await _addressService.SetPrimaryAddressAsync(userId.Value, id);
                if (!result)
                    return NotFound(ApiResponse.Fail("NOT_FOUND", "Address not found"));

                var updated = await _addressService.GetByIdAsync(id);
                return Ok(ApiResponse.Ok(updated));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", ex.Message));
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var result = await _addressService.DeleteAsync(userId.Value, id);
                if (!result)
                    return NotFound(ApiResponse.Fail("NOT_FOUND", "Address not found"));

                return Ok(ApiResponse.Ok(new { message = "Address deleted successfully" }));
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        [HttpGet("{id}/validate")]
        public async Task<ActionResult<ValidateAddressDto>> ValidateAddress(int id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var result = await _addressService.ValidateAddressChangeAsync(userId.Value, id);
            return Ok(ApiResponse.Ok(result));
        }
    }
}