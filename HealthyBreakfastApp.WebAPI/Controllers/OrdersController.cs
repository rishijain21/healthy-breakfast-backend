using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService, 
            ICurrentUserService currentUserService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        // ============================
        // 🟢 GET ALL ORDER HISTORY (ENHANCED)
        // ============================
        [HttpGet("history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetAllOrderHistory()
        {
            try
            {
                // ✅ Filter to current user only
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                    return Unauthorized("User not authenticated");

                var orderHistory = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
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
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            // ✅ Verify ownership - user can only see their own orders
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized("User not authenticated");

            if (order.UserId != userId.Value)
                return Forbid(); // 403 — not your order

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
        // ✅ UNIFIED USER ID EXTRACTION (using ICurrentUserService for no DB hit)
        // ============================
        private async Task<int?> GetCurrentUserIdAsync()
            => await _currentUserService.GetCurrentUserIdAsync();
    }
}
