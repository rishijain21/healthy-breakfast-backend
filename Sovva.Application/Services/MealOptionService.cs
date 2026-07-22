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
        private readonly IUnitOfWork _unitOfWork;

        public MealOptionService(
            IMealOptionRepository repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> CreateMealOptionAsync(CreateMealOptionDto dto)
        {
            var entity = new MealOption
            {
                MealId = dto.MealId,
                CategoryId = dto.CategoryId,
                IsRequired = dto.IsRequired,
                MaxSelectable = dto.MaxSelectable
            };

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            return entity.MealOptionId;
        }

        // ✅ REMOVED GetMealOptionByIdAsync - not needed for admin meal feature
    }
}
