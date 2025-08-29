using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealService
    {
        Task<int> CreateMealAsync(CreateMealDto dto);
        Task<MealDto?> GetMealByIdAsync(int id);
        
        // New price calculation methods
        Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto);
        Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients);
        Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients);
        Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients);
    }
}
