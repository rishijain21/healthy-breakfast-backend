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
        private readonly ICurrentUserService _currentUserService;
        private readonly IDashboardService _dashboardService;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<SubscriptionsController> _logger;
        private readonly ISubscriptionRepository _subscriptionRepository;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            IScheduledOrderService scheduledOrderService,
            ICurrentUserService currentUserService,
            IDashboardService dashboardService,
            IAppTimeProvider time,
            ILogger<SubscriptionsController> logger,
            ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _scheduledOrderService = scheduledOrderService;
            _currentUserService = currentUserService;
            _dashboardService = dashboardService;
            _time = time;
            _logger = logger;
            _subscriptionRepository = subscriptionRepository;
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

            // ✅ SECURE: Scope subscription fetch to authenticated user ID (eliminates IDOR)
            var subscription = await _subscriptionService.GetSubscriptionByIdAndUserIdAsync(id, userId.Value);
            if (subscription == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

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

            // ✅ Filter active subscriptions at DB level
            var active = await _subscriptionService.GetActiveSubscriptionsByUserIdAsync(userId.Value);
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

            // ✅ PERF: Single EXISTS query instead of full entity fetch for ownership check
            var belongs = await _subscriptionRepository.BelongsToUserAsync(id, userId.Value);
            if (!belongs)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

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

            // ✅ PERF: EXISTS query — no full entity materialization needed for delete
            var belongs = await _subscriptionRepository.BelongsToUserAsync(id, userId.Value);
            if (!belongs)
            {
                // ✅ Idempotent: treat not-found as already deleted
                _logger.LogInformation("Subscription {SubscriptionId} not found during delete (likely already deleted)", id);
                return NoContent();
            }

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

            // ✅ SECURE: Check subscription ownership in DB via EXISTS query
            var belongs = await _subscriptionRepository.BelongsToUserAsync(id, userId.Value);
            if (!belongs)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            _logger.LogInformation("Resuming subscription {SubscriptionId}", id);

            var result = await _subscriptionService.ActivateSubscriptionAsync(id);
            
            if (!result)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            // ✅ Generate tomorrow's order immediately when resuming.
            // Track success separately — order generation failure must NOT rollback the activation.
            var orderGenerated = false;
            string? orderWarning = null;
            try
            {
                await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(id, userId.Value, authId.Value);
                orderGenerated = true;
                _logger.LogInformation("Generated order for resumed subscription {SubscriptionId}", id);
            }
            catch (Exception ex)
            {
                // Activation itself succeeded — only order generation failed.
                // The midnight job will generate it automatically tonight.
                orderWarning = "Subscription activated, but we couldn't generate today's order. It will be created automatically tonight.";
                _logger.LogWarning(ex, "Failed to generate order for resumed subscription {SubscriptionId} — midnight job will retry", id);
            }

            await _dashboardService.InvalidateDashboardCacheAsync(userId.Value);

            return Ok(ApiResponse.Ok(new
            {
                activated = true,
                orderGenerated,
                message = orderGenerated
                    ? "Subscription activated and order generated"
                    : "Subscription activated",
                warning = orderWarning
            }));
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

            // ✅ SECURE: Check subscription ownership in DB via EXISTS query
            var belongs = await _subscriptionRepository.BelongsToUserAsync(id, userId.Value);
            if (!belongs)
                return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

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
