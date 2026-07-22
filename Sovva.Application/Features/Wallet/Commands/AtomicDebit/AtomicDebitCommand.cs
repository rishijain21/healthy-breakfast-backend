using MediatR;

namespace Sovva.Application.Features.Wallet.Commands.AtomicDebit;

public record AtomicDebitCommand(
    int UserId,
    decimal Amount,
    string Description,
    int? ScheduledOrderId = null
) : IRequest<(bool Success, long? TransactionId)>;
