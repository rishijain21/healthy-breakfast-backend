using FluentValidation;
using Sovva.Application.DTOs;

namespace Sovva.Application.Validators;

public class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        // AuthId and Email are populated server-side from JWT — not validated here
    }
}
