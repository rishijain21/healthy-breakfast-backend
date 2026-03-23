using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Sovva.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CurrentUserService> _logger;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository,
            IMemoryCache cache,
            ILogger<CurrentUserService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
            _cache = cache;
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
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                // ✅ NEW: Try sovva_user_id claim first (zero DB hit)
                var sovvaUserIdClaim = context.User.FindFirst("sovva_user_id")?.Value;
                if (int.TryParse(sovvaUserIdClaim, out var sovvaUserId))
                {
                    _logger.LogInformation($"✅ CurrentUserService: UserId from sovva_user_id claim: {sovvaUserId}");
                    return sovvaUserId;
                }

                // Fallback: Get authId and lookup user (for backwards compatibility)
                var authId = GetAuthId();
                if (string.IsNullOrEmpty(authId))
                    return null;

                if (!Guid.TryParse(authId, out var authGuid))
                    return null;

                // Cache authId → UserId mapping for 5 minutes
                var cacheKey = $"userid_{authId}";
                if (_cache.TryGetValue(cacheKey, out int cachedUserId))
                    return cachedUserId;

                var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
                if (user == null) return null;

                _cache.Set(cacheKey, user.UserId, TimeSpan.FromMinutes(5));
                return user.UserId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ CurrentUserService GetCurrentUserIdAsync error: {ex.Message}");
                return null;
            }
        }

        // Returns the currently logged-in user's details as UserDto
        public async Task<UserDto?> GetCurrentUserAsync()
        {
            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId))
                return null;

            if (!Guid.TryParse(authId, out var authGuid))
                return null;

            // ✅ Cache authId → User mapping for 5 minutes
            var cacheKey = $"user_{authId}";
            if (_cache.TryGetValue(cacheKey, out UserDto? cachedUser))
                return cachedUser;

            var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
            if (user == null) return null;

            var userDto = new UserDto
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                WalletBalance = user.WalletBalance,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            _cache.Set(cacheKey, userDto, TimeSpan.FromMinutes(5));
            return userDto;
        }
    }
}
