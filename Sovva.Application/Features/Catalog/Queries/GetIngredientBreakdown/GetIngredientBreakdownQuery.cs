using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Queries.GetIngredientBreakdown;

public record GetIngredientBreakdownQuery(
    List<SelectedIngredientDto> SelectedIngredients,
    IDictionary<int, Ingredient> IngredientMap
) : IRequest<List<IngredientBreakdownDto>>;
