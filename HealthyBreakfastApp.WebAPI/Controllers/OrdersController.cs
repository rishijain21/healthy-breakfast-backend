using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        // ============================
        // 🟢 GET ALL ORDER HISTORY (ENHANCED)
        // ============================
        [HttpGet("history")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetAllOrderHistory()
        {
            try
            {
                var orderHistory = await _orderService.GetAllOrderHistoryWithDetailsAsync();
                return Ok(orderHistory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetAllOrderHistory: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An error occurred while retrieving order history" });
            }
        }

        // ============================
        // 🟢 GET CURRENT USER ORDERS (ENHANCED)
        // ============================
        [HttpGet("users/me/orders")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetMyOrders()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                    return Unauthorized("User not authenticated");

                var userOrders = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
                return Ok(userOrders);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetMyOrders: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Unable to retrieve your orders" });
            }
        }

        // ============================
        // 🟢 BACKWARD COMPATIBILITY: Simple order endpoints
        // ============================
        [HttpGet("users/me/orders/simple")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrdersSimple()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                    return Unauthorized("User not authenticated");

                var userOrders = await _orderService.GetUserOrdersAsync(userId.Value);
                return Ok(userOrders);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Unable to retrieve your orders" });
            }
        }

        // ============================
        // 🟢 CREATE ORDER
        // ============================
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized("User not found");

            dto.UserId = userId.Value;
            var id = await _orderService.CreateOrderAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        // ============================
        // 🟢 GET ORDER BY ID
        // ============================
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            return Ok(order);
        }

        // ============================
        // 🟢 CREATE ORDER FROM MEAL BUILDER
        // ============================
        [HttpPost("create-from-meal-builder")]
        [Authorize]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder([FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                    return Unauthorized("User not authenticated");

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

        // ============================
        // ✅ UNIFIED USER ID EXTRACTION
        // ============================
        private async Task<int?> GetCurrentUserIdAsync()
        {
            try
            {
                var supabaseUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                     ?? User.FindFirst("sub")?.Value;

                Console.WriteLine($"🔍 Found Supabase User ID: {supabaseUserId}");

                if (string.IsNullOrEmpty(supabaseUserId))
                {
                    Console.WriteLine("❌ No user ID found in token claims");
                    return null;
                }

                if (int.TryParse(supabaseUserId, out var directUserId))
                {
                    Console.WriteLine($"✅ Direct numeric user ID: {directUserId}");
                    return directUserId;
                }

                if (Guid.TryParse(supabaseUserId, out var authId))
                {
                    Console.WriteLine($"🔍 Supabase GUID found: {authId}, looking up user in database...");
                    var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                    var name = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value ?? User.FindFirst("full_name")?.Value;

                    Console.WriteLine($"🔍 Email: {email}, Name: {name}");

                    var userDto = await _userService.FindOrCreateUserByAuthIdAsync(authId, name, email);
                    if (userDto != null)
                    {
                        Console.WriteLine($"✅ Found/Created user in database: UserId = {userDto.UserId}");
                        return userDto.UserId;
                    }

                    Console.WriteLine("❌ Failed to find/create user in database");
                    return null;
                }

                Console.WriteLine($"❌ Could not parse user ID: {supabaseUserId}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ User ID extraction error: {ex.Message}");
                return null;
            }
        }
    }
}
