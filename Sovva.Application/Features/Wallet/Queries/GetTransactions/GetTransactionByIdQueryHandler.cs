using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, WalletTransactionDto?>
{
    private readonly IWalletTransactionRepository _repository;

    public GetTransactionByIdQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<WalletTransactionDto?> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.TransactionId);
        if (transaction == null) return null;
        return WalletTransactionMapper.MapToDto(transaction);
    }
}
