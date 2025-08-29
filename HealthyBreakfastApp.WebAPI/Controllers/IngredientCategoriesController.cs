using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngredientCategoriesController : ControllerBase
    {
        private readonly IIngredientCategoryService _service;

        public IngredientCategoriesController(IIngredientCategoryService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIngredientCategoryDto dto)
        {
            var categoryId = await _service.CreateIngredientCategoryAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = categoryId }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _service.GetIngredientCategoryByIdAsync(id);
            if (category == null) return NotFound();
            return Ok(category);
        }
    }
}
