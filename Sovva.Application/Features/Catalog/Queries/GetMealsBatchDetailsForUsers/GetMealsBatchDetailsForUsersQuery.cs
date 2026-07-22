using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetailsForUsers;

public record GetMealsBatchDetailsForUsersQuery(List<int> MealIds) : IRequest<List<MealWithDetailsDto>>;
