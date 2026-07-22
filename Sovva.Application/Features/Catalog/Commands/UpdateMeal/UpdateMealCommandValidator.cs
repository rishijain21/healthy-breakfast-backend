using FluentValidation;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMeal;

public class UpdateMealCommandValidator : AbstractValidator<UpdateMealCommand>
{
    public UpdateMealCommandValidator()
    {
        RuleFor(x => x.MealId).GreaterThan(0).WithMessage("Meal ID must be greater than zero.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Update meal data cannot be null.");
        RuleFor(x => x.Dto.MealName).NotEmpty().WithMessage("Meal name is required.");
        RuleFor(x => x.Dto.BasePrice).GreaterThanOrEqualTo(0).WithMessage("Base price cannot be negative.");
    }
}
