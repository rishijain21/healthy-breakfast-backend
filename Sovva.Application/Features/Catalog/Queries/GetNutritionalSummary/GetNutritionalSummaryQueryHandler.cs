using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Sovva.Application.Features.Catalog.Queries.GetNutritionalSummary;

public class GetNutritionalSummaryQueryHandler : IRequestHandler<GetNutritionalSummaryQuery, (int calories, decimal protein, decimal fiber)>
{
    public Task<(int calories, decimal protein, decimal fiber)> Handle(GetNutritionalSummaryQuery request, CancellationToken cancellationToken)
    {
        if (request.Ingredients == null || !request.Ingredients.Any())
            return Task.FromResult((0, 0m, 0m));

        int totalCalories = 0;
        decimal totalProtein = 0;
        decimal totalFiber = 0;

        foreach (var item in request.Ingredients)
        {
            if (request.IngredientMap.TryGetValue(item.IngredientId, out var ingredient))
            {
                totalCalories += ingredient.Calories * item.Quantity;
                totalProtein += ingredient.Protein * item.Quantity;
                totalFiber += ingredient.Fiber * item.Quantity;
            }
        }

        return Task.FromResult((totalCalories, totalProtein, totalFiber));
    }
}
