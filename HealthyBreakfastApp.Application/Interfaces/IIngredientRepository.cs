using System.Collections.Generic;
using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientRepository
    {
        // Read operations
        Task<IEnumerable<Ingredient>> GetAllAsync();
        Task<IEnumerable<Ingredient>> GetByCategoryIdAsync(int categoryId);
        Task<Ingredient?> GetByIdAsync(int id);
        Task<Ingredient?> GetByIdWithCategoryAsync(int id);
        
        // Create operations
        Task AddIngredientAsync(Ingredient ingredient);
        
        // Update operations
        Task UpdateIngredientAsync(Ingredient ingredient);
        
        // Delete operations
        Task DeleteIngredientAsync(Ingredient ingredient);
        
        // Check operations
        Task<bool> IsIngredientUsedInMealsAsync(int ingredientId);
        
        // Save
        Task SaveChangesAsync();
    }
}
