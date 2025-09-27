using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HealthyBreakfastApp.Application.Services
{
    public interface IJwtService
    {
        ClaimsPrincipal? ValidateToken(string token, string secret, string issuer);
        string? ExtractSubject(string token);
        string? ExtractEmail(string token);
    }

    public class JwtService : IJwtService
    {
        public ClaimsPrincipal? ValidateToken(string token, string secret, string issuer)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                
                // For Supabase, we'll do basic validation without signature verification for now
                var jsonToken = tokenHandler.ReadJwtToken(token);
                
                // Check if token is expired
                if (jsonToken.ValidTo < DateTime.UtcNow)
                    return null;

                // Create claims principal from JWT claims
                var claims = jsonToken.Claims.ToList();
                var identity = new ClaimsIdentity(claims, "jwt");
                return new ClaimsPrincipal(identity);
            }
            catch
            {
                return null;
            }
        }

        public string? ExtractSubject(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                return jsonToken.Subject;
            }
            catch
            {
                return null;
            }
        }

        public string? ExtractEmail(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                return jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}
