using FluentValidation;

namespace Sovva.Application.Features.Orders.Commands.CreateOrderFromMealBuilder;

public class CreateOrderFromMealBuilderCommandValidator : AbstractValidator<CreateOrderFromMealBuilderCommand>
{
    public CreateOrderFromMealBuilderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("Valid user ID is required.");

        RuleFor(x => x.Dto)
            .NotNull().WithMessage("Order details cannot be null.");

        RuleFor(x => x.Dto.SelectedIngredients)
            .NotEmpty().When(x => x.Dto != null)
            .WithMessage("At least one ingredient must be selected.");
    }
}
