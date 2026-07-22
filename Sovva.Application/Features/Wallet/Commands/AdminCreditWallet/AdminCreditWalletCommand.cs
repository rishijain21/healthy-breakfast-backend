using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Commands.AdminCreditWallet;

public record AdminCreditWalletCommand(
    long UserId,
    decimal Amount,
    string Description,
    int AdminUserId
) : IRequest<WalletTransactionDto>;
