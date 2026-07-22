using FluentValidation;

namespace Sovva.Application.Features.Orders.Commands.Reorder;

public class ReorderCommandValidator : AbstractValidator<ReorderCommand>
{
    public ReorderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("Order ID is required.");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID is required.");
    }
}
