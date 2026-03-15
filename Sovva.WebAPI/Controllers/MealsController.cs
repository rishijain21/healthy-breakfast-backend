using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealsController : ControllerBase
    {
        private readonly IMealService _mealService;
        private readonly ILogger<MealsController> _logger;
        private readonly ISupabaseStorageService _storageService;

        public MealsController(IMealService mealService, ILogger<MealsController> logger, ISupabaseStorageService storageService)
        {
            _mealService = mealService;
            _logger = logger;
            _storageService = storageService;
        }

        // ========== EXISTING CUSTOMER ENDPOINTS ==========

        // ✅ Public endpoint — no [Authorize] needed
        [HttpGet("public")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<MealDto>), 200)]
        public async Task<IActionResult> GetPublicMeals()
        {
            var meals = await _mealService.GetActiveMealsAsync();
            return Ok(meals);
        }

        // ✅ ADD THIS — meal details for logged-in users (meal builder)
        [HttpGet("{id}/details")]
        [Authorize]
        [ProducesResponseType(typeof(AdminMealDetailDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
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

        // ✅ FIX 9: Add authorization to legacy endpoints (or remove if not needed)
        // These appear to be legacy endpoints - prefer the admin versions below
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMeal([FromBody] CreateMealDto dto)
        {
            var mealId = await _mealService.CreateMealAsync(dto);
            return CreatedAtAction(nameof(GetMealById), new { id = mealId }, null);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMealById(int id)
        {
            var meal = await _mealService.GetMealByIdAsync(id);
            if (meal == null) return NotFound();
            return Ok(meal);
        }

        [HttpPost("calculate-price")]
        [Authorize]
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
        [Authorize]
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
        [Authorize]
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
        /// Get paginated meals for admin dashboard
        /// Usage: GET /api/meals/admin/all?page=1&pageSize=20
        /// </summary>
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PagedResult<AdminMealListDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PagedResult<AdminMealListDto>>> GetAllMealsForAdmin(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _mealService.GetAllMealsForAdminPagedAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Get meal details with all options and ingredients for admin editing
        /// </summary>
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AdminMealDetailDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<AdminMealDetailDto>> GetMealDetailForAdmin(int id)
        {
            var meal = await _mealService.GetMealDetailForAdminAsync(id);
            if (meal == null) 
                return NotFound(new { message = $"Meal with ID {id} not found" });
            
            return Ok(meal);
        }

        /// <summary>
        /// Create meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPost("admin")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(CreatedAtActionResult), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> CreateMealWithOptions([FromBody] AdminCreateMealDto dto)
        {
            var mealId = await _mealService.CreateMealWithOptionsAsync(dto);
            return CreatedAtAction(nameof(GetMealDetailForAdmin), new { id = mealId }, new { mealId, message = "Meal created successfully" });
        }

        /// <summary>
        /// Update meal with options and ingredients (Admin only)
        /// </summary>
        [HttpPut("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMeal(int id, [FromBody] UpdateMealDto dto)
        {
            var success = await _mealService.UpdateMealAsync(id, dto);
            if (!success) 
                return NotFound(new { message = $"Meal with ID {id} not found" });
            
            return Ok(new { message = "Meal updated successfully" });
        }

        /// <summary>
        /// Delete meal (Admin only) - Cascades to meal options and ingredients
        /// </summary>
        [HttpDelete("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMeal(int id)
        {
            var success = await _mealService.DeleteMealAsync(id);
            if (!success) 
                return NotFound(new { message = $"Meal with ID {id} not found" });
            
            return Ok(new { message = "Meal deleted successfully" });
        }

        /// <summary>
        /// Update meal completion status (Admin only) - PATCH endpoint
        /// </summary>
        [HttpPatch("admin/{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMealStatus(int id, [FromBody] UpdateMealStatusDto dto)
        {
            var updated = await _mealService.UpdateMealStatusAsync(id, dto.IsComplete);

            if (!updated)
                return NotFound(new { message = $"Meal with ID {id} not found." });

            return Ok(new { id, isComplete = dto.IsComplete, message = "Meal status updated successfully." });
        }

        /// <summary>
        /// Get all categories with their available ingredients (for meal builder UI)
        /// </summary>
        [HttpGet("admin/categories-with-ingredients")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<CategoryWithIngredientsDto>>> GetCategoriesWithIngredients()
        {
            var categories = await _mealService.GetCategoriesWithIngredientsAsync();
            return Ok(categories);
        }

        /// <summary>
        /// Get all meals with details (bulk endpoint for admin)
        /// </summary>
        [HttpGet("admin/all-with-details")]
        [Authorize]
        public async Task<ActionResult<List<AdminMealListDto>>> GetAllMealsWithDetails()
        {
            // ✅ Use existing service method — no direct DB access
            var meals = await _mealService.GetAllMealsForAdminAsync();
            return Ok(meals);
        }

        /// <summary>
        /// Upload image for a meal (Admin only)
        /// </summary>
        [HttpPost("admin/{id}/image")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadMealImage(int id, IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "No image provided" });

            // ✅ FIX 8: Validate file type and size
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(image.FileName).ToLower();
            
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed" });
            
            if (image.Length > 5 * 1024 * 1024) // 5MB limit
                return BadRequest(new { message = "Image size cannot exceed 5MB" });

            var fileName = $"meal-{id}/{Guid.NewGuid():N}{ext}";
            var imageUrl = await _storageService.UploadImageAsync(image, fileName);

            // ✅ Use service method instead of direct DB access
            var success = await _mealService.UpdateMealImageAsync(id, imageUrl);
            if (!success) return NotFound(new { message = $"Meal {id} not found" });

            _logger.LogInformation("Image uploaded for meal {MealId}: {Url}", id, imageUrl);
            return Ok(new { imageUrl, message = "Image uploaded successfully" });
        }

        /// <summary>
        /// Delete image for a meal (Admin only)
        /// </summary>
        [HttpDelete("admin/{id}/image")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMealImage(int id)
        {
            // ✅ Use service method to get existing URL and clear it
            var existingUrl = await _mealService.DeleteMealImageAsync(id);
            if (existingUrl == null)
                return NotFound(new { message = $"Meal {id} not found" });
            if (string.IsNullOrEmpty(existingUrl))
                return Ok(new { message = "No image to delete" });

            // Delete from storage
            await _storageService.DeleteImageAsync(existingUrl);

            _logger.LogInformation("Image deleted for meal {MealId}", id);
            return Ok(new { message = "Image deleted successfully" });
        }
    }
}
