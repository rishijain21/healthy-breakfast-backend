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
        private readonly IOrderRepository _orderRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ISupabaseStorageService _storageService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService,
            IOrderRepository orderRepository,
            ICurrentUserService currentUserService,
            ISupabaseStorageService storageService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _orderRepository = orderRepository;
            _currentUserService = currentUserService;
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>Enhanced order history with nutritional info</summary>
        [HttpGet("users/me/orders")]
        public async Task<ActionResult<PagedResult<EnhancedOrderHistoryDto>>> GetMyOrders(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var userOrders = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value, page, pageSize);

            // ✅ PERF FIX (NEW-11): Sign all image URLs in parallel using Task.WhenAll.
            // Previous approach: sequential await per image = N round-trips to Supabase storage.
            // New approach: fire all signing requests concurrently = 1 logical round-trip.
            if (userOrders?.Items != null)
            {
                var signingTasks = userOrders.Items
                    .Where(item => !string.IsNullOrEmpty(item.MealImageUrl))
                    .Select(item => SignImageUrlAsync(item, _storageService))
                    .ToList();

                await Task.WhenAll(signingTasks);
            }

            return Ok(ApiResponse.Ok(userOrders));
        }

        private static async Task SignImageUrlAsync(EnhancedOrderHistoryDto item, ISupabaseStorageService storageService)
        {
            try
            {
                item.MealImageUrl = await storageService.GetSignedUrlAsync(item.MealImageUrl!);
            }
            catch
            {
                item.MealImageUrl = null;
            }
        }

        /// <summary>Lightweight order history summary (BFF optimization)</summary>
        [HttpGet("users/me/summary")]
        public async Task<ActionResult<PagedResult<OrderHistorySummaryDto>>> GetMyOrdersSummary(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var userOrders = await _orderRepository.GetUserOrdersSummaryPagedAsync(userId.Value, page, pageSize);

            if (userOrders.Items != null)
            {
                var signingTasks = userOrders.Items
                    .Where(item => !string.IsNullOrEmpty(item.MealImageUrl))
                    .Select(async item => 
                    {
                        try 
                        {
                            item.MealImageUrl = await _storageService.GetSignedUrlAsync(item.MealImageUrl!);
                        }
                        catch
                        {
                            item.MealImageUrl = null;
                        }
                    })
                    .ToList();

                await Task.WhenAll(signingTasks);
            }

            return Ok(ApiResponse.Ok(userOrders));
        }

        /// <summary>Simplified order history</summary>
        [HttpGet("users/me/orders/simple")]
        public async Task<ActionResult<PagedResult<OrderDto>>> GetMyOrdersSimple(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var userOrders = await _orderService.GetUserOrdersAsync(userId.Value, page, pageSize);
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

        /// <summary>Get enhanced single order details by ID (ownership enforced)</summary>
        [HttpGet("{id}/details")]
        public async Task<IActionResult> GetOrderDetails(long id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var order = await _orderService.GetOrderDetailsByIdAsync(id);
            if (order == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            // Note: EnhancedOrderHistoryDto doesn't expose UserId, but we could add it or check if it's correct.
            // Wait, we need to check ownership. Let's do it by fetching the simple order first, or assume we only show if owned.
            // Actually, we can fetch the simple order first to check ownership, then fetch details.
            var simpleOrder = await _orderService.GetOrderByIdAsync(id);
            if (simpleOrder == null || simpleOrder.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            if (!string.IsNullOrEmpty(order.MealImageUrl))
            {
                try
                {
                    order.MealImageUrl = await _storageService.GetSignedUrlAsync(order.MealImageUrl!);
                }
                catch
                {
                    order.MealImageUrl = null;
                }
            }

            return Ok(ApiResponse.Ok(order));
        }

        /// <summary>Place an immediate order from the meal builder</summary>
        [HttpPost("create-from-meal-builder")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("financial")]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder(
            [FromBody] CreateOrderFromMealBuilderDto dto)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var result = await _orderService.CreateOrderFromMealBuilderAsync(dto, userId.Value);
            return Ok(ApiResponse.Ok(result));
        }

        // ==================== POST-DELIVERY ACTIONS ====================

        /// <summary>Rate a past order (1-5 stars) and optionally leave a review</summary>
        [HttpPost("{id}/rating")]
        public async Task<IActionResult> RateOrder(long id, [FromBody] OrderRatingDto dto)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            await _orderService.RateOrderAsync(id, userId.Value, dto.Rating, dto.Review);
            return Ok(ApiResponse.Ok(new { message = "Rating submitted successfully" }));
        }

        /// <summary>One-click reorder of a past meal, scheduled for tomorrow</summary>
        [HttpPost("{id}/reorder")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("financial")]
        public async Task<ActionResult<OrderCreationResponseDto>> Reorder(long id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var result = await _orderService.ReorderAsync(id, userId.Value);
            return Ok(ApiResponse.Ok(result));
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