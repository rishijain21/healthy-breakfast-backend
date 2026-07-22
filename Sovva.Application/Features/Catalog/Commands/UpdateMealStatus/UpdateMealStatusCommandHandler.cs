using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMealStatus;

public class UpdateMealStatusCommandHandler : IRequestHandler<UpdateMealStatusCommand, bool>
{
    private readonly IMealRepository _mealRepository;
    private readonly ICacheService _cacheService;

    public UpdateMealStatusCommandHandler(IMealRepository mealRepository, ICacheService cacheService)
    {
        _mealRepository = mealRepository;
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(UpdateMealStatusCommand request, CancellationToken cancellationToken)
    {
        var result = await _mealRepository.UpdateMealStatusAsync(request.MealId, request.IsComplete);
        if (result)
        {
            await MealCatalogHelper.InvalidateMealCachesAsync(_cacheService, request.MealId);
        }
        return result;
    }
}
