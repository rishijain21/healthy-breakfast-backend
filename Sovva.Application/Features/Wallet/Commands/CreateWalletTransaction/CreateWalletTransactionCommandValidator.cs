using FluentValidation;
using Sovva.Domain.Constants;

namespace Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;

public class CreateWalletTransactionCommandValidator : AbstractValidator<CreateWalletTransactionCommand>
{
    public CreateWalletTransactionCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Transaction DTO cannot be null.");
        RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("User ID must be greater than zero.");
        RuleFor(x => x.Dto.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Dto.Type)
            .Must(t => t == WalletConstants.Credit || t == WalletConstants.Debit)
            .WithMessage("Transaction type must be 'Credit' or 'Debit'.");
    }
}
