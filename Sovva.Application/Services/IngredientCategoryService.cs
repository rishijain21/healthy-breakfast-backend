using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class IngredientCategoryService : IIngredientCategoryService
    {
        private readonly IIngredientCategoryRepository _repository;
        private readonly ICacheService _cacheService;

        private const string AllCategoriesCacheKey = "categories:all";
        private const string CategoryByIdCacheKeyPrefix = "categories:id:";

        public IngredientCategoryService(IIngredientCategoryRepository repository, ICacheService cacheService)
        {
            _repository = repository;
            _cacheService = cacheService;
        }

        // ADD THIS NEW METHOD ⬇️
        public async Task<IEnumerable<IngredientCategoryDto>> GetAllIngredientCategoriesAsync()
        {
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientCategoryDto>>(AllCategoriesCacheKey);
            if (cached != null) return cached;

            var entities = await _repository.GetAllAsync();
            var result = entities.Select(entity => new IngredientCategoryDto
            {
                CategoryId = entity.CategoryId,
                CategoryName = entity.CategoryName,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            }).ToList();

            await _cacheService.SetAsync(AllCategoriesCacheKey, result, TimeSpan.FromMinutes(60));
            return result;
        }

        public async Task<int> CreateIngredientCategoryAsync(CreateIngredientCategoryDto dto)
        {
            var entity = new IngredientCategory
            {
                CategoryName = dto.CategoryName
            };
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            await _cacheService.RemoveAsync(AllCategoriesCacheKey);
            await _cacheService.RemoveAsync(CategoryByIdCacheKeyPrefix + entity.CategoryId);
            await _cacheService.RemoveAsync("meals:categories_with_ingredients");

            return entity.CategoryId;
        }

        public async Task<IngredientCategoryDto?> GetIngredientCategoryByIdAsync(int id)
        {
            var cacheKey = CategoryByIdCacheKeyPrefix + id;
            var cached = await _cacheService.GetAsync<IngredientCategoryDto>(cacheKey);
            if (cached != null) return cached;

            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return null;
            
            var result = new IngredientCategoryDto
            {
                CategoryId = entity.CategoryId,
                CategoryName = entity.CategoryName,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60));
            return result;
        }
    }
}
