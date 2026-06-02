using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sovva.WebAPI.Extensions;
using System.Threading.Tasks;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]   // ← ADD: users must be logged in to manage their meal ingredients
    public class UserMealIngredientsController : ControllerBase
    {
        private readonly IUserMealIngredientService _service;
        private readonly IUserMealService _userMealService;
        private readonly ICurrentUserService _currentUserService;

        public UserMealIngredientsController(
            IUserMealIngredientService service, 
            IUserMealService userMealService,
            ICurrentUserService currentUserService)
        {
            _service = service;
            _userMealService = userMealService;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserMealIngredientDto dto)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            // ✅ SEC-02: Verify parent UserMeal belongs to user before adding ingredient
            var userMeal = await _userMealService.GetByIdForUserAsync(dto.UserMealId ?? 0, userId.Value);
            if (userMeal == null)
            {
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied: UserMeal does not belong to user"));
            }

            var id = await _service.CreateUserMealIngredientAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var entity = await _service.GetUserMealIngredientByIdAsync(id);
            if (entity == null) return NotFound(ApiResponse.Fail("NOT_FOUND", "Resource not found"));

            // ✅ SEC-01: Verify parent UserMeal belongs to user
            var userMeal = await _userMealService.GetByIdForUserAsync(entity.UserMealId, userId.Value);
            if (userMeal == null)
            {
                return StatusCode(403, ApiResponse.Fail("FORBIDDEN", "Access denied"));
            }

            return Ok(ApiResponse.Ok(entity));
        }
    }
}
