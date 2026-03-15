using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly IIngredientRepository _ingredientRepository;

        public IngredientService(IIngredientRepository ingredientRepository)
        {
            _ingredientRepository = ingredientRepository;
        }

        // ==================== READ OPERATIONS ====================

        public async Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync()
        {
            var ingredients = await _ingredientRepository.GetAllAsync();
            return ingredients.Select(MapToDto);
        }

        public async Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId)
        {
            var ingredients = await _ingredientRepository.GetByCategoryIdAsync(categoryId);
            return ingredients.Select(MapToDto);
        }

        public async Task<IngredientDto?> GetIngredientByIdAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            return ingredient == null ? null : MapToDto(ingredient);
        }

        // ==================== CREATE OPERATIONS ====================

        public async Task<int> CreateIngredientAsync(CreateIngredientDto dto)
        {
            var ingredient = new Ingredient
            {
                CategoryId = dto.CategoryId,
                IngredientName = dto.IngredientName,
                Price = dto.Price,
                Available = dto.Available,
                Calories = 0,
                Protein = 0,
                Fiber = 0,
                Description = "",
                IconEmoji = "🥘",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _ingredientRepository.AddIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            return ingredient.IngredientId;
        }

        // ==================== UPDATE OPERATIONS ====================

        public async Task<bool> UpdateIngredientAsync(int id, UpdateIngredientDto dto)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null)
                return false;

            // Update properties
            ingredient.CategoryId = dto.CategoryId;
            ingredient.IngredientName = dto.IngredientName;
            ingredient.Price = dto.Price;
            ingredient.Available = dto.Available;
            ingredient.Calories = dto.Calories;
            ingredient.Protein = dto.Protein;
            ingredient.Fiber = dto.Fiber;
            ingredient.Description = dto.Description;
            ingredient.IconEmoji = dto.IconEmoji;
            ingredient.UpdatedAt = DateTime.UtcNow;

            await _ingredientRepository.UpdateIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ToggleIngredientAvailabilityAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null)
                return false;

            ingredient.Available = !ingredient.Available;
            ingredient.UpdatedAt = DateTime.UtcNow;

            await _ingredientRepository.UpdateIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            return true;
        }

        // ==================== DELETE OPERATIONS ====================

        public async Task<bool> DeleteIngredientAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null)
                return false;

            // Check if ingredient is used in any meals
            var isUsed = await _ingredientRepository.IsIngredientUsedInMealsAsync(id);
            if (isUsed)
            {
                throw new InvalidOperationException(
                    $"Cannot delete ingredient '{ingredient.IngredientName}' because it is used in one or more meals."
                );
            }

            await _ingredientRepository.DeleteIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            return true;
        }

        // ==================== HELPER METHODS ====================

        private static IngredientDto MapToDto(Ingredient ingredient)
        {
            return new IngredientDto
            {
                IngredientId = ingredient.IngredientId,
                CategoryId = ingredient.CategoryId,
                IngredientName = ingredient.IngredientName,
                Price = ingredient.Price,
                Available = ingredient.Available,
                Calories = ingredient.Calories,
                Protein = ingredient.Protein,
                Fiber = ingredient.Fiber,
                Description = ingredient.Description,
                IconEmoji = ingredient.IconEmoji
            };
        }
    }
}
