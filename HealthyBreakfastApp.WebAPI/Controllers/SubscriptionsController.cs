using System.Security.Claims;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthyBreakfastApp.WebAPI.Controllers
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
        private readonly ICurrentUserService _currentUserService;
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            ICurrentUserService currentUserService,
            AppDbContext context,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _currentUserService = currentUserService;
            _context = context;
            _logger = logger;
        }

        // Helper to get current userId from ICurrentUserService
        private async Task<int?> GetCurrentUserIdAsync()
        {
            return await _currentUserService.GetCurrentUserIdAsync();
        }

        private async Task<Guid?> GetCurrentAuthIdAsync()
        {
            var authId = _currentUserService.GetAuthId();
            if (string.IsNullOrEmpty(authId) || !Guid.TryParse(authId, out var guid))
                return null;
            return guid;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetAllSubscriptions()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(subscriptions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
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
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId.Value);
            return Ok(subscriptions);
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetActiveSubscriptions()
        {
            var userId = await GetCurrentUserIdAsync();
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
            var userId = await GetCurrentUserIdAsync();
            var authId = await GetCurrentAuthIdAsync();
            
            if (userId == null || authId == null)
                return Unauthorized();

            try
            {
                _logger.LogInformation($"📦 Creating subscription for user {userId}");

                // 1. Create subscription
                var internalDto = new CreateSubscriptionInternalDto
                {
                    UserId = userId.Value,
                    UserMealId = createSubscriptionDto.UserMealId,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error creating subscription");
                return StatusCode(500, new { message = "An error occurred while creating the subscription" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubscriptionDto>> UpdateSubscription(int id, UpdateSubscriptionDto updateSubscriptionDto)
        {
            var userId = await GetCurrentUserIdAsync();
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
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            try
            {
                var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
                if (existing == null)
                    return NotFound();

                if (existing.UserId != userId.Value)
                    return Forbid();

                _logger.LogInformation($"🗑️ Deleting subscription #{id}");

                // ✅ Delete associated scheduled orders first
                try
                {
                    var orders = await _scheduledOrderRepository.GetBySubscriptionIdAsync(id);
                    foreach (var order in orders)
                    {
                        await _scheduledOrderRepository.DeleteAsync(order.ScheduledOrderId);
                        _logger.LogInformation($"✅ Deleted order #{order.ScheduledOrderId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"⚠️ Failed to delete orders for subscription #{id}");
                }

                // ✅ Delete the subscription
                var result = await _subscriptionService.DeleteSubscriptionAsync(id);
                if (!result)
                {
                    _logger.LogWarning($"⚠️ Subscription #{id} not found during delete");
                    return NotFound();
                }

                _logger.LogInformation($"✅ Subscription #{id} deleted successfully");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error deleting subscription #{id}");
                return StatusCode(500, new { message = "Failed to delete subscription" });
            }
        }

        /// <summary>
        /// ✅ UPDATED: Activates subscription AND generates tomorrow's order immediately
        /// </summary>
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var authId = await GetCurrentAuthIdAsync();
            
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
                await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(id, authId.Value);
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
            var userId = await GetCurrentUserIdAsync();
            var authId = await GetCurrentAuthIdAsync();
            
            if (userId == null || authId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return Forbid();

            _logger.LogInformation($"⏸️ Pausing subscription #{id}");

            // ✅ NEW: Cancel tomorrow's order when pausing
            try
            {
                await _subscriptionSchedulingService.CancelOrderForSubscriptionAsync(id, authId.Value);
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
            try
            {
                await _subscriptionService.UpdateNextScheduledDatesAsync();
                
                return Ok(new 
                { 
                    success = true,
                    message = "Subscription dates synchronized successfully",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    success = false,
                    message = "Failed to sync subscription dates" 
                });
            }
        }
    }
}
