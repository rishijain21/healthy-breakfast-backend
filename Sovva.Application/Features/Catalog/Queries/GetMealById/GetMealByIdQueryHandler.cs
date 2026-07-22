using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetMealById;

public class GetMealByIdQueryHandler : IRequestHandler<GetMealByIdQuery, MealDto?>
{
    private readonly IMealRepository _mealRepository;
    private readonly ICacheService _cacheService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetMealByIdQueryHandler> _logger;

    public GetMealByIdQueryHandler(
        IMealRepository mealRepository,
        ICacheService cacheService,
        ISupabaseStorageService storageService,
        ILogger<GetMealByIdQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _cacheService = cacheService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<MealDto?> Handle(GetMealByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.MealById(request.MealId);
        var cached = await _cacheService.GetAsync<MealDto>(cacheKey);
        if (cached != null) return cached;

        var meal = await _mealRepository.GetByIdAsync(request.MealId);
        if (meal == null) return null;

        var dto = new MealDto
        {
            MealId = meal.MealId,
            MealName = meal.MealName,
            Description = meal.Description,
            BasePrice = meal.BasePrice,
            CreatedAt = meal.CreatedAt,
            UpdatedAt = meal.UpdatedAt,
            ImageUrl = await MealCatalogHelper.GetSafeSignedUrlAsync(_storageService, _logger, meal.ImageUrl)
        };

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30));
        return dto;
    }
}
