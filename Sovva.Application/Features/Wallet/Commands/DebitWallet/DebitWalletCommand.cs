using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Commands.DebitWallet;

public record DebitWalletCommand(
    int UserId,
    decimal Amount,
    string Description
) : IRequest<WalletTransactionDto>;
