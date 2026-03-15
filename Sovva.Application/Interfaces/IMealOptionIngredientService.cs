using Sovva.Application.DTOs;
using System.Threading.Tasks;

namespace Sovva.Application.Interfaces
{
    public interface IMealOptionIngredientService
    {
        Task<int> CreateMealOptionIngredientAsync(CreateMealOptionIngredientDto dto);
        // ✅ REMOVED: Task<MealOptionIngredientDto?> GetMealOptionIngredientByIdAsync(int id);
    }
}
