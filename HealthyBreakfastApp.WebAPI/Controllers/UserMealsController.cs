using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        /// Creates a new UserMeal (for subscription meal template)
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

                // ✅ FIX: Set UserId from authenticated context - don't trust client input
                dto.UserId = userId;

                var userMealId = await _userMealService.CreateUserMealAsync(dto);
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
        /// Gets all UserMeals for a specific user
        /// </summary>
        [HttpGet("ByUser/{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            try
            {
                var userMeals = await _userMealService.GetUserMealsByUserIdAsync(userId);
                return Ok(userMeals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error fetching UserMeals for user {userId}");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
