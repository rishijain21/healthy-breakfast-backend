using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;

public record CreateWalletTransactionCommand(CreateWalletTransactionDto Dto) : IRequest<WalletTransactionDto>;
