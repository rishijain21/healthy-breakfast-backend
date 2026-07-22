using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public class GetAllTransactionsQueryHandler : IRequestHandler<GetAllTransactionsQuery, IEnumerable<WalletTransactionDto>>
{
    private readonly IWalletTransactionRepository _repository;

    public GetAllTransactionsQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<WalletTransactionDto>> Handle(GetAllTransactionsQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetAllAsync();
        return transactions.Select(t => WalletTransactionMapper.MapToDto(t!));
    }
}
