using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdminPaged;

public class GetAllMealsForAdminPagedQueryHandler : IRequestHandler<GetAllMealsForAdminPagedQuery, PagedResult<AdminMealListDto>>
{
    private readonly IMealRepository _mealRepository;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetAllMealsForAdminPagedQueryHandler> _logger;

    public GetAllMealsForAdminPagedQueryHandler(
        IMealRepository mealRepository,
        ISupabaseStorageService storageService,
        ILogger<GetAllMealsForAdminPagedQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<PagedResult<AdminMealListDto>> Handle(GetAllMealsForAdminPagedQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

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
                MealOptionsCount = 0,
                IsComplete = meal.IsComplete,
                ApproxCalories = meal.ApproxCalories,
                ApproxProtein = meal.ApproxProtein,
                ApproxCarbs = meal.ApproxCarbs,
                ApproxFats = meal.ApproxFats,
                ImageUrl = await MealCatalogHelper.GetSafeSignedUrlAsync(_storageService, _logger, meal.ImageUrl),
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
}
