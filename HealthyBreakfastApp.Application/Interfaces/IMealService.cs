using HealthyBreakfastApp.Application.DTOs;
using System.Threading.Tasks;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface IMealService
    {
        // Existing methods
        Task<int> CreateMealAsync(CreateMealDto dto);
        Task<MealDto?> GetMealByIdAsync(int id);
        Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto);
        Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients);
        Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients);
        Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients);
        
        // NEW ADMIN METHODS
        Task<List<AdminMealListDto>> GetAllMealsForAdminAsync();
        Task<AdminMealDetailDto?> GetMealDetailForAdminAsync(int id);
        Task<int> CreateMealWithOptionsAsync(AdminCreateMealDto dto);
        Task<bool> UpdateMealAsync(int id, UpdateMealDto dto);
        Task<bool> DeleteMealAsync(int id);
        Task<List<CategoryWithIngredientsDto>> GetCategoriesWithIngredientsAsync();
    }
}
