using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public class GetUserTransactionsPagedQueryHandler : IRequestHandler<GetUserTransactionsPagedQuery, PagedResult<WalletTransactionDto>>
{
    private readonly IWalletTransactionRepository _repository;

    public GetUserTransactionsPagedQueryHandler(IWalletTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<WalletTransactionDto>> Handle(GetUserTransactionsPagedQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (transactions, totalCount) = await _repository.GetByUserIdAsync(request.UserId, page, pageSize);

        return new PagedResult<WalletTransactionDto>
        {
            Items = transactions.Select(WalletTransactionMapper.MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
