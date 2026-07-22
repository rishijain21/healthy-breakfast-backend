using FluentValidation;

namespace Sovva.Application.Features.Catalog.Commands.CreateMeal;

public class CreateMealCommandValidator : AbstractValidator<CreateMealCommand>
{
    public CreateMealCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Meal creation data cannot be null.");
        RuleFor(x => x.Dto.MealName).NotEmpty().WithMessage("Meal name is required.");
        RuleFor(x => x.Dto.BasePrice).GreaterThanOrEqualTo(0).WithMessage("Base price cannot be negative.");
    }
}
