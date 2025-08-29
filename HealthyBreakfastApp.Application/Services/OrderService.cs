using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;

namespace HealthyBreakfastApp.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMealService _mealService;
        private readonly IWalletTransactionService _walletService;

        public OrderService(
            IOrderRepository orderRepository,
            IMealService mealService,
            IWalletTransactionService walletService)
        {
            _orderRepository = orderRepository;
            _mealService = mealService;
            _walletService = walletService;
        }

        // Existing methods
        public async Task<int> CreateOrderAsync(CreateOrderDto dto)
        {
            var entity = new Order
            {
                UserId = dto.UserId,
                OrderStatus = dto.OrderStatus,
                TotalPrice = dto.TotalPrice,
                OrderDate = DateTime.UtcNow,
                ScheduledFor = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _orderRepository.AddAsync(entity);
            await _orderRepository.SaveChangesAsync();

            return entity.OrderId;
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            var entity = await _orderRepository.GetByIdAsync(id);
            if (entity == null) return null;

            return new OrderDto
            {
                OrderId = entity.OrderId,
                UserId = entity.UserId,
                OrderStatus = entity.OrderStatus,
                TotalPrice = entity.TotalPrice,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        // NEW: Simplified meal-to-order method
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto)
        {
            // Step 1: Calculate meal price
            var priceCalculation = await _mealService.CalculateMealPriceAsync(new MealPriceCalculationDto
            {
                MealId = dto.MealId,
                SelectedIngredients = dto.SelectedIngredients
            });

            // Step 2: Check wallet balance
            var walletBalanceBefore = await _walletService.GetUserBalanceAsync(dto.UserId);
            var hasSufficientBalance = await _walletService.HasSufficientBalanceAsync(dto.UserId, priceCalculation.TotalPrice);
            
            if (!hasSufficientBalance)
            {
                throw new InvalidOperationException($"Insufficient wallet balance. Required: ₹{priceCalculation.TotalPrice}, Available: ₹{walletBalanceBefore}");
            }

            // Step 3: Create the order
            var order = new Order
            {
                UserId = dto.UserId,
                OrderStatus = "Pending",
                TotalPrice = priceCalculation.TotalPrice,
                OrderDate = DateTime.UtcNow,
                ScheduledFor = dto.ScheduledFor ?? DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();

            // Step 4: Debit wallet
            var walletTransaction = await _walletService.DebitWalletAsync(
                dto.UserId,
                priceCalculation.TotalPrice,
                $"Order #{order.OrderId} - {priceCalculation.MealName}"
            );

            // Step 5: Update order status to Confirmed
            order.OrderStatus = "Confirmed";
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            var walletBalanceAfter = await _walletService.GetUserBalanceAsync(dto.UserId);

            return new OrderCreationResponseDto
            {
                OrderId = order.OrderId,
                UserMealId = 0, // Simplified for now
                MealName = priceCalculation.MealName,
                TotalPrice = priceCalculation.TotalPrice,
                WalletBalanceBefore = walletBalanceBefore,
                WalletBalanceAfter = walletBalanceAfter,
                OrderStatus = order.OrderStatus,
                TransactionId = walletTransaction.TransactionId,
                OrderDate = order.OrderDate,
                ScheduledFor = order.ScheduledFor,
                IngredientBreakdown = priceCalculation.IngredientBreakdown
            };
        }
    }
}
