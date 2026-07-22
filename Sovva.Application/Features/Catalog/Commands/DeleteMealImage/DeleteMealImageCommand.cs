using MediatR;

namespace Sovva.Application.Features.Catalog.Commands.DeleteMealImage;

public record DeleteMealImageCommand(int MealId) : IRequest<string?>;
