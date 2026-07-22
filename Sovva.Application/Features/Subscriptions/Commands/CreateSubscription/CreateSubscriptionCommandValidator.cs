using FluentValidation;

namespace Sovva.Application.Features.Subscriptions.Commands.CreateSubscription
{
    public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
    {
        public CreateSubscriptionCommandValidator()
        {
            RuleFor(v => v.Dto).NotNull();

            When(v => v.Dto != null, () =>
            {
                RuleFor(v => v.Dto.UserId).GreaterThan(0).WithMessage("Valid UserId is required.");
                
                RuleFor(v => v.Dto)
                    .Must(d => d.StartDate < d.EndDate)
                    .WithMessage("Start date must be before end date");

                RuleFor(v => v.Dto)
                    .Must(d => (d.MealId.HasValue && !d.UserMealId.HasValue) || (!d.MealId.HasValue && d.UserMealId.HasValue))
                    .WithMessage("Either MealId or UserMealId must be provided, but not both.");
            });
        }
    }
}
