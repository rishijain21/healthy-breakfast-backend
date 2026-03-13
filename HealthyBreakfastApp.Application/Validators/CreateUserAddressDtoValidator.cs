using FluentValidation;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Validators
{
    public class CreateUserAddressDtoValidator : AbstractValidator<CreateUserAddressDto>
    {
        public CreateUserAddressDtoValidator()
        {
            RuleFor(x => x.ServiceableLocationId)
                .GreaterThan(0).WithMessage("Serviceable location is required");

            RuleFor(x => x.FlatNumber)
                .NotEmpty().WithMessage("Flat number is required")
                .MaximumLength(50).WithMessage("Flat number cannot exceed 50 characters");

            RuleFor(x => x.Wing)
                .MaximumLength(50).WithMessage("Wing cannot exceed 50 characters");

            RuleFor(x => x.Block)
                .MaximumLength(50).WithMessage("Block cannot exceed 50 characters");

            RuleFor(x => x.Floor)
                .MaximumLength(20).WithMessage("Floor cannot exceed 20 characters");

            RuleFor(x => x.AdditionalInstructions)
                .MaximumLength(500).WithMessage("Additional instructions cannot exceed 500 characters");

            RuleFor(x => x.Label)
                .MaximumLength(50).WithMessage("Label cannot exceed 50 characters");
        }
    }
}
