using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Services
{
    public class MealService : IMealService
    {
        private readonly IMealRepository _mealRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IMealOptionRepository _mealOptionRepository;
        private readonly IIngredientCategoryRepository _ingredientCategoryRepository;
        private readonly IMealOptionIngredientRepository _mealOptionIngredientRepository;
        private readonly ISupabaseStorageService _storageService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MealService> _logger;
        private readonly IAppTimeProvider _time;
        private const string CategoriesWithIngredientsCacheKey = "meals:categories_with_ingredients";
        private const string MealByIdCacheKeyPrefix = "meals:id:";

        public MealService(
            IMealRepository mealRepository,
            IIngredientRepository ingredientRepository,
            IMealOptionRepository mealOptionRepository,
            IIngredientCategoryRepository ingredientCategoryRepository,
            IMealOptionIngredientRepository mealOptionIngredientRepository,
            ISupabaseStorageService storageService,
            ICacheService cacheService,
            ILogger<MealService> logger,
            IAppTimeProvider time)
        {
            _mealRepository = mealRepository;
            _ingredientRepository = ingredientRepository;
            _mealOptionRepository = mealOptionRepository;
            _ingredientCategoryRepository = ingredientCategoryRepository;
            _mealOptionIngredientRepository = mealOptionIngredientRepository;
            _storageService = storageService;
            _cacheService = cacheService;
            _logger = logger;
            _time = time;
        }

        // ✅ FIX 3.2: Pagination-Aware Cache for Active Meals (10-minute TTL)
        public async Task<PagedResult<MealDto>> GetActiveMealsAsync(int page, int pageSize)
        {
            // Clamp inputs
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var cacheKey = $"meals:active:p{page}:s{pageSize}";
            var cachedResult = await _cacheService.GetAsync<PagedResult<MealDto>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }

            var (meals, totalCount) = await _mealRepository.GetActiveMealsAsync(page, pageSize);
            var result = new List<MealDto>();
            
            // Filter: IsComplete (IsDeleted already filtered in repo)
            foreach (var m in meals.Where(m => m.IsComplete))
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
                dto.ImageUrl = await GetSafeSignedUrlAsync(m.ImageUrl);
                
                result.Add(dto);
            }
            
            var pagedResult = new PagedResult<MealDto>
            {
                Items = result,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            await _cacheService.SetAsync(cacheKey, pagedResult, TimeSpan.FromMinutes(10));
            return pagedResult;
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
                
                // Nutrition fields
                ApproxCalories = dto.ApproxCalories,
                ApproxProtein = dto.ApproxProtein,
                ApproxCarbs = dto.ApproxCarbs,
                ApproxFats = dto.ApproxFats,
                
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _mealRepository.AddMealAsync(meal);
            await _mealRepository.SaveChangesAsync();

            // Bust the cache when a meal is created
            await InvalidateMealCachesAsync(meal.MealId);

            return meal.MealId;
        }

        public async Task<MealDto?> GetMealByIdAsync(int id)
        {
            var cacheKey = MealByIdCacheKeyPrefix + id;
            var cached = await _cacheService.GetAsync<MealDto>(cacheKey);
            if (cached != null) return cached;

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
            dto.ImageUrl = await GetSafeSignedUrlAsync(meal.ImageUrl);

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30));

            return dto;
        }

        public async Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto)
        {
            var meal = await _mealRepository.GetByIdAsync(calculationDto.MealId);
            if (meal == null)
                throw new ArgumentException("Meal not found");

            var ids = calculationDto.SelectedIngredients?.Select(i => i.IngredientId) ?? Enumerable.Empty<int>();
            var ingredientMap = await _ingredientRepository.GetByIdsAsync(ids);

            var isValidSelection = await ValidateIngredientSelectionAsync(calculationDto.MealId, calculationDto.SelectedIngredients, ingredientMap);
            if (!isValidSelection)
                throw new InvalidOperationException("Invalid ingredient selection based on meal options");

            var ingredientsPrice = await GetIngredientsTotalPriceAsync(calculationDto.SelectedIngredients, ingredientMap);
            var (totalCalories, totalProtein, totalFiber) = await GetNutritionalSummaryAsync(calculationDto.SelectedIngredients, ingredientMap);
            var ingredientBreakdown = await GetIngredientBreakdownAsync(calculationDto.SelectedIngredients, ingredientMap);

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

        public Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        {
            if (ingredients == null || !ingredients.Any()) return Task.FromResult(0m);
            
            decimal total = 0;
            foreach (var item in ingredients)
            {
                if (ingredientMap.TryGetValue(item.IngredientId, out var ingredient) && ingredient.IsAvailable)
                {
                    total += ingredient.Price * item.Quantity;
                }
            }
            
            return Task.FromResult(total);
        }

        public Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        {
            if (ingredients == null || !ingredients.Any()) return Task.FromResult((0, 0m, 0m));

            int totalCalories = 0;
            decimal totalProtein = 0;
            decimal totalFiber = 0;

            foreach (var item in ingredients)
            {
                if (ingredientMap.TryGetValue(item.IngredientId, out var ingredient))
                {
                    totalCalories += ingredient.Calories * item.Quantity;
                    totalProtein += ingredient.Protein * item.Quantity;
                    totalFiber += ingredient.Fiber * item.Quantity;
                }
            }

            return Task.FromResult((totalCalories, totalProtein, totalFiber));
        }

        public async Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        {
            var mealOptions = await _mealOptionRepository.GetByMealIdAsync(mealId);
            var ingredientsByCategory = new Dictionary<int, List<SelectedIngredientDto>>();
            
            foreach (var selectedIngredient in ingredients)
            {
                if (ingredientMap.TryGetValue(selectedIngredient.IngredientId, out var ingredient))
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

        private Task<List<IngredientBreakdownDto>> GetIngredientBreakdownAsync(List<SelectedIngredientDto> selectedIngredients, IDictionary<int, Ingredient> ingredientMap)
        {
            if (selectedIngredients == null || !selectedIngredients.Any()) return Task.FromResult(new List<IngredientBreakdownDto>());
            var breakdown = new List<IngredientBreakdownDto>();

            foreach (var item in selectedIngredients)
            {
                if (ingredientMap.TryGetValue(item.IngredientId, out var ingredient))
                {
                    breakdown.Add(new IngredientBreakdownDto
                    {
                        IngredientId = ingredient.IngredientId,
                        IngredientName = ingredient.IngredientName,
                        Quantity = item.Quantity,
                        UnitPrice = ingredient.Price,
                        TotalPrice = ingredient.Price * item.Quantity,
                        Calories = ingredient.Calories * item.Quantity,
                        Protein = ingredient.Protein * item.Quantity,
                    });
                }
            }

            return Task.FromResult(breakdown);
        }

        // ========== ADMIN METHODS (UPDATED) ==========

        // Use eager loading to avoid N+1 queries
        public async Task<List<AdminMealListDto>> GetAllMealsForAdminAsync()
        {
            var meals = await _mealRepository.GetAllWithOptionsCountAsync();
            var mealList = new List<AdminMealListDto>();

            foreach (var meal in meals)
            {
                // Options are already loaded via Include - no extra DB call
                var mealOptions = meal.MealOptions ?? Enumerable.Empty<MealOption>();
                
                mealList.Add(new AdminMealListDto
                {
                    MealId = meal.MealId,
                    MealName = meal.MealName,
                    Description = meal.Description,
                    BasePrice = meal.BasePrice,
                    MealOptionsCount = mealOptions.Count(),
                    IsComplete = mealOptions.Any(),
                    
                    // Map nutrition fields
                    ApproxCalories = meal.ApproxCalories,
                    ApproxProtein = meal.ApproxProtein,
                    ApproxCarbs = meal.ApproxCarbs,
                    ApproxFats = meal.ApproxFats,
                    
                    // Image URL (generate signed URL for secure access)
                    ImageUrl = !string.IsNullOrEmpty(meal.ImageUrl) 
                        ? await _storageService.GetSignedUrlAsync(ExtractStoragePath(meal.ImageUrl)) 
                        : null,
                    
                    CreatedAt = meal.CreatedAt,
                    UpdatedAt = meal.UpdatedAt
                });
            }

            return mealList;
        }

        // Paginated admin list
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
                        ? await _storageService.GetSignedUrlAsync(ExtractStoragePath(meal.ImageUrl))
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

            return await MapToAdminMealDetailDtoAsync(meal);
        }

        public async Task<List<AdminMealDetailDto>> GetMealsBatchDetailsAsync(List<int> mealIds)
        {
            if (mealIds == null || !mealIds.Any()) return new List<AdminMealDetailDto>();
            
            var meals = await _mealRepository.GetByIdsWithOptionsAsync(mealIds);
            var results = new List<AdminMealDetailDto>();
            
            foreach (var meal in meals)
            {
                results.Add(await MapToAdminMealDetailDtoAsync(meal));
            }
            
            return results;
        }

        private async Task<AdminMealDetailDto> MapToAdminMealDetailDtoAsync(Meal meal)
        {
            var mealDetail = new AdminMealDetailDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                ApproxCalories = meal.ApproxCalories,
                ApproxProtein = meal.ApproxProtein,
                ApproxCarbs = meal.ApproxCarbs,
                ApproxFats = meal.ApproxFats,
                ImageUrl = await GetSafeSignedUrlAsync(meal.ImageUrl),
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt,
                MealOptions = new List<AdminMealOptionDetailDto>()
            };

            if (meal.MealOptions != null)
            {
                foreach (var mealOption in meal.MealOptions)
                {
                    var optionDetail = new AdminMealOptionDetailDto
                    {
                        MealOptionId = mealOption.MealOptionId,
                        CategoryId = mealOption.CategoryId,
                        CategoryName = mealOption.IngredientCategory?.CategoryName ?? string.Empty,
                        IsRequired = mealOption.IsRequired,
                        MaxSelectable = mealOption.MaxSelectable,
                        Ingredients = new List<MealIngredientDto>()
                    };

                    if (mealOption.MealOptionIngredients != null)
                    {
                        foreach (var mealOptionIngredient in mealOption.MealOptionIngredients)
                        {
                            optionDetail.Ingredients.Add(new MealIngredientDto
                            {
                                IngredientId = mealOptionIngredient.Ingredient.IngredientId,
                                IngredientName = mealOptionIngredient.Ingredient.IngredientName,
                                Price = mealOptionIngredient.Ingredient.Price,
                                IconEmoji = mealOptionIngredient.Ingredient.IconEmoji,
                                Available = mealOptionIngredient.Ingredient.IsAvailable,
                                Calories = mealOptionIngredient.Ingredient.Calories,
                                Protein = mealOptionIngredient.Ingredient.Protein,
                                Fiber = mealOptionIngredient.Ingredient.Fiber
                            });
                        }
                    }

                    mealDetail.MealOptions.Add(optionDetail);
                }
            }

            return mealDetail;
        }

        public async Task<int> CreateMealWithOptionsAsync(AdminCreateMealDto dto)
        {
            // P1-6 FIX: Batch-validate all ingredients in a single query (kills N+1)
            var allIngredientIds = dto.MealOptions
                .SelectMany(o => o.IngredientIds)
                .Distinct()
                .ToList();
            var existingIngredients = await _ingredientRepository.GetByIdsAsync(allIngredientIds);
            var missingIds = allIngredientIds.Where(id => !existingIngredients.ContainsKey(id)).ToList();
            if (missingIds.Any())
                throw new ArgumentException($"Ingredients not found: {string.Join(", ", missingIds)}");

            // Create meal
            var meal = new Meal
            {
                MealName = dto.MealName,
                Description = dto.Description,
                BasePrice = dto.BasePrice,
                
                // Map nutrition fields
                ApproxCalories = dto.ApproxCalories,
                ApproxProtein = dto.ApproxProtein,
                ApproxCarbs = dto.ApproxCarbs,
                ApproxFats = dto.ApproxFats,
                
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
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
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow
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
                        CreatedAt = _time.UtcNow,
                        UpdatedAt = _time.UtcNow
                    };

                    await _mealOptionIngredientRepository.AddAsync(mealOptionIngredient);
                }

                await _mealOptionIngredientRepository.SaveChangesAsync();
            }

            // Bust the cache when a meal is created
            await _cacheService.RemoveAsync(CategoriesWithIngredientsCacheKey);

            return meal.MealId;
        }

        public async Task<bool> UpdateMealAsync(int id, UpdateMealDto dto)
        {
            var meal = await _mealRepository.GetByIdWithOptionsAsync(id);
            if (meal == null) return false;

            // P1-6 FIX: Batch-validate all ingredients in a single query (kills N+1)
            var allIngredientIds = dto.MealOptions
                .SelectMany(o => o.IngredientIds)
                .Distinct()
                .ToList();
            var existingIngredients = await _ingredientRepository.GetByIdsAsync(allIngredientIds);
            var missingIds = allIngredientIds.Where(id => !existingIngredients.ContainsKey(id)).ToList();
            if (missingIds.Any())
                throw new ArgumentException($"Ingredients not found: {string.Join(", ", missingIds)}");

            // Update meal basic info
            meal.MealName = dto.MealName;
            meal.Description = dto.Description;
            meal.BasePrice = dto.BasePrice;
            
            // Update nutrition fields
            meal.ApproxCalories = dto.ApproxCalories;
            meal.ApproxProtein = dto.ApproxProtein;
            meal.ApproxCarbs = dto.ApproxCarbs;
            meal.ApproxFats = dto.ApproxFats;
            
            meal.UpdatedAt = _time.UtcNow;

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
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow
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
                        CreatedAt = _time.UtcNow,
                        UpdatedAt = _time.UtcNow
                    };

                    await _mealOptionIngredientRepository.AddAsync(mealOptionIngredient);
                }

                await _mealOptionIngredientRepository.SaveChangesAsync();
            }

            await _mealRepository.UpdateMealAsync(meal);
            
            // Bust the cache when a meal is updated
            await InvalidateMealCachesAsync(id);
            
            return true;
        }

        public async Task<bool> DeleteMealAsync(int id)
        {
            var meal = await _mealRepository.GetByIdAsync(id);
            if (meal == null) return false;

            // Delete cascade will handle meal options and meal option ingredients
            await _mealRepository.DeleteMealAsync(meal);
            
            // Bust the cache when a meal is deleted
            await InvalidateMealCachesAsync(id);
            
            return true;
        }

        public async Task<bool> UpdateMealStatusAsync(int id, bool isComplete)
        {
            var result = await _mealRepository.UpdateMealStatusAsync(id, isComplete);
            if (result)
            {
                await InvalidateMealCachesAsync(id);
            }
            return result;
        }

        public async Task<List<CategoryWithIngredientsDto>> GetCategoriesWithIngredientsAsync()
        {
            var cached = await _cacheService.GetAsync<List<CategoryWithIngredientsDto>>(CategoriesWithIngredientsCacheKey);
            if (cached != null) return cached;

            var categories = await _ingredientCategoryRepository.GetAllWithIngredientsAsync();
            var result = categories.Select(category => new CategoryWithIngredientsDto
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Ingredients = category.Ingredients.Select(i => new IngredientDto
                {
                    IngredientId = i.IngredientId,
                    CategoryId = i.CategoryId,
                    IngredientName = i.IngredientName,
                    Price = i.Price,
                    Available = i.IsAvailable,
                    Calories = i.Calories,
                    Protein = i.Protein,
                    Fiber = i.Fiber,
                    IconEmoji = i.IconEmoji,
                    Description = i.Description
                }).ToList()
            }).ToList();

            await _cacheService.SetAsync(CategoriesWithIngredientsCacheKey, result, TimeSpan.FromMinutes(30));
            
            return result;
        }

        // Update meal image
        public async Task<bool> UpdateMealImageAsync(int mealId, string imageUrl)
        {
            var meal = await _mealRepository.GetByIdAsync(mealId);
            if (meal == null) return false;

            meal.ImageUrl = imageUrl;
            meal.UpdatedAt = _time.UtcNow;
            await _mealRepository.UpdateMealAsync(meal);
            await InvalidateMealCachesAsync(mealId);
            return true;
        }

        // Delete meal image
        public async Task<string?> DeleteMealImageAsync(int mealId)
        {
            var meal = await _mealRepository.GetByIdAsync(mealId);
            if (meal == null) return null;

            var existingUrl = meal.ImageUrl;
            meal.ImageUrl = null;
            meal.UpdatedAt = _time.UtcNow;
            await _mealRepository.UpdateMealAsync(meal);
            await InvalidateMealCachesAsync(mealId);
            return existingUrl;
        }

        // Helper method to safely get signed URL without throwing
        private async Task<string?> GetSafeSignedUrlAsync(string? storagePath)
        {
            if (string.IsNullOrEmpty(storagePath)) return null;
            try
            {
                var filePath = ExtractStoragePath(storagePath);
                return await _storageService.GetSignedUrlAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Signed URL failed for {Path}: {Error}", storagePath, ex.Message);
                return null;
            }
        }

        // User-facing batch details - uses single query, filters IsComplete, preserves order
        public async Task<List<MealWithDetailsDto>> GetMealsBatchDetailsForUsersAsync(List<int> mealIds)
        {
            if (mealIds == null || mealIds.Count == 0)
                return new List<MealWithDetailsDto>();

            // Remove duplicates and preserve input order
            var uniqueIds = mealIds.Distinct().ToList();
            var meals = await _mealRepository.GetByIdsForUsersAsync(uniqueIds);

            // Build lookup map for order preservation safely
            var mealMap = meals.GroupBy(m => m.MealId).ToDictionary(g => g.Key, g => g.First());
            var results = new List<MealWithDetailsDto>();

            foreach (var id in uniqueIds)
            {
                if (mealMap.TryGetValue(id, out var meal))
                {
                    var dto = MapToMealWithDetailsDto(meal);
                    // Generate signed URL for image (MealWithDetailsDto now has ImageUrl field)
                    dto.ImageUrl = await GetSafeSignedUrlAsync(meal.ImageUrl);
                    results.Add(dto);
                }
            }

            return results;
        }

        // Helper: Map Meal entity to user-facing DTO
        private static MealWithDetailsDto MapToMealWithDetailsDto(Meal meal)
        {
            var dto = new MealWithDetailsDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                ApproxCalories = meal.ApproxCalories,
                ApproxProtein = meal.ApproxProtein,
                ApproxCarbs = meal.ApproxCarbs,
                ApproxFats = meal.ApproxFats,
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt,
                MealOptionsCount = meal.MealOptions?.Count ?? 0,
                MealOptions = new List<MealOptionDto>()
            };

            if (meal.MealOptions != null)
            {
                foreach (var option in meal.MealOptions)
                {
                    var optionDto = new MealOptionDto
                    {
                        MealOptionId = option.MealOptionId,
                        CategoryId = option.CategoryId,
                        CategoryName = option.IngredientCategory?.CategoryName ?? "",
                        IsRequired = option.IsRequired,
                        MaxSelectable = option.MaxSelectable,
                        Ingredients = new List<MealIngredientDto>()
                    };

                    if (option.MealOptionIngredients != null)
                    {
                        foreach (var moi in option.MealOptionIngredients)
                        {
                            if (moi.Ingredient != null)
                            {
                                optionDto.Ingredients.Add(new MealIngredientDto
                                {
                                    IngredientId = moi.Ingredient.IngredientId,
                                    IngredientName = moi.Ingredient.IngredientName,
                                    Price = moi.Ingredient.Price,
                                    IconEmoji = moi.Ingredient.IconEmoji ?? "",
                                    Available = moi.Ingredient.IsAvailable,
                                    Calories = moi.Ingredient.Calories,
                                    Protein = moi.Ingredient.Protein,
                                    Fiber = moi.Ingredient.Fiber
                                });
                            }
                        }
                    }

                    dto.MealOptions.Add(optionDto);
                }
            }

            return dto;
        }

        private async Task InvalidateMealCachesAsync(int? mealId = null)
        {
            if (mealId.HasValue)
            {
                await _cacheService.RemoveAsync(MealByIdCacheKeyPrefix + mealId.Value);
            }
            await _cacheService.RemoveAsync(CategoriesWithIngredientsCacheKey);
            await _cacheService.RemoveAsync("meals:active:p1:s20");
            await _cacheService.RemoveAsync("meals:active:p1:s200");
            await _cacheService.RemoveAsync("meals:active:p1:s10");
            await _cacheService.RemoveAsync("meals:active:p1:s50");
        }
    }
}
