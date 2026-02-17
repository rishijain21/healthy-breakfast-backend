using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserMealsController : ControllerBase
    {
        private readonly IUserMealService _userMealService;
        private readonly ILogger<UserMealsController> _logger;

        public UserMealsController(
            IUserMealService userMealService,
            ILogger<UserMealsController> logger)
        {
            _userMealService = userMealService;
            _logger = logger;
        }

        /// <summary>
        /// ✅ SECURE: Creates a new UserMeal (userId from JWT token)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserMealDto dto)
        {
            try
            {
                // ✅ Get UserId from HttpContext (set by AuthMiddleware)
                if (!HttpContext.Items.ContainsKey("UserId"))
                {
                    _logger.LogWarning("❌ UserId not found in HttpContext. User not authenticated.");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userId = (int)HttpContext.Items["UserId"]!;
                _logger.LogInformation($"✅ Creating UserMeal for UserId: {userId}");

                // ✅ ADD THIS DEBUG LOGGING HERE:
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"🔵 CONTROLLER: CreateUserMeal called");
                Console.WriteLine($"🔵 UserId: {userId}");
                Console.WriteLine($"🔵 MealName: {dto.MealName}");
                Console.WriteLine($"🔵 SelectedIngredients count: {dto.SelectedIngredients?.Count ?? 0}");
                
                if (dto.SelectedIngredients != null && dto.SelectedIngredients.Any())
                {
                    Console.WriteLine($"🔵 Ingredients received in controller:");
                    foreach (var ing in dto.SelectedIngredients)
                    {
                        Console.WriteLine($"    ✅ IngredientId: {ing.IngredientId}, Qty: {ing.Quantity}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ NO INGREDIENTS RECEIVED IN CONTROLLER!");
                }
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // ✅ Pass userId as separate parameter (not trusting client input)
                var userMealId = await _userMealService.CreateUserMealAsync(dto, userId);
                return Ok(new { userMealId, message = "UserMeal created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating UserMeal");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets a specific UserMeal by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var userMeal = await _userMealService.GetUserMealByIdAsync(id);
                if (userMeal == null)
                {
                    return NotFound();
                }
                return Ok(userMeal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error fetching UserMeal {id}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// ✅ SECURE: Gets all UserMeals for the authenticated user (uses JWT)
        /// </summary>
        [HttpGet("my-meals")]
        public async Task<IActionResult> GetMyUserMeals()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userMeals = await _userMealService.GetUserMealsByUserIdAsync(userId.Value);
                return Ok(userMeals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error fetching UserMeals for authenticated user");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// ✅ Extract user ID from JWT token
        /// </summary>
        private async Task<int?> GetCurrentUserIdAsync()
        {
            try
            {
                var authId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                             ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(authId) || !Guid.TryParse(authId, out var authGuid))
                {
                    _logger.LogWarning("❌ No valid auth ID found in token");
                    return null;
                }

                // This would need IUserService injected - for simplicity, we'll use HttpContext
                if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
                {
                    return userId;
                }

                _logger.LogWarning("❌ UserId not found in HttpContext");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error extracting user ID from token");
                return null;
            }
        }
    }
}
