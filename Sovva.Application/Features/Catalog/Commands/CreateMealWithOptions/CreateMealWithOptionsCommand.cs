using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Commands.CreateMealWithOptions;

public record CreateMealWithOptionsCommand(AdminCreateMealDto Dto) : IRequest<int>;
