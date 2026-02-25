using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealsController : ControllerBase
    {
        private readonly IMealService _mealService;
        private readonly AppDbContext _context;
        private readonly ILogger<MealsController> _logger;

        public MealsController(IMealService mealService, AppDbContext context, ILogger<MealsController> logger)
        {
            _mealService = mealService;
            _context = context;
            _logger = logger;
        }

        // ========== EXISTING CUSTOMER ENDPOINTS ==========

        // ✅ Public endpoint — no [Authorize] needed
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicMeals()
        {
            var meals = await _mealService.GetActiveMealsAsync();
            return Ok(meals);
        }

        // ✅ ADD THIS — meal details for logged-in users (meal builder)
        [HttpGet("{id}/details")]
        [Authorize]
        public async Task<IActionResult> GetMealDetails(int id)
        {
            try
            {
                var meal = await _mealService.GetMealDetailForAdminAsync(id);
                if (meal == null)
                    return NotFound(new { message = $"Meal with ID {id} not found" });

                return Ok(meal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving meal details");
                return StatusCode(500, new { message = "Error retrieving meal details" });
            }
        }

        // ✅ ADD after GetMealDetails — batch meal details for logged-in users
        [HttpPost("batch-details")]
        [Authorize]
        public async Task<IActionResult> GetMealsBatchDetails([FromBody] BatchMealRequestDto request)
        {
            try
            {
                if (request.MealIds == null || request.MealIds.Count == 0)
                    return BadRequest(new { message = "No meal IDs provided" });

                if (request.MealIds.Count > 20)
                    return BadRequest(new { message = "Maximum 20 meals per batch request" });

                var meals = await _mealService.GetMealsBatchDetailsAsync(request.MealIds);
                return Ok(meals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch meal details");
                return StatusCode(500, new { message = "Error retrieving meal details" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateMeal([FromBody] CreateMealDto dto)
        {
            var mealId = await _mealService.CreateMealAsync(dto);
            return CreatedAtAction(nameof(GetMealById), new { id = mealId }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMealById(int id)
        {
            var meal = await _mealService.GetMealByIdAsync(id);
            if (meal == null) return NotFound();
            return Ok(meal);
        }

        [HttpPost("calculate-price")]
        public async Task<ActionResult<MealPriceResponseDto>> CalculateMealPrice([FromBody] MealPriceCalculationDto calculationDto)
        {
            try
            {
                var priceResponse = await _mealService.CalculateMealPriceAsync(calculationDto);
                return Ok(priceResponse);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("validate-selection")]
        public async Task<ActionResult<bool>> ValidateIngredientSelection([FromBody] MealPriceCalculationDto calculationDto)
        {
            try
            {
                var isValid = await _mealService.ValidateIngredientSelectionAsync(
                    calculationDto.MealId, 
                    calculationDto.SelectedIngredients);
                
                return Ok(new { isValid, message = isValid ? "Selection is valid" : "Invalid ingredient selection" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("nutritional-summary")]
        public async Task<ActionResult> GetNutritionalSummary([FromBody] List<SelectedIngredientDto> ingredients)
        {
            try
            {
                var (calories, protein, fiber) = await _mealService.GetNutritionalSummaryAsync(ingredients);
                
                return Ok(new 
                { 
                    totalCalories = calories, 
                    totalProtein = protein, 
                    totalFiber = fiber,
                    ingredientCount = ingredients.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ========== NEW ADMIN ENDPOINTS ==========

        /// <summary>
        /// Get all meals for admin dashboard (with meal options count and completion status)
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AdminMealListDto>>> GetAllMealsForAdmin()
        {
            try
            {
                var meals = await _mealService.GetAllMealsForAdminAsync();
                return Ok(meals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving meals");
                return StatusCode(500, new { message = "Error retrieving meals" });
            }
        }

        /// <summary>
        /// Get meal details with all options and ingredients for admin editing
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AdminMealDetailDto>> GetMealDetailForAdmin(int id)
        {
            try
            {
                var meal = await _mealService.GetMealDetailForAdminAsync(id);
                if (meal == null) 
                    return NotFound(new { message = $"Meal with ID {id} not found" });
                
                return Ok(meal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving meal details");
                return StatusCode(500, new { message = "Error retrieving meal details" });
            }
        }

        /// <summary>
        /// Create meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPost("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMealWithOptions([FromBody] AdminCreateMealDto dto)
        {
            try
            {
                var mealId = await _mealService.CreateMealWithOptionsAsync(dto);
                return CreatedAtAction(nameof(GetMealDetailForAdmin), new { id = mealId }, new { mealId, message = "Meal created successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating meal");
                return StatusCode(500, new { message = "Error creating meal" });
            }
        }

        /// <summary>
        /// Update meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPut("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMeal(int id, [FromBody] UpdateMealDto dto)
        {
            try
            {
                var success = await _mealService.UpdateMealAsync(id, dto);
                if (!success) 
                    return NotFound(new { message = $"Meal with ID {id} not found" });
                
                return Ok(new { message = "Meal updated successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating meal");
                return StatusCode(500, new { message = "Error updating meal" });
            }
        }

        /// <summary>
        /// Delete meal (Admin only) - Cascades to meal options and ingredients
        /// </summary>
        [HttpDelete("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMeal(int id)
        {
            try
            {
                var success = await _mealService.DeleteMealAsync(id);
                if (!success) 
                    return NotFound(new { message = $"Meal with ID {id} not found" });
                
                return Ok(new { message = "Meal deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting meal");
                return StatusCode(500, new { message = "Error deleting meal" });
            }
        }

        /// <summary>
        /// Get all categories with their available ingredients (for meal builder UI)
        /// </summary>
        [HttpGet("admin/categories-with-ingredients")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<CategoryWithIngredientsDto>>> GetCategoriesWithIngredients()
        {
            try
            {
                var categories = await _mealService.GetCategoriesWithIngredientsAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving categories");
                return StatusCode(500, new { message = "Error retrieving categories" });
            }
        }

        /// <summary>
        /// Get all meals with details (bulk endpoint for admin)
        /// </summary>
        [HttpGet("admin/all-with-details")]
        [Authorize]
        public async Task<ActionResult<List<MealWithDetailsDto>>> GetAllMealsWithDetails()
        {
            try
            {
                var meals = await _context.Meals
                    .Include(m => m.MealOptions)
                        .ThenInclude(o => o.MealOptionIngredients)
                            .ThenInclude(moi => moi.Ingredient)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(meals.Select(m => new MealWithDetailsDto
                {
                    MealId = m.MealId,
                    MealName = m.MealName,
                    Description = m.Description,
                    BasePrice = m.BasePrice,
                    ApproxCalories = m.ApproxCalories,
                    ApproxProtein = m.ApproxProtein,
                    ApproxCarbs = m.ApproxCarbs,
                    ApproxFats = m.ApproxFats,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                    MealOptionsCount = m.MealOptions.Count,
                    MealOptions = m.MealOptions.Select(o => new MealOptionDto
                    {
                        MealOptionId = o.MealOptionId,
                        MealId = o.MealId,
                        CategoryId = o.CategoryId,
                        IsRequired = o.IsRequired,
                        MaxSelectable = o.MaxSelectable,
                        CreatedAt = o.CreatedAt,
                        UpdatedAt = o.UpdatedAt
                    }).ToList()
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving meals with details");
                return StatusCode(500, new { message = "Error retrieving meals with details" });
            }
        }
    }
}
