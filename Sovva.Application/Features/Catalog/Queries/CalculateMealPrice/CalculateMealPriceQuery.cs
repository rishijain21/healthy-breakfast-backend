using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Catalog.Queries.CalculateMealPrice;

public record CalculateMealPriceQuery(MealPriceCalculationDto CalculationDto) : IRequest<MealPriceResponseDto>;
