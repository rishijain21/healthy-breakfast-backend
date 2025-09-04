using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IIngredientService
    {
     
        Task<IEnumerable<IngredientDto>> GetAllIngredientsAsync();
        Task<IEnumerable<IngredientDto>> GetIngredientsByCategoryIdAsync(int categoryId);
        
      
        Task<int> CreateIngredientAsync(CreateIngredientDto dto);
        Task<IngredientDto?> GetIngredientByIdAsync(int id);
    }
}
