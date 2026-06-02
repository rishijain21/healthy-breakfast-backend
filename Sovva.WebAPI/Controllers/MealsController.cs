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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("default")]
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
        [ProducesResponseType(typeof(PagedResult<MealDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetPublicMeals(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (pageSize > 50)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Maximum page size is 50"));

            var result = await _mealService.GetActiveMealsAsync(page, pageSize);
            return Ok(ApiResponse.Ok(result));
        }

        /// <summary>
        /// Returns ALL active meals as a flat array (no pagination wrapper).
        /// Used by the Angular menu component to avoid having to unwrap a paginated response.
        /// </summary>
        [HttpGet("public/all")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<MealDto>), 200)]
        public async Task<IActionResult> GetAllPublicMeals()
        {
            // Fetch up to 200 meals in one shot — menu never has more than that
            var result = await _mealService.GetActiveMealsAsync(1, 200);
            return Ok(ApiResponse.Ok(result.Items));
        }


        // ✅ ADD THIS — meal details for logged-in users (meal builder)
        // ✅ FIX: Use user-facing method that returns MealWithDetailsDto (not AdminMealDetailDto)
        [HttpGet("{id}/details")]
        [Authorize]
        [ProducesResponseType(typeof(MealWithDetailsDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMealDetails(int id)
        {
            // ✅ FIX: Use user-facing batch method for single meal too
            var meals = await _mealService.GetMealsBatchDetailsForUsersAsync(new List<int> { id });
            var meal = meals.FirstOrDefault();
            
            if (meal == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal with ID {id} not found or not available"));

            return Ok(ApiResponse.Ok(meal));
        }

        // ✅ ADD after GetMealDetails — batch meal details for logged-in users
        // ✅ FIX 1,2,3,4: Use user-facing method, single query, remove duplicates, preserve order, remove redundant catch
        [HttpPost("batch-details")]
        [Authorize]
        public async Task<IActionResult> GetMealsBatchDetails([FromBody] BatchMealRequestDto request)
        {
            if (request == null || request.MealIds == null || request.MealIds.Count == 0)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "No meal IDs provided"));

            if (request.MealIds.Count > 20)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Maximum 20 meals per batch request"));

            // ✅ FIX 1: Use user-facing method that returns MealWithDetailsDto (not AdminMealDetailDto)
            // ✅ FIX 2: Single DB query (N+1 fixed via WHERE IN)
            // ✅ FIX 3: Duplicates removed and order preserved in service method
            // ✅ FIX 4: Removed redundant catch - GlobalExceptionMiddleware handles errors
            var meals = await _mealService.GetMealsBatchDetailsForUsersAsync(request.MealIds);
            return Ok(ApiResponse.Ok(meals));
        }

        // ========== ADMIN ENDPOINTS ==========

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
            return Ok(ApiResponse.Ok(result));
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
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal with ID {id} not found"));
            
            return Ok(ApiResponse.Ok(meal));
        }

        /// <summary>
        /// Get categories with ingredients (Admin only)
        /// </summary>
        [HttpGet("admin/categories-with-ingredients")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(List<CategoryWithIngredientsDto>), 200)]
        public async Task<IActionResult> GetCategoriesWithIngredients()
        {
            var result = await _mealService.GetCategoriesWithIngredientsAsync();
            return Ok(ApiResponse.Ok(result));
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
            return CreatedAtAction(nameof(GetMealDetailForAdmin), new { id = mealId }, ApiResponse.Ok(new { mealId, message = "Meal created successfully" }));
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
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal with ID {id} not found"));
            
            return Ok(ApiResponse.Ok(new { message = "Meal updated successfully" }));
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
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal with ID {id} not found"));
            
            return Ok(ApiResponse.Ok(new { message = "Meal deleted successfully" }));
        }


        [HttpPost("admin/{id}/image")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadMealImage(int id, IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "No image provided"));

            // ✅ FIX 8: Validate file type and size
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(image.FileName).ToLower();
            
            if (!allowedExtensions.Contains(ext))
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Only JPG, PNG, and WebP images are allowed"));
            
            if (image.Length > 10 * 1024 * 1024) // 10MB limit
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", "Image size cannot exceed 10MB"));

            var fileName = $"meal-{id}/{Guid.NewGuid():N}{ext}";
            var imageUrl = await _storageService.UploadImageAsync(image, fileName);

            // ✅ Use service method instead of direct DB access
            var success = await _mealService.UpdateMealImageAsync(id, imageUrl);
            if (!success) return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal {id} not found"));

            _logger.LogInformation("Image uploaded for meal {MealId}: {Url}", id, imageUrl);
            return Ok(ApiResponse.Ok(new { imageUrl, message = "Image uploaded successfully" }));
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
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Meal {id} not found"));
            if (string.IsNullOrEmpty(existingUrl))
                return Ok(ApiResponse.Ok(new { message = "No image to delete" }));

            // Delete from storage
            await _storageService.DeleteImageAsync(existingUrl);

            _logger.LogInformation("Image deleted for meal {MealId}", id);
            return Ok(ApiResponse.Ok(new { message = "Image deleted successfully" }));
        }
    }
}
