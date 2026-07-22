using MediatR;

namespace Sovva.Application.Features.Wallet.Queries.HasSufficientBalance;

public record HasSufficientBalanceQuery(int UserId, decimal Amount) : IRequest<bool>;
