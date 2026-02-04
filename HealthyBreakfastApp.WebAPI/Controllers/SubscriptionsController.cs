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
        private readonly AppDbContext _context;

        public SubscriptionsController(
            ISubscriptionService subscriptionService,
            AppDbContext context)
        {
            _subscriptionService = subscriptionService;
            _context = context;
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
        /// ✅ UPDATED: Extracts userId from JWT token and maps to CreateSubscriptionInternalDto
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SubscriptionDto>> CreateSubscription(CreateSubscriptionDto createSubscriptionDto)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            try
            {
                // ✅ Map CreateSubscriptionDto to CreateSubscriptionInternalDto with UserId from JWT
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
            if (userId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null)
                return NotFound();

            if (existing.UserId != userId.Value)
                return Forbid();

            var result = await _subscriptionService.DeleteSubscriptionAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return Forbid();

            var result = await _subscriptionService.ActivateSubscriptionAsync(id);
            return result ? Ok(new { message = "Subscription activated" }) : NotFound();
        }
/// <summary>
/// ✅ NEW: Manual endpoint to force update all subscription NextScheduledDates
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

        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateSubscription(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var existing = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (existing == null || existing.UserId != userId.Value)
                return Forbid();

            var result = await _subscriptionService.DeactivateSubscriptionAsync(id);
            return result ? Ok(new { message = "Subscription deactivated" }) : NotFound();
        }
    }
}
