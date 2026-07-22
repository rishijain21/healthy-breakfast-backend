using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Queries.GetNutritionalSummary;

public record GetNutritionalSummaryQuery(
    List<SelectedIngredientDto> Ingredients,
    IDictionary<int, Ingredient> IngredientMap
) : IRequest<(int calories, decimal protein, decimal fiber)>;
