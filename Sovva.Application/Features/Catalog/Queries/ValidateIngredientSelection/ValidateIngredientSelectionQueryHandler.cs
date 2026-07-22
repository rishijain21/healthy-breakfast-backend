using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Catalog.Queries.ValidateIngredientSelection;

public class ValidateIngredientSelectionQueryHandler : IRequestHandler<ValidateIngredientSelectionQuery, bool>
{
    private readonly IMealOptionRepository _mealOptionRepository;

    public ValidateIngredientSelectionQueryHandler(IMealOptionRepository mealOptionRepository)
    {
        _mealOptionRepository = mealOptionRepository;
    }

    public async Task<bool> Handle(ValidateIngredientSelectionQuery request, CancellationToken cancellationToken)
    {
        var mealOptions = await _mealOptionRepository.GetByMealIdAsync(request.MealId);
        var ingredientsByCategory = new Dictionary<int, List<SelectedIngredientDto>>();

        foreach (var selectedIngredient in request.Ingredients)
        {
            if (request.IngredientMap.TryGetValue(selectedIngredient.IngredientId, out var ingredient))
            {
                if (!ingredientsByCategory.ContainsKey(ingredient.CategoryId))
                    ingredientsByCategory[ingredient.CategoryId] = new List<SelectedIngredientDto>();

                ingredientsByCategory[ingredient.CategoryId].Add(selectedIngredient);
            }
        }

        foreach (var mealOption in mealOptions)
        {
            var categoryIngredients = ingredientsByCategory.GetValueOrDefault(mealOption.CategoryId, new List<SelectedIngredientDto>());

            if (mealOption.IsRequired && !categoryIngredients.Any())
                return false;

            if (categoryIngredients.Count > mealOption.MaxSelectable)
                return false;
        }

        return true;
    }
}
