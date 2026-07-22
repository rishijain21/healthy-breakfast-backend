using FluentValidation;

namespace Sovva.Application.Features.ScheduledOrders.Commands.CreateScheduledOrder
{
    public class CreateScheduledOrderCommandValidator : AbstractValidator<CreateScheduledOrderCommand>
    {
        public CreateScheduledOrderCommandValidator()
        {
            RuleFor(x => x.UserId)
                .GreaterThan(0).WithMessage("Valid UserId is required.");

            RuleFor(x => x.AuthId)
                .NotEmpty().WithMessage("Valid AuthId is required.");

            RuleFor(x => x.Dto)
                .NotNull().WithMessage("CreateScheduledOrderDto cannot be null.");

            When(x => x.Dto != null, () =>
            {
                RuleFor(x => x.Dto.SelectedIngredients)
                    .NotEmpty().WithMessage("At least one ingredient must be selected.");

                RuleForEach(x => x.Dto.SelectedIngredients).ChildRules(item =>
                {
                    item.RuleFor(i => i.IngredientId)
                        .GreaterThan(0).WithMessage("IngredientId must be greater than 0.");
                    item.RuleFor(i => i.Quantity)
                        .GreaterThan(0).WithMessage("Quantity must be at least 1.");
                });
            });
        }
    }
}
