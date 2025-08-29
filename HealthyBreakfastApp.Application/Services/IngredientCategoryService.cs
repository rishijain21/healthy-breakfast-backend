using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class IngredientCategoryService : IIngredientCategoryService
    {
        private readonly IIngredientCategoryRepository _repository;

        public IngredientCategoryService(IIngredientCategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateIngredientCategoryAsync(CreateIngredientCategoryDto dto)
        {
            var entity = new IngredientCategory
            {
                CategoryName = dto.CategoryName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();
            return entity.CategoryId;
        }

        public async Task<IngredientCategoryDto?> GetIngredientCategoryByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return null;
            return new IngredientCategoryDto
            {
                CategoryId = entity.CategoryId,
                CategoryName = entity.CategoryName,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
