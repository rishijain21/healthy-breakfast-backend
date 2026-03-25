using System.Security.Claims;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sovva.WebAPI.Extensions;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ISubscriptionSchedulingService _subscriptionSchedulingService;
        private readonly IScheduledOrderService _scheduledOrderService;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _logger = logger;
        }

        // ✅ NEW: Zero DB hit — reads sovva_user_id JWT claim
        private int? GetCurrentUserId()
            => User.GetSovvaUserId();

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
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(subscriptions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
                return NotFound();

            if (subscription.UserId != userId.Value)
                return Forbid();

            return Ok(subscription);
        }

        [HttpGet("user/me")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetMySubscriptions()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(subscriptions);
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetActiveSubscriptions()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // ✅ Filter by the current user only
            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            var active = subscriptions.Where(s => s.Active);
            return Ok(active);
        }

        /// <summary>
        /// ✅ UPDATED: Creates subscription AND generates tomorrow's order immediately
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SubscriptionDto>> CreateSubscription(CreateSubscriptionDto createSubscriptionDto)
        {
            var userId = GetCurrentUserId();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized();

            try
            {
                _logger.LogInformation($"📦 Creating subscription for user {userId}");

                // 1. Create subscription
                var internalDto = new CreateSubscriptionInternalDto
                {
                    UserId = userId.Value,
                    MealId = createSubscriptionDto.MealId,  // ✅ Changed from UserMealId
                    Frequency = createSubscriptionDto.Frequency,
                    StartDate = createSubscriptionDto.StartDate,
                    EndDate = createSubscriptionDto.EndDate,
                    Active = createSubscriptionDto.Active,
                    WeeklySchedule = createSubscriptionDto.WeeklySchedule
                };

                var subscription = await _subscriptionService.CreateSubscriptionAsync(internalDto);

                _logger.LogInformation($"✅ Subscription #{subscription.SubscriptionId} created");

                // ✅ Order is already created inside CreateSubscriptionAsync() via CreateFirstScheduledOrderAsync()
                // No need to generate it again!

                return CreatedAtAction(nameof(GetSubscription),
                    new { id = subscription.SubscriptionId },
                    subscription);
            }
            catch (InvalidOperationException ex)
            {
                // ✅ This catches duplicate subscription attempts
                _logger.LogWarning(ex, "⚠️ Duplicate subscription attempt blocked");
                return Conflict(new { message = ex.Message }); // ✅ Returns 409 Conflict
            }
            catch (UnauthorizedAccessException ex)
            {
                // ✅ This catches security violations
                _logger.LogWarning(ex, "⚠️ Unauthorized subscription attempt");
                return Forbid(); // ✅ Returns 403 Forbidden
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "❌ Invalid argument");
                return BadRequest(new { message = ex.Message });
            }
            // ✅ No generic catch — middleware handles unexpected exceptions
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubscriptionDto>> UpdateSubscription(int id, UpdateSubscriptionDto updateSubscriptionDto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null)
                return NotFound();

            if (existing.UserId != userId.Value)
                return Forbid();

            try
            {
                var subscription = await _subscriptionService.UpdateSubscriptionAsync(id, updateSubscriptionDto);
                if (subscription == null)
                    return NotFound();

                return Ok(subscription);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubscription(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // ✅ Delegate to service - it handles scheduled orders properly
            // (keeps processed orders, deletes pending ones)
            var result = await _subscriptionService.DeleteSubscriptionAsync(id);
            if (!result)
            {
                _logger.LogWarning($"⚠️ Subscription #{id} not found during delete");
                return NotFound();
            }

            _logger.LogInformation($"✅ Subscription #{id} deleted successfully");
            return NoContent();
        }

        /// <summary>
        /// ✅ UPDATED: Activates subscription AND generates tomorrow's order immediately
        /// </summary>
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateSubscription(int id)
        {
            var userId = GetCurrentUserId();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return Forbid();

            _logger.LogInformation($"▶️ Resuming subscription #{id}");

            var result = await _subscriptionService.ActivateSubscriptionAsync(id);
            
            if (!result)
                return NotFound();

            // ✅ NEW: Generate tomorrow's order immediately when resuming
            try
            {
                await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(id, userId.Value, authId.Value);
                _logger.LogInformation($"✅ Generated order for resumed subscription #{id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"⚠️ Failed to generate order for resumed subscription #{id}");
            }

            return Ok(new { message = "Subscription activated and order generated" });
        }

        /// <summary>
        /// ✅ UPDATED: Deactivates subscription AND cancels tomorrow's order
        /// </summary>
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateSubscription(int id)
        {
            var userId = GetCurrentUserId();
            var authId = GetCurrentAuthId();
            
            if (userId == null || authId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return Forbid();

            _logger.LogInformation($"⏸️ Pausing subscription #{id}");

            // ✅ NEW: Cancel tomorrow's order when pausing
            try
            {
                await _subscriptionSchedulingService.CancelOrderForSubscriptionAsync(id, userId.Value, authId.Value);
                _logger.LogInformation($"✅ Cancelled order for paused subscription #{id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"⚠️ Failed to cancel order for subscription #{id}");
            }

            var result = await _subscriptionService.DeactivateSubscriptionAsync(id);
            
            return result ? Ok(new { message = "Subscription paused and order cancelled" }) : NotFound();
        }

        /// <summary>
        /// ✅ Manual endpoint to sync all subscription dates
        /// </summary>
        [HttpPost("sync-dates")]
        [Authorize(Roles = "Admin")]   // ← ADD: only admin should trigger batch operations
        public async Task<IActionResult> SyncSubscriptionDates()
        {
            await _subscriptionService.UpdateNextScheduledDatesAsync();
            
            return Ok(new 
            { 
                success = true,
                message = "Subscription dates synchronized successfully",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
