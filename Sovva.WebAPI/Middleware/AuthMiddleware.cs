using System.IdentityModel.Tokens.Jwt;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

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
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var publicEndpoints = new[]
            {
                "/swagger",
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/check-user-exists",
                "/api/scheduledorders/time-until-midnight"
            };

            if (publicEndpoints.Any(endpoint => path.StartsWith(endpoint)))
            {
                await _next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api")
                && context.User.Identity?.IsAuthenticated == true)
            {
                await ProcessAuthenticationAsync(context);
            }

            await _next(context);
        }

        private async Task ProcessAuthenticationAsync(HttpContext context)
        {
            try
            {
                var authId = ExtractAuthIdFromToken(context);
                _logger.LogInformation("🔐 AuthMiddleware: Extracted authId: {AuthId}", authId);

                if (!string.IsNullOrEmpty(authId) && Guid.TryParse(authId, out var authGuid))
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                    var userDto = await userService.GetUserByAuthIdAsync(authGuid);

                    if (userDto != null)
                    {
                        context.Items["UserId"] = userDto.UserId;
                        context.Items["User"] = userDto;
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
                                "sovva_role",
                                userDto.Role ?? "User"
                            ));

                            // ✅ FIX 3: Add sovva_user_id claim — used by User.GetSovvaUserId()
                            // Only add if not already present in the JWT from Supabase hook
                            var existingUserIdClaim = identity.FindFirst("sovva_user_id");
                            if (existingUserIdClaim == null)
                            {
                                identity.AddClaim(new System.Security.Claims.Claim(
                                    "sovva_user_id",
                                    userDto.UserId.ToString()
                                ));
                            }
                        }

                        _logger.LogInformation(
                            "✅ AuthMiddleware: User {UserId} authenticated with role {Role}",
                            userDto.UserId, userDto.Role);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "🆕 AuthMiddleware: New user detected (authId: {AuthId}) - awaiting registration",
                            authId);
                        context.Items["auth_id"] = authId;
                        context.Items["AuthId"] = authGuid;
                        context.Items["IsNewUser"] = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ AuthMiddleware error");
            }
        }

        private string? ExtractAuthIdFromToken(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            try
            {
                return context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Token extraction error");
                return null;
            }
        }

        private (string? Name, string? Email) ExtractUserInfoFromToken(HttpContext context)
        {
            var name = context.User.FindFirst("name")?.Value
                ?? context.User.FindFirst("full_name")?.Value;
            var email = context.User.FindFirst("email")?.Value;
            return (name, email);
        }
    }
}