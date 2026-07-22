using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Commands.DeleteMealImage;

public class DeleteMealImageCommandHandler : IRequestHandler<DeleteMealImageCommand, string?>
{
    private readonly IMealRepository _mealRepository;
    private readonly ICacheService _cacheService;
    private readonly IAppTimeProvider _time;

    public DeleteMealImageCommandHandler(
        IMealRepository mealRepository,
        ICacheService cacheService,
        IAppTimeProvider time)
    {
        _mealRepository = mealRepository;
        _cacheService = cacheService;
        _time = time;
    }

    public async Task<string?> Handle(DeleteMealImageCommand request, CancellationToken cancellationToken)
    {
        var meal = await _mealRepository.GetByIdAsync(request.MealId);
        if (meal == null) return null;

        var existingUrl = meal.ImageUrl;
        meal.ImageUrl = null;
        meal.UpdatedAt = _time.UtcNow;
        await _mealRepository.UpdateMealAsync(meal);
        await MealCatalogHelper.InvalidateMealCachesAsync(_cacheService, request.MealId);
        return existingUrl;
    }
}
