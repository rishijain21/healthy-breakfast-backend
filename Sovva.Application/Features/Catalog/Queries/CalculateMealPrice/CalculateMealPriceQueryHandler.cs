using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog.Queries.GetIngredientBreakdown;
using Sovva.Application.Features.Catalog.Queries.GetIngredientsTotalPrice;
using Sovva.Application.Features.Catalog.Queries.GetNutritionalSummary;
using Sovva.Application.Features.Catalog.Queries.ValidateIngredientSelection;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.CalculateMealPrice;

public class CalculateMealPriceQueryHandler : IRequestHandler<CalculateMealPriceQuery, MealPriceResponseDto>
{
    private readonly IMealRepository _mealRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly ISender _sender;

    public CalculateMealPriceQueryHandler(
        IMealRepository mealRepository,
        IIngredientRepository ingredientRepository,
        ISender sender)
    {
        _mealRepository = mealRepository;
        _ingredientRepository = ingredientRepository;
        _sender = sender;
    }

    public async Task<MealPriceResponseDto> Handle(CalculateMealPriceQuery request, CancellationToken cancellationToken)
    {
        var calculationDto = request.CalculationDto;
        var meal = await _mealRepository.GetByIdAsync(calculationDto.MealId);
        if (meal == null)
            throw new ArgumentException("Meal not found");

        var ids = calculationDto.SelectedIngredients?.Select(i => i.IngredientId) ?? Enumerable.Empty<int>();
        var ingredientMap = await _ingredientRepository.GetByIdsAsync(ids);

        var isValidSelection = await _sender.Send(new ValidateIngredientSelectionQuery(calculationDto.MealId, calculationDto.SelectedIngredients ?? new(), ingredientMap), cancellationToken);
        if (!isValidSelection)
            throw new InvalidOperationException("Invalid ingredient selection based on meal options");

        var ingredientsPrice = await _sender.Send(new GetIngredientsTotalPriceQuery(calculationDto.SelectedIngredients ?? new(), ingredientMap), cancellationToken);
        var (totalCalories, totalProtein, totalFiber) = await _sender.Send(new GetNutritionalSummaryQuery(calculationDto.SelectedIngredients ?? new(), ingredientMap), cancellationToken);
        var ingredientBreakdown = await _sender.Send(new GetIngredientBreakdownQuery(calculationDto.SelectedIngredients ?? new(), ingredientMap), cancellationToken);

        return new MealPriceResponseDto
        {
            MealId = meal.MealId,
            MealName = meal.MealName,
            BaseMealPrice = meal.BasePrice,
            IngredientsPrice = ingredientsPrice,
            TotalPrice = meal.BasePrice + ingredientsPrice,
            TotalCalories = totalCalories,
            TotalProtein = totalProtein,
            TotalFiber = totalFiber,
            IngredientBreakdown = ingredientBreakdown
        };
    }
}
