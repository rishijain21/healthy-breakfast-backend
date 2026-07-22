using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetMealById;

public record GetMealByIdQuery(int MealId) : IRequest<MealDto?>;
