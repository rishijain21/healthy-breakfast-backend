using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;
using Sovva.Domain.Constants;

namespace Sovva.Application.Features.Wallet.Commands.DebitWallet;

public class DebitWalletCommandHandler : IRequestHandler<DebitWalletCommand, WalletTransactionDto>
{
    private readonly ISender _sender;

    public DebitWalletCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<WalletTransactionDto> Handle(DebitWalletCommand request, CancellationToken cancellationToken)
    {
        return await _sender.Send(new CreateWalletTransactionCommand(new CreateWalletTransactionDto
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Type = WalletConstants.Debit,
            Description = request.Description
        }), cancellationToken);
    }
}
