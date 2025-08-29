using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealOptionIngredientService
    {
        Task<int> CreateMealOptionIngredientAsync(CreateMealOptionIngredientDto dto);
        Task<MealOptionIngredientDto?> GetMealOptionIngredientByIdAsync(int id);
    }
}
