using System.Linq;
using FluentValidation;
using Sovva.Application.DTOs;

namespace Sovva.Application.Validators
{
    public class CreateOrderFromMealBuilderDtoValidator : AbstractValidator<CreateOrderFromMealBuilderDto>
    {
        public CreateOrderFromMealBuilderDtoValidator()
        {
            RuleFor(x => x.MealId)
                .GreaterThan(0).WithMessage("Meal ID is required.");

            RuleFor(x => x.SelectedIngredients)
                .NotNull().WithMessage("At least one ingredient must be selected")
                .NotEmpty().WithMessage("At least one ingredient must be selected")
                .Must(list => list == null || 
                      list.Select(i => i.IngredientId).Distinct().Count() 
                      == list.Count)
                .WithMessage("Duplicate ingredients are not allowed in a single order");

            RuleForEach(x => x.SelectedIngredients).ChildRules(ingredient =>
            {
                ingredient.RuleFor(i => i.IngredientId)
                    .GreaterThan(0).WithMessage("Ingredient ID is required.");

                ingredient.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Ingredient quantity must be greater than zero.")
                    .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100 per ingredient");
            });

            RuleFor(x => x.MealName)
                .MaximumLength(200)
                .When(x => x.MealName != null);

            RuleFor(x => x.SpecialInstructions)
                .MaximumLength(500)
                .When(x => x.SpecialInstructions != null);

            RuleFor(x => x.DeliveryAddress)
                .MaximumLength(500)
                .When(x => x.DeliveryAddress != null);
        }
    }
}
