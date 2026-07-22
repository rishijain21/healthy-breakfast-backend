using Sovva.Application.DTOs;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Wallet;

public static class WalletTransactionMapper
{
    public static WalletTransactionDto MapToDto(WalletTransaction t)
        => new WalletTransactionDto
        {
            TransactionId = t.TransactionId,
            UserId = t.UserId,
            Amount = t.Amount,
            Type = t.Type,
            Description = t.Description,
            CreatedAt = t.CreatedAt
        };
}
