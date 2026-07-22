using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Sovva.WebAPI.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AuthMiddleware> _logger;

        public AuthMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<AuthMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // ✅ Always pass CORS preflight (OPTIONS) requests through without auth processing.
            // Preflights carry no Authorization header and are handled by UseCors earlier in the pipeline.
            if (context.Request.Method == HttpMethods.Options)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value?.ToLower() ?? "";
            var publicEndpoints = new[]
            {
                "/swagger",
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/check-user-exists",
                "/api/scheduledorders/time-until-midnight"
            };

            if (publicEndpoints.Any(endpoint => context.Request.Path.StartsWithSegments(endpoint, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api")
                && context.User.Identity?.IsAuthenticated == true)
            {
                var isAuthenticatedAndValid = await ProcessAuthenticationAsync(context);
                if (!isAuthenticatedAndValid)
                {
                    // Response already written (e.g. 401 Account Deleted), short-circuit pipeline
                    return;
                }
            }

            await _next(context);
        }

        private async Task<bool> ProcessAuthenticationAsync(HttpContext context)
        {
            try
            {
                var authId = ExtractAuthIdFromToken(context);
                _logger.LogDebug("AuthMiddleware: Extracted authId: {AuthId}", authId);

                if (!string.IsNullOrEmpty(authId) && Guid.TryParse(authId, out var authGuid))
                {
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                    var cacheKey = $"auth:{authGuid}";
                    var authInfo = await cacheService.GetAsync<Sovva.Domain.Entities.AuthInfo?>(cacheKey);
                    
                    if (!authInfo.HasValue)
                    {
                        authInfo = await userRepository.GetAuthInfoByAuthIdAsync(authGuid);
                        if (authInfo.HasValue)
                        {
                            await cacheService.SetAsync(cacheKey, authInfo, TimeSpan.FromSeconds(30));
                        }
                    }

                    if (authInfo.HasValue)
                    {
                        // Check if user account is deleted
                        if (authInfo.Value.AccountStatus == AccountStatusConstants.Deleted)
                        {
                            _logger.LogWarning(
                                "Access denied - deleted account: AuthId={AuthId} Path={Path}",
                                authId, context.Request.Path);
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            var errorResponse = new { success = false, code = "ACCOUNT_DELETED", message = "Account deleted" };
                            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                            return false;
                        }

                        context.Items["UserId"] = authInfo.Value.UserId;
                        context.Items["auth_id"] = authId;
                        context.Items["AuthId"] = authGuid;

                        var identity = context.User.Identity as System.Security.Claims.ClaimsIdentity;
                        if (identity != null)
                        {
                            // ✅ FIX 1: Remove ALL existing role claims (Supabase adds "authenticated")
                            var existingRoleClaims = identity.FindAll(identity.RoleClaimType).ToList();
                            foreach (var claim in existingRoleClaims)
                                identity.RemoveClaim(claim);

                            // ✅ FIX 2: Add role to "sovva_role" claim — matches RoleClaimType in Program.cs
                            identity.AddClaim(new System.Security.Claims.Claim(
                                RoleConstants.SovvaRole,
                                authInfo.Value.Role ?? "User"
                            ));

                            // ✅ FIX 3: Add sovva_user_id claim — used by User.GetSovvaUserId()
                            // Only add if not already present in the JWT from Supabase hook
                            var existingUserIdClaim = identity.FindFirst(RoleConstants.SovvaUserId);
                            if (existingUserIdClaim == null)
                            {
                                identity.AddClaim(new System.Security.Claims.Claim(
                                    RoleConstants.SovvaUserId,
                                    authInfo.Value.UserId.ToString()
                                ));
                            }
                        }

                        _logger.LogDebug(
                            "AuthMiddleware: User {UserId} authenticated with role {Role}",
                            authInfo.Value.UserId, authInfo.Value.Role);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "AuthMiddleware: New user detected (authId: {AuthId}) - awaiting registration",
                            authId);
                        context.Items["auth_id"] = authId;
                        context.Items["AuthId"] = authGuid;
                        context.Items["IsNewUser"] = true;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthMiddleware error");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                var errorResponse = new { success = false, code = "SERVICE_UNAVAILABLE", message = "Authentication service temporarily unavailable." };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                return false; // Block the request — return 503
            }
        }

        private string? ExtractAuthIdFromToken(HttpContext context)
        {

            try
            {
                return context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token extraction error");
                return null;
            }
        }


    }
}