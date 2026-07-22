using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.TransactionExistsForScheduledOrder;

public class TransactionExistsForScheduledOrderQueryHandler : IRequestHandler<TransactionExistsForScheduledOrderQuery, bool>
{
    private readonly IWalletTransactionRepository _repository;

    public TransactionExistsForScheduledOrderQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(TransactionExistsForScheduledOrderQuery request, CancellationToken cancellationToken)
    {
        return await _repository.ExistsForScheduledOrderAsync(request.ScheduledOrderId);
    }
}
