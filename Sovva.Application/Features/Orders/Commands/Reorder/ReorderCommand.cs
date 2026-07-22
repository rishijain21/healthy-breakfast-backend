using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Orders.Commands.Reorder;

public record ReorderCommand(long OrderId, int UserId) : IRequest<OrderCreationResponseDto>;
