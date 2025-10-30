using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class MealService : IMealService
    {
        private readonly IMealRepository _mealRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IMealOptionRepository _mealOptionRepository;
        private readonly IIngredientCategoryRepository _ingredientCategoryRepository; // ADDED THIS LINE

        // UPDATED CONSTRUCTOR - Added IIngredientCategoryRepository parameter
        public MealService(
            IMealRepository mealRepository,
            IIngredientRepository ingredientRepository,
            IMealOptionRepository mealOptionRepository,
            IIngredientCategoryRepository ingredientCategoryRepository) // ADDED THIS PARAMETER
        {
            _mealRepository = mealRepository;
            _ingredientRepository = ingredientRepository;
            _mealOptionRepository = mealOptionRepository;
            _ingredientCategoryRepository = ingredientCategoryRepository; // ADDED THIS ASSIGNMENT
        }

        public async Task<int> CreateMealAsync(CreateMealDto dto)
        {
            var meal = new Meal
            {
                MealName = dto.MealName,
                Description = dto.Description,
                BasePrice = dto.BasePrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _mealRepository.AddMealAsync(meal);
            await _mealRepository.SaveChangesAsync();

            return meal.MealId;
        }

        public async Task<MealDto?> GetMealByIdAsync(int id)
        {
            var meal = await _mealRepository.GetByIdAsync(id);
            if (meal == null) return null;

            return new MealDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt
            };
        }

        public async Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto)
        {
            // Get meal details
            var meal = await _mealRepository.GetByIdAsync(calculationDto.MealId);
            if (meal == null)
                throw new ArgumentException("Meal not found");

            // Validate ingredient selection against meal options
            var isValidSelection = await ValidateIngredientSelectionAsync(calculationDto.MealId, calculationDto.SelectedIngredients);
            if (!isValidSelection)
                throw new InvalidOperationException("Invalid ingredient selection based on meal options");

            // Calculate ingredients price
            var ingredientsPrice = await GetIngredientsTotalPriceAsync(calculationDto.SelectedIngredients);
            
            // Get nutritional summary
            var (totalCalories, totalProtein, totalFiber) = await GetNutritionalSummaryAsync(calculationDto.SelectedIngredients);
            
            // Get ingredient breakdown
            var ingredientBreakdown = await GetIngredientBreakdownAsync(calculationDto.SelectedIngredients);

            return new MealPriceResponseDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                BaseMealPrice = meal.BasePrice,
                IngredientsPrice = ingredientsPrice,
                TotalPrice = meal.BasePrice + ingredientsPrice,
                TotalCalories = totalCalories,
                TotalProtein = totalProtein,
                TotalFiber = totalFiber,
                IngredientBreakdown = ingredientBreakdown
            };
        }

        public async Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients)
        {
            decimal total = 0;
            
            foreach (var selectedIngredient in ingredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(selectedIngredient.IngredientId);
                if (ingredient != null && ingredient.Available)
                {
                    total += ingredient.Price * selectedIngredient.Quantity;
                }
            }
            
            return total;
        }

        public async Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients)
        {
            int totalCalories = 0;
            decimal totalProtein = 0;
            decimal totalFiber = 0;

            foreach (var selectedIngredient in ingredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(selectedIngredient.IngredientId);
                if (ingredient != null)
                {
                    totalCalories += ingredient.Calories * selectedIngredient.Quantity;
                    totalProtein += ingredient.Protein * selectedIngredient.Quantity;
                    totalFiber += ingredient.Fiber * selectedIngredient.Quantity;
                }
            }

            return (totalCalories, totalProtein, totalFiber);
        }

        public async Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients)
        {
            // Get meal options for this meal
            var mealOptions = await _mealOptionRepository.GetByMealIdAsync(mealId);
            
            // Group selected ingredients by category
            var ingredientsByCategory = new Dictionary<int, List<SelectedIngredientDto>>();
            
            foreach (var selectedIngredient in ingredients)
            {
                var ingredient = await _ingredientRepository.GetByIdAsync(selectedIngredient.IngredientId);
                if (ingredient != null)
                {
                    if (!ingredientsByCategory.ContainsKey(ingredient.CategoryId))
                        ingredientsByCategory[ingredient.CategoryId] = new List<SelectedIngredientDto>();
                    
                    ingredientsByCategory[ingredient.CategoryId].Add(selectedIngredient);
                }
            }

            // Validate against meal options
            foreach (var mealOption in mealOptions)
            {
                var categoryIngredients = ingredientsByCategory.GetValueOrDefault(mealOption.CategoryId, new List<SelectedIngredientDto>());
                
                // Check if required category has selections
                if (mealOption.IsRequired && !categoryIngredients.Any())
                    return false;
                
                // Check if selection doesn't exceed max selectable
                if (categoryIngredients.Count > mealOption.MaxSelectable)
                    return false;
            }

            return true;
        }

        // UPDATED METHOD - Now uses GetByIdWithCategoryAsync
        private async Task<List<IngredientBreakdownDto>> GetIngredientBreakdownAsync(List<SelectedIngredientDto> selectedIngredients)
        {
            var breakdown = new List<IngredientBreakdownDto>();

            foreach (var selectedIngredient in selectedIngredients)
            {
                // CHANGED: Use the new method that includes category navigation property
                var ingredient = await _ingredientRepository.GetByIdWithCategoryAsync(selectedIngredient.IngredientId);
                if (ingredient != null)
                {
                    breakdown.Add(new IngredientBreakdownDto
                    {
                        IngredientId = ingredient.IngredientId,
                        IngredientName = ingredient.IngredientName,
                        Quantity = selectedIngredient.Quantity,
                        UnitPrice = ingredient.Price,
                        TotalPrice = ingredient.Price * selectedIngredient.Quantity,
                        Calories = ingredient.Calories * selectedIngredient.Quantity,
                        Protein = ingredient.Protein * selectedIngredient.Quantity,
                    
                    });
                }
            }

            return breakdown;
        }
    }
}
