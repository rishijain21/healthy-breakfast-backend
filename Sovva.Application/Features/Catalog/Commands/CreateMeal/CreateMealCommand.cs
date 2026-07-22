using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Commands.CreateMeal;

public record CreateMealCommand(CreateMealDto Dto) : IRequest<int>;
