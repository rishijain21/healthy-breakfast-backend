using FluentValidation;
using HealthyBreakfastApp.Application.DTOs;

namespace HealthyBreakfastApp.Application.Validators
{
    public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
    {
        public CreateOrderDtoValidator()
        {
            RuleFor(x => x.OrderStatus)
                .NotEmpty().WithMessage("Order status is required")
                .MaximumLength(50).WithMessage("Order status cannot exceed 50 characters");

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0).WithMessage("Total price must be greater than 0")
                .LessThanOrEqualTo(100000).WithMessage("Total price cannot exceed 100,000");
        }
    }
}
