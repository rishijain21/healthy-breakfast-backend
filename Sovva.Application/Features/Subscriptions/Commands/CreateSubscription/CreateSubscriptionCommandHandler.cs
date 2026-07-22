using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Domain.Exceptions;

namespace Sovva.Application.Features.Subscriptions.Commands.CreateSubscription
{
    public class CreateSubscriptionCommandHandler : IRequestHandler<CreateSubscriptionCommand, SubscriptionDto>
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUserMealRepository _userMealRepository;
        private readonly IMealRepository _mealRepository;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IScheduledOrderRepository _scheduledOrderRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IUserMealIngredientRepository _userMealIngredientRepository;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<CreateSubscriptionCommandHandler> _logger;
        private readonly IUserLoader _userLoader;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public CreateSubscriptionCommandHandler(
            ISubscriptionRepository subscriptionRepository,
            IUserMealRepository userMealRepository,
            IMealRepository mealRepository,
            IUserAddressRepository userAddressRepository,
            IScheduledOrderRepository scheduledOrderRepository,
            IIngredientRepository ingredientRepository,
            IUserMealIngredientRepository userMealIngredientRepository,
            IAppTimeProvider time,
            ILogger<CreateSubscriptionCommandHandler> logger,
            IUserLoader userLoader,
            IUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _subscriptionRepository = subscriptionRepository;
            _userMealRepository = userMealRepository;
            _mealRepository = mealRepository;
            _userAddressRepository = userAddressRepository;
            _scheduledOrderRepository = scheduledOrderRepository;
            _ingredientRepository = ingredientRepository;
            _userMealIngredientRepository = userMealIngredientRepository;
            _time = time;
            _logger = logger;
            _userLoader = userLoader;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<SubscriptionDto> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;
            var user = await _userLoader.GetUserWithAuthMappingAsync(dto.UserId);
            if (user == null)
                throw new UserNotFoundException(dto.UserId);

            if (user.UserId != dto.UserId)
            {
                _logger.LogWarning("Security violation: User {UserId} attempted to subscribe with invalid user account", dto.UserId);
                throw new UnauthorizedAccessException("Invalid user account");
            }

            if (!dto.MealId.HasValue && !dto.UserMealId.HasValue)
                throw new ArgumentException("Either MealId or UserMealId must be provided");
            if (dto.MealId.HasValue && dto.UserMealId.HasValue)
                throw new ArgumentException("Cannot provide both MealId and UserMealId");

            Meal? meal = null;
            UserMeal? userMeal = null;

            if (dto.MealId.HasValue)
            {
                meal = await _mealRepository.GetByIdAsync(dto.MealId.Value);
                if (meal == null) throw new ArgumentException("Meal not found");
            }
            else
            {
                userMeal = await _userMealRepository.GetByIdAsync(dto.UserMealId!.Value);
                if (userMeal == null || userMeal.UserId != dto.UserId)
                    throw new ArgumentException("Custom meal not found or unauthorized");
                meal = await _mealRepository.GetByIdAsync(userMeal.MealId);
                if (meal == null) throw new ArgumentException("Base meal not found for custom meal");
            }

            var primaryAddress = await _userAddressRepository.GetPrimaryAddressAsync(dto.UserId);
            if (primaryAddress == null)
                throw new AddressNotFoundException(dto.UserId, "Please set a default delivery address before creating a subscription");

            Subscription? createdSubscriptionEntity = null;

            var finalSubscription = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    Subscription? existingSubscription = null;
                    if (dto.MealId.HasValue)
                    {
                        existingSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByMealIdAsync(dto.UserId, dto.MealId.Value);
                    }
                    else
                    {
                        existingSubscription = await _subscriptionRepository.GetAnyActiveSubscriptionByUserMealIdAsync(dto.UserId, dto.UserMealId!.Value);
                    }

                    if (existingSubscription != null)
                    {
                        throw new DuplicateSubscriptionException();
                    }

                    if (dto.StartDate >= dto.EndDate)
                        throw new ArgumentException("Start date must be before end date");

                    if (dto.Frequency == SubscriptionFrequency.Weekly)
                    {
                        if (dto.WeeklySchedule == null || !dto.WeeklySchedule.Any())
                            throw new ArgumentException("Weekly schedule is required for Weekly subscriptions");
                    }

                    var subscription = new Subscription
                    {
                        UserId = dto.UserId,
                        MealId = dto.MealId,
                        UserMealId = dto.UserMealId,
                        AgreedPrice = dto.MealId.HasValue ? meal.BasePrice : userMeal!.TotalPrice,
                        Frequency = dto.Frequency,
                        StartDate = dto.StartDate,
                        EndDate = dto.EndDate,
                        IsActive = dto.IsActive,
                        DeliveryAddressId = primaryAddress.Id,
                        NextScheduledDate = SubscriptionHelper.CalculateInitialNextDeliveryDate(dto.StartDate, dto.Frequency, dto.WeeklySchedule, _time)
                    };

                    var createdSubscription = await _subscriptionRepository.CreateAsync(subscription);

                    if (dto.Frequency == SubscriptionFrequency.Weekly && dto.WeeklySchedule != null)
                    {
                        var schedules = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                        {
                            SubscriptionId = createdSubscription.SubscriptionId,
                            DayOfWeek = s.DayOfWeek,
                            Quantity = s.Quantity
                        });

                        await _subscriptionRepository.AddSchedulesAsync(createdSubscription.SubscriptionId, schedules);
                    }

