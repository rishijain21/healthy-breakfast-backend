using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly IIngredientRepository _ingredientRepository;

        public IngredientService(IIngredientRepository ingredientRepository)
        {
            _ingredientRepository = ingredientRepository;
        }

        // FIXED: Include all nutritional fields in mapping
        public async Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync()
        {
            var ingredients = await _ingredientRepository.GetAllAsync();
            return ingredients.Select(ingredient => new IngredientDto
            {
                IngredientId = ingredient.IngredientId,
                CategoryId = ingredient.CategoryId,
                IngredientName = ingredient.IngredientName,
                Price = ingredient.Price,
                Available = ingredient.Available,
                CreatedAt = ingredient.CreatedAt,
                UpdatedAt = ingredient.UpdatedAt,
                // ✅ ADD THESE NUTRITIONAL FIELDS:
                Calories = ingredient.Calories,
                Protein = ingredient.Protein,
                Fiber = ingredient.Fiber,
                Description = ingredient.Description,
                IconEmoji = ingredient.IconEmoji
            });
        }

        // FIXED: Include all nutritional fields in mapping
        public async Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId)
        {
            var ingredients = await _ingredientRepository.GetByCategoryIdAsync(categoryId);
            return ingredients.Select(ingredient => new IngredientDto
            {
                IngredientId = ingredient.IngredientId,
                CategoryId = ingredient.CategoryId,
                IngredientName = ingredient.IngredientName,
                Price = ingredient.Price,
                Available = ingredient.Available,
                CreatedAt = ingredient.CreatedAt,
                UpdatedAt = ingredient.UpdatedAt,
                // ✅ ADD THESE NUTRITIONAL FIELDS:
                Calories = ingredient.Calories,
                Protein = ingredient.Protein,
                Fiber = ingredient.Fiber,
                Description = ingredient.Description,
                IconEmoji = ingredient.IconEmoji
            });
        }

        public async Task<int> CreateIngredientAsync(CreateIngredientDto dto)
        {
            var ingredient = new Ingredient
            {
                CategoryId = dto.CategoryId,
                IngredientName = dto.IngredientName,
                Price = dto.Price,
                Available = dto.Available,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _ingredientRepository.AddIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            return ingredient.IngredientId;
        }

        // FIXED: Include all nutritional fields in mapping
        public async Task<IngredientDto?> GetIngredientByIdAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null) return null;

            return new IngredientDto
            {
                IngredientId = ingredient.IngredientId,
                CategoryId = ingredient.CategoryId,
                IngredientName = ingredient.IngredientName,
                Price = ingredient.Price,
                Available = ingredient.Available,
                CreatedAt = ingredient.CreatedAt,
                UpdatedAt = ingredient.UpdatedAt,
                // ✅ ADD THESE NUTRITIONAL FIELDS:
                Calories = ingredient.Calories,
                Protein = ingredient.Protein,
                Fiber = ingredient.Fiber,
                Description = ingredient.Description,
                IconEmoji = ingredient.IconEmoji
            };
        }
    }
}
