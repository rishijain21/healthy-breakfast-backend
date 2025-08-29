using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }


        [HttpPost]

        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetUserById), new { id = userId }, null);
        }

       [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var userDto = await _userService.GetUserByIdAsync(id);
            if (userDto == null)
                return NotFound();

            return Ok(userDto);
        }
    }
}
