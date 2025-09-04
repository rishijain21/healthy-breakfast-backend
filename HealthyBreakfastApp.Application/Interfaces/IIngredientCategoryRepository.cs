using HealthyBreakfastApp.Domain.Entities;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientCategoryRepository
    {
        // ADD THIS LINE ⬇️ (This fixes the CS1061 error)
        Task<IEnumerable<IngredientCategory>> GetAllAsync();
        
        // Your existing methods
        Task AddAsync(IngredientCategory entity);
        Task SaveChangesAsync();
        Task<IngredientCategory?> GetByIdAsync(int id);
    }
}
