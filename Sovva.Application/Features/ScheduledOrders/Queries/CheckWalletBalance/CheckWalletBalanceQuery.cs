using MediatR;

namespace Sovva.Application.Features.ScheduledOrders.Queries.CheckWalletBalance
{
    public record CheckWalletBalanceQuery(
        int UserId,
        decimal Amount
    ) : IRequest<bool>;
}
