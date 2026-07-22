using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetails;

public record GetMealsBatchDetailsQuery(List<int> MealIds) : IRequest<List<AdminMealDetailDto>>;
