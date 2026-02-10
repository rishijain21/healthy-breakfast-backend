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
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            IScheduledOrderService scheduledOrderService,
            IScheduledOrderRepository scheduledOrderRepository,
            AppDbContext context,
            ILogger<SubscriptionsController> logger)
        {
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _scheduledOrderService = scheduledOrderService;
            _scheduledOrderRepository = scheduledOrderRepository;
            _context = context;
            _logger = logger;
        }

        // Helper to get current userId from JWT + AuthMapping
        private async Task<int?> GetCurrentUserIdAsync()
        {
            var authId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(authId))
                return null;

            var mapping = await _context.UserAuthMappings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AuthId.ToString() == authId);

            return mapping?.UserId;
        }

        private async Task<Guid?> GetCurrentAuthIdAsync()
        {
            var authId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

            var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
            return Ok(subscriptions);
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

                // 2. ✅ NEW: Generate tomorrow's order immediately if active
                if (subscription.Active)
                {
                    try
                    {
                        await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(
                            subscription.SubscriptionId, 
                            authId.Value
                        );
                        
                        _logger.LogInformation($"✅ Generated immediate order for subscription #{subscription.SubscriptionId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"⚠️ Failed to generate immediate order for subscription #{subscription.SubscriptionId}");
                        // Don't fail the subscription creation, just log the warning
                    }
                }

                return CreatedAtAction(nameof(GetSubscription),
                    new { id = subscription.SubscriptionId },
                    subscription);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
            var authId = await GetCurrentAuthIdAsync();
            
            if (userId == null || authId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null)
                return NotFound();

            if (existing.UserId != userId.Value)
                return Forbid();

            // ✅ NEW: Cancel tomorrow's scheduled order when deleting subscription
            try
            {
                await _subscriptionSchedulingService.CancelOrderForSubscriptionAsync(id, authId.Value);
                _logger.LogInformation($"✅ Cancelled scheduled order for deleted subscription #{id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"⚠️ Failed to cancel order for subscription #{id}");
            }

            var result = await _subscriptionService.DeleteSubscriptionAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
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
                    message = "Failed to sync subscription dates", 
                    error = ex.Message 
                });
            }
        }
    }
}
