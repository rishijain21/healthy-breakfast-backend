using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Queries.GetIngredientsTotalPrice;

public record GetIngredientsTotalPriceQuery(
    List<SelectedIngredientDto> Ingredients,
    IDictionary<int, Ingredient> IngredientMap
) : IRequest<decimal>;
