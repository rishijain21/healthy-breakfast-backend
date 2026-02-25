using System.IdentityModel.Tokens.Jwt;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace HealthyBreakfastApp.WebAPI.Middleware
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
            // ✅ SKIP: Public endpoints that don't need middleware processing
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var publicEndpoints = new[]
            {
                "/swagger",
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/check-user-exists",  // ✅ ADDED: Skip this endpoint too
                "/api/scheduledorders/time-until-midnight"
            };

            if (publicEndpoints.Any(endpoint => path.StartsWith(endpoint)))
            {
                await _next(context);
                return;
            }

            // ✅ CHECK: If this is an API route that needs processing
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var endpoint = context.GetEndpoint();
                var requiresAuth = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;
                var allowAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

                if (requiresAuth && !allowAnonymous)
                {
                    await ProcessAuthenticationAsync(context);
                }
            }

            await _next(context);
        }

        private async Task ProcessAuthenticationAsync(HttpContext context)
        {
            try
            {
                var authId = ExtractAuthIdFromToken(context);
                _logger.LogInformation($"🔐 AuthMiddleware: Extracted authId: {authId}");

                if (!string.IsNullOrEmpty(authId) && Guid.TryParse(authId, out var authGuid))
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                    // ✅ CRITICAL: Only FIND user, NEVER create
                    var userDto = await userService.GetUserByAuthIdAsync(authGuid);

                    if (userDto != null)
                    {
                        // ✅ Existing user - set context
                        context.Items["UserId"] = userDto.UserId;
                        context.Items["User"] = userDto;
                        context.Items["auth_id"] = authId;
                        context.Items["AuthId"] = authGuid;
                        
                        // ✅ ADD: Inject role into the ClaimsPrincipal so [Authorize(Roles="Admin")] works
                        var identity = context.User.Identity as System.Security.Claims.ClaimsIdentity;
                        if (identity != null)
                        {
                            // Remove any existing role claims from Supabase ("authenticated")
                            var existingRoleClaims = identity.FindAll(identity.RoleClaimType).ToList();
                            foreach (var claim in existingRoleClaims)
                                identity.RemoveClaim(claim);

                            // Inject the role from your database
                            identity.AddClaim(new System.Security.Claims.Claim(
                                identity.RoleClaimType,
                                userDto.Role ?? "User"
                            ));
                        }
                        
                        _logger.LogInformation($"✅ AuthMiddleware: User {userDto.UserId} authenticated with role {userDto.Role}");
                    }
                    else
                    {
                        // ✅ New user - don't create yet, just mark as pending
                        _logger.LogInformation($"🆕 AuthMiddleware: New user detected (authId: {authId}) - awaiting registration");
                        context.Items["auth_id"] = authId;
                        context.Items["AuthId"] = authGuid;
                        context.Items["IsNewUser"] = true;
                        
                        // ❌ DO NOT CREATE USER HERE!
                        // User will be created when they submit the profile form at /api/Auth/register
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ AuthMiddleware error: {ex.Message}");
            }
        }

        private string? ExtractAuthIdFromToken(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            try
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);
                
                return jsonToken.Subject ?? jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Token decode error: {ex.Message}");
                return null;
            }
        }

        private (string? Name, string? Email) ExtractUserInfoFromToken(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
                return (null, null);

            try
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);

                var name = jsonToken.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == "full_name")?.Value;
                var email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

                return (name, email);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}
