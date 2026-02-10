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
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService, 
            IUserService userService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _userService = userService;
            _logger = logger;
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
                _logger.LogError(ex, "❌ Error in GetAllOrderHistory");
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
                _logger.LogError(ex, "❌ Error in GetMyOrders");
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
                _logger.LogError(ex, "❌ Error in GetMyOrdersSimple");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Unable to retrieve your orders" });
            }
        }

        // ============================
        // ✅ SECURE: CREATE ORDER (userId from JWT token)
        // ============================
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized("User not found");

            // ✅ Pass userId as separate parameter (extracted from JWT token)
            var id = await _orderService.CreateOrderAsync(dto, userId.Value);
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
        // 🟢 CREATE ORDER FROM MEAL BUILDER (SECURE - Uses JWT UserId)
        // ============================
        [HttpPost("create-from-meal-builder")]
        [Authorize]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder([FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Pass userId as separate parameter (extracted from JWT token)
                var result = await _orderService.CreateOrderFromMealBuilderAsync(dto, userId.Value);
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
        // ✅ UNIFIED USER ID EXTRACTION (FIXED)
        // ============================
        private async Task<int?> GetCurrentUserIdAsync()
        {
            try
            {
                var supabaseUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                     ?? User.FindFirst("sub")?.Value;

                _logger.LogInformation($"🔍 Found Supabase User ID: {supabaseUserId}");

                if (string.IsNullOrEmpty(supabaseUserId))
                {
                    _logger.LogWarning("❌ No user ID found in token claims");
                    return null;
                }

                // Try direct numeric user ID first
                if (int.TryParse(supabaseUserId, out var directUserId))
                {
                    _logger.LogInformation($"✅ Direct numeric user ID: {directUserId}");
                    return directUserId;
                }

                // Try GUID (Supabase auth_id)
                if (Guid.TryParse(supabaseUserId, out var authId))
                {
                    _logger.LogInformation($"🔍 Supabase GUID found: {authId}, looking up user in database...");

                    // ✅ FIXED: Only find user, don't create
                    var userDto = await _userService.GetUserByAuthIdAsync(authId);
                    if (userDto == null)
                    {
                        _logger.LogWarning($"⚠️ User not found for authId: {authId}");
                        return null;
                    }

                    _logger.LogInformation($"✅ Found user in database: UserId = {userDto.UserId}");
                    return userDto.UserId;
                }

                _logger.LogWarning($"❌ Could not parse user ID: {supabaseUserId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ User ID extraction error");
                return null;
            }
        }
    }
}
