using FluentValidation;
using Sovva.Application.DTOs;

namespace Sovva.Application.Validators
{
    public class WalletTopUpDtoValidator : AbstractValidator<WalletTopUpDto>
    {
        public WalletTopUpDtoValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than 0")
                .LessThanOrEqualTo(10000).WithMessage("Amount cannot exceed 10,000");

            RuleFor(x => x.Description)
                .MaximumLength(200).WithMessage("Description cannot exceed 200 characters");
        }
    }
}
