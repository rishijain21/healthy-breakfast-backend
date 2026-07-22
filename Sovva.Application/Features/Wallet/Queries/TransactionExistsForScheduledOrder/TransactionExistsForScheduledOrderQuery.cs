using MediatR;

namespace Sovva.Application.Features.Wallet.Queries.TransactionExistsForScheduledOrder;

public record TransactionExistsForScheduledOrderQuery(int ScheduledOrderId) : IRequest<bool>;
