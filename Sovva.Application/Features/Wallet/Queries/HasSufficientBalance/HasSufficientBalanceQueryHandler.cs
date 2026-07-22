using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.HasSufficientBalance;

public class HasSufficientBalanceQueryHandler : IRequestHandler<HasSufficientBalanceQuery, bool>
{
    private readonly IWalletTransactionRepository _repository;

    public HasSufficientBalanceQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(HasSufficientBalanceQuery request, CancellationToken cancellationToken)
    {
        return await _repository.HasSufficientBalanceAsync(request.UserId, request.Amount);
    }
}
