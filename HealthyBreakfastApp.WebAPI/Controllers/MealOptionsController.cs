using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MealOptionsController : ControllerBase
    {
        private readonly IMealOptionService _service;

        public MealOptionsController(IMealOptionService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMealOptionDto dto)
        {
            var id = await _service.CreateMealOptionAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetMealOptionByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }
    }
}
