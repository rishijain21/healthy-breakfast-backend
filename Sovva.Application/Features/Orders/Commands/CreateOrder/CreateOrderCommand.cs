using System;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Commands.CreateOrder;

[Obsolete("Do not use. Relies on client-trusted TotalPrice. Use ConfirmScheduledOrderAsync or MealBuilder paths.")]
public record CreateOrderCommand(CreateOrderDto Dto, int UserId) : IRequest<long>;
