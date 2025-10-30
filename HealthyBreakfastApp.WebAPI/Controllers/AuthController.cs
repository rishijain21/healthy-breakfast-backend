using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // Simple test login
            if (request.Email == "test@example.com" && request.Password == "password123")
            {
                var token = GenerateJwtToken(request.Email, "2"); // Use existing user ID 2
                return Ok(new { access_token = token, token_type = "bearer" });
            }

            return Unauthorized("Invalid credentials");
        }

        private string GenerateJwtToken(string email, string userId)
        {
            var secret = _configuration["Jwt:Secret"];
            var issuer = _configuration["Jwt:Issuer"];
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", userId),
                    new Claim("user_id", userId),
                    new Claim("email", email)
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = issuer,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
