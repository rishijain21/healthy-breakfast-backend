using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetMealDetailForAdmin;

public record GetMealDetailForAdminQuery(int MealId) : IRequest<AdminMealDetailDto?>;
