using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Queries.ValidateIngredientSelection;

public record ValidateIngredientSelectionQuery(
    int MealId,
    List<SelectedIngredientDto> Ingredients,
    IDictionary<int, Ingredient> IngredientMap
) : IRequest<bool>;
