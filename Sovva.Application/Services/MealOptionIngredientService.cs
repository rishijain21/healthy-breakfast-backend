using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class MealOptionIngredientService : IMealOptionIngredientService
    {
        private readonly IMealOptionIngredientRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public MealOptionIngredientService(
            IMealOptionIngredientRepository repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> CreateMealOptionIngredientAsync(CreateMealOptionIngredientDto dto)
        {
            var entity = new MealOptionIngredient
            {
                MealOptionId = dto.MealOptionId,
                IngredientId = dto.IngredientId
            };

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            return entity.MealOptionIngredientId;
        }

        // ✅ REMOVED GetMealOptionIngredientByIdAsync - not needed for admin meal feature
    }
}
