using MediatR;

namespace Sovva.Application.Features.Wallet.Queries.GetBalance;

public record GetUserBalanceQuery(int UserId) : IRequest<decimal>;
