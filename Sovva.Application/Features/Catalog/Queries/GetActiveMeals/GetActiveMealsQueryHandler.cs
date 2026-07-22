using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetActiveMeals;

public class GetActiveMealsQueryHandler : IRequestHandler<GetActiveMealsQuery, PagedResult<MealDto>>
{
    private readonly IMealRepository _mealRepository;
    private readonly ICacheService _cacheService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetActiveMealsQueryHandler> _logger;

    public GetActiveMealsQueryHandler(
        IMealRepository mealRepository,
        ICacheService cacheService,
        ISupabaseStorageService storageService,
        ILogger<GetActiveMealsQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _cacheService = cacheService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<PagedResult<MealDto>> Handle(GetActiveMealsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var cacheKey = CacheKeys.MealsActive(page, pageSize);
        var cachedResult = await _cacheService.GetAsync<PagedResult<MealDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var (meals, totalCount) = await _mealRepository.GetActiveMealsAsync(page, pageSize);
        var result = new List<MealDto>();

        foreach (var m in meals.Where(m => m.IsComplete))
        {
            var dto = new MealDto
            {
                MealId = m.MealId,
                MealName = m.MealName,
                Description = m.Description,
                BasePrice = m.BasePrice,
                IsComplete = m.IsComplete,
                ImageUrl = await MealCatalogHelper.GetSafeSignedUrlAsync(_storageService, _logger, m.ImageUrl)
            };
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
}
