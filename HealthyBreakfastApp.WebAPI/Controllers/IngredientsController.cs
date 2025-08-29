using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngredientsController : ControllerBase
    {
        private readonly IIngredientService _ingredientService;

        public IngredientsController(IIngredientService ingredientService)
        {
            _ingredientService = ingredientService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateIngredient([FromBody] CreateIngredientDto dto)
        {
            var ingredientId = await _ingredientService.CreateIngredientAsync(dto);
            return CreatedAtAction(nameof(GetIngredientById), new { id = ingredientId }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetIngredientById(int id)
        {
            var ingredient = await _ingredientService.GetIngredientByIdAsync(id);
            if (ingredient == null) return NotFound();
            return Ok(ingredient);
        }
    }
}
