using System.IdentityModel.Tokens.Jwt;
using HealthyBreakfastApp.Application.Interfaces;

namespace HealthyBreakfastApp.WebAPI.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AuthMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only process API routes
            if (context.Request.Path.StartsWithSegments("/api"))
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

                if (!string.IsNullOrEmpty(authId) && Guid.TryParse(authId, out var authGuid))
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                    // Extract user info from token
                    var (name, email) = ExtractUserInfoFromToken(context);

                    // Find or create user automatically
                    var userDto = await userService.FindOrCreateUserByAuthIdAsync(authGuid, name, email);

                    if (userDto != null)
                    {
                        // Make user available to controllers
                        context.Items["UserId"] = userDto.UserId;
                        context.Items["User"] = userDto;
                        context.Items["auth_id"] = authId; // ✅ FIXED: lowercase with underscore
                        context.Items["AuthId"] = authGuid; // Keep for backwards compatibility
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't block the request
                Console.WriteLine($"Auth middleware error: {ex.Message}");
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
                Console.WriteLine($"Token decode error: {ex.Message}");
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
