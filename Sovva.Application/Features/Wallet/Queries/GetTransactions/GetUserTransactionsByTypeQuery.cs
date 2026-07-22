using System.Collections.Generic;
using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public record GetUserTransactionsByTypeQuery(int UserId, string Type) : IRequest<IEnumerable<WalletTransactionDto>>;
