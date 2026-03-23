using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, IDashboardService dashboardService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _dashboardService = dashboardService;
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

        // ✅ NEW: Dashboard Summary - aggregates all user data for fast login bootstrap
        /// <summary>
        /// Get dashboard summary - runs 5 parallel queries for fast response
        /// Returns: profile, wallet balance, recent transactions, active subscriptions, tomorrow's orders
        /// </summary>
        [HttpGet("dashboard-summary")]
        [Authorize]
        [ResponseCache(Duration = 60, VaryByHeader = "Authorization")]
        public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(CancellationToken ct)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(ct);
                if (userId == null)
                    return Unauthorized(new { message = "User not authenticated" });

                var summary = await _dashboardService.GetDashboardSummaryAsync(userId.Value, ct);
                return Ok(summary);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard summary");
                return StatusCode(500, new { message = "An error occurred while fetching dashboard" });
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
            // ✅ NEW: Zero DB hit - read userId directly from JWT claim
            var currentUserId = User.GetSovvaUserId();
            if (currentUserId.HasValue && currentUserId.Value == id && dto.Role != "Admin")
                return BadRequest(new { message = "You cannot remove your own admin role" });

            var result = await _userService.UpdateUserRoleAsync(id, dto.Role);
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = $"User {id} role updated to {dto.Role}" });
        }

        // Helper to extract user ID from JWT claims (fast path + fallback for old tokens)
        private async Task<int?> GetCurrentUserIdAsync(CancellationToken ct = default)
        {
            // ✅ Fast path — JWT claim, zero DB
            var claim = User.FindFirst("sovva_user_id")?.Value;
            if (int.TryParse(claim, out var userId))
                return userId;

            // Fallback for old tokens (remove after all users re-login once)
            var authIdStr = User.FindFirst("sub")?.Value
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(authIdStr, out var authId)) return null;
            var user = await _userService.GetUserByAuthIdAsync(authId);
            return user?.UserId;
        }
    }
}
