using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Enums;
using Sovva.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
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

        /// <summary>Enhanced order history with nutritional info</summary>
        [HttpGet("users/me/orders")]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetMyOrders()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var userOrders = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
            return Ok(ApiResponse.Ok(userOrders));
        }

        /// <summary>Simplified order history</summary>
        [HttpGet("users/me/orders/simple")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrdersSimple()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var userOrders = await _orderService.GetUserOrdersAsync(userId.Value);
            return Ok(ApiResponse.Ok(userOrders));
        }

        /// <summary>Get single order by ID (ownership enforced)</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            // ✅ SOLID-4 FIX: Auth check FIRST — avoid wasting a DB call on unauthenticated requests
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            if (order.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            return Ok(ApiResponse.Ok(order));
        }

        /// <summary>Place an immediate order from the meal builder</summary>
        [HttpPost("create-from-meal-builder")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("financial")]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder(
            [FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null)
                    return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var result = await _orderService.CreateOrderFromMealBuilderAsync(dto, userId.Value);
                return Ok(ApiResponse.Ok(result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        // ==================== POST-DELIVERY ACTIONS ====================

        /// <summary>Rate a past order (1-5 stars) and optionally leave a review</summary>
        [HttpPost("{id}/rating")]
        public async Task<IActionResult> RateOrder(long id, [FromBody] OrderRatingDto dto)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null) return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                await _orderService.RateOrderAsync(id, userId.Value, dto.Rating, dto.Review);
                return Ok(ApiResponse.Ok(new { message = "Rating submitted successfully" }));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        /// <summary>One-click reorder of a past meal, scheduled for tomorrow</summary>
        [HttpPost("{id}/reorder")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("financial")]
        public async Task<ActionResult<OrderCreationResponseDto>> Reorder(long id)
        {
            try
            {
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId is null) return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

                var result = await _orderService.ReorderAsync(id, userId.Value);
                return Ok(ApiResponse.Ok(result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }

        // ==================== ADMIN ENDPOINTS ====================

        /// <summary>Get all order history (Admin only)</summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetAllHistoryAdmin(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (pageSize > 200) return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Max page size is 200"));
            var result = await _orderService.GetAllOrderHistoryAsync(page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>Get all order history with rich details (Admin only)</summary>
        [HttpGet("admin/details")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResult<EnhancedOrderHistoryDto>>>> GetAllHistoryWithDetailsAdmin(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (pageSize > 200) return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Max page size is 200"));
            var result = await _orderService.GetAllOrderHistoryWithDetailsAsync(page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>Get orders by status (Admin only)</summary>
        [HttpGet("admin/status/{status}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetByStatusAdmin(
            OrderStatus status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (pageSize > 200) return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Max page size is 200"));
            var result = await _orderService.GetOrdersByStatusAsync(status, page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }
    }
}