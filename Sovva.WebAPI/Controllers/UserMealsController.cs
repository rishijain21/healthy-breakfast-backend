using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
    [Authorize]
    public class UserMealsController : ControllerBase
    {
        private readonly IUserMealService _userMealService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ISubscriptionSchedulingService _subscriptionSchedulingService;
        private readonly ILogger<UserMealsController> _logger;

        public UserMealsController(
            IUserMealService userMealService,
            ISubscriptionService subscriptionService,
            ISubscriptionSchedulingService subscriptionSchedulingService,
            ILogger<UserMealsController> logger)
        {
            _userMealService = userMealService;
            _subscriptionService = subscriptionService;
            _subscriptionSchedulingService = subscriptionSchedulingService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new UserMeal — userId read from JWT via AuthMiddleware.
        /// Prerequisite for creating a subscription.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserMealDto dto)
        {
            if (!HttpContext.Items.ContainsKey("UserId"))
            {
                _logger.LogWarning("UserId not found in HttpContext — user not authenticated");
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "User not authenticated"));
            }

            if (!int.TryParse(HttpContext.Items["UserId"]?.ToString(), out int userId))
            {
                _logger.LogWarning("Invalid UserId format in HttpContext: {UserId}", HttpContext.Items["UserId"]);
                return Unauthorized(ApiResponse.Fail("UNAUTHORIZED", "Invalid user identification"));
            }

            _logger.LogInformation("Creating UserMeal for UserId: {UserId}, MealName: {MealName}, Ingredients: {Count}",
                userId, dto.MealName, dto.SelectedIngredients?.Count ?? 0);

            var userMealId = await _userMealService.CreateUserMealAsync(dto, userId);

            // After meal builder completes, generate tomorrow's scheduled order if subscription exists
            try
            {
                var authIdStr = HttpContext.Items["auth_id"]?.ToString();
                if (!string.IsNullOrEmpty(authIdStr) && Guid.TryParse(authIdStr, out var authGuid))
                {
                    var subscription = await _subscriptionService
                        .GetActiveSubscriptionByUserMealIdAsync(userId, userMealId);

                    if (subscription != null)
                    {
                        _logger.LogInformation(
                            "Meal builder complete — triggering scheduled order for subscription #{SubId}",
                            subscription.SubscriptionId);

                        await _subscriptionSchedulingService.GenerateOrderForSubscriptionAsync(
                            subscription.SubscriptionId, userId, authGuid);

                        _logger.LogInformation("Scheduled order generated after meal builder");
                    }
                    else
                    {
                        _logger.LogInformation("No active subscription found for UserMeal #{UserMealId} — skipping scheduling", userMealId);
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't fail meal builder if scheduling fails — just log it
                _logger.LogWarning(ex, "Meal builder succeeded but scheduling failed for userId {UserId}", userId);
            }

            return Ok(ApiResponse.Ok(new { userMealId, message = "UserMeal created successfully" }));
        }
    }
}
