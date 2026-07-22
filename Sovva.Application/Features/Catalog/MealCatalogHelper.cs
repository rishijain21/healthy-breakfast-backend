using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog;

public static class MealCatalogHelper
{
    public static string ExtractStoragePath(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return imageUrl;
        const string marker = "/meal-images/";
        var idx = imageUrl.IndexOf(marker, StringComparison.Ordinal);
        return idx >= 0 ? imageUrl[(idx + marker.Length)..] : imageUrl;
    }

    public static async Task<string?> GetSafeSignedUrlAsync(ISupabaseStorageService storageService, ILogger? logger, string? storagePath)
    {
        if (string.IsNullOrEmpty(storagePath)) return null;
        try
        {
            var filePath = ExtractStoragePath(storagePath);
            return await storageService.GetSignedUrlAsync(filePath);
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Signed URL failed for {Path}: {Error}", storagePath, ex.Message);
            return null;
        }
    }

    public static async Task InvalidateMealCachesAsync(ICacheService cacheService, int? mealId = null)
    {
        if (mealId.HasValue)
        {
            await cacheService.RemoveAsync(CacheKeys.MealById(mealId.Value));
        }
        await cacheService.RemoveAsync(CacheKeys.MealCategories());
        await cacheService.RemoveAsync(CacheKeys.MealsActive(1, 20));
        await cacheService.RemoveAsync(CacheKeys.MealsActive(1, 200));
        await cacheService.RemoveAsync(CacheKeys.MealsActive(1, 10));
        await cacheService.RemoveAsync(CacheKeys.MealsActive(1, 50));
    }

    public static async Task<AdminMealDetailDto> MapToAdminMealDetailDtoAsync(Meal meal, ISupabaseStorageService storageService, ILogger? logger)
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
            ImageUrl = await GetSafeSignedUrlAsync(storageService, logger, meal.ImageUrl),
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
                        if (mealOptionIngredient.Ingredient != null)
                        {
                            optionDetail.Ingredients.Add(new MealIngredientDto
                            {
                                IngredientId = mealOptionIngredient.Ingredient.IngredientId,
                                IngredientName = mealOptionIngredient.Ingredient.IngredientName,
                                Price = mealOptionIngredient.Ingredient.Price,
                                IconEmoji = mealOptionIngredient.Ingredient.IconEmoji ?? "",
                                Available = mealOptionIngredient.Ingredient.IsAvailable,
                                Calories = mealOptionIngredient.Ingredient.Calories,
                                Protein = mealOptionIngredient.Ingredient.Protein,
                                Fiber = mealOptionIngredient.Ingredient.Fiber
                            });
                        }
                    }
                }

                mealDetail.MealOptions.Add(optionDetail);
            }
        }

        return mealDetail;
    }

    public static MealWithDetailsDto MapToMealWithDetailsDto(Meal meal)
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
}
