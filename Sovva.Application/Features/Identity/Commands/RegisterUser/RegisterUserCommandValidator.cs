using FluentValidation;

namespace Sovva.Application.Features.Identity.Commands.RegisterUser
{
    public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.Request)
                .NotNull().WithMessage("Registration request cannot be null.");

            When(x => x.Request != null, () =>
            {
                RuleFor(x => x.Request.AuthId)
                    .NotEmpty().WithMessage("Valid AuthId is required.");

                RuleFor(x => x.Request.Email)
                    .NotEmpty().WithMessage("Email is required.")
                    .EmailAddress().WithMessage("Valid email address is required.");

                RuleFor(x => x.Request.Name)
                    .NotEmpty().WithMessage("Name is required.");
            });
        }
    }
}
