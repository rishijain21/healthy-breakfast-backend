using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Application.Common.Infrastructure;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly IIngredientRepository _ingredientRepository;
        private readonly ICacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;

        public IngredientService(
            IIngredientRepository ingredientRepository,
            ICacheService cacheService,
            IUnitOfWork unitOfWork)
        {
            _ingredientRepository = ingredientRepository;
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }

        // ==================== READ OPERATIONS ====================

        public async Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync()
        {
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientDto>>(CacheKeys.IngredientsAll());
            if (cached != null) return cached;

            var ingredients = await _ingredientRepository.GetAllAsync();
            var result = ingredients.Select(MapToDto).ToList();

            await _cacheService.SetAsync(CacheKeys.IngredientsAll(), result, TimeSpan.FromMinutes(30));
            return result;
        }

        public async Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId)
        {
            var cacheKey = CacheKeys.IngredientsByCategory(categoryId);
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientDto>>(cacheKey);
            if (cached != null) return cached;

            var ingredients = await _ingredientRepository.GetByCategoryIdAsync(categoryId);
            var result = ingredients.Select(MapToDto).ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }

        public async Task<IngredientDto?> GetIngredientByIdAsync(int id)
        {
            var cacheKey = CacheKeys.IngredientById(id);
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
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            await _cacheService.RemoveAsync(CacheKeys.IngredientsAll());
            await _cacheService.RemoveAsync(CacheKeys.IngredientsByCategory(ingredient.CategoryId));
            await _cacheService.RemoveAsync(CacheKeys.IngredientById(ingredient.IngredientId));
            await _cacheService.RemoveAsync(CacheKeys.MealCategories());

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
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            await _cacheService.RemoveAsync(CacheKeys.IngredientsAll());
            await _cacheService.RemoveAsync(CacheKeys.IngredientsByCategory(ingredient.CategoryId));
            await _cacheService.RemoveAsync(CacheKeys.IngredientById(ingredient.IngredientId));
            await _cacheService.RemoveAsync(CacheKeys.MealCategories());

            return true;
        }

        public async Task<bool> ToggleIngredientAvailabilityAsync(int id)
        {
            var ingredient = await _ingredientRepository.GetByIdAsync(id);
            if (ingredient == null)
                return false;

            ingredient.IsAvailable = !ingredient.IsAvailable;

            await _ingredientRepository.UpdateIngredientAsync(ingredient);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            await _cacheService.RemoveAsync(CacheKeys.IngredientsAll());
            await _cacheService.RemoveAsync(CacheKeys.IngredientsByCategory(ingredient.CategoryId));
            await _cacheService.RemoveAsync(CacheKeys.IngredientById(ingredient.IngredientId));
            await _cacheService.RemoveAsync(CacheKeys.MealCategories());

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
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            await _cacheService.RemoveAsync(CacheKeys.IngredientsAll());
            await _cacheService.RemoveAsync(CacheKeys.IngredientsByCategory(ingredient.CategoryId));
            await _cacheService.RemoveAsync(CacheKeys.IngredientById(ingredient.IngredientId));
            await _cacheService.RemoveAsync(CacheKeys.MealCategories());

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
