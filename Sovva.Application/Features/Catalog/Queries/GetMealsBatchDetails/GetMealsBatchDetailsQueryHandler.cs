using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetails;

public class GetMealsBatchDetailsQueryHandler : IRequestHandler<GetMealsBatchDetailsQuery, List<AdminMealDetailDto>>
{
    private readonly IMealRepository _mealRepository;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetMealsBatchDetailsQueryHandler> _logger;

    public GetMealsBatchDetailsQueryHandler(
        IMealRepository mealRepository,
        ISupabaseStorageService storageService,
        ILogger<GetMealsBatchDetailsQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<List<AdminMealDetailDto>> Handle(GetMealsBatchDetailsQuery request, CancellationToken cancellationToken)
    {
        if (request.MealIds == null || !request.MealIds.Any())
            return new List<AdminMealDetailDto>();

        var meals = await _mealRepository.GetByIdsWithOptionsAsync(request.MealIds);
        var results = new List<AdminMealDetailDto>();

        foreach (var meal in meals)
        {
            results.Add(await MealCatalogHelper.MapToAdminMealDetailDtoAsync(meal, _storageService, _logger));
        }

        return results;
    }
}
