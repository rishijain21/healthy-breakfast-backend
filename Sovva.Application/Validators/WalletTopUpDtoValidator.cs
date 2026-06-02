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
                .LessThanOrEqualTo(10000).WithMessage("Amount cannot exceed 10,000")
                .PrecisionScale(12, 2, false).WithMessage("Amount must have at most 2 decimal places");

            RuleFor(x => x.Description)
                .Must(d => string.IsNullOrEmpty(d) || d.Trim().Length > 0)
                .WithMessage("Description cannot be whitespace only.")
                .When(x => x.Description != null);

            RuleFor(x => x.Description)
                .MaximumLength(300).WithMessage("Description cannot exceed 300 characters")
                .When(x => x.Description != null);
        }
    }
}
