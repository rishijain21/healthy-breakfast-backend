using MediatR;
using Sovva.Application.DTOs;

namespace Sovva.Application.Features.Wallet.Queries.GetSummary;

public record GetUserWalletSummaryQuery(int UserId) : IRequest<UserWalletSummaryDto?>;
