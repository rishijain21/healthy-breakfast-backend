using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdmin;

public record GetAllMealsForAdminQuery : IRequest<List<AdminMealListDto>>;
