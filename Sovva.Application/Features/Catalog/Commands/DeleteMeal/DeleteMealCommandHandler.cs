using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Commands.DeleteMeal;

public class DeleteMealCommandHandler : IRequestHandler<DeleteMealCommand, bool>
{
    private readonly IMealRepository _mealRepository;
    private readonly ICacheService _cacheService;

    public DeleteMealCommandHandler(IMealRepository mealRepository, ICacheService cacheService)
    {
        _mealRepository = mealRepository;
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(DeleteMealCommand request, CancellationToken cancellationToken)
    {
        var meal = await _mealRepository.GetByIdAsync(request.MealId);
        if (meal == null) return false;

        await _mealRepository.DeleteMealAsync(meal);
        await MealCatalogHelper.InvalidateMealCachesAsync(_cacheService, request.MealId);
        return true;
    }
}
