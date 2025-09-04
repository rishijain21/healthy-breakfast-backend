using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientCategoryService
    {
        // ADD THIS NEW METHOD ⬇️
        Task<IEnumerable<IngredientCategoryDto>> GetAllIngredientCategoriesAsync();
        
        // Your existing methods
        Task<int> CreateIngredientCategoryAsync(CreateIngredientCategoryDto dto);
        Task<IngredientCategoryDto?> GetIngredientCategoryByIdAsync(int id);
    }
}
