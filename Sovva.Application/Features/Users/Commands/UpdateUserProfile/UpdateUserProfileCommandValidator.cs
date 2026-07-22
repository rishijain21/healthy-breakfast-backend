using FluentValidation;

namespace Sovva.Application.Features.Users.Commands.UpdateUserProfile
{
    public class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
    {
        public UpdateUserProfileCommandValidator()
        {
            RuleFor(x => x.AuthId)
                .NotEmpty().WithMessage("Valid AuthId is required.");

            RuleFor(x => x.Dto)
                .NotNull().WithMessage("UpdateUserProfileDto cannot be null.");

            When(x => x.Dto != null && x.Dto.Name != null, () =>
            {
                RuleFor(x => x.Dto.Name)
                    .NotEmpty().WithMessage("Name cannot be empty when provided.");
            });
        }
    }
}
