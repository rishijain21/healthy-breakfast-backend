using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.GetMealDetailForAdmin;

public class GetMealDetailForAdminQueryHandler : IRequestHandler<GetMealDetailForAdminQuery, AdminMealDetailDto?>
{
    private readonly IMealRepository _mealRepository;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<GetMealDetailForAdminQueryHandler> _logger;

    public GetMealDetailForAdminQueryHandler(
        IMealRepository mealRepository,
        ISupabaseStorageService storageService,
        ILogger<GetMealDetailForAdminQueryHandler> logger)
    {
        _mealRepository = mealRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<AdminMealDetailDto?> Handle(GetMealDetailForAdminQuery request, CancellationToken cancellationToken)
    {
        var meal = await _mealRepository.GetByIdWithOptionsAsync(request.MealId);
        if (meal == null) return null;

        return await MealCatalogHelper.MapToAdminMealDetailDtoAsync(meal, _storageService, _logger);
    }
}
