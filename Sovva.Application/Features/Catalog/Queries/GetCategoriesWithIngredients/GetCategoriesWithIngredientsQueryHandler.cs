using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetCategoriesWithIngredients;

public class GetCategoriesWithIngredientsQueryHandler : IRequestHandler<GetCategoriesWithIngredientsQuery, List<CategoryWithIngredientsDto>>
{
    private readonly IIngredientCategoryRepository _ingredientCategoryRepository;
    private readonly ICacheService _cacheService;

    public GetCategoriesWithIngredientsQueryHandler(
        IIngredientCategoryRepository ingredientCategoryRepository,
        ICacheService cacheService)
    {
        _ingredientCategoryRepository = ingredientCategoryRepository;
        _cacheService = cacheService;
    }

    public async Task<List<CategoryWithIngredientsDto>> Handle(GetCategoriesWithIngredientsQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<List<CategoryWithIngredientsDto>>(CacheKeys.MealCategories());
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

        await _cacheService.SetAsync(CacheKeys.MealCategories(), result, TimeSpan.FromMinutes(30));
        return result;
    }
}
