using FluentValidation;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Validators
{
    public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserDtoValidator()
        {
            RuleFor(u => u.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(u => u.Email).NotEmpty().EmailAddress().WithMessage("Valid email is required.");
            RuleFor(u => u.Phone).NotEmpty().WithMessage("Phone is required.");
        }
    }
}
