using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public class GetUserTransactionsByTypeQueryHandler : IRequestHandler<GetUserTransactionsByTypeQuery, IEnumerable<WalletTransactionDto>>
{
    private readonly IWalletTransactionRepository _repository;

    public GetUserTransactionsByTypeQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<WalletTransactionDto>> Handle(GetUserTransactionsByTypeQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetByUserIdAndTypeAsync(request.UserId, request.Type);
        return transactions.Select(t => WalletTransactionMapper.MapToDto(t!));
    }
}
