using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        /// </summary>
        [HttpGet("check-user-exists")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> CheckUserExists([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            _logger.LogInformation("Checking if user exists with email: {Email}", email);

            try
            {
                var exists = await _userService.UserExistsAsync(email);
                _logger.LogInformation("User exists check for {Email}: {Exists}", email, exists);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence for email: {Email}", email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Register a new user in the database after Supabase OTP verification
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation(
                "Registering new user - AuthId: {AuthId}, Email: {Email}, Name: {Name}",
                request.AuthId, request.Email, request.Name
            );

            try
            {
                var userDto = await _userService.RegisterUserAsync(request);

                _logger.LogInformation(
                    "User registered successfully - UserId: {UserId}, Email: {Email}",
                    userDto.UserId, userDto.Email
                );

                return CreatedAtAction(
                    nameof(Register),
                    new { id = userDto.UserId },
                    userDto
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Registration failed - {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Test login endpoint (can be removed in production)
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);
            
            // This is just a test endpoint - Supabase handles real authentication
            return Ok(new { message = "Use Supabase authentication for login" });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
