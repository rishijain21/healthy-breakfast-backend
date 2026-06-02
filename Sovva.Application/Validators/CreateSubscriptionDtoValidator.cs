using FluentValidation;
using Sovva.Application.DTOs;
using Sovva.Application.Helpers;

namespace Sovva.Application.Validators;

public class CreateSubscriptionDtoValidator : AbstractValidator<CreateSubscriptionDto>
{
    public CreateSubscriptionDtoValidator(IAppTimeProvider time)
    {
        RuleFor(x => x.MealId)
            .GreaterThan(0).WithMessage("A valid meal must be selected");


        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(time.TodayIst)
            .WithMessage("Start date cannot be in the past");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.")
            .When(x => x.EndDate != default);

        RuleFor(x => x.EndDate)
            .LessThanOrEqualTo(x => x.StartDate.AddYears(1))
            .WithMessage("Subscription duration cannot exceed 1 year.")
            .When(x => x.EndDate != default && x.StartDate != default);


        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Invalid subscription frequency");

        RuleFor(x => x.WeeklySchedule)
            .NotNull()
            .NotEmpty()
            .WithMessage("Weekly schedule is required for Weekly frequency")
            .When(x => x.Frequency == Sovva.Domain.Enums.SubscriptionFrequency.Weekly);

        RuleForEach(x => x.WeeklySchedule)
            .ChildRules(schedule => {
                schedule.RuleFor(s => s.DayOfWeek)
                    .InclusiveBetween(0, 6);
                schedule.RuleFor(s => s.Quantity)
                    .GreaterThan(0)
                    .LessThanOrEqualTo(10);
            })
            .When(x => x.WeeklySchedule != null);
    }
}
