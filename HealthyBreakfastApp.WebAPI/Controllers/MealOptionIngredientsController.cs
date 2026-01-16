using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealOptionIngredientsController : ControllerBase
    {
        private readonly IMealOptionIngredientService _service;

        public MealOptionIngredientsController(IMealOptionIngredientService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMealOptionIngredient([FromBody] CreateMealOptionIngredientDto dto)
        {
            var id = await _service.CreateMealOptionIngredientAsync(dto);
            return CreatedAtAction(nameof(CreateMealOptionIngredient), new { id }, null);
        }

        // ✅ COMMENTED OUT - Not needed
        // [HttpGet("{id}")]
        // public async Task<IActionResult> GetMealOptionIngredientById(int id)
        // {
        //     var ingredient = await _service.GetMealOptionIngredientByIdAsync(id);
        //     if (ingredient == null) return NotFound();
        //     return Ok(ingredient);
        // }
    }
}
