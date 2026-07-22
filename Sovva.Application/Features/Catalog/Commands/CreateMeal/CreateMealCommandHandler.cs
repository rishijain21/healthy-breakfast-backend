using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Commands.CreateMeal;

public class CreateMealCommandHandler : IRequestHandler<CreateMealCommand, int>
{
    private readonly IMealRepository _mealRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IAppTimeProvider _time;

    public CreateMealCommandHandler(
        IMealRepository mealRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IAppTimeProvider time)
    {
        _mealRepository = mealRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _time = time;
    }

    public async Task<int> Handle(CreateMealCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var meal = new Meal
        {
            MealName = dto.MealName,
            Description = dto.Description,
            BasePrice = dto.BasePrice,
            ApproxCalories = dto.ApproxCalories,
            ApproxProtein = dto.ApproxProtein,
            ApproxCarbs = dto.ApproxCarbs,
            ApproxFats = dto.ApproxFats,
            CreatedAt = _time.UtcNow,
            UpdatedAt = _time.UtcNow
        };

        await _mealRepository.AddMealAsync(meal);
        await _unitOfWork.SaveChangesAsync();

        await MealCatalogHelper.InvalidateMealCachesAsync(_cacheService, meal.MealId);
        return meal.MealId;
    }
}
