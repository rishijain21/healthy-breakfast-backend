using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IngredientCategoriesController : ControllerBase
    {
        private readonly IIngredientCategoryService _service;

        public IngredientCategoriesController(IIngredientCategoryService service)
        {
            _service = service;
        }

        // ADD THIS NEW METHOD ⬇️ (This fixes the 405 error!)
        [HttpGet]
        [AllowAnonymous]   // ← GET is fine public (meal builder needs it)
        public async Task<IActionResult> GetAll()
        {
            var categories = await _service.GetAllIngredientCategoriesAsync();
            return Ok(categories);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]   // ← Only admin creates categories
        public async Task<IActionResult> Create([FromBody] CreateIngredientCategoryDto dto)
        {
            var categoryId = await _service.CreateIngredientCategoryAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = categoryId }, null);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]   // ← GET is fine public
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _service.GetIngredientCategoryByIdAsync(id);
            if (category == null) return NotFound();
            return Ok(category);
        }
    }
}
