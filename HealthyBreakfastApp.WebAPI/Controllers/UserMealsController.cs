using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserMealsController : ControllerBase
    {
        private readonly IUserMealService _service;

        public UserMealsController(IUserMealService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserMealDto dto)
        {
            var id = await _service.CreateUserMealAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetUserMealByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpGet("ByUser/{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            var entities = await _service.GetUserMealsByUserIdAsync(userId);
            return Ok(entities);
        }
    }
}
