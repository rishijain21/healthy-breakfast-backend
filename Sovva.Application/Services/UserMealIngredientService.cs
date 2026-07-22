using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class UserMealIngredientService : IUserMealIngredientService
    {
        private readonly IUserMealIngredientRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public UserMealIngredientService(
            IUserMealIngredientRepository repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> CreateUserMealIngredientAsync(CreateUserMealIngredientDto dto)
        {
            var entity = new UserMealIngredient
            {
                UserMealId = dto.UserMealId ?? 0, // Handle nullable
                IngredientId = dto.IngredientId,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.TotalPrice
            };

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            return entity.UserMealIngredientId;
        }

        public async Task CreateUserMealIngredientsAsync(IEnumerable<CreateUserMealIngredientDto> dtos)
        {
            var entities = dtos.Select(dto => new UserMealIngredient
            {
                UserMealId = dto.UserMealId ?? 0,
                IngredientId = dto.IngredientId,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.TotalPrice
            });

            await _repository.AddRangeAsync(entities);
        }

        public async Task<UserMealIngredientDto?> GetUserMealIngredientByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return null;

            return new UserMealIngredientDto
            {
                UserMealIngredientId = entity.UserMealIngredientId,
                UserMealId = entity.UserMealId,
                IngredientId = entity.IngredientId,
                Quantity = entity.Quantity,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
