using FluentValidation;

namespace Sovva.Application.Features.Users.Commands.CreateUser
{
    public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserCommandValidator()
        {
            RuleFor(x => x.Dto)
                .NotNull().WithMessage("CreateUserDto cannot be null.");

            When(x => x.Dto != null, () =>
            {
                RuleFor(x => x.Dto.Name)
                    .NotEmpty().WithMessage("Name is required.");

                RuleFor(x => x.Dto.Email)
                    .NotEmpty().WithMessage("Email is required.")
                    .EmailAddress().WithMessage("Valid email address is required.");

                RuleFor(x => x.Dto.Phone)
                    .NotEmpty().WithMessage("Phone is required.");
            });
        }
    }
}
