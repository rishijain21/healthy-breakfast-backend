using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IUserService _userService;

        public OrdersController(IOrderService orderService, IUserService userService)
        {
            _orderService = orderService;
            _userService = userService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var id = await _orderService.CreateOrderAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpPost("create-from-meal-builder")]
        // [Authorize]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder([FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                // Extract user ID from JWT token
                var userId = await GetUserIdFromJwtAsync();
                if (userId == null) 
                {
                    return Unauthorized("User not authenticated");
                }

                dto.UserId = userId.Value;
                var result = await _orderService.CreateOrderFromMealBuilderAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task<int?> GetUserIdFromJwtAsync()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return null;

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);
                
                var authId = jsonToken.Subject ?? jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                
                if (string.IsNullOrEmpty(authId) || !Guid.TryParse(authId, out var authGuid))
                    return null;

                var name = jsonToken.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == "full_name")?.Value;
                var email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

                var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authGuid, name, email);
                
                return userDto?.UserId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JWT extraction error: {ex.Message}");
                return null;
            }
        }

        private int? GetCurrentUserId()
        {
            return HttpContext.Items["UserId"] as int?;
        }
    }
}
