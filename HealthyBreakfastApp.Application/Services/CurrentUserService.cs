using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace HealthyBreakfastApp.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<CurrentUserService> _logger;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository,
            ILogger<CurrentUserService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
            _logger = logger;
        }

        // ✅ ENHANCED: Retrieves the auth_id from multiple sources
        public string? GetAuthId()
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                // ✅ METHOD 1: Try AuthMiddleware context items first
                var authIdFromMiddleware = context.Items["auth_id"]?.ToString();
                if (!string.IsNullOrEmpty(authIdFromMiddleware))
                {
                    _logger.LogInformation($"✅ CurrentUserService: AuthId from middleware: {authIdFromMiddleware}");
                    return authIdFromMiddleware;
                }

                // ✅ METHOD 2: Try JWT claims directly (fallback)
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var authIdFromClaims = context.User.FindFirst("sub")?.Value 
                                        ?? context.User.FindFirst("user_id")?.Value 
                                        ?? context.User.FindFirst("id")?.Value
                                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (!string.IsNullOrEmpty(authIdFromClaims))
                    {
                        _logger.LogInformation($"✅ CurrentUserService: AuthId from JWT claims: {authIdFromClaims}");
                        return authIdFromClaims;
                    }
                }

                _logger.LogWarning("⚠️ CurrentUserService: No authId found from any source");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ CurrentUserService GetAuthId error: {ex.Message}");
                return null;
            }
        }

        // Returns the currently logged-in user's UserId
        public async Task<int?> GetCurrentUserIdAsync()
        {
            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId))
                return null;

            if (!Guid.TryParse(authId, out var authGuid))
                return null;

            var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
            return user?.UserId;
        }

        // Returns the currently logged-in user's details as UserDto
        public async Task<UserDto?> GetCurrentUserAsync()
        {
            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId))
                return null;

            if (!Guid.TryParse(authId, out var authGuid))
                return null;

            var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
            if (user == null)
                return null;

            return new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                WalletBalance = user.WalletBalance,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}
