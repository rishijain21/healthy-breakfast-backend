using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;

namespace Sovva.Application.Features.Catalog.Queries.GetActiveMeals;

public record GetActiveMealsQuery(int Page, int PageSize) : IRequest<PagedResult<MealDto>>;
