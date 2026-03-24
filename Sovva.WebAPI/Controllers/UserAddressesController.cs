using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;          // ✅ ADD
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserAddressesController : ControllerBase
    {
        private readonly IUserAddressService _addressService;
        // ✅ REMOVED: ICurrentUserService

        public UserAddressesController(IUserAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserAddressDetailDto>>> GetMyAddresses()
        {
            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized(new { message = "User not authenticated" });

            var addresses = await _addressService.GetActiveUserAddressesAsync(userId.Value);
            return Ok(addresses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> GetById(int id)
        {
            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized(new { message = "User not authenticated" });

            var address = await _addressService.GetByIdAsync(id);
            if (address == null)
                return NotFound(new { message = "Address not found" });

            if (address.UserId != userId.Value)
                return Forbid();

            return Ok(address);
        }

        [HttpGet("primary")]
        public async Task<ActionResult<UserAddressDetailDto>> GetPrimaryAddress()
        {
            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized(new { message = "User not authenticated" });

            var address = await _addressService.GetPrimaryAddressAsync(userId.Value);
            if (address == null)
                return NotFound(new { message = "No primary address found. Please add an address." });

            return Ok(address);
        }

        [HttpPost]
        public async Task<ActionResult<UserAddressDetailDto>> Create([FromBody] CreateUserAddressDto dto)
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized(new { message = "User not authenticated" });

                var created = await _addressService.CreateAsync(userId.Value, dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserAddressDetailDto>> Update(int id, [FromBody] UpdateUserAddressDto dto)
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized(new { message = "User not authenticated" });

                var updated = await _addressService.UpdateAsync(userId.Value, id, dto);
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

        [HttpPut("{id}/set-primary")]
        public async Task<ActionResult<UserAddressDetailDto>> SetPrimary(int id)
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _addressService.SetPrimaryAddressAsync(userId.Value, id);
                if (!result)
                    return NotFound(new { message = "Address not found" });

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

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _addressService.DeleteAsync(userId.Value, id);
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

        [HttpGet("{id}/validate")]
        public async Task<ActionResult<ValidateAddressDto>> ValidateAddress(int id)
        {
            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _addressService.ValidateAddressChangeAsync(userId.Value, id);
            return Ok(result);
        }
    }
}