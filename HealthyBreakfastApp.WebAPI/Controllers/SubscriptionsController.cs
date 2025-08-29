using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetAllSubscriptions()
        {
            var subscriptions = await _subscriptionService.GetAllSubscriptionsAsync();
            return Ok(subscriptions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionDto>> GetSubscription(int id)
        {
            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
                return NotFound();

            return Ok(subscription);
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetUserSubscriptions(int userId)
        {
            var subscriptions = await _subscriptionService.GetSubscriptionsByUserIdAsync(userId);
            return Ok(subscriptions);
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetActiveSubscriptions()
        {
            var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
            return Ok(subscriptions);
        }

        [HttpPost]
        public async Task<ActionResult<SubscriptionDto>> CreateSubscription(CreateSubscriptionDto createSubscriptionDto)
        {
            try
            {
                var subscription = await _subscriptionService.CreateSubscriptionAsync(createSubscriptionDto);
                return CreatedAtAction(nameof(GetSubscription), new { id = subscription.SubscriptionId }, subscription);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SubscriptionDto>> UpdateSubscription(int id, UpdateSubscriptionDto updateSubscriptionDto)
        {
            try
            {
                var subscription = await _subscriptionService.UpdateSubscriptionAsync(id, updateSubscriptionDto);
                if (subscription == null)
                    return NotFound();

                return Ok(subscription);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubscription(int id)
        {
            var result = await _subscriptionService.DeleteSubscriptionAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateSubscription(int id)
        {
            var result = await _subscriptionService.ActivateSubscriptionAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateSubscription(int id)
        {
            var result = await _subscriptionService.DeactivateSubscriptionAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
