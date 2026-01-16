using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetUserById), new { id = userId }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var userDto = await _userService.GetUserByIdAsync(id);
            if (userDto == null)
                return NotFound();

            return Ok(userDto);
        }

        // ✅ NEW: Get current user's profile
        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetUserProfile()
        {
            try
            {
                // Get AuthId from JWT token (sub claim)
                var authIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                               ?? User.FindFirst("sub")?.Value;
                
                if (string.IsNullOrEmpty(authIdClaim) || !Guid.TryParse(authIdClaim, out Guid authId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                var userDto = await _userService.GetUserProfileByAuthIdAsync(authId);
                if (userDto == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user profile");
                return StatusCode(500, new { message = "An error occurred while fetching profile" });
            }
        }

        // ✅ NEW: Update current user's profile
        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<UserDto>> UpdateUserProfile([FromBody] UpdateUserProfileDto updateDto)
        {
            try
            {
                // Get AuthId from JWT token
                var authIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                               ?? User.FindFirst("sub")?.Value;
                
                if (string.IsNullOrEmpty(authIdClaim) || !Guid.TryParse(authIdClaim, out Guid authId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                // Validation
                if (updateDto.Name != null && string.IsNullOrWhiteSpace(updateDto.Name))
                {
                    return BadRequest(new { message = "Name cannot be empty" });
                }

                var updatedUser = await _userService.UpdateUserProfileAsync(authId, updateDto);
                return Ok(updatedUser);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(500, new { message = "An error occurred while updating profile" });
            }
        }
    }
}
