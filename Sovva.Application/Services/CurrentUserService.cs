using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Sovva.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cache;
        private readonly ILogger<CurrentUserService> _logger;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository,
            ICacheService cache,
            ILogger<CurrentUserService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
            _cache = cache;
            _logger = logger;
        }

        public string? GetAuthId()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // ✅ METHOD 1: Try AuthMiddleware context items first
            var authIdFromMiddleware = context.Items["auth_id"]?.ToString();
            if (!string.IsNullOrEmpty(authIdFromMiddleware))
            {
                _logger.LogDebug("CurrentUserService: AuthId resolved via middleware context");
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
                    _logger.LogDebug("CurrentUserService: AuthId resolved via JWT claims");
                    return authIdFromClaims;
                }
            }

            _logger.LogWarning("⚠️ CurrentUserService: No authId found from any source");
            return null;
        }

        // Returns the currently logged-in user's UserId
        public async Task<int?> GetCurrentUserIdAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // ✅ NEW: Try sovva_user_id claim first (zero DB hit)
            var sovvaUserIdClaim = context.User.FindFirst(RoleConstants.SovvaUserId)?.Value;
            if (int.TryParse(sovvaUserIdClaim, out var sovvaUserId))
            {
                _logger.LogInformation("CurrentUserService: UserId from sovva_user_id claim: {UserId}", sovvaUserId);
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
            var cachedUserId = await _cache.GetAsync<int?>(cacheKey);
            if (cachedUserId.HasValue)
                return cachedUserId.Value;

            try
            {
                var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
                if (user == null) return null;

                await _cache.SetAsync(cacheKey, user.UserId, TimeSpan.FromMinutes(5));
                return user.UserId;
            }
            catch (Exception ex) when (ex is not System.Data.Common.DbException 
                                        and not TimeoutException
                                        and not OperationCanceledException)
            {
                _logger.LogWarning(ex, "CurrentUserService: Non-critical error resolving userId for authId {AuthId}", authId);
                return null;
            }
        }

        // Returns the currently logged-in user's details as UserDto
        public async Task<UserDto?> GetCurrentUserAsync()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return null;

            // ✅ Cache userId → User mapping for 5 minutes
            var cacheKey = $"user_{userId}";
            var cachedUser = await _cache.GetAsync<UserDto>(cacheKey);
            if (cachedUser != null)
                return cachedUser;

            var authId = GetAuthId();
            if (string.IsNullOrEmpty(authId) || !Guid.TryParse(authId, out var authGuid))
                return null;

            try
            {
                var user = await _userRepository.GetUserByAuthIdAsync(authGuid);
                if (user == null) return null;

                var userDto = new UserDto
                {
                    UserId = user.UserId,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    // WalletBalance omitted — always use GET /api/WalletTransactions/my-balance
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                };

                await _cache.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(5));
                return userDto;
            }
            catch (Exception ex) when (ex is not System.Data.Common.DbException 
                                        and not TimeoutException
                                        and not OperationCanceledException)
            {
                _logger.LogWarning(ex, "CurrentUserService: Non-critical error resolving user for authId {AuthId}", authId);
                return null;
            }
        }

        public async Task InvalidateCacheAsync(int userId)
        {
            await _cache.RemoveAsync($"user_{userId}");
            // We do NOT invalidate userid_{authId} because the authId -> userId mapping is immutable.
            _logger.LogInformation("CurrentUserService: Cache invalidated for user {UserId}", userId);
        }
    }
}
