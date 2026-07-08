using Sovva.Application.DTOs;
using System.Threading.Tasks;
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IMealService
    {
        /// <summary>
        /// Retrieves a paginated list of active meals available for customers to browse.
        /// </summary>
        /// <param name="page">Page number starting from 1.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>A paged result containing meal DTOs.</returns>
        Task<PagedResult<MealDto>> GetActiveMealsAsync(int page, int pageSize);

        /// <summary>
        /// Creates a new basic meal template.
        /// </summary>
        /// <param name="dto">The meal data.</param>
        /// <returns>The internal ID of the created meal.</returns>
        Task<int> CreateMealAsync(CreateMealDto dto);

        /// <summary>
        /// Retrieves a meal template by its ID.
        /// </summary>
        /// <param name="id">The meal ID.</param>
        /// <returns>The meal DTO or null if not found.</returns>
        Task<MealDto?> GetMealByIdAsync(int id);

        /// <summary>
        /// Calculates the total price and nutrition for a customized meal selection.
        /// </summary>
        /// <param name="calculationDto">The meal and selected ingredients.</param>
        /// <returns>Detailed price and nutritional breakdown.</returns>
        Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto);

        /// <summary>
        /// Calculates only the price portion of a set of ingredients.
        /// </summary>
        /// <param name="ingredients">List of selected ingredients and quantities.</param>
        /// <param name="ingredientMap">Prefetched ingredient map.</param>
        /// <returns>The sum of ingredient prices.</returns>
        Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap);

        /// <summary>
        /// Aggregates nutritional data (calories, protein, fiber) for a set of ingredients.
        /// </summary>
        /// <param name="ingredients">List of selected ingredients.</param>
        /// <param name="ingredientMap">Prefetched ingredient map.</param>
        /// <returns>A tuple containing aggregated nutrition values.</returns>
        Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap);

        /// <summary>
        /// Validates that the selected ingredients are valid for the specified meal template.
        /// </summary>
        /// <param name="mealId">The template meal ID.</param>
        /// <param name="ingredients">The user's selection.</param>
        /// <param name="ingredientMap">Prefetched ingredient map.</param>
        /// <returns>True if the selection is valid, false otherwise.</returns>
        Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap);
        
        /// <summary>
        /// Retrieves all meals for the admin dashboard without pagination.
        /// </summary>
        /// <returns>A list of admin-focused meal DTOs.</returns>
        Task<List<AdminMealListDto>> GetAllMealsForAdminAsync();

        /// <summary>
        /// Retrieves comprehensive meal details including all available options and ingredients (Admin only).
        /// </summary>
        /// <param name="id">The meal ID.</param>
        /// <returns>Detailed admin meal DTO or null if not found.</returns>
        Task<AdminMealDetailDto?> GetMealDetailForAdminAsync(int id);

        /// <summary>
        /// Retrieves detailed information for multiple meals in a single batch (Admin only).
        /// </summary>
        /// <param name="mealIds">List of meal IDs to fetch.</param>
        /// <returns>List of detailed admin meal DTOs.</returns>
        Task<List<AdminMealDetailDto>> GetMealsBatchDetailsAsync(List<int> mealIds);

        /// <summary>
        /// Creates a complete meal with all its associated options and ingredients in one transaction (Admin only).
        /// </summary>
        /// <param name="dto">The complex meal creation data.</param>
        /// <returns>The ID of the created meal.</returns>
        Task<int> CreateMealWithOptionsAsync(AdminCreateMealDto dto);

        /// <summary>
        /// Updates an existing meal's basic info, options, and ingredients (Admin only).
        /// </summary>
        /// <param name="id">The ID of the meal to update.</param>
        /// <param name="dto">The updated meal data.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> UpdateMealAsync(int id, UpdateMealDto dto);

        /// <summary>
        /// Toggles a meal's completion/active status (Admin only).
        /// </summary>
        /// <param name="id">The meal ID.</param>
        /// <param name="isComplete">The new status.</param>
        /// <returns>True if successful.</returns>
        Task<bool> UpdateMealStatusAsync(int id, bool isComplete);

        /// <summary>
        /// Deletes a meal and all its associated data (Admin only).
        /// </summary>
        /// <param name="id">The meal ID to delete.</param>
        /// <returns>True if successful.</returns>
        Task<bool> DeleteMealAsync(int id);

        /// <summary>
        /// Retrieves the entire ingredient hierarchy for the meal builder.
        /// </summary>
        /// <returns>List of categories with their nested ingredients.</returns>
        Task<List<CategoryWithIngredientsDto>> GetCategoriesWithIngredientsAsync();

        /// <summary>
        /// Retrieves a paginated list of all meals for the admin dashboard.
        /// </summary>
        /// <param name="page">Page number starting from 1.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>A paged result of admin meal DTOs.</returns>
        Task<PagedResult<AdminMealListDto>> GetAllMealsForAdminPagedAsync(int page, int pageSize);

        /// <summary>
        /// Updates the cover image URL for a specific meal (Admin only).
        /// </summary>
        /// <param name="mealId">The meal ID.</param>
        /// <param name="imageUrl">The new public image URL.</param>
        /// <returns>True if successful.</returns>
        Task<bool> UpdateMealImageAsync(int mealId, string imageUrl);

        /// <summary>
        /// Clears the image URL for a meal and returns the old URL for storage cleanup (Admin only).
        /// </summary>
        /// <param name="mealId">The meal ID.</param>
        /// <returns>The old image URL or null.</returns>
        Task<string?> DeleteMealImageAsync(int mealId);

        /// <summary>
        /// Retrieves detailed information for multiple meals, filtered for customer visibility (IsComplete only).
        /// </summary>
        /// <param name="mealIds">List of meal IDs to fetch.</param>
        /// <returns>List of user-focused meal detail DTOs.</returns>
        Task<List<MealWithDetailsDto>> GetMealsBatchDetailsForUsersAsync(List<int> mealIds);
    }
}
