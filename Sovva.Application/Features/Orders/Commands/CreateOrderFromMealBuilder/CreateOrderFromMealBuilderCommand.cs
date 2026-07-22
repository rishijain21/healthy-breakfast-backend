using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Commands.CreateOrderFromMealBuilder;

public record CreateOrderFromMealBuilderCommand(
    CreateOrderFromMealBuilderDto Dto,
    int UserId,
    int? DeliveryAddressId = null
) : IRequest<OrderCreationResponseDto>;
