using MediatR;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMealImage;

public record UpdateMealImageCommand(int MealId, string ImageUrl) : IRequest<bool>;
