using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders.Commands.DuplicateScheduledOrder
{
    public class DuplicateScheduledOrderCommandHandler : IRequestHandler<DuplicateScheduledOrderCommand, ScheduledOrderResponseDto>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<DuplicateScheduledOrderCommandHandler> _logger;

        public DuplicateScheduledOrderCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IWalletTransactionService walletService,
            IUserAddressRepository userAddressRepository,
            IIngredientRepository ingredientRepository,
            IAppTimeProvider time,
            ILogger<DuplicateScheduledOrderCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _walletService = walletService;
            _userAddressRepository = userAddressRepository;
            _ingredientRepository = ingredientRepository;
            _time = time;
            _logger = logger;
        }

        public async Task<ScheduledOrderResponseDto> Handle(DuplicateScheduledOrderCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            var authId = request.AuthId;
            var scheduledOrderId = request.ScheduledOrderId;

            try
            {
                _logger.LogInformation("Duplicating order {OrderId} for user {UserId}", scheduledOrderId, userId);

                var originalOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
                if (originalOrder == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for duplication", scheduledOrderId);
                    throw new ScheduledOrderNotFoundException(scheduledOrderId);
                }

                _logger.LogInformation("Found original order {OrderId}: {MealName}", scheduledOrderId, originalOrder.MealName);

                if (originalOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                {
                    _logger.LogWarning("Cannot duplicate order {OrderId} with status {OrderStatus}", scheduledOrderId, originalOrder.OrderStatus);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Cannot duplicate order with status '{originalOrder.OrderStatus}'");
                }

                if (!await _walletService.HasSufficientBalanceAsync(userId, originalOrder.TotalPrice))
                {
                    _logger.LogWarning("Insufficient balance for duplication of order {OrderId}", scheduledOrderId);
                    var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                    throw new InsufficientBalanceException(originalOrder.TotalPrice, currentBalance);
                }

                var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
                if (primaryAddress == null)
                {
                    _logger.LogWarning("No primary address for user {UserId}", userId);
                    throw new AddressNotFoundException(originalOrder.UserId);
                }

                if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
                {
                    _logger.LogWarning("Location inactive for user {UserId}", userId);
                    throw new AddressNotFoundException(originalOrder.UserId);
                }

                if (originalOrder.Ingredients == null || originalOrder.Ingredients.Count == 0)
                {
                    _logger.LogWarning("Original order {OrderId} has no ingredients", scheduledOrderId);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("Original order has no ingredients");
                }

                var ingredientIds = originalOrder.Ingredients.Select(i => i.IngredientId).ToList();
                var existingIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
                var existingIds = existingIngredients.Keys.ToHashSet();

                if (ingredientIds.Any(id => !existingIds.Contains(id)))
                {
                    _logger.LogWarning("Some ingredients no longer available for order {OrderId}", scheduledOrderId);
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("Some ingredients are no longer available");
                }

                _logger.LogInformation("All validations passed for order {OrderId}, creating duplicate", scheduledOrderId);

                var duplicateOrder = new ScheduledOrder
                {
                    UserId = userId,
                    AuthId = authId,
                    MealName = originalOrder.MealName,
                    MealId = originalOrder.MealId,
                    MealImageUrl = ScheduledOrderHelper.CleanMealImageUrl(originalOrder.MealImageUrl),
                    ScheduledFor = originalOrder.ScheduledFor,
                    DeliveryTimeSlot = originalOrder.DeliveryTimeSlot,
                    TotalPrice = originalOrder.TotalPrice,
                    NutritionalSummary = originalOrder.NutritionalSummary,
                    OrderStatus = ScheduledOrderStatus.Scheduled,
                    CanModify = true,
                    ExpiresAt = _time.ToUtc(originalOrder.ScheduledFor.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                    DeliveryAddressId = originalOrder.DeliveryAddressId
                };

                foreach (var originalIngredient in originalOrder.Ingredients)
                {
                    duplicateOrder.Ingredients.Add(new ScheduledOrderIngredient
                    {
                        IngredientId = originalIngredient.IngredientId,
                        Quantity = originalIngredient.Quantity,
                        UnitPrice = originalIngredient.UnitPrice,
                        TotalPrice = originalIngredient.TotalPrice
                    });
                }

                _logger.LogInformation("Duplicate prepared with {IngredientCount} ingredients", duplicateOrder.Ingredients.Count);

                var createdOrder = await _scheduledOrderRepository.CreateAsync(duplicateOrder);

                _logger.LogInformation(
                    $"✅ Duplicated order #{scheduledOrderId} → #{createdOrder.ScheduledOrderId} " +
                    $"for {createdOrder.ScheduledFor:yyyy-MM-dd} (₹{createdOrder.TotalPrice})");

                return ScheduledOrderHelper.MapToResponseDto(createdOrder);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Duplication validation failed for order {OrderId}: {ErrorMessage}", scheduledOrderId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Unexpected error duplicating order #{scheduledOrderId}");
                throw new InvalidOperationException("Failed to duplicate order. Please try again.", ex);
            }
        }
    }
}
