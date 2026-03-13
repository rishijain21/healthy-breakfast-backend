using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace HealthyBreakfastApp.Application.Services
{
    public class MealService : IMealService
    {
        private readonly IMealRepository _mealRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IMealOptionRepository _mealOptionRepository;
        private readonly IIngredientCategoryRepository _ingredientCategoryRepository;
        private readonly IMealOptionIngredientRepository _mealOptionIngredientRepository;
        private readonly ISupabaseStorageService _storageService;
        private readonly IMemoryCache _cache;
        private const string ActiveMealsCacheKey = "active_meals";
        private const string CategoriesWithIngredientsCacheKey = "meals:categories_with_ingredients";

        public MealService(
            IMealRepository mealRepository,
            IIngredientRepository ingredientRepository,
            IMealOptionRepository mealOptionRepository,
            IIngredientCategoryRepository ingredientCategoryRepository,
            IMealOptionIngredientRepository mealOptionIngredientRepository,
            ISupabaseStorageService storageService,
            IMemoryCache cache)
        {
            _mealRepository = mealRepository;
            _ingredientRepository = ingredientRepository;
            _mealOptionRepository = mealOptionRepository;
            _ingredientCategoryRepository = ingredientCategoryRepository;
            _mealOptionIngredientRepository = mealOptionIngredientRepository;
            _storageService = storageService;
            _cache = cache;
        }

        // ✅ Public method for meal builder - returns only complete meals for public browsing
        public async Task<List<MealDto>> GetActiveMealsAsync()
        {
            // ✅ Try to get from cache first
            if (_cache.TryGetValue(ActiveMealsCacheKey, out List<MealDto>? cachedMeals))
                return cachedMeals!;

            var meals = await _mealRepository.GetAllAsync();
            var result = new List<MealDto>();
            
            // ✅ Filter: IsComplete AND not soft-deleted
            foreach (var m in meals.Where(m => m.IsComplete && !m.IsDeleted))
            {
                var dto = new MealDto
                {
                    MealId = m.MealId,
                    MealName = m.MealName,
                    Description = m.Description,
                    BasePrice = m.BasePrice,
                    IsComplete = m.IsComplete
                };
                
                // Generate signed URL for secure image access (expires in 1 hour)
                if (!string.IsNullOrEmpty(m.ImageUrl))
                {
                    var filePath = ExtractStoragePath(m.ImageUrl);
                    dto.ImageUrl = await _storageService.GetSignedUrlAsync(filePath);
                }
                
                result.Add(dto);
            }
            
            // ✅ Cache for 5 minutes
            _cache.Set(ActiveMealsCacheKey, result, TimeSpan.FromMinutes(5));
            
            return result;
        }

        // Helper to extract storage path from full URL or clean path
        private static string ExtractStoragePath(string imageUrl)
        {
            // Handles both old full URLs and new relative paths
            const string marker = "/meal-images/";
            var idx = imageUrl.IndexOf(marker);
            // Returns "meal-10/abc.png" regardless of whether input is:
            // - "https://.../object/public/meal-images/meal-10/abc.png"  (old)
            // - "meal-images/meal-10/abc.png"                            (new)
            return idx >= 0 ? imageUrl[(idx + marker.Length)..] : imageUrl;
        }

        // EXISTING METHODS
        public async Task<int> CreateMealAsync(CreateMealDto dto)
        {
            var meal = new Meal
            {
                MealName = dto.MealName,
                Description = dto.Description,
                BasePrice = dto.BasePrice,
                
                // ✅ ADD NUTRITION FIELDS
                ApproxCalories = dto.ApproxCalories,
                ApproxProtein = dto.ApproxProtein,
                ApproxCarbs = dto.ApproxCarbs,
                ApproxFats = dto.ApproxFats,
                
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _mealRepository.AddMealAsync(meal);
            await _mealRepository.SaveChangesAsync();

            // ✅ Bust the cache when a meal is created
            _cache.Remove(ActiveMealsCacheKey);

            return meal.MealId;
        }

        public async Task<MealDto?> GetMealByIdAsync(int id)
        {
            var meal = await _mealRepository.GetByIdAsync(id);
            if (meal == null) return null;

            var dto = new MealDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt
            };

            // Generate signed URL for secure image access (expires in 1 hour)
            if (!string.IsNullOrEmpty(meal.ImageUrl))
            {
                var filePath = ExtractStoragePath(meal.ImageUrl);
                dto.ImageUrl = await _storageService.GetSignedUrlAsync(filePath);
            }

            return dto;
        }

        public async Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto)
        {
            var meal = await _mealRepository.GetByIdAsync(calculationDto.MealId);
            if (meal == null)
                throw new ArgumentException("Meal not found");

            var isValidSelection = await ValidateIngredientSelectionAsync(calculationDto.MealId, calculationDto.SelectedIngredients);
            if (!isValidSelection)
                throw new InvalidOperationException("Invalid ingredient selection based on meal options");

            var ingredientsPrice = await GetIngredientsTotalPriceAsync(calculationDto.SelectedIngredients);
            var (totalCalories, totalProtein, totalFiber) = await GetNutritionalSummaryAsync(calculationDto.SelectedIngredients);
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
            var mealOptions = await _mealOptionRepository.GetByMealIdAsync(mealId);
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

            foreach (var mealOption in mealOptions)
            {
                var categoryIngredients = ingredientsByCategory.GetValueOrDefault(mealOption.CategoryId, new List<SelectedIngredientDto>());
                
                if (mealOption.IsRequired && !categoryIngredients.Any())
                    return false;
                
                if (categoryIngredients.Count > mealOption.MaxSelectable)
                    return false;
            }

            return true;
        }

        private async Task<List<IngredientBreakdownDto>> GetIngredientBreakdownAsync(List<SelectedIngredientDto> selectedIngredients)
        {
            var breakdown = new List<IngredientBreakdownDto>();

            foreach (var selectedIngredient in selectedIngredients)
            {
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

        // ========== ADMIN METHODS (UPDATED) ==========

        // ✅ FIXED: Use eager loading to avoid N+1 queries
        public async Task<List<AdminMealListDto>> GetAllMealsForAdminAsync()
        {
            var meals = await _mealRepository.GetAllWithOptionsCountAsync();
            var mealList = new List<AdminMealListDto>();

            foreach (var meal in meals)
            {
                // ✅ Options are already loaded via Include - no extra DB call
                var mealOptions = meal.MealOptions ?? Enumerable.Empty<MealOption>();
                
                mealList.Add(new AdminMealListDto
                {
                    MealId = meal.MealId,
                    MealName = meal.MealName,
                    Description = meal.Description,
                    BasePrice = meal.BasePrice,
                    MealOptionsCount = mealOptions.Count(),
                    IsComplete = mealOptions.Any(),
                    
                    // ✅ MAP NUTRITION FIELDS
                    ApproxCalories = meal.ApproxCalories,
                    ApproxProtein = meal.ApproxProtein,
                    ApproxCarbs = meal.ApproxCarbs,
                    ApproxFats = meal.ApproxFats,
                    
                    // Image URL (generate signed URL for secure access)
                    ImageUrl = !string.IsNullOrEmpty(meal.ImageUrl) 
                        ? await _storageService.GetSignedUrlAsync(meal.ImageUrl) 
                        : null,
                    
                    CreatedAt = meal.CreatedAt,
                    UpdatedAt = meal.UpdatedAt
                });
            }

            return mealList;
        }

        // ✅ NEW: Paginated admin list
        public async Task<PagedResult<AdminMealListDto>> GetAllMealsForAdminPagedAsync(int page, int pageSize)
        {
            // Clamp inputs — never trust raw user input
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50); // max 50 per page

            var (meals, totalCount) = await _mealRepository.GetPagedAsync(page, pageSize);

            var items = new List<AdminMealListDto>();
            foreach (var meal in meals)
            {
                items.Add(new AdminMealListDto
                {
                    MealId = meal.MealId,
                    MealName = meal.MealName,
                    Description = meal.Description,
                    BasePrice = meal.BasePrice,
                    MealOptionsCount = 0, // ← avoids N+1; load separately if needed
                    IsComplete = meal.IsComplete,
                    ApproxCalories = meal.ApproxCalories,
                    ApproxProtein = meal.ApproxProtein,
                    ApproxCarbs = meal.ApproxCarbs,
                    ApproxFats = meal.ApproxFats,
                    ImageUrl = !string.IsNullOrEmpty(meal.ImageUrl)
                        ? await _storageService.GetSignedUrlAsync(meal.ImageUrl)
                        : null,
                    CreatedAt = meal.CreatedAt,
                    UpdatedAt = meal.UpdatedAt
                });
            }

            return new PagedResult<AdminMealListDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminMealDetailDto?> GetMealDetailForAdminAsync(int id)
        {
            var meal = await _mealRepository.GetByIdWithOptionsAsync(id);
            if (meal == null) return null;

            var mealDetail = new AdminMealDetailDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                
                // ✅ MAP NUTRITION FIELDS
                ApproxCalories = meal.ApproxCalories,
                ApproxProtein = meal.ApproxProtein,
                ApproxCarbs = meal.ApproxCarbs,
                ApproxFats = meal.ApproxFats,
                
                // Image URL (generate signed URL for secure access)
                ImageUrl = !string.IsNullOrEmpty(meal.ImageUrl) 
                    ? await _storageService.GetSignedUrlAsync(meal.ImageUrl) 
                    : null,
                
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt,
                MealOptions = new List<AdminMealOptionDetailDto>()
            };

            foreach (var mealOption in meal.MealOptions)
            {
                var optionDetail = new AdminMealOptionDetailDto
                {
                    MealOptionId = mealOption.MealOptionId,
                    CategoryId = mealOption.CategoryId,
                    CategoryName = mealOption.IngredientCategory.CategoryName,
                    IsRequired = mealOption.IsRequired,
                    MaxSelectable = mealOption.MaxSelectable,
                    Ingredients = new List<MealIngredientDto>()
                };

                foreach (var mealOptionIngredient in mealOption.MealOptionIngredients)
                {
                    optionDetail.Ingredients.Add(new MealIngredientDto
                    {
                        IngredientId = mealOptionIngredient.Ingredient.IngredientId,
                        IngredientName = mealOptionIngredient.Ingredient.IngredientName,
                        Price = mealOptionIngredient.Ingredient.Price,
                        IconEmoji = mealOptionIngredient.Ingredient.IconEmoji,
                        Available = mealOptionIngredient.Ingredient.Available,
                        
                        // ✅ OPTIONAL: Map ingredient nutrition
                        Calories = mealOptionIngredient.Ingredient.Calories,
                        Protein = mealOptionIngredient.Ingredient.Protein,
                        Fiber = mealOptionIngredient.Ingredient.Fiber
                    });
                }

                mealDetail.MealOptions.Add(optionDetail);
            }

            return mealDetail;
        }

        public async Task<List<AdminMealDetailDto>> GetMealsBatchDetailsAsync(List<int> mealIds)
        {
            var results = new List<AdminMealDetailDto>();
            foreach (var id in mealIds)
            {
                var meal = await GetMealDetailForAdminAsync(id);
                if (meal != null) results.Add(meal);
            }
            return results;
        }

        public async Task<int> CreateMealWithOptionsAsync(AdminCreateMealDto dto)
        {
            // Validate that all ingredients exist
            foreach (var mealOption in dto.MealOptions)
            {
                foreach (var ingredientId in mealOption.IngredientIds)
                {
                    var ingredient = await _ingredientRepository.GetByIdAsync(ingredientId);
                    if (ingredient == null)
                        throw new ArgumentException($"Ingredient with ID {ingredientId} does not exist");
                }
            }

            // Create meal
            var meal = new Meal
            {
                MealName = dto.MealName,
                Description = dto.Description,
                BasePrice = dto.BasePrice,
                
                // ✅ MAP NUTRITION FIELDS
                ApproxCalories = dto.ApproxCalories,
                ApproxProtein = dto.ApproxProtein,
                ApproxCarbs = dto.ApproxCarbs,
                ApproxFats = dto.ApproxFats,
                
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _mealRepository.AddMealAsync(meal);
            await _mealRepository.SaveChangesAsync();

            // Create meal options
            foreach (var optionDto in dto.MealOptions)
            {
                var mealOption = new MealOption
                {
                    MealId = meal.MealId,
                    CategoryId = optionDto.CategoryId,
                    IsRequired = optionDto.IsRequired,
                    MaxSelectable = optionDto.MaxSelectable,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _mealOptionRepository.AddAsync(mealOption);
                await _mealOptionRepository.SaveChangesAsync();

                // Create meal option ingredients
                foreach (var ingredientId in optionDto.IngredientIds)
                {
                    var mealOptionIngredient = new MealOptionIngredient
                    {
                        MealOptionId = mealOption.MealOptionId,
                        IngredientId = ingredientId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _mealOptionIngredientRepository.AddAsync(mealOptionIngredient);
                }

                await _mealOptionIngredientRepository.SaveChangesAsync();
            }

            // ✅ Bust the cache when a meal is created
            _cache.Remove(ActiveMealsCacheKey);

            return meal.MealId;
        }

        public async Task<bool> UpdateMealAsync(int id, UpdateMealDto dto)
        {
            var meal = await _mealRepository.GetByIdWithOptionsAsync(id);
            if (meal == null) return false;

            // Validate that all ingredients exist
            foreach (var mealOption in dto.MealOptions)
            {
                foreach (var ingredientId in mealOption.IngredientIds)
                {
                    var ingredient = await _ingredientRepository.GetByIdAsync(ingredientId);
                    if (ingredient == null)
                        throw new ArgumentException($"Ingredient with ID {ingredientId} does not exist");
                }
            }

            // Update meal basic info
            meal.MealName = dto.MealName;
            meal.Description = dto.Description;
            meal.BasePrice = dto.BasePrice;
            
            // ✅ UPDATE NUTRITION FIELDS
            meal.ApproxCalories = dto.ApproxCalories;
            meal.ApproxProtein = dto.ApproxProtein;
            meal.ApproxCarbs = dto.ApproxCarbs;
            meal.ApproxFats = dto.ApproxFats;
            
            meal.UpdatedAt = DateTime.UtcNow;

            // Delete existing meal options and their ingredients
            var existingOptions = await _mealOptionRepository.GetByMealIdAsync(id);
            foreach (var option in existingOptions)
            {
                await _mealOptionIngredientRepository.DeleteByMealOptionIdAsync(option.MealOptionId);
                await _mealOptionRepository.DeleteAsync(option);
            }
            await _mealOptionRepository.SaveChangesAsync();

            // Create new meal options
            foreach (var optionDto in dto.MealOptions)
            {
                var mealOption = new MealOption
                {
                    MealId = meal.MealId,
                    CategoryId = optionDto.CategoryId,
                    IsRequired = optionDto.IsRequired,
                    MaxSelectable = optionDto.MaxSelectable,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _mealOptionRepository.AddAsync(mealOption);
                await _mealOptionRepository.SaveChangesAsync();

                // Create meal option ingredients
                foreach (var ingredientId in optionDto.IngredientIds)
                {
                    var mealOptionIngredient = new MealOptionIngredient
                    {
                        MealOptionId = mealOption.MealOptionId,
                        IngredientId = ingredientId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _mealOptionIngredientRepository.AddAsync(mealOptionIngredient);
                }

                await _mealOptionIngredientRepository.SaveChangesAsync();
            }

            await _mealRepository.UpdateMealAsync(meal);
            
            // ✅ Bust the cache when a meal is updated
            _cache.Remove(ActiveMealsCacheKey);
            
            return true;
        }

        public async Task<bool> DeleteMealAsync(int id)
        {
            var meal = await _mealRepository.GetByIdAsync(id);
            if (meal == null) return false;

            // Delete cascade will handle meal options and meal option ingredients
            await _mealRepository.DeleteMealAsync(meal);
            
            // ✅ Bust the cache when a meal is deleted
            _cache.Remove(ActiveMealsCacheKey);
            
            return true;
        }

        public async Task<bool> UpdateMealStatusAsync(int id, bool isComplete)
        {
            return await _mealRepository.UpdateMealStatusAsync(id, isComplete);
        }

        public async Task<List<CategoryWithIngredientsDto>> GetCategoriesWithIngredientsAsync()
        {
            // ✅ Try to get from cache first
            if (_cache.TryGetValue(CategoriesWithIngredientsCacheKey, out List<CategoryWithIngredientsDto>? cached))
                return cached!;

            var categories = await _ingredientCategoryRepository.GetAllAsync();
            var result = new List<CategoryWithIngredientsDto>();

            foreach (var category in categories)
            {
                var ingredients = await _ingredientRepository.GetByCategoryIdAsync(category.CategoryId);
                
                result.Add(new CategoryWithIngredientsDto
                {
                    CategoryId = category.CategoryId,
                    CategoryName = category.CategoryName,
                    Ingredients = ingredients.Select(i => new IngredientDto
                    {
                        IngredientId = i.IngredientId,
                        CategoryId = i.CategoryId,
                        IngredientName = i.IngredientName,
                        Price = i.Price,
                        Available = i.Available,
                        Calories = i.Calories,
                        Protein = i.Protein,
                        Fiber = i.Fiber,
                        IconEmoji = i.IconEmoji,
                        Description = i.Description
                    }).ToList()
                });
            }

            // ✅ Cache for 10 minutes
            _cache.Set(CategoriesWithIngredientsCacheKey, result, TimeSpan.FromMinutes(10));
            
            return result;
        }

        // ✅ NEW: Update meal image
        public async Task<bool> UpdateMealImageAsync(int mealId, string imageUrl)
        {
            var meal = await _mealRepository.GetByIdAsync(mealId);
            if (meal == null) return false;

            meal.ImageUrl = imageUrl;
            meal.UpdatedAt = DateTime.UtcNow;
            await _mealRepository.UpdateMealAsync(meal);
            _cache.Remove(ActiveMealsCacheKey);
            return true;
        }

        // ✅ NEW: Delete meal image
        public async Task<string?> DeleteMealImageAsync(int mealId)
        {
            var meal = await _mealRepository.GetByIdAsync(mealId);
            if (meal == null) return null;

            var existingUrl = meal.ImageUrl;
            meal.ImageUrl = null;
            meal.UpdatedAt = DateTime.UtcNow;
            await _mealRepository.UpdateMealAsync(meal);
            _cache.Remove(ActiveMealsCacheKey);
            return existingUrl;
        }
    }
}
