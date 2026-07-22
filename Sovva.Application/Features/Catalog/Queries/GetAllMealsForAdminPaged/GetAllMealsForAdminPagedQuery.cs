using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdminPaged;

public record GetAllMealsForAdminPagedQuery(int Page, int PageSize) : IRequest<PagedResult<AdminMealListDto>>;
