using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]   // ← ADD: entire controller is admin-only
    public class MealOptionsController : ControllerBase
    {
        private readonly IMealOptionService _service;

        public MealOptionsController(IMealOptionService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMealOption([FromBody] CreateMealOptionDto dto)
        {
            var id = await _service.CreateMealOptionAsync(dto);
            return CreatedAtAction(nameof(CreateMealOption), new { id }, null);
        }

        // ✅ COMMENTED OUT - Not needed
        // [HttpGet("{id}")]
        // public async Task<IActionResult> GetMealOptionById(int id)
        // {
        //     var mealOption = await _service.GetMealOptionByIdAsync(id);
        //     if (mealOption == null) return NotFound();
        //     return Ok(mealOption);
        // }
    }
}
