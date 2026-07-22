using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetailsForUsers;

public class GetMealsBatchDetailsForUsersQueryHandler : IRequestHandler<GetMealsBatchDetailsForUsersQuery, List<MealWithDetailsDto>>
{
    private readonly IMealRepository _mealRepository;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetMealsBatchDetailsForUsersQueryHandler> _logger;

    public GetMealsBatchDetailsForUsersQueryHandler(
        IMealRepository mealRepository,
        ISupabaseStorageService storageService,
        ILogger<GetMealsBatchDetailsForUsersQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<List<MealWithDetailsDto>> Handle(GetMealsBatchDetailsForUsersQuery request, CancellationToken cancellationToken)
    {
        if (request.MealIds == null || request.MealIds.Count == 0)
            return new List<MealWithDetailsDto>();

        var uniqueIds = request.MealIds.Distinct().ToList();
        var meals = await _mealRepository.GetByIdsForUsersAsync(uniqueIds);

        var mealMap = meals.GroupBy(m => m.MealId).ToDictionary(g => g.Key, g => g.First());
        var results = new List<MealWithDetailsDto>();

        foreach (var id in uniqueIds)
        {
            if (mealMap.TryGetValue(id, out var meal))
            {
                var dto = MealCatalogHelper.MapToMealWithDetailsDto(meal);
                dto.ImageUrl = await MealCatalogHelper.GetSafeSignedUrlAsync(_storageService, _logger, meal.ImageUrl);
                results.Add(dto);
            }
        }

        return results;
    }
}