                    createdSubscription.User = user;
                    createdSubscription.Meal = meal;
                    createdSubscription.UserMeal = userMeal;

                    createdSubscriptionEntity = createdSubscription;

                    return SubscriptionHelper.MapToDto(createdSubscription);
                }
                catch (DuplicateSubscriptionException)
                {
                    throw;
                }
            });

            string? firstOrderWarning = null;

            if (createdSubscriptionEntity != null)
            {
                if (dto.Frequency == SubscriptionFrequency.Weekly && dto.WeeklySchedule != null)
                {
                    createdSubscriptionEntity.WeeklySchedule = dto.WeeklySchedule.Select(s => new SubscriptionSchedule
                    {
                        DayOfWeek = s.DayOfWeek,
                        Quantity = s.Quantity
                    }).ToList();
                }

                var firstOrderResult = await CreateFirstScheduledOrderAsync(
                    createdSubscriptionEntity,
                    user,
                    userMeal,
                    meal,
                    primaryAddress
                );

                if (!firstOrderResult.Success)
                {
                    _logger.LogWarning("Subscription {Id} created but first order failed: {Error}",
                        finalSubscription.SubscriptionId, firstOrderResult.Error);
                    firstOrderWarning = firstOrderResult.Error?.Contains("No ingredients") == true
                        ? "Subscription created! First delivery will be scheduled once meal ingredients are configured by admin."
                        : "Subscription created! Your first order will be scheduled automatically.";
                }
            }
            else
            {
                _logger.LogWarning("Subscription entity not captured after commit for user {UserId}", dto.UserId);
            }

            await _cacheService.RemoveAsync(CacheKeys.SubscriptionsByUser(dto.UserId));

            finalSubscription.Warning = firstOrderWarning;
            return finalSubscription;
        }

        private async Task<(bool Success, string? Error)> CreateFirstScheduledOrderAsync(
            Subscription subscription,
            User user,
            UserMeal? userMeal,
            Meal meal,
            UserAddress deliveryAddress)
        {
            _logger.LogInformation(
                "CreateFirstScheduledOrderAsync called - SubscriptionId: {SubscriptionId}, UserId: {UserId}, UserMealId: {UserMealId}, MealName: {MealName}",
                subscription.SubscriptionId,
                user.UserId,
                subscription.UserMealId?.ToString() ?? "(fixed meal)",
                subscription.MealId.HasValue ? meal.MealName : userMeal?.MealName ?? "(unknown)");

            try
            {
                _logger.LogInformation("Creating first order for subscription #{SubscriptionId}", subscription.SubscriptionId);

                var scheduledOrderIngredients = new List<ScheduledOrderIngredient>();
                decimal totalPrice = subscription.AgreedPrice;

                int totalCalories = 0;
                decimal totalProtein = 0m;
                int itemCount = 0;

                if (subscription.UserMealId.HasValue)
                {
                    var ingredients = await _userMealIngredientRepository.GetByUserMealIdAsync(subscription.UserMealId.Value);
                    if (!ingredients.Any()) return (false, $"No ingredients found for UserMeal #{subscription.UserMealId.Value}");

                    var ingredientIds = ingredients.Select(i => i.IngredientId).ToList();
                    var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

                    foreach (var umi in ingredients)
                    {
                        if (ingredientMap.TryGetValue(umi.IngredientId, out var ing))
                        {
                            var itemTotal = ing.Price * umi.Quantity;
                            scheduledOrderIngredients.Add(new ScheduledOrderIngredient
                            {
                                IngredientId = ing.IngredientId,
                                Quantity = umi.Quantity,
                                UnitPrice = ing.Price,
                                TotalPrice = itemTotal
                            });

                            totalCalories += ing.Calories * umi.Quantity;
                            totalProtein += ing.Protein * umi.Quantity;
                            itemCount += umi.Quantity;
                        }
                    }
                }
                else
                {
                    var mealWithDetails = await _mealRepository.GetByIdWithOptionsAsync(subscription.MealId!.Value);
                    var defaultOption = mealWithDetails?.MealOptions?.FirstOrDefault();
                    if (defaultOption != null && defaultOption.MealOptionIngredients.Any())
                    {
                        var ingredientIds = defaultOption.MealOptionIngredients.Select(i => i.IngredientId).ToList();
                        var ingredientMap = await _ingredientRepository.GetByIdsAsync(ingredientIds);

                        foreach (var moi in defaultOption.MealOptionIngredients)
                        {
                            if (ingredientMap.TryGetValue(moi.IngredientId, out var ing))
                            {
                                var itemTotal = ing.Price * 1;
                                scheduledOrderIngredients.Add(new ScheduledOrderIngredient
                                {
                                    IngredientId = ing.IngredientId,
                                    Quantity = 1,
                                    UnitPrice = ing.Price,
                                    TotalPrice = itemTotal
                                });

                                totalCalories += ing.Calories * 1;
                                totalProtein += ing.Protein * 1;
                                itemCount += 1;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No ingredients configured for master meal #{MealId}. Order created without ingredients.", subscription.MealId.Value);
                    }
                }

                var firstDeliveryDate = SubscriptionHelper.CalculateFirstDeliveryDate(subscription, _time, _logger);
                _logger.LogInformation("First delivery: {FirstDeliveryDate:yyyy-MM-dd}", firstDeliveryDate);

                var deliveryDateTimeUtc = _time.ToUtc(firstDeliveryDate.ToDateTime(TimeOnly.MinValue));

                var nutritionalSummary = new NutritionalSummaryDto
                {
                    TotalCalories = totalCalories,
                    TotalProtein = totalProtein,
                    ItemCount = itemCount
                };

                var scheduledOrder = new ScheduledOrder
                {
                    UserId = subscription.UserId,
                    AuthId = user.AuthMapping?.AuthId ?? throw new BusinessRuleException("User has no AuthMapping"),
                    MealName = subscription.MealId.HasValue ? meal.MealName : userMeal!.MealName,
                    MealId = subscription.MealId,
                    MealImageUrl = subscription.MealId.HasValue ? meal.ImageUrl : null,
                    ScheduledFor = firstDeliveryDate,
                    DeliveryTimeSlot = DeliveryConstants.DefaultTimeSlot,
                    TotalPrice = totalPrice,
                    NutritionalSummary = System.Text.Json.JsonSerializer.Serialize(nutritionalSummary),
                    OrderStatus = ScheduledOrderStatus.Scheduled,
                    CanModify = true,
                    ExpiresAt = deliveryDateTimeUtc.AddDays(1),
                    DeliveryAddressId = deliveryAddress.Id,
                    SubscriptionId = subscription.SubscriptionId,
                    Ingredients = scheduledOrderIngredients
                };

                var created = await _scheduledOrderRepository.CreateAsync(scheduledOrder);

                _logger.LogInformation("ScheduledOrder #{ScheduledOrderId} created successfully for delivery date {DeliveryDate}", created.ScheduledOrderId, firstDeliveryDate);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create first order for subscription #{SubscriptionId}", subscription.SubscriptionId);
                return (false, ex.Message);
            }
        }
    }
}
