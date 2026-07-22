using MediatR;

namespace Sovva.Application.Features.Wallet.Commands.WriteTransactionRecord;

public record WriteTransactionRecordCommand(
    int UserId,
    decimal Amount,
    string Type,
    string Description,
    int? ScheduledOrderId = null
) : IRequest;
