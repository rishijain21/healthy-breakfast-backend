using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;          // ✅ ADD
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        // ✅ REMOVED: ICurrentUserService
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetAllOrderHistory()
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized("User not authenticated");

                var orderHistory = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
                return Ok(orderHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllOrderHistory");
                return StatusCode(500, new { error = "An error occurred while retrieving order history" });
            }
        }

        [HttpGet("users/me/orders")]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetMyOrders()
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized("User not authenticated");

                var userOrders = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
                return Ok(userOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMyOrders");
                return StatusCode(500, new { error = "Unable to retrieve your orders" });
            }
        }

        [HttpGet("users/me/orders/simple")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrdersSimple()
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized("User not authenticated");

                var userOrders = await _orderService.GetUserOrdersAsync(userId.Value);
                return Ok(userOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMyOrdersSimple");
                return StatusCode(500, new { error = "Unable to retrieve your orders" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized("User not found");

            var id = await _orderService.CreateOrderAsync(dto, userId.Value);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            var userId = User.GetSovvaUserId();           // ✅ JWT claim
            if (userId is null)
                return Unauthorized("User not authenticated");

            if (order.UserId != userId.Value)
                return Forbid();

            return Ok(order);
        }

        [HttpPost("create-from-meal-builder")]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder(
            [FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                var userId = User.GetSovvaUserId();       // ✅ JWT claim
                if (userId is null)
                    return Unauthorized(new { message = "User not authenticated" });

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
    }
}