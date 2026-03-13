using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Check if a user exists in the database by email
        /// Returns a vague response to prevent user enumeration attacks
        /// </summary>
        [HttpGet("check-user-exists")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult> CheckUserExists([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(ApiResponse<object>.Fail("Email is required"));
            }

            // ✅ FIX 5: Prevent user enumeration - don't reveal whether user exists
            // Return same response regardless of outcome
            try
            {
                await _userService.UserExistsAsync(email);
                
                // Always return the same response
                return Ok(ApiResponse<object>.Ok(new { message = "If this email is registered, you will receive further instructions." }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence for email: {Email}", email);
                return StatusCode(500, ApiResponse<object>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Register a new user in the database after Supabase OTP verification
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // ✅ CRITICAL: Verify the AuthId matches the token — prevents registering as someone else
            var tokenAuthId = User.FindFirst("sub")?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(tokenAuthId) || tokenAuthId != request.AuthId.ToString())
            {
                _logger.LogWarning("Registration authId mismatch. Token: {TokenId}, Request: {RequestId}",
                    tokenAuthId, request.AuthId);
                return Forbid(); // 403
            }

            _logger.LogInformation(
                "Registering new user - AuthId: {AuthId}, Email: {Email}, Name: {Name}",
                request.AuthId, request.Email, request.Name
            );

            try
            {
                // ✅ Check if user already exists by AuthId
                var existingUserByAuth = await _userService.GetUserByAuthIdAsync(request.AuthId);
                if (existingUserByAuth != null)
                {
                    _logger.LogInformation("User already exists with AuthId, returning existing user: {UserId}", existingUserByAuth.UserId);
                    return Ok(ApiResponse<object>.Ok(
                        new { user = existingUserByAuth, isNewUser = false },
                        "User already registered"));
                }

                // ✅ Check if user already exists by Email
                var existingUserByEmail = await _userService.GetUserByEmailAsync(request.Email);
                if (existingUserByEmail != null)
                {
                    _logger.LogWarning("User already exists with Email: {Email}", request.Email);
                    return Conflict(ApiResponse<object>.Fail("Email already registered. Please login instead."));
                }

                // ✅ Create new user
                var userDto = await _userService.RegisterUserAsync(request);

                _logger.LogInformation(
                    "✅ User registered successfully - UserId: {UserId}, Email: {Email}",
                    userDto.UserId, userDto.Email
                );

                return Ok(ApiResponse<object>.Ok(
                    new { user = userDto, isNewUser = true },
                    "User registered successfully"));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Registration failed - {Message}", ex.Message);
                return Conflict(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                return StatusCode(500, ApiResponse<object>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Test login endpoint (can be removed in production)
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);
            
            // This is just a test endpoint - Supabase handles real authentication
            return Ok(ApiResponse<object>.Ok(new { message = "Use Supabase authentication for login" }));
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
