using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class MealOptionIngredientService : IMealOptionIngredientService
    {
        private readonly IMealOptionIngredientRepository _repository;

        public MealOptionIngredientService(IMealOptionIngredientRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateMealOptionIngredientAsync(CreateMealOptionIngredientDto dto)
        {
            var entity = new MealOptionIngredient
            {
                MealOptionId = dto.MealOptionId,
                IngredientId = dto.IngredientId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            return entity.MealOptionIngredientId;
        }

        // ✅ REMOVED GetMealOptionIngredientByIdAsync - not needed for admin meal feature
    }
}
