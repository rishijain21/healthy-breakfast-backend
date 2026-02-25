using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]   // ← ADD: users must be logged in to manage their meal ingredients
    public class UserMealIngredientsController : ControllerBase
    {
        private readonly IUserMealIngredientService _service;

        public UserMealIngredientsController(IUserMealIngredientService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserMealIngredientDto dto)
        {
            var id = await _service.CreateUserMealIngredientAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetUserMealIngredientByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }
    }
}
