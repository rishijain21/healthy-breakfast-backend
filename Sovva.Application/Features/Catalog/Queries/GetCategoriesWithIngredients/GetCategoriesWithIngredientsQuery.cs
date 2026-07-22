using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.GetCategoriesWithIngredients;

public record GetCategoriesWithIngredientsQuery : IRequest<List<CategoryWithIngredientsDto>>;
