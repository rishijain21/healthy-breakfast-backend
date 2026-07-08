using System.Security.Claims;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sovva.WebAPI.Extensions;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ISubscriptionSchedulingService _subscriptionSchedulingService;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDashboardService _dashboardService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            ICurrentUserService currentUserService,
            IDashboardService dashboardService,
            IAppTimeProvider time,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _currentUserService = currentUserService;
            _dashboardService = dashboardService;
            _time = time;
            _logger = logger;
        }

        // ✅ NEW: Uses ICurrentUserService to correctly fallback to DB if token lacks claim
        private async Task<int?> GetCurrentUserIdAsync()
            => await _currentUserService.GetCurrentUserIdAsync();

        // ✅ NEW: Zero DB hit — reads sub/nameidentifier claim
        private Guid? GetCurrentAuthId()
        {
            var claim = User.FindFirst("sub")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var guid) ? guid : null;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetAllSubscriptions()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(ApiResponse.Ok(subscriptions));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            if (subscription.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            return Ok(ApiResponse.Ok(subscription));
        }

        /// <summary>
        /// Deprecated: Use GET /api/subscriptions instead.
        /// This endpoint returns identical data and will be removed in a future release.
        /// </summary>
        [Obsolete("Use GET /api/subscriptions instead. This duplicate endpoint will be removed in v2.")]
        [HttpGet("user/me")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetMySubscriptions()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(ApiResponse.Ok(subscriptions));
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetActiveSubscriptions()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            // ✅ Filter by the current user only
            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            var active = subscriptions.Where(s => s.IsActive);
            return Ok(ApiResponse.Ok(active));
        }

        /// <summary>
        /// ✅ UPDATED: Creates subscription AND generates tomorrow's order immediately
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SubscriptionDto>> CreateSubscription(CreateSubscriptionDto createSubscriptionDto)
        {
            var userId = await GetCurrentUserIdAsync();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            try
            {
                _logger.LogInformation("Creating subscription for user {UserId}", userId);

                // 1. Create subscription
                var internalDto = new CreateSubscriptionInternalDto
                {
                    UserId = userId.Value,
                    MealId = createSubscriptionDto.MealId,
                    UserMealId = createSubscriptionDto.UserMealId,
                    Frequency = createSubscriptionDto.Frequency,
                    StartDate = createSubscriptionDto.StartDate,
                    EndDate = createSubscriptionDto.EndDate,
                    IsActive = createSubscriptionDto.IsActive,
                    WeeklySchedule = createSubscriptionDto.WeeklySchedule
                };

                var subscription = await _subscriptionService.CreateSubscriptionAsync(internalDto);

                _logger.LogInformation("Subscription {SubscriptionId} created", subscription.SubscriptionId);

                // ✅ Order is already created inside CreateSubscriptionAsync() via CreateFirstScheduledOrderAsync()
                // No need to generate it again!

                // ✅ FIX: Invalidate dashboard cache so active sub count updates
                await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);

                return CreatedAtAction(nameof(GetSubscription),
                    new { id = subscription.SubscriptionId }, ApiResponse.Ok(subscription, subscription.Warning));
            }
            catch (DuplicateSubscriptionException ex)
            {
                // ✅ This catches duplicate subscription attempts
                _logger.LogWarning(ex, "Duplicate subscription attempt blocked");
                return Conflict(ApiResponse.Fail("CONFLICT", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                // ✅ This catches security violations
                _logger.LogWarning(ex, "Unauthorized subscription attempt");
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied")); // ✅ Returns 403 Forbidden
            }
            // ✅ No generic catch — middleware handles unexpected exceptions
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubscriptionDto>> UpdateSubscription(int id, UpdateSubscriptionDto updateSubscriptionDto)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            if (existing.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            var subscription = await _subscriptionService.UpdateSubscriptionAsync(id, updateSubscriptionDto);
            if (subscription == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);

            return Ok(ApiResponse.Ok(subscription));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
            {
                // ✅ FIX: Idempotent delete. If already deleted, treat as success to prevent frontend UX errors.
                _logger.LogInformation("Subscription {SubscriptionId} not found during delete (likely already deleted)", id);
                return NoContent();
            }

            if (subscription.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            // ✅ Delegate to service - it handles scheduled orders properly
            // (keeps processed orders, deletes pending ones)
            await _subscriptionService.DeleteSubscriptionAsync(id);
            
            await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);
            
            _logger.LogInformation("Subscription {SubscriptionId} deleted successfully", id);
            return NoContent();
        }

        /// <summary>
        /// ✅ UPDATED: Activates subscription AND generates tomorrow's order immediately
        /// </summary>
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            _logger.LogInformation("Resuming subscription {SubscriptionId}", id);

            var result = await _subscriptionService.ActivateSubscriptionAsync(id);
            
            if (!result)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            // ✅ NEW: Generate tomorrow's order immediately when resuming
            try
            {
                await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(id, userId.Value, authId.Value);
                _logger.LogInformation("Generated order for resumed subscription {SubscriptionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate order for resumed subscription {SubscriptionId}", id);
            }

            await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);

            return Ok(ApiResponse.Ok(new { message = "Subscription activated and order generated" }));
        }

        /// <summary>
        /// ✅ UPDATED: Deactivates subscription AND cancels tomorrow's order
        /// </summary>
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));

            _logger.LogInformation("Pausing subscription {SubscriptionId}", id);

            // ✅ NEW: Cancel tomorrow's order when pausing
            try
            {
                await _subscriptionSchedulingService.CancelOrderForSubscriptionAsync(id, userId.Value, authId.Value);
                _logger.LogInformation("Cancelled order for paused subscription {SubscriptionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel order for subscription {SubscriptionId}", id);
            }

            var result = await _subscriptionService.DeactivateSubscriptionAsync(id);
            
            if (result)
            {
                await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);
                return Ok(ApiResponse.Ok(new { message = "Subscription paused and order cancelled" }));
            }
            
            return NotFound(ApiResponse.Fail("NOT_FOUND", "Subscription not found"));
        }

        /// <summary>
        /// ✅ Manual endpoint to sync all subscription dates
        /// </summary>
        [HttpPost("sync-dates")]
        [Authorize(Roles = "Admin")]   // ← ADD: only admin should trigger batch operations
        public async Task<IActionResult> SyncSubscriptionDates()
        {
            await _subscriptionService.UpdateNextScheduledDatesAsync();
            
            return Ok(ApiResponse.Ok(new 
            { 
                success = true,
                message = "Subscription dates synchronized successfully",
                timestamp = _time.UtcNow
            }));
        }
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResult<SubscriptionDto>>>> GetAllSubscriptionsAdmin(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (pageSize > 200) return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Max page size is 200"));
            var result = await _subscriptionService.GetAllSubscriptionsAsync(page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }
    }
}
