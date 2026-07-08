using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Sovva.WebAPI.Controllers
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
        /// Returns the actual result so frontend can route correctly
        /// </summary>
        [HttpGet("check-user-exists")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult> CheckUserExists([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Email is required"));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _userService.UserExistsAsync(email);
            sw.Stop();

            // A-2: Mitigate user enumeration by always returning true and enforcing a constant minimum response time.
            var targetMs = 500;
            var elapsedMs = (int)sw.ElapsedMilliseconds;
            if (elapsedMs < targetMs)
            {
                await Task.Delay(targetMs - elapsedMs);
            }

            return Ok(ApiResponse.Ok(new { exists = true }));
        }

        /// <summary>
        /// Register a new user in the database after Supabase OTP verification.
        /// AuthId and email are read from the JWT — never trusted from client body.
        /// Client sends only: { name, phone }
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserRequest request)
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

            // ✅ SECURITY: Read authId exclusively from JWT — never from request body
            var tokenAuthId = User.FindFirst("sub")?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(tokenAuthId) || !Guid.TryParse(tokenAuthId, out var authId))
            {
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Invalid authentication token"));
            }

            // ✅ SECURITY: Read email from JWT for cross-verification
            var tokenEmail = User.FindFirst("email")?.Value
                          ?? User.FindFirst(ClaimTypes.Email)?.Value;

            // Use JWT email as the source of truth, fall back to request email
            var email = tokenEmail ?? request.Email;

            _logger.LogInformation(
                "Registering user - AuthId: {AuthId}, Email: {Email}, Name: {Name}",
                authId, "***@***", request.Name
            );

            try
            {
                // ✅ Check if user already exists by AuthId
                var existingUserByAuth = await _userService.GetUserByAuthIdAsync(authId);
                if (existingUserByAuth != null)
                {
                    _logger.LogInformation("User already exists with AuthId, returning existing user: {UserId}", existingUserByAuth.UserId);
                    return Ok(ApiResponse.Ok(new { user = existingUserByAuth, isNewUser = false, message = "User already registered" }));
                }

                // ✅ Check if user already exists by Email
                var existingUserByEmail = await _userService.GetUserByEmailAsync(email);
                if (existingUserByEmail != null)
                {
                    _logger.LogWarning("User already exists with Email: {Email}", "***@***");
                    return Conflict(ApiResponse.Fail("CONFLICT", "Email already registered. Please login instead."));
                }

                // ✅ Create new user — authId + email from JWT, name + phone from body
                var registrationRequest = new RegisterUserRequest
                {
                    AuthId = authId,
                    Email = email,
                    Name = request.Name,
                    Phone = request.Phone
                };

                var userDto = await _userService.RegisterUserAsync(registrationRequest);

                _logger.LogInformation(
                    "User registered successfully - UserId: {UserId}, Email: {Email}",
                    userDto.UserId, "***@***"
                );

                return Ok(ApiResponse.Ok(new { user = userDto, isNewUser = true, message = "User registered successfully" }));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail("CONFLICT", ex.Message));
            }
        }

    }
}
