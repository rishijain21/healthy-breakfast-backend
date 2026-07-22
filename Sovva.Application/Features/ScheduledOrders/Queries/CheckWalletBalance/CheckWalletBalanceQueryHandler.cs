using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.ScheduledOrders.Queries.CheckWalletBalance
{
    public class CheckWalletBalanceQueryHandler : IRequestHandler<CheckWalletBalanceQuery, bool>
    {
        private readonly IWalletTransactionService _walletService;

        public CheckWalletBalanceQueryHandler(IWalletTransactionService walletService)
        {
            _walletService = walletService;
        }

        public async Task<bool> Handle(CheckWalletBalanceQuery request, CancellationToken cancellationToken)
        {
            return await _walletService.HasSufficientBalanceAsync(request.UserId, request.Amount);
        }
    }
}
