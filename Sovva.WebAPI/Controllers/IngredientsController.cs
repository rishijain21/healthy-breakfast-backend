using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IngredientsController : ControllerBase
    {
        private readonly IIngredientService _ingredientService;

        public IngredientsController(IIngredientService ingredientService)
        {
            _ingredientService = ingredientService;
        }

        // ==================== READ OPERATIONS ====================

        /// <summary>Get all ingredients (cached by frontend)</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var ingredients = await _ingredientService.GetAllIngredientsAsync();
            return Ok(ApiResponse.Ok(ingredients));
        }

        /// <summary>Get ingredients filtered by category</summary>
        [HttpGet("category/{categoryId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory(int categoryId)
        {
            var ingredients = await _ingredientService.GetIngredientsByCategoryIdAsync(categoryId);
            return Ok(ApiResponse.Ok(ingredients));
        }

        /// <summary>Get a single ingredient by ID</summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetIngredientById(int id)
        {
            var ingredient = await _ingredientService.GetIngredientByIdAsync(id);
            if (ingredient == null)
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Ingredient with ID {id} not found"));

            return Ok(ApiResponse.Ok(ingredient));
        }

        // ==================== CREATE OPERATIONS ====================

        /// <summary>
        /// Create a new ingredient
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateIngredient([FromBody] CreateIngredientDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<List<string>>
                    .Fail("VALIDATION_ERROR", string.Join("; ", errors)));
            }

            var ingredientId = await _ingredientService.CreateIngredientAsync(dto);
            return CreatedAtAction(nameof(GetIngredientById), 
                new { id = ingredientId }, ApiResponse.Ok(new { ingredientId, message = "Ingredient created successfully" }));
        }

        // ==================== UPDATE OPERATIONS ====================

        /// <summary>
        /// Update an existing ingredient
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateIngredient(int id, [FromBody] UpdateIngredientDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<List<string>>
                    .Fail("VALIDATION_ERROR", string.Join("; ", errors)));
            }

            var success = await _ingredientService.UpdateIngredientAsync(id, dto);
            if (!success)
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Ingredient with ID {id} not found"));

            return Ok(ApiResponse.Ok(new { message = "Ingredient updated successfully" }));
        }

        /// <summary>
        /// Toggle ingredient availability (active/inactive)
        /// </summary>
        [HttpPatch("{id}/toggle-availability")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var success = await _ingredientService.ToggleIngredientAvailabilityAsync(id);
            if (!success)
                return NotFound(ApiResponse.Fail("NOT_FOUND", $"Ingredient with ID {id} not found"));

            var ingredient = await _ingredientService.GetIngredientByIdAsync(id);
            return Ok(ApiResponse.Ok(new 
            { 
                message = "Availability toggled successfully",
                ingredientId = id,
                available = ingredient?.Available 
            }));
        }

        // ==================== DELETE OPERATIONS ====================

        /// <summary>
        /// Delete an ingredient
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteIngredient(int id)
        {
            try
            {
                var success = await _ingredientService.DeleteIngredientAsync(id);
                if (!success)
                    return NotFound(ApiResponse.Fail("NOT_FOUND", $"Ingredient with ID {id} not found"));

                return Ok(ApiResponse.Ok(new { message = "Ingredient deleted successfully" }));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail("BAD_REQUEST", ex.Message));
            }
        }
    }
}
