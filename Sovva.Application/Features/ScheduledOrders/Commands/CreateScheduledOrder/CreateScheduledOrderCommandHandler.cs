using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

namespace Sovva.Application.Features.ScheduledOrders.Commands.CreateScheduledOrder
{
    public class CreateScheduledOrderCommandHandler : IRequestHandler<CreateScheduledOrderCommand, ScheduledOrderResponseDto>
    {
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IUserRepository _userRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IWalletTransactionService _walletService;
        private readonly IAppTimeProvider _time;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IMealRepository _mealRepository;
        private readonly ILogger<CreateScheduledOrderCommandHandler> _logger;

        public CreateScheduledOrderCommandHandler(
            IScheduledOrderRepository scheduledOrderRepository,
            IUserRepository userRepository,
            IIngredientRepository ingredientRepository,
            IWalletTransactionService walletService,
            IAppTimeProvider time,
            IUserAddressRepository userAddressRepository,
            IMealRepository mealRepository,
            ILogger<CreateScheduledOrderCommandHandler> logger)
        {
            _scheduledOrderRepository = scheduledOrderRepository;
            _userRepository = userRepository;
            _ingredientRepository = ingredientRepository;
            _walletService = walletService;
            _time = time;
            _userAddressRepository = userAddressRepository;
            _mealRepository = mealRepository;
            _logger = logger;
        }

        public async Task<ScheduledOrderResponseDto> Handle(CreateScheduledOrderCommand request, CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            var authId = request.AuthId;
            var dto = request.Dto;
            var skipWalletCheck = request.SkipWalletCheck;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new UserNotFoundException(userId);

            int? deliveryAddressId = dto.DeliveryAddressId;
            UserAddress? primaryAddress = null;

            if (deliveryAddressId == null)
            {
                primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(user.UserId);

                if (primaryAddress == null)
                {
                    throw new AddressNotFoundException(user.UserId);
                }
                deliveryAddressId = primaryAddress.Id;
            }
            else
            {
                primaryAddress = await _userAddressRepository.GetByIdWithDetailsAsync(deliveryAddressId.Value);
                if (primaryAddress == null || primaryAddress.UserId != user.UserId)
                {
                    throw new AddressNotFoundException(user.UserId);
                }
            }

            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new AddressNotFoundException(user.UserId,
                    $"Sorry, we don't deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"} currently. " +
                    "Please update your delivery address.");
            }

            _logger.LogInformation("Delivery address validated: {Area}, {City}", primaryAddress.ServiceableLocation.Area, primaryAddress.ServiceableLocation.City);

            DateOnly deliveryDate;
            var todayIst = _time.TodayIst;

            if (dto.ScheduledFor != default(DateTimeOffset))
            {
                var utc = dto.ScheduledFor.UtcDateTime;
                var ist = _time.ToIst(utc);
                deliveryDate = DateOnly.FromDateTime(ist);

                _logger.LogInformation("[ScheduledOrder] Parsed delivery date: {Date}", deliveryDate);
            }
            else
            {
                deliveryDate = todayIst.AddDays(1);
                _logger.LogInformation("[ScheduledOrder] No date provided, defaulting to tomorrow: {Date}", deliveryDate);
            }

            if (deliveryDate <= todayIst)
            {
                _logger.LogWarning("[ScheduledOrder] Date {Date} is today/past, overriding to tomorrow", deliveryDate);
                deliveryDate = todayIst.AddDays(1);
            }

            _logger.LogInformation("[ScheduledOrder] Order placed at: {Ist:yyyy-MM-dd HH:mm:ss} IST", _time.NowIst);
            _logger.LogInformation("[ScheduledOrder] Delivery scheduled for: {Date}", deliveryDate);

            decimal totalPrice;
            var ingredients = new List<(Ingredient ingredient, int quantity)>();

            var ingredientIds = dto.SelectedIngredients.Select(i => i.IngredientId).ToList();
            var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

            foreach (var ingredientDto in dto.SelectedIngredients)
            {
                if (!ingredientMap.TryGetValue(ingredientDto.IngredientId, out var ingredient))
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Ingredient {ingredientDto.IngredientId} not found");

                ingredients.Add((ingredient, ingredientDto.Quantity));
            }

            if (dto.MealPrice.HasValue && dto.MealPrice.Value > 0)
            {
                totalPrice = dto.MealPrice.Value;
                _logger.LogInformation("Using featured meal fixed price: {TotalPrice}", totalPrice);
            }
            else
            {
                if (!dto.MealId.HasValue)
                    throw new Sovva.Domain.Exceptions.BusinessRuleException("MealId is required for custom meal calculation");

                var meal = await _mealRepository.GetByIdAsync(dto.MealId.Value);
                if (meal == null)
                    throw new Sovva.Domain.Exceptions.BusinessRuleException($"Meal {dto.MealId} not found");

                totalPrice = meal.BasePrice + ingredients.Sum(i => i.ingredient.Price * i.quantity);
                _logger.LogInformation("Calculated price from ingredients + BasePrice ({BasePrice}): {TotalPrice}", meal.BasePrice, totalPrice);
            }

            if (!skipWalletCheck && !await _walletService.HasSufficientBalanceAsync(userId, totalPrice))
            {
                var currentBalance = await _walletService.GetUserBalanceAsync(userId);
                throw new InsufficientBalanceException(totalPrice, currentBalance);
            }

            var scheduledOrder = new ScheduledOrder
            {
                UserId = userId,
                AuthId = authId,
                MealName = dto.MealName ?? DeliveryConstants.DefaultMealName,
                MealId = dto.MealId,
                MealImageUrl = ScheduledOrderHelper.CleanMealImageUrl(dto.MealImageUrl),
                ScheduledFor = deliveryDate,
                DeliveryTimeSlot = dto.DeliveryTimeSlot ?? DeliveryConstants.DefaultTimeSlot,
                TotalPrice = totalPrice,
                NutritionalSummary = dto.NutritionalSummary != null
                    ? JsonSerializer.Serialize(dto.NutritionalSummary)
                    : null,
                OrderStatus = ScheduledOrderStatus.Scheduled,
                CanModify = true,
                ExpiresAt = _time.ToUtc(deliveryDate.AddDays(1).ToDateTime(TimeOnly.MinValue)),
                DeliveryAddressId = deliveryAddressId,
                SubscriptionId = dto.SubscriptionId
            };

            foreach (var (ingredient, quantity) in ingredients)
            {
                scheduledOrder.Ingredients.Add(new ScheduledOrderIngredient
                {
                    IngredientId = ingredient.IngredientId,
                    Quantity = quantity,
                    UnitPrice = ingredient.Price,
                    TotalPrice = ingredient.Price * quantity
                });
            }

            var createdOrder = await _scheduledOrderRepository.CreateAsync(scheduledOrder);

            _logger.LogInformation("Order {OrderId} created for {DeliveryDate} delivery, total: {TotalPrice}", createdOrder.ScheduledOrderId, deliveryDate, totalPrice);

            return ScheduledOrderHelper.MapToResponseDto(createdOrder);
        }
    }
}
