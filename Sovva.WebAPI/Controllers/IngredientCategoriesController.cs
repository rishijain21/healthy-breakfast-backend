using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
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

        /// <summary>
        /// Get all ingredient categories (used by meal builder UI)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _service.GetAllIngredientCategoriesAsync();
            return Ok(ApiResponse.Ok(categories));
        }
    }
}
