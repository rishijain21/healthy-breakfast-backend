// Sovva.Application/Interfaces/IUserMealIngredientRepository.cs

using Sovva.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IUserMealIngredientRepository
    {
        Task AddAsync(UserMealIngredient entity);
        Task SaveChangesAsync();
        Task<UserMealIngredient?> GetByIdAsync(int id);
        
        // ✅ NEW: Get all ingredients for a specific UserMeal
        Task<IEnumerable<UserMealIngredient>> GetByUserMealIdAsync(int userMealId);
    }
}
