using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.Orders;

public static class OrdersHelper
{
    public static IEnumerable<EnhancedOrderHistoryDto> MapToEnhancedDto(IEnumerable<Order> orders)
    {
        return orders.Select(order =>
        {
            var hasUserMeal = order.UserMeal?.UserMealIngredients?.Any() == true;
            var hasScheduledOrder = order.SourceScheduledOrder?.Ingredients?.Any() == true;

            return new EnhancedOrderHistoryDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                ScheduledFor = order.ScheduledFor,

                MealId = order.UserMeal?.MealId ?? order.SourceScheduledOrder?.MealId ?? 0,
                MealName = order.UserMeal?.MealName
                        ?? order.SourceScheduledOrder?.MealName
                        ?? "Order",
                MealImageUrl = order.UserMeal?.Meal?.ImageUrl ?? order.SourceScheduledOrder?.MealImageUrl,

                NutritionalInfo = new NutritionalInfoDto
                {
                    TotalCalories = hasUserMeal
                        ? order.UserMeal!.UserMealIngredients.Sum(i => i.Ingredient.Calories * i.Quantity)
                        : 0,
                    TotalProtein = hasUserMeal
                        ? order.UserMeal!.UserMealIngredients.Sum(i => i.Ingredient.Protein * i.Quantity)
                        : 0,
                    TotalFiber = hasUserMeal
                        ? order.UserMeal!.UserMealIngredients.Sum(i => i.Ingredient.Fiber * i.Quantity)
                        : 0
                },

                Ingredients = hasUserMeal
                    ? order.UserMeal!.UserMealIngredients.Select(umi => new OrderIngredientDetailDto
                    {
                        IngredientId = umi.IngredientId,
                        IngredientName = umi.Ingredient.IngredientName,
                        Quantity = umi.Quantity,
                        UnitPrice = umi.Ingredient.Price,
                        TotalPrice = umi.Ingredient.Price * umi.Quantity,
                        IconEmoji = umi.Ingredient.IconEmoji,
                        Calories = umi.Ingredient.Calories,
                        Protein = umi.Ingredient.Protein,
                        Fiber = umi.Ingredient.Fiber
                    }).ToList()
                    : hasScheduledOrder
                        ? order.SourceScheduledOrder!.Ingredients.Select(i => new OrderIngredientDetailDto
                        {
                            IngredientId = i.IngredientId,
                            IngredientName = i.Ingredient?.IngredientName ?? "Ingredient",
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            TotalPrice = i.TotalPrice,
                            IconEmoji = i.Ingredient?.IconEmoji ?? "🥣",
                            Calories = i.Ingredient?.Calories ?? 0,
                            Protein = i.Ingredient?.Protein ?? 0,
                            Fiber = i.Ingredient?.Fiber ?? 0
                        }).ToList()
                        : new List<OrderIngredientDetailDto>
                        {
                            new OrderIngredientDetailDto
                            {
                                IngredientId = 0,
                                IngredientName = "Order Items",
                                Quantity = 1,
                                UnitPrice = order.TotalPrice,
                                TotalPrice = order.TotalPrice,
                                IconEmoji = "🥣"
                            }
                        }
            };
        });
    }

    public static async Task<OrderCreationResponseDto> ExecuteOrderCreationAsync(
        int userId,
        int mealId,
        List<SelectedIngredientDto> ingredients,
        int deliveryAddressId,
        decimal? overrideTotalPrice,
        DateTime? scheduledFor,
        string? mealName,
        IMealService mealService,
        IWalletTransactionService walletService,
        IUserMealService userMealService,
        IUserMealIngredientService userMealIngredientService,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IAppTimeProvider time)
    {
        if (ingredients == null || !ingredients.Any())
            throw new ArgumentException("At least one ingredient must be selected to place an order.");

        MealPriceResponseDto priceCalculation;
        if (overrideTotalPrice.HasValue)
        {
            priceCalculation = new MealPriceResponseDto
            {
                MealName = mealName ?? "Scheduled Order",
                TotalPrice = overrideTotalPrice.Value,
                IngredientBreakdown = ingredients.Select(i => new IngredientBreakdownDto
                {
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice ?? 0,
                    TotalPrice = i.TotalPrice ?? 0
                }).ToList()
            };
        }
        else
        {
            priceCalculation = await mealService.CalculateMealPriceAsync(new MealPriceCalculationDto
            {
                MealId = mealId,
                SelectedIngredients = ingredients
            });
        }

        var walletBalanceBefore = await walletService.GetUserBalanceAsync(userId);
        var hasSufficientBalance = await walletService.HasSufficientBalanceAsync(userId, priceCalculation.TotalPrice);

        if (!hasSufficientBalance)
        {
            throw new InsufficientBalanceException(priceCalculation.TotalPrice, walletBalanceBefore);
        }

        OrderCreationResponseDto response = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var userMealDto = new CreateUserMealDto
            {
                MealId = mealId,
                MealName = priceCalculation.MealName,
                TotalPrice = priceCalculation.TotalPrice,
                CreatedAt = time.UtcNow
            };

            var createdUserMealId = await userMealService.CreateUserMealAsync(userMealDto, userId);

            var ingredientDtos = new List<CreateUserMealIngredientDto>();

            if (overrideTotalPrice.HasValue)
            {
                foreach (var selectedIngredient in ingredients)
                {
                    var ingredientDetail = priceCalculation.IngredientBreakdown
                        .FirstOrDefault(i => i.IngredientId == selectedIngredient.IngredientId);

                    if (ingredientDetail != null)
                    {
                        ingredientDtos.Add(new CreateUserMealIngredientDto
                        {
                            UserMealId = createdUserMealId,
                            IngredientId = selectedIngredient.IngredientId,
                            Quantity = selectedIngredient.Quantity,
                            UnitPrice = ingredientDetail.UnitPrice,
                            TotalPrice = ingredientDetail.TotalPrice
                        });
                    }
                }
            }
            else
            {
                foreach (var selectedIngredient in ingredients)
                {
                    ingredientDtos.Add(new CreateUserMealIngredientDto
                    {
                        UserMealId = createdUserMealId,
                        IngredientId = selectedIngredient.IngredientId,
                        Quantity = selectedIngredient.Quantity
                    });
                }
            }

            if (ingredientDtos.Any())
            {
                await userMealIngredientService.CreateUserMealIngredientsAsync(ingredientDtos);
            }

            var order = new Order
            {
                UserId = userId,
                UserMealId = createdUserMealId,
                DeliveryAddressId = deliveryAddressId,
                OrderStatus = OrderStatus.Pending,
                TotalPrice = priceCalculation.TotalPrice,
                OrderDate = time.UtcNow,
                ScheduledFor = scheduledFor ?? time.UtcNow.AddHours(2),
                CreatedAt = time.UtcNow,
                UpdatedAt = time.UtcNow
            };

            await orderRepository.AddAsync(order);
            await unitOfWork.SaveChangesAsync();

            var walletTransaction = await walletService.DebitWalletAsync(
                userId,
                priceCalculation.TotalPrice,
                $"Order #{order.OrderId} - {priceCalculation.MealName}"
            );

            order.TransitionTo(OrderStatus.Confirmed);
            order.UpdatedAt = time.UtcNow;
            orderRepository.Update(order);
            await unitOfWork.SaveChangesAsync();

            var walletBalanceAfter = await walletService.GetUserBalanceAsync(userId);

            response = new OrderCreationResponseDto
            {
                OrderId = order.OrderId,
                UserMealId = createdUserMealId,
                MealName = priceCalculation.MealName,
                TotalPrice = priceCalculation.TotalPrice,
                WalletBalanceBefore = walletBalanceBefore,
                WalletBalanceAfter = walletBalanceAfter,
                OrderStatus = order.OrderStatus.ToString(),
                TransactionId = walletTransaction.TransactionId,
                OrderDate = order.OrderDate,
                ScheduledFor = order.ScheduledFor,
                IngredientBreakdown = priceCalculation.IngredientBreakdown
            };
        });

        return response;
    }
}
