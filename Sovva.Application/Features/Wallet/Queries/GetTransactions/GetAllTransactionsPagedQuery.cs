using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;

namespace Sovva.Application.Features.Wallet.Queries.GetTransactions;

public record GetAllTransactionsPagedQuery(int Page, int PageSize) : IRequest<PagedResult<WalletTransactionDto>>;
