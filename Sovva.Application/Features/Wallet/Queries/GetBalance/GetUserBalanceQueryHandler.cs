using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetBalance;

public class GetUserBalanceQueryHandler : IRequestHandler<GetUserBalanceQuery, decimal>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly ICacheService _cacheService;

    public GetUserBalanceQueryHandler(
        IWalletTransactionRepository walletTransactionRepository,
        ICacheService cacheService)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _cacheService = cacheService;
    }

    public async Task<decimal> Handle(GetUserBalanceQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.WalletBalance(request.UserId);
        var cached = await _cacheService.GetAsync<decimal?>(cacheKey);
        if (cached.HasValue) return cached.Value;

        var balance = await _walletTransactionRepository.GetUserBalanceAsync(request.UserId);
        await _cacheService.SetAsync(cacheKey, balance, TimeSpan.FromSeconds(30));
        return balance;
    }
}
