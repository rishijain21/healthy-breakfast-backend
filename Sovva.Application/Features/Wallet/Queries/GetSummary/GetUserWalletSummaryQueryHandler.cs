using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Wallet.Queries.GetBalance;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Wallet.Queries.GetSummary;

public class GetUserWalletSummaryQueryHandler : IRequestHandler<GetUserWalletSummaryQuery, UserWalletSummaryDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly ISender _sender;

    public GetUserWalletSummaryQueryHandler(
        IUserRepository userRepository,
        IWalletTransactionRepository walletTransactionRepository,
        ISender sender)
    {
        _userRepository = userRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _sender = sender;
    }

    public async Task<UserWalletSummaryDto?> Handle(GetUserWalletSummaryQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) return null;

        var summary = await _walletTransactionRepository.GetUserWalletSummaryAsync(request.UserId);
        var balance = await _sender.Send(new GetUserBalanceQuery(request.UserId), cancellationToken);

        return new UserWalletSummaryDto
        {
            UserId = request.UserId,
            UserName = user.Name,
            UserEmail = user.Email,
            CurrentBalance = balance,
            TotalCredits = summary.totalCredits,
            TotalDebits = summary.totalDebits,
            TransactionCount = summary.transactionCount,
            LastTransactionDate = summary.lastTransactionDate ?? DateTime.MinValue
        };
    }
}
