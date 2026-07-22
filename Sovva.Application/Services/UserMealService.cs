using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Services
{
    public class UserMealService : IUserMealService
    {
        private readonly IUserMealRepository _repository;
        private readonly IUserMealIngredientRepository _ingredientRepository;
        private readonly ILogger<UserMealService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public UserMealService(
            IUserMealRepository repository,
            IUserMealIngredientRepository ingredientRepository,
            ILogger<UserMealService> logger,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _ingredientRepository = ingredientRepository;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        // ✅ SECURE: CreateUserMealAsync with userId from JWT token
        public async Task<int> CreateUserMealAsync(CreateUserMealDto dto, int userId)
        {
            var entity = new UserMeal
            {
                UserId = userId,
                MealId = dto.MealId,
                MealName = dto.MealName ?? "Custom Meal", // ✅ FIXED: Handle potential null

                TotalPrice = dto.TotalPrice
            };

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1

            // ✅ Save ingredients if provided
            if (dto.SelectedIngredients != null && dto.SelectedIngredients.Any())
            {
                _logger.LogInformation("Saving {IngredientCount} ingredients for UserMeal {UserMealId}", dto.SelectedIngredients.Count, entity.UserMealId);
                
                foreach (var ingredientDto in dto.SelectedIngredients)
                {
                    var userMealIngredient = new UserMealIngredient
                    {
                        UserMealId = entity.UserMealId, // Set from created UserMeal
                        IngredientId = ingredientDto.IngredientId,
                        Quantity = ingredientDto.Quantity
                    };
                    
                    await _ingredientRepository.AddAsync(userMealIngredient);
                }
                
                await _unitOfWork.SaveChangesAsync(); // ARCH-MIGRATION: TASK-1.1
                _logger.LogInformation("Saved {IngredientCount} ingredients for UserMeal {UserMealId}", dto.SelectedIngredients.Count, entity.UserMealId);
            }
            else
            {
                _logger.LogWarning("No ingredients provided for UserMeal {UserMealId}", entity.UserMealId);
            }

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

        public async Task<UserMealDto?> GetByIdForUserAsync(int id, int userId)
        {
            var entity = await _repository.GetByIdForUserAsync(id, userId);
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
    }
}
