using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class UserMealService : IUserMealService
    {
        private readonly IUserMealRepository _repository;

        public UserMealService(IUserMealRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateUserMealAsync(CreateUserMealDto dto)
        {
            var entity = new UserMeal
            {
                UserId = dto.UserId,
                MealId = dto.MealId,
                MealName = dto.MealName,
                TotalPrice = dto.TotalPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            return entity.UserMealId;
        }

        public async Task<UserMealDto?> GetUserMealByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) return null;

            return new UserMealDto
            {
                UserMealId = entity.UserMealId,
                UserId = entity.UserId,
                MealId = entity.MealId,
                MealName = entity.MealName,
                TotalPrice = entity.TotalPrice,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public async Task<IEnumerable<UserMealDto>> GetUserMealsByUserIdAsync(int userId)
        {
            var entities = await _repository.GetByUserIdAsync(userId);
            return entities.Select(entity => new UserMealDto
            {
                UserMealId = entity.UserMealId,
                UserId = entity.UserId,
                MealId = entity.MealId,
                MealName = entity.MealName,
                TotalPrice = entity.TotalPrice,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            });
        }
    }
}
