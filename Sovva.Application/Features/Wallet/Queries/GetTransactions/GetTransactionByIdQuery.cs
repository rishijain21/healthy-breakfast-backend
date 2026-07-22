using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public record GetTransactionByIdQuery(long TransactionId) : IRequest<WalletTransactionDto?>;
