using MediatR;

namespace Sovva.Application.Features.Catalog.Commands.DeleteMeal;

public record DeleteMealCommand(int MealId) : IRequest<bool>;
