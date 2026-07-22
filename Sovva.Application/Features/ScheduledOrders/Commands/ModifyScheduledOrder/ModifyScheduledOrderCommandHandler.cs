using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Exceptions;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders.Commands.ModifyScheduledOrder
{
    public class ModifyScheduledOrderCommandHandler : IRequestHandler<ModifyScheduledOrderCommand, bool>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly ILogger<ModifyScheduledOrderCommandHandler> _logger;

        public ModifyScheduledOrderCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            ILogger<ModifyScheduledOrderCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _logger = logger;
        }

        public async Task<bool> Handle(ModifyScheduledOrderCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            var authId = request.AuthId;
            var scheduledOrderId = request.ScheduledOrderId;
            var dto = request.Dto;

            var scheduledOrder = await _scheduledOrderRepository.GetByIdAndAuthIdAsync(scheduledOrderId, authId);
            if (scheduledOrder == null)
                throw new ScheduledOrderNotFoundException(scheduledOrderId);

            if (scheduledOrder.UserId != userId)
                throw new UnauthorizedAccessException("Order does not belong to this user");

            if (!scheduledOrder.CanModify || scheduledOrder.OrderStatus != ScheduledOrderStatus.Scheduled)
                throw new Sovva.Domain.Exceptions.BusinessRuleException("Order can no longer be modified");

            var ingredients = new List<(Ingredient ingredient, int quantity)>();
            decimal newTotalPrice = 0;

            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
                newTotalPrice += ingredient.Price * ingredientDto.Quantity;
            }

            if (!await _walletService.HasSufficientBalanceAsync(userId, newTotalPrice))
            {
                var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                throw new InsufficientBalanceException(newTotalPrice, currentBalance);
            }

            scheduledOrder.Ingredients.Clear();

            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    ScheduledOrderId = scheduledOrder.ScheduledOrderId,
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price,
                    TotalPrice = ingredient.Price * quantity
                });
            }

            scheduledOrder.TotalPrice = newTotalPrice;
            scheduledOrder.DeliveryTimeSlot = dto.DeliveryTimeSlot ?? scheduledOrder.DeliveryTimeSlot;
            scheduledOrder.NutritionalSummary = dto.NutritionalSummary != null
                ? JsonSerializer.Serialize(dto.NutritionalSummary)
                : scheduledOrder.NutritionalSummary;

            await _scheduledOrderRepository.UpdateAsync(scheduledOrder);

            _logger.LogInformation("Order {OrderId} modified - New total: {NewTotalPrice}", scheduledOrderId, newTotalPrice);
            return true;
        }
    }
}
