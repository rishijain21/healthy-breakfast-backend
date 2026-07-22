using FluentValidation;

namespace Sovva.Application.Features.Orders.Commands.RateOrder;

public class RateOrderCommandValidator : AbstractValidator<RateOrderCommand>
{
    public RateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("Order ID is required.");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID is required.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");
    }
}
