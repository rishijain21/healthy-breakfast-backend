using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class MealOptionService : IMealOptionService
    {
        private readonly IMealOptionRepository _repository;

        public MealOptionService(IMealOptionRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateMealOptionAsync(CreateMealOptionDto dto)
        {
            var entity = new MealOption
            {
                MealId = dto.MealId,
                CategoryId = dto.CategoryId,
                IsRequired = dto.IsRequired,
                MaxSelectable = dto.MaxSelectable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            return entity.MealOptionId;
        }

        // ✅ REMOVED GetMealOptionByIdAsync - not needed for admin meal feature
    }
}
