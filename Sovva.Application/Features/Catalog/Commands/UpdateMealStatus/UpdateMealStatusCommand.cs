using MediatR;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMealStatus;

public record UpdateMealStatusCommand(int MealId, bool IsComplete) : IRequest<bool>;
