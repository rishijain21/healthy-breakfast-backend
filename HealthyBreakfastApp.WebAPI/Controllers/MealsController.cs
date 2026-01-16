using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealsController : ControllerBase
    {
        private readonly IMealService _mealService;

        public MealsController(IMealService mealService)
        {
            _mealService = mealService;
        }

        // ========== EXISTING CUSTOMER ENDPOINTS ==========

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
        public async Task<ActionResult<List<AdminMealListDto>>> GetAllMealsForAdmin()
        {
            try
            {
                var meals = await _mealService.GetAllMealsForAdminAsync();
                return Ok(meals);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving meals", error = ex.Message });
            }
        }

        /// <summary>
        /// Get meal details with all options and ingredients for admin editing
        /// </summary>
        [HttpGet("admin/{id}")]
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
                return StatusCode(500, new { message = "Error retrieving meal details", error = ex.Message });
            }
        }

        /// <summary>
        /// Create meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPost("admin")]
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
                return StatusCode(500, new { message = "Error creating meal", error = ex.Message });
            }
        }

        /// <summary>
        /// Update meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPut("admin/{id}")]
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
                return StatusCode(500, new { message = "Error updating meal", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete meal (Admin only) - Cascades to meal options and ingredients
        /// </summary>
        [HttpDelete("admin/{id}")]
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
                return StatusCode(500, new { message = "Error deleting meal", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all categories with their available ingredients (for meal builder UI)
        /// </summary>
        [HttpGet("admin/categories-with-ingredients")]
        public async Task<ActionResult<List<CategoryWithIngredientsDto>>> GetCategoriesWithIngredients()
        {
            try
            {
                var categories = await _mealService.GetCategoriesWithIngredientsAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving categories", error = ex.Message });
            }
        }
    }
}
