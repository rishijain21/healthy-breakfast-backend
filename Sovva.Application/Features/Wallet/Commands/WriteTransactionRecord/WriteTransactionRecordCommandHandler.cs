using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Wallet.Commands.WriteTransactionRecord;

public class WriteTransactionRecordCommandHandler : IRequestHandler<WriteTransactionRecordCommand>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;

    public WriteTransactionRecordCommandHandler(IWalletTransactionRepository walletTransactionRepository)
    {
        _walletTransactionRepository = walletTransactionRepository;
    }

    public async Task Handle(WriteTransactionRecordCommand request, CancellationToken cancellationToken)
    {
        var transaction = new WalletTransaction
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Type = request.Type,
            Description = request.Description,
            ScheduledOrderId = request.ScheduledOrderId
        };

        // Use WriteRecordOnlyAsync — balance already deducted atomically upstream
        await _walletTransactionRepository.WriteRecordOnlyAsync(transaction);
    }
}
