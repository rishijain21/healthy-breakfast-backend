using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public record GetAllTransactionsQuery : IRequest<IEnumerable<WalletTransactionDto>>;
