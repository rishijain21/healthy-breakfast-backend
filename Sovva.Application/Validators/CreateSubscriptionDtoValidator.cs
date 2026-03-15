using FluentValidation;
using Sovva.Application.DTOs;

namespace Sovva.Application.Validators;

public class CreateSubscriptionDtoValidator : AbstractValidator<CreateSubscriptionDto>
{
    public CreateSubscriptionDtoValidator()
    {
        RuleFor(x => x.MealId)
            .GreaterThan(0).WithMessage("A valid meal must be selected");

        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Start date cannot be in the past");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Invalid subscription frequency");
    }
}
