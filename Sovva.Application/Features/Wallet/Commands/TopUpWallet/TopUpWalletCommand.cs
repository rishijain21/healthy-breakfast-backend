using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Commands.TopUpWallet;

public record TopUpWalletCommand(
    int UserId,
    decimal Amount,
    string Description = "Wallet top-up"
) : IRequest<UserDto>;

public record TopUpWalletByDtoCommand(
    int UserId,
    WalletTopUpDto TopUpDto
) : IRequest<WalletTransactionDto>;
