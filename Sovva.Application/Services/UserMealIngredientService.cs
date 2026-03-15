using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services
{
    public class UserMealIngredientService : IUserMealIngredientService
    {
        private readonly IUserMealIngredientRepository _repository;

        public UserMealIngredientService(IUserMealIngredientRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateUserMealIngredientAsync(CreateUserMealIngredientDto dto)
        {
            var entity = new UserMealIngredient
            {
                UserMealId = dto.UserMealId ?? 0, // Handle nullable
                IngredientId = dto.IngredientId,
                Quantity = dto.Quantity,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            return entity.UserMealIngredientId;
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
