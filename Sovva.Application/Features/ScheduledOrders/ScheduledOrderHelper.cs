using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders
{
    public static class ScheduledOrderHelper
    {
        public static ScheduledOrderResponseDto MapToResponseDto(ScheduledOrder order)
        {
            NutritionalSummaryDto? nutritionalSummary = null;
            if (!string.IsNullOrEmpty(order.NutritionalSummary))
            {
                try
                {
                    nutritionalSummary = JsonSerializer.Deserialize<NutritionalSummaryDto>(order.NutritionalSummary);
                }
                catch
                {
                    // Silently ignore malformed JSON
                }
            }

            return new ScheduledOrderResponseDto
            {
                ScheduledOrderId = order.ScheduledOrderId,
                MealName = order.MealName,
                MealId = order.MealId,
                MealImageUrl = order.MealImageUrl,
                ScheduledFor = order.ScheduledFor.ToDateTime(TimeOnly.MinValue),
                DeliveryTimeSlot = order.DeliveryTimeSlot,
                TotalPrice = order.TotalPrice,
                OrderStatus = order.OrderStatus.ToString(),
                CanModify = order.CanModify,
                CreatedAt = order.CreatedAt,
                ExpiresAt = order.ExpiresAt,
                NutritionalSummary = nutritionalSummary,
                Ingredients = order.Ingredients?.Select(i => new ScheduledOrderIngredientDetailDto
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.Ingredient?.IngredientName ?? string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    Category = i.Ingredient?.IngredientCategory?.CategoryName ?? string.Empty,
                    ImageUrl = i.Ingredient?.IconEmoji ?? string.Empty,
                    Calories = i.Ingredient?.Calories ?? 0,
                    Protein = i.Ingredient?.Protein ?? 0,
                    Fiber = i.Ingredient?.Fiber ?? 0
                }).ToList() ?? new List<ScheduledOrderIngredientDetailDto>(),
                SubscriptionId = order.SubscriptionId
            };
        }

        public static string? CleanMealImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var idx = url.IndexOf("meal-images/", StringComparison.OrdinalIgnoreCase);
            var clean = idx >= 0 ? url.Substring(idx) : url;
            var qIdx = clean.IndexOf('?');
            return qIdx >= 0 ? clean.Substring(0, qIdx) : clean;
        }

        public static async Task<bool> ProcessSingleScheduledOrderAsync(
            ScheduledOrder scheduledOrder,
            IReadOnlyDictionary<Guid, User> usersByAuthId,
            IReadOnlyDictionary<int, Order> existingOrders,
            IReadOnlyDictionary<int, WalletTransaction> existingTransactions,
            IReadOnlyDictionary<int, UserAddress> addressesMap,
            IScheduledOrderRepository scheduledOrderRepository,
            IWalletTransactionService walletService,
            IOrderService orderService,
            IAppTimeProvider time,
            IUnitOfWork unitOfWork,
            ILogger logger)
        {
            try
            {
                logger.LogInformation("Processing order #{Id}", scheduledOrder.ScheduledOrderId);

                if (!usersByAuthId.TryGetValue(scheduledOrder.AuthId, out var user))
                {
                    logger.LogWarning("User not found for order #{Id}", scheduledOrder.ScheduledOrderId);
                    await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                if (scheduledOrder.DeliveryAddressId == null)
                {
                    logger.LogWarning("No delivery address for order #{Id}", scheduledOrder.ScheduledOrderId);
                    await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                if (!addressesMap.TryGetValue(scheduledOrder.DeliveryAddressId.Value, out var address))
                {
                    logger.LogWarning("Address {AddressId} not found for order #{Id}", scheduledOrder.DeliveryAddressId.Value, scheduledOrder.ScheduledOrderId);
                    await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                if (address?.ServiceableLocation == null || !address.ServiceableLocation.IsActive)
                {
                    logger.LogWarning("Invalid/inactive address for order #{Id}", scheduledOrder.ScheduledOrderId);
                    await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                    return false;
                }

                logger.LogInformation("Address validated: {Area} — active: {Active}",
                    address.ServiceableLocation.Area, address.ServiceableLocation.IsActive);

                existingOrders.TryGetValue(scheduledOrder.ScheduledOrderId, out var existingOrder);

                if (existingOrder != null)
                {
                    var walletTxExists = existingTransactions.ContainsKey(scheduledOrder.ScheduledOrderId);

                    if (walletTxExists)
                    {
                        logger.LogInformation("Order #{OrderId} exists + wallet debited — marking processed", existingOrder.OrderId);
                        await scheduledOrderRepository.MarkAsProcessedAsync(scheduledOrder.ScheduledOrderId, existingOrder.OrderId, time.UtcNow);
                        return true;
                    }
                    else
                    {
                        logger.LogWarning("Order #{OrderId} exists but no wallet transaction found - completing payment now", existingOrder.OrderId);

                        var debitResult = await walletService.AtomicDebitAsync(
                            user.UserId,
                            scheduledOrder.TotalPrice,
                            $"Order #{existingOrder.OrderId} - {scheduledOrder.MealName}",
                            scheduledOrder.ScheduledOrderId);

                        if (!debitResult.Success)
                        {
                            await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, ScheduledOrderStatus.Cancelled.ToString());
                            return false;
                        }

                        await scheduledOrderRepository.MarkAsProcessedAsync(scheduledOrder.ScheduledOrderId, existingOrder.OrderId, time.UtcNow);
                        return true;
                    }
                }

                await unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var debitResult = await walletService.AtomicDebitAsync(
                        user.UserId,
                        scheduledOrder.TotalPrice,
                        $"Scheduled Order #{scheduledOrder.ScheduledOrderId} - {scheduledOrder.MealName}",
                        scheduledOrder.ScheduledOrderId);

                    if (!debitResult.Success)
                    {
                        var currentBalance = await walletService.GetUserBalanceAsync(user.UserId);
                        throw new InsufficientBalanceException(scheduledOrder.TotalPrice, currentBalance);
                    }

                    var orderId = await orderService.ConfirmScheduledOrderAsync(scheduledOrder, existingOrder);

                    await scheduledOrderRepository.MarkAsProcessedAsync(scheduledOrder.ScheduledOrderId, orderId, time.UtcNow);

                    logger.LogInformation("Confirmed Order #{OrderId} from ScheduledOrder #{Id} - {Price}", orderId, scheduledOrder.ScheduledOrderId, scheduledOrder.TotalPrice);
                });

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception confirming order #{Id}", scheduledOrder.ScheduledOrderId);

                if (ex is InsufficientBalanceException)
                {
                    await scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "Failed");
                }

                return false;
            }
        }
    }
}
