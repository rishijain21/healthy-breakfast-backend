using MediatR;

namespace Sovva.Application.Features.Orders.Commands.RateOrder;

public record RateOrderCommand(long OrderId, int UserId, int Rating, string? Review) : IRequest<bool>;
