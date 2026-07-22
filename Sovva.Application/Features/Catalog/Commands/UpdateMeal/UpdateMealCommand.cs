using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMeal;

public record UpdateMealCommand(int MealId, UpdateMealDto Dto) : IRequest<bool>;
