using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdmin;

public class GetAllMealsForAdminQueryHandler : IRequestHandler<GetAllMealsForAdminQuery, List<AdminMealListDto>>
{
    private readonly IMealRepository _mealRepository;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetAllMealsForAdminQueryHandler> _logger;

    public GetAllMealsForAdminQueryHandler(
        IMealRepository mealRepository,
        ISupabaseStorageService storageService,
        ILogger<GetAllMealsForAdminQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<List<AdminMealListDto>> Handle(GetAllMealsForAdminQuery request, CancellationToken cancellationToken)
    {
        var meals = await _mealRepository.GetAllWithOptionsCountAsync();
        var mealList = new List<AdminMealListDto>();

        foreach (var meal in meals)
        {
            var mealOptions = meal.MealOptions ?? Enumerable.Empty<Domain.Entities.MealOption>();

            mealList.Add(new AdminMealListDto
            {
                MealId = meal.MealId,
                MealName = meal.MealName,
                Description = meal.Description,
                BasePrice = meal.BasePrice,
                MealOptionsCount = mealOptions.Count(),
                IsComplete = mealOptions.Any(),
                ApproxCalories = meal.ApproxCalories,
                ApproxProtein = meal.ApproxProtein,
                ApproxCarbs = meal.ApproxCarbs,
                ApproxFats = meal.ApproxFats,
                ImageUrl = await MealCatalogHelper.GetSafeSignedUrlAsync(_storageService, _logger, meal.ImageUrl),
                CreatedAt = meal.CreatedAt,
                UpdatedAt = meal.UpdatedAt
            });
        }

        return mealList;
    }
}
