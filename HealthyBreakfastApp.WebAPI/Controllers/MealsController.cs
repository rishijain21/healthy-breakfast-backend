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
    }
}
