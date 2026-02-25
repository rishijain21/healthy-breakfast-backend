using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<UserDto>>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetUserById), new { id = userId }, null);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
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

        // ✅ ADMIN: Update user role
        /// <summary>
        /// Promote or demote a user's role — Admin only
        /// </summary>
        [HttpPatch("{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
        {
            var validRoles = new[] { "User", "Admin" };
            if (!validRoles.Contains(dto.Role))
                return BadRequest(new { message = $"Invalid role. Must be one of: {string.Join(", ", validRoles)}" });

            // Prevent self-demotion
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                 ?? User.FindFirst("sub")?.Value;
            
            if (!string.IsNullOrEmpty(currentUserIdClaim) && Guid.TryParse(currentUserIdClaim, out var currentAuthId))
            {
                var currentUser = await _userService.GetUserByAuthIdAsync(currentAuthId);
                if (currentUser != null && currentUser.UserId == id && dto.Role != "Admin")
                    return BadRequest(new { message = "You cannot remove your own admin role" });
            }

            var result = await _userService.UpdateUserRoleAsync(id, dto.Role);
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = $"User {id} role updated to {dto.Role}" });
        }
    }
}
