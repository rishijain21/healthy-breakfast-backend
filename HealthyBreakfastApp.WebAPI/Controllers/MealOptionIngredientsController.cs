using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Create([FromBody] CreateMealOptionIngredientDto dto)
        {
            var id = await _service.CreateMealOptionIngredientAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetMealOptionIngredientByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }
    }
}
