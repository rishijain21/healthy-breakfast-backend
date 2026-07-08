using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ISupabaseStorageService _storageService;

        public UsersController(IUserService userService, IDashboardService dashboardService, ILogger<UsersController> logger, ISupabaseStorageService storageService)
        {
            _userService = userService;
            _dashboardService = dashboardService;
            _logger = logger;
            _storageService = storageService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (pageSize > 100)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Maximum page size for users is 100."));
            }

            var usersResult = await _userService.GetAllUsersAsync(page, pageSize);
            return Ok(ApiResponse.Ok(usersResult));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<List<string>>
                    .Fail("VALIDATION_ERROR", string.Join("; ", errors)));
            }

            var userId = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetUserById), new { id = userId }, null);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var userDto = await _userService.GetUserByIdAsync(id);
            if (userDto == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            return Ok(ApiResponse.Ok(userDto));
        }

        // ✅ NEW: Get current user's profile
        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetUserProfile()
        {
            // Get AuthId from JWT token (sub claim)
            var authIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
            
            if (string.IsNullOrEmpty(authIdClaim) || !Guid.TryParse(authIdClaim, out Guid authId))
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Invalid user token"));
            }

            var userDto = await _userService.GetUserProfileByAuthIdAsync(authId);
            if (userDto == null)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", "User not found"));
            }

            return Ok(ApiResponse.Ok(userDto));
        }

        // ✅ NEW: Dashboard Summary - aggregates all user data for fast login bootstrap
        /// <summary>
        /// Get dashboard summary - runs 5 parallel queries for fast response
        /// Returns: profile, wallet balance, recent transactions, active subscriptions, tomorrow's orders
        /// </summary>
        [HttpGet("dashboard-summary")]
        [Authorize]
        public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(CancellationToken ct)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(ct);
                if (userId == null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var summary = await _dashboardService.GetDashboardSummaryAsync(userId.Value, ct);

                // ✅ FIX: Sign raw storage paths for subscription meal images so frontend can render them
                if (summary.ActiveSubscriptions != null)
                {
                    foreach (var sub in summary.ActiveSubscriptions)
                    {
                        if (!string.IsNullOrEmpty(sub.MealImageUrl))
                        {
                            try { sub.MealImageUrl = await _storageService.GetSignedUrlAsync(sub.MealImageUrl); }
                            catch { sub.MealImageUrl = null; }
                        }
                    }
                }

                // ✅ FIX: Sign raw storage paths for tomorrow's order meal images
                if (summary.TomorrowOrders != null)
                {
                    foreach (var order in summary.TomorrowOrders)
                    {
                        if (!string.IsNullOrEmpty(order.MealImageUrl))
                        {
                            try { order.MealImageUrl = await _storageService.GetSignedUrlAsync(order.MealImageUrl); }
                            catch { order.MealImageUrl = null; }
                        }
                    }
                }

                return Ok(ApiResponse.Ok(summary));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", ex.Message));
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
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Invalid user token"));
                }

                // Validation
                if (updateDto.Name != null && string.IsNullOrWhiteSpace(updateDto.Name))
                {
                    return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Name cannot be empty"));
                }

                var updatedUser = await _userService.UpdateUserProfileAsync(authId, updateDto);
                return Ok(ApiResponse.Ok(updatedUser));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiResponse.Fail("NOT_FOUND", ex.Message));
            }
        }

        // ✅ ACCOUNT DELETION
        /// <summary>
        /// Soft deletes the authenticated user's account
        /// </summary>
        [HttpDelete("account")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));
            }

            var success = await _userService.DeleteAccountAsync(userId.Value);
            if (!success) return NotFound(ApiResponse.Fail("NOT_FOUND", "User not found"));

            return Ok(ApiResponse.Ok(new { message = "Account deleted successfully" }));
        }

        // ✅ ADMIN: Update user role
        /// <summary>
        /// Promote or demote a user's role — Admin only
        /// </summary>
        [HttpPatch("{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
        {
            var validRoles = new[] { "User", RoleConstants.Admin };
            if (!validRoles.Contains(dto.Role))
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", $"Invalid role. Must be one of: {string.Join(", ", validRoles)}"));

            // ✅ NEW: Zero DB hit - read userId directly from JWT claim
            var currentUserId = await GetCurrentUserIdAsync();
            if (currentUserId.HasValue && currentUserId.Value == id && dto.Role != RoleConstants.Admin)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "You cannot remove your own admin role"));

            var result = await _userService.UpdateUserRoleAsync(id, dto.Role);
            if (!result)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "User not found"));

            return Ok(ApiResponse.Ok(new { message = $"User {id} role updated to {dto.Role}" }));
        }

        // Helper to extract user ID from JWT claims (fast path + fallback for old tokens)
        private async Task<int?> GetCurrentUserIdAsync(CancellationToken ct = default)
        {
            // ✅ Fast path — JWT claim, zero DB
            var claim = User.FindFirst(RoleConstants.SovvaUserId)?.Value;
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
