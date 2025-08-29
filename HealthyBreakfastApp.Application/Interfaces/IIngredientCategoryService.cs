using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientCategoryService
    {
        Task<int> CreateIngredientCategoryAsync(CreateIngredientCategoryDto dto);
        Task<IngredientCategoryDto?> GetIngredientCategoryByIdAsync(int id);
    }
}
