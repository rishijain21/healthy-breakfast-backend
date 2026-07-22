using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Sovva.Application.Features.Catalog.Queries.GetIngredientsTotalPrice;

public class GetIngredientsTotalPriceQueryHandler : IRequestHandler<GetIngredientsTotalPriceQuery, decimal>
{
    public Task<decimal> Handle(GetIngredientsTotalPriceQuery request, CancellationToken cancellationToken)
    {
        if (request.Ingredients == null || !request.Ingredients.Any())
            return Task.FromResult(0m);

        decimal total = 0;
        foreach (var item in request.Ingredients)
        {
            if (request.IngredientMap.TryGetValue(item.IngredientId, out var ingredient) && ingredient.IsAvailable)
            {
                total += ingredient.Price * item.Quantity;
            }
        }

        return Task.FromResult(total);
    }
}
