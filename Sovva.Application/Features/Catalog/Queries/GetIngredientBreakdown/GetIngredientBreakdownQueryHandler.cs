using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetIngredientBreakdown;

public class GetIngredientBreakdownQueryHandler : IRequestHandler<GetIngredientBreakdownQuery, List<IngredientBreakdownDto>>
{
    public Task<List<IngredientBreakdownDto>> Handle(GetIngredientBreakdownQuery request, CancellationToken cancellationToken)
    {
        if (request.SelectedIngredients == null || !request.SelectedIngredients.Any())
            return Task.FromResult(new List<IngredientBreakdownDto>());

        var breakdown = new List<IngredientBreakdownDto>();

        foreach (var item in request.SelectedIngredients)
        {
            if (request.IngredientMap.TryGetValue(item.IngredientId, out var ingredient))
            {
                breakdown.Add(new IngredientBreakdownDto
                {
                    IngredientId = ingredient.IngredientId,
                    IngredientName = ingredient.IngredientName,
                    Quantity = item.Quantity,
                    UnitPrice = ingredient.Price,
                    TotalPrice = ingredient.Price * item.Quantity,
                    Calories = ingredient.Calories * item.Quantity,
                    Protein = ingredient.Protein * item.Quantity,
                });
            }
        }

        return Task.FromResult(breakdown);
    }
}
