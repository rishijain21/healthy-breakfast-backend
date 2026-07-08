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
        private readonly ICacheService _cacheService;

        private const string AllIngredientsCacheKey = "ingredients:all";
        private const string CategoryIngredientsCacheKeyPrefix = "ingredients:category:";
        private const string IngredientByIdCacheKeyPrefix = "ingredients:id:";

        public IngredientService(IIngredientRepository ingredientRepository, ICacheService cacheService)
        {
            _ingredientRepository = ingredientRepository;
            _cacheService = cacheService;
        }

        // ==================== READ OPERATIONS ====================

        public async Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync()
        {
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientDto>>(AllIngredientsCacheKey);
            if (cached != null) return cached;

            var ingredients = await _ingredientRepository.GetAllAsync();
            var result = ingredients.Select(MapToDto).ToList();

            await _cacheService.SetAsync(AllIngredientsCacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }

        public async Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId)
        {
            var cacheKey = CategoryIngredientsCacheKeyPrefix + categoryId;
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientDto>>(cacheKey);
            if (cached != null) return cached;

            var ingredients = await _ingredientRepository.GetByCategoryIdAsync(categoryId);
            var result = ingredients.Select(MapToDto).ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }

        public async Task<IngredientDto?> GetIngredientByIdAsync(int id)
        {
            var cacheKey = IngredientByIdCacheKeyPrefix + id;
            var cached = await _cacheService.GetAsync<IngredientDto>(cacheKey);
            if (cached != null) return cached;

            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            var result = ingredient == null ? null : MapToDto(ingredient);

            if (result != null)
            {
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
            }
            return result;
        }

        // ==================== CREATE OPERATIONS ====================

        public async Task<int> CreateIngredientAsync(CreateIngredientDto dto)
        {
            var ingredient = new Ingredient
            {
                CategoryId = dto.CategoryId,
                IngredientName = dto.IngredientName,
                Price = dto.Price,
                IsAvailable = dto.Available,
                Calories = dto.Calories,
                Protein = dto.Protein,
                Fiber = dto.Fiber,
                Description = dto.Description ?? string.Empty,
                IconEmoji = dto.IconEmoji ?? "🥘"
            };

            await _ingredientRepository.AddIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync(AllIngredientsCacheKey);
            await _cacheService.RemoveAsync(CategoryIngredientsCacheKeyPrefix + ingredient.CategoryId);
            await _cacheService.RemoveAsync(IngredientByIdCacheKeyPrefix + ingredient.IngredientId);
            await _cacheService.RemoveAsync("meals:categories_with_ingredients");

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
            ingredient.IsAvailable = dto.Available;
            ingredient.Calories = dto.Calories;
            ingredient.Protein = dto.Protein;
            ingredient.Fiber = dto.Fiber;
            ingredient.Description = dto.Description;
            ingredient.IconEmoji = dto.IconEmoji;

            await _ingredientRepository.UpdateIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync(AllIngredientsCacheKey);
            await _cacheService.RemoveAsync(CategoryIngredientsCacheKeyPrefix + ingredient.CategoryId);
            await _cacheService.RemoveAsync(IngredientByIdCacheKeyPrefix + ingredient.IngredientId);
            await _cacheService.RemoveAsync("meals:categories_with_ingredients");

            return true;
        }

        public async Task<bool> ToggleIngredientAvailabilityAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null)
                return false;

            ingredient.IsAvailable = !ingredient.IsAvailable;

            await _ingredientRepository.UpdateIngredientAsync(ingredient);
            await _ingredientRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync(AllIngredientsCacheKey);
            await _cacheService.RemoveAsync(CategoryIngredientsCacheKeyPrefix + ingredient.CategoryId);
            await _cacheService.RemoveAsync(IngredientByIdCacheKeyPrefix + ingredient.IngredientId);
            await _cacheService.RemoveAsync("meals:categories_with_ingredients");

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

            await _cacheService.RemoveAsync(AllIngredientsCacheKey);
            await _cacheService.RemoveAsync(CategoryIngredientsCacheKeyPrefix + ingredient.CategoryId);
            await _cacheService.RemoveAsync(IngredientByIdCacheKeyPrefix + ingredient.IngredientId);
            await _cacheService.RemoveAsync("meals:categories_with_ingredients");

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
                Available = ingredient.IsAvailable,
                Calories = ingredient.Calories,
                Protein = ingredient.Protein,
                Fiber = ingredient.Fiber,
                Description = ingredient.Description,
                IconEmoji = ingredient.IconEmoji
            };
        }
    }
}
