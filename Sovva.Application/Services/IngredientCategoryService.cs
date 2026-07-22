using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Application.Common.Infrastructure;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class IngredientCategoryService : IIngredientCategoryService
    {
        private readonly IIngredientCategoryRepository _repository;
        private readonly ICacheService _cacheService;
        private readonly IUnitOfWork _unitOfWork;

        public IngredientCategoryService(
            IIngredientCategoryRepository repository,
            ICacheService cacheService,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _cacheService = cacheService;
            _unitOfWork = unitOfWork;
        }

        // ADD THIS NEW METHOD ⬇️
        public async Task<IEnumerable<IngredientCategoryDto>> GetAllIngredientCategoriesAsync()
        {
            var cached = await _cacheService.GetAsync<IEnumerable<IngredientCategoryDto>>(CacheKeys.CategoriesAll());
            if (cached != null) return cached;

            var entities = await _repository.GetAllAsync();
            var result = entities.Select(entity => new IngredientCategoryDto
            {
                CategoryId = entity.CategoryId,
                CategoryName = entity.CategoryName,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            }).ToList();

            await _cacheService.SetAsync(CacheKeys.CategoriesAll(), result, TimeSpan.FromMinutes(60));
            return result;
        }

        public async Task<int> CreateIngredientCategoryAsync(CreateIngredientCategoryDto dto)
        {
            var entity = new IngredientCategory
            {
                CategoryName = dto.CategoryName
            };
            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            await _cacheService.RemoveAsync(CacheKeys.CategoriesAll());
            await _cacheService.RemoveAsync(CacheKeys.CategoryById(entity.CategoryId));
            await _cacheService.RemoveAsync(CacheKeys.MealCategories());

            return entity.CategoryId;
        }

        public async Task<IngredientCategoryDto?> GetIngredientCategoryByIdAsync(int id)
        {
            var cacheKey = CacheKeys.CategoryById(id);
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
