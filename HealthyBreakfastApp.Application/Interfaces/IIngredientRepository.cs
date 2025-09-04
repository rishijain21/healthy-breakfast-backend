using System.Threading.Tasks;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientRepository
    {
        // ADD THESE NEW METHODS ⬇️
        Task<IEnumerable<Ingredient>> GetAllAsync();
        Task<IEnumerable<Ingredient>> GetByCategoryIdAsync(int categoryId);
        
        // Your existing methods
        Task AddIngredientAsync(Ingredient ingredient);
        Task SaveChangesAsync();
        Task<Ingredient?> GetByIdAsync(int id);
        Task<Ingredient?> GetByIdWithCategoryAsync(int id);
    }
}
