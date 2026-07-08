using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMealService _mealService;
        private readonly IWalletTransactionService _walletService;
        private readonly IUserMealService _userMealService;
        private readonly IUserMealIngredientService _userMealIngredientService;
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAppTimeProvider _time;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IOrderRepository orderRepository,
            IMealService mealService,
            IWalletTransactionService walletService,
            IUserMealService userMealService,
            IUserMealIngredientService userMealIngredientService,
            IUserAddressRepository userAddressRepository,
            IUnitOfWork unitOfWork,
            IAppTimeProvider time,
            ILogger<OrderService> logger) // ✅ ADDED
        {
            _orderRepository = orderRepository;
            _mealService = mealService;
            _walletService = walletService;
            _userMealService = userMealService;
            _userMealIngredientService = userMealIngredientService;
            _userAddressRepository = userAddressRepository;
            _unitOfWork = unitOfWork;
            _time = time;
            _logger = logger;
        }

        // ✅ SECURE: Create order with userId from JWT token
        public async Task<long> CreateOrderAsync(CreateOrderDto dto, int userId)
        {
            var entity = new Order
            {
                UserId = userId,
                OrderStatus = OrderStatus.Pending,
                TotalPrice = dto.TotalPrice,
                OrderDate = _time.UtcNow,
                ScheduledFor = _time.UtcNow.AddHours(2),
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _orderRepository.AddAsync(entity);
            await _orderRepository.SaveChangesAsync();

            return entity.OrderId;
        }

        public async Task<OrderDto?> GetOrderByIdAsync(long id)
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

        // ✅ EXISTING: Simple methods for backward compatibility
        public async Task<PagedResult<OrderDto>> GetAllOrderHistoryAsync(int page = 1, int pageSize = 50)
        {
            var orders = await _orderRepository.GetAllAsync(page, pageSize);
            var totalCount = await _orderRepository.CountAsync(); // Assuming CountAsync exists or use a workaround
            
            var items = orders.Select(order => new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            }).ToList();

            return new PagedResult<OrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<OrderDto>> GetOrdersByStatusAsync(OrderStatus status, int page = 1, int pageSize = 50)
        {
            var orders = await _orderRepository.GetByStatusAsync(status, page, pageSize);
            var totalCount = await _orderRepository.CountByStatusAsync(status);
            
            var items = orders.Select(order => new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            }).ToList();

            return new PagedResult<OrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<OrderDto>> GetUserOrdersAsync(int userId, int page = 1, int pageSize = 20)
        {
            var (orders, totalCount) = await _orderRepository.GetByUserIdPagedAsync(userId, page, pageSize);
            
            var items = orders.Select(order => new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            }).ToList();

            return new PagedResult<OrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // ✅ NEW: Enhanced methods with rich data
        public async Task<PagedResult<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var (orders, totalCount) = await _orderRepository.GetUserOrdersWithDetailsPagedAsync(userId, page, pageSize);
            
            return new PagedResult<EnhancedOrderHistoryDto>
            {
                Items = MapToEnhancedDto(orders).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync(int page = 1, int pageSize = 50)
        {
            var orders = await _orderRepository.GetAllOrdersWithDetailsAsync(page, pageSize);
            var totalCount = await _orderRepository.CountAsync();
            
            return new PagedResult<EnhancedOrderHistoryDto>
            {
                Items = MapToEnhancedDto(orders).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // ✅ ENHANCED: Map complex entities to DTOs with better legacy handling
        private IEnumerable<EnhancedOrderHistoryDto> MapToEnhancedDto(IEnumerable<Order> orders)
        {
            return orders.Select(order =>
            {
                // ✅ Determine data source: UserMeal (real-time) or SourceScheduledOrder (confirmed)
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

                    // ✅ Meal name & image: UserMeal → ScheduledOrder snapshot → fallback
                    MealId = order.UserMeal?.MealId ?? order.SourceScheduledOrder?.MealId ?? 0,
                    MealName = order.UserMeal?.MealName
                            ?? order.SourceScheduledOrder?.MealName
                            ?? "Order",
                    MealImageUrl = order.UserMeal?.Meal?.ImageUrl ?? order.SourceScheduledOrder?.MealImageUrl,

                    // ✅ Nutritional info from UserMeal ingredients (only available for real-time orders)
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

                    // ✅ Ingredients: UserMeal path (full data) or ScheduledOrder path (snapshot prices)
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

        // ✅ UNCHANGED: First overload - uses primary address
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId)
        {
            // ✅ FIX 6: Guard against soft-deleted meals
            // Check this before the address validation so we fail fast on invalid meal
            if (dto.MealId > 0)
            {
                var meal = await _mealService.GetMealByIdAsync(dto.MealId);
                if (meal == null)
                    throw new InvalidOperationException(
                        "The selected meal is no longer available.");
            }

            // ✅ STEP 0: Validate Primary Address (using userId from token)
            var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);

            if (primaryAddress == null)
            {
                throw new AddressNotFoundException(userId);
            }

            // ✅ Validate location is serviceable
            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new InvalidOperationException(
                    $"Sorry, we don't currently deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"}. " +
                    $"Please update your delivery address to a serviceable location."
                );
            }

            // ✅ EXTRACTED: Call shared core logic with individual params
            return await ExecuteOrderCreationAsync(
                userId: userId,
                mealId: dto.MealId,
                ingredients: dto.SelectedIngredients,
                deliveryAddressId: primaryAddress.Id,
                overrideTotalPrice: null,
                scheduledFor: dto.ScheduledFor,
                mealName: dto.MealName);
        }

        // ✅ UNCHANGED: Second overload - validates provided address
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto,
            int userId,
            int? deliveryAddressId)
        {
            int? addressIdToUse = deliveryAddressId;

            if (addressIdToUse.HasValue)
            {
                // Validate the provided address exists and is serviceable
                var address = await _userAddressRepository.GetByIdWithDetailsAsync(addressIdToUse.Value);
                if (address == null || address.UserId != userId)
                {
                    throw new InvalidOperationException("Invalid delivery address");
                }

                if (address.ServiceableLocation == null || !address.ServiceableLocation.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Sorry, we don't currently deliver to {address.ServiceableLocation?.Area ?? "your location"}. " +
                        "Please update your delivery address."
                    );
                }
            }
            else
            {
                // Fall back to getting primary address (original behavior)
                var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
                if (primaryAddress == null)
                {
                    throw new AddressNotFoundException(userId);
                }

                if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Sorry, we don't currently deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"}. " +
                        "Please update your delivery address."
                    );
                }

                addressIdToUse = primaryAddress.Id;
            }

            // ✅ EXTRACTED: Call shared core logic with individual params
            return await ExecuteOrderCreationAsync(
                userId: userId,
                mealId: dto.MealId,
                ingredients: dto.SelectedIngredients,
                deliveryAddressId: addressIdToUse!.Value,
                overrideTotalPrice: null,
                scheduledFor: dto.ScheduledFor,
                mealName: dto.MealName);
        }

        // ✅ EXTRACTED: Core order creation logic with UnitOfWork transaction
        private async Task<OrderCreationResponseDto> ExecuteOrderCreationAsync(
            int userId, 
            int mealId,
            List<SelectedIngredientDto> ingredients,
            int deliveryAddressId,
            decimal? overrideTotalPrice,
            DateTime? scheduledFor,
            string? mealName = null)
        {
            // ✅ O5 FIX: Guard against empty ingredients
            if (ingredients == null || !ingredients.Any())
                throw new ArgumentException("At least one ingredient must be selected to place an order.");

            // ✅ STEP 1: Calculate meal price and validate ingredients
            // ✅ Use override price from scheduled order, or recalculate
            MealPriceResponseDto priceCalculation;
            if (overrideTotalPrice.HasValue)
            {
                // Use the price agreed at order creation time (from scheduled order snapshot)
                priceCalculation = new MealPriceResponseDto
                {
                    // ✅ Use meal name from scheduled order snapshot, or fallback
                    MealName = mealName ?? "Scheduled Order",
                    TotalPrice = overrideTotalPrice.Value,
                    IngredientBreakdown = ingredients.Select(i => new IngredientBreakdownDto
                    {
                        IngredientId = i.IngredientId,
                        Quantity = i.Quantity,
                        // ✅ Use snapshot prices from scheduled order, or fallback to 0
                        UnitPrice = i.UnitPrice ?? 0,
                        TotalPrice = i.TotalPrice ?? 0
                    }).ToList()
                };
            }
            else
            {
                priceCalculation = await _mealService.CalculateMealPriceAsync(new MealPriceCalculationDto
                {
                    MealId = mealId,
                    SelectedIngredients = ingredients
                });
            }

            // ✅ STEP 2: Check wallet balance
            var walletBalanceBefore = await _walletService.GetUserBalanceAsync(userId);
            var hasSufficientBalance = await _walletService.HasSufficientBalanceAsync(userId, priceCalculation.TotalPrice);

            if (!hasSufficientBalance)
            {
                throw new InsufficientBalanceException(priceCalculation.TotalPrice, walletBalanceBefore);
            }

            // ✅ All writes inside a single transaction (UnitOfWork)
            OrderCreationResponseDto response = null;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // ✅ STEP 3: Create UserMeal record
                var userMealDto = new CreateUserMealDto
                {
                    MealId = mealId,
                    MealName = priceCalculation.MealName,
                    TotalPrice = priceCalculation.TotalPrice,
                    CreatedAt = _time.UtcNow
                };

                var createdUserMealId = await _userMealService.CreateUserMealAsync(userMealDto, userId);

                // ✅ STEP 4: Create UserMealIngredient records for each selected ingredient
                var ingredientDtos = new List<CreateUserMealIngredientDto>();
                
                if (overrideTotalPrice.HasValue)
                {
                    // Variant used by first overload (stores UnitPrice/TotalPrice when available)
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
                    // Variant used by second overload (omits UnitPrice/TotalPrice)
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
                    await _userMealIngredientService.CreateUserMealIngredientsAsync(ingredientDtos);
                }

                // ✅ STEP 5: Create Order with DeliveryAddressId
                var order = new Order
                {
                    UserId = userId,
                    UserMealId = createdUserMealId,
                    DeliveryAddressId = deliveryAddressId,
                    OrderStatus = OrderStatus.Pending,
                    TotalPrice = priceCalculation.TotalPrice,
                    OrderDate = _time.UtcNow,
                    ScheduledFor = scheduledFor ?? _time.UtcNow.AddHours(2),
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow
                };

                await _orderRepository.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // ✅ STEP 6: Process payment via wallet
                var walletTransaction = await _walletService.DebitWalletAsync(
                    userId,
                    priceCalculation.TotalPrice,
                    $"Order #{order.OrderId} - {priceCalculation.MealName}"
                );

                // ✅ STEP 7: Confirm order after successful payment (SP-1: uses state transition guard)
                order.TransitionTo(OrderStatus.Confirmed);
                order.UpdatedAt = _time.UtcNow;
                _orderRepository.Update(order);
                await _unitOfWork.SaveChangesAsync();

                var walletBalanceAfter = await _walletService.GetUserBalanceAsync(userId);

                // ✅ STEP 8: Return comprehensive order creation response
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

        // ✅ NEW: Dedicated method for confirming scheduled orders
        // No catalogue lookup, no UserMeal creation, no price recalculation
        // Everything comes from the snapshot
        // NOTE: Wallet deduction is now done atomically in ScheduledOrderService.ConfirmAllScheduledOrdersAsync
        // before calling this method, to prevent race conditions
        // NOTE: No manual transaction - NpgsqlRetryingExecutionStrategy blocks user-initiated transactions
        public async Task<int> ConfirmScheduledOrderAsync(ScheduledOrder scheduledOrder, Order? existingOrder = null)
        {
            // ✅ IDEMPOTENCY: If preloaded or a previous attempt already created the Order row,
            // return its ID immediately — no duplicate insert, no double charge
            existingOrder ??= await _orderRepository
                .GetByScheduledOrderIdAsync(scheduledOrder.ScheduledOrderId);
            
            if (existingOrder != null)
            {
                return existingOrder.OrderId;
            }

            // ✅ FIX [O3]: Guard against null DeliveryAddressId — prevents NRE crash
            // Callers (ConfirmSingleOrderAsync, ProcessOrdersForDate) catch this,
            // mark the order as Failed, and continue processing remaining orders.
            if (scheduledOrder.DeliveryAddressId == null)
            {
                throw new InvalidOperationException(
                    $"ScheduledOrder #{scheduledOrder.ScheduledOrderId} has no DeliveryAddressId. " +
                    "Cannot create Order without a delivery address.");
            }

            // ✅ Single INSERT — atomic by itself, no manual transaction needed
            // (NpgsqlRetryingExecutionStrategy blocks manual transactions)
            var order = new Order
            {
                UserId            = scheduledOrder.UserId,
                UserMealId        = null,
                ScheduledOrderId  = scheduledOrder.ScheduledOrderId,
                DeliveryAddressId = scheduledOrder.DeliveryAddressId.Value,
                OrderStatus       = OrderStatus.Confirmed,
                TotalPrice        = scheduledOrder.TotalPrice,

                // ✅ ScheduledFor comes from DATE column → DateOnly → convert to UTC midnight
                ScheduledFor = _time.ToUtc(scheduledOrder.ScheduledFor.ToDateTime(TimeOnly.MinValue)),

                OrderDate = _time.UtcNow,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();



            return order.OrderId;
        }

        /// <summary>
        /// ✅ NEW: Get order by ScheduledOrderId for idempotency check
        /// </summary>
        public async Task<Order?> GetByScheduledOrderIdAsync(int scheduledOrderId)
        {
            return await _orderRepository.GetByScheduledOrderIdAsync(scheduledOrderId);
        }

        // ==================== POST-DELIVERY ACTIONS ====================

        public async Task<bool> RateOrderAsync(long orderId, int userId, int rating, string? review)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null || order.UserId != userId)
                throw new InvalidOperationException("Order not found or access denied.");

            if (!order.IsPrepared)
                throw new InvalidOperationException("Cannot rate an order that hasn't been prepared/delivered yet.");

            order.Rating = rating;
            order.Review = review;
            order.UpdatedAt = _time.UtcNow;

            _orderRepository.Update(order);
            await _orderRepository.SaveChangesAsync();

            return true;
        }

        public async Task<OrderCreationResponseDto> ReorderAsync(long orderId, int userId)
        {
            // 1. Fetch past order
            var pastOrder = await _orderRepository.GetByIdAsync(orderId);
            if (pastOrder == null || pastOrder.UserId != userId)
                throw new InvalidOperationException("Order not found or access denied.");

            if (pastOrder.UserMealId == null)
                throw new InvalidOperationException("Cannot reorder this meal as its components are no longer available.");

            // 2. Determine price and check wallet balance
            var price = pastOrder.TotalPrice;
            var currentBalance = await _walletService.GetUserBalanceAsync(userId);

            // ✅ Idempotency check (C-1): Prevent double-clicks within 30 seconds
            var recentOrder = await _orderRepository.GetRecentOrderByUserMealIdAsync(pastOrder.UserMealId.Value, userId, 30);
            if (recentOrder != null)
            {
                _logger.LogInformation("Reorder duplicate detected for User {UserId}, returning existing Order {OrderId}", userId, recentOrder.OrderId);
                return new OrderCreationResponseDto
                {
                    OrderId = recentOrder.OrderId,
                    MealName = "Reorder (Duplicate Prevention)",
                    OrderStatus = recentOrder.OrderStatus.ToString(),
                    UserMealId = recentOrder.UserMealId ?? 0,
                    TotalPrice = recentOrder.TotalPrice,
                    WalletBalanceBefore = currentBalance,
                    WalletBalanceAfter = currentBalance,
                    OrderDate = recentOrder.OrderDate,
                    ScheduledFor = recentOrder.ScheduledFor
                };
            }

            if (currentBalance < price)
                throw new InsufficientBalanceException(price, currentBalance);

            // ✅ All writes inside a single transaction (UnitOfWork)
            OrderCreationResponseDto response = null;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 3. Deduct balance via the dedicated transaction method
                var transaction = await _walletService.DebitWalletAsync(
                    userId, 
                    price, 
                    $"Reorder of past order #{orderId}"
                );

                // 4. Create new order scheduled for tomorrow
                // Tomorrow 7:00 AM IST. Note: Storing as Unspecified kind to match existing behavior, 
                // or just DateTime.UtcNow for creation.
                var tomorrowIst = _time.TomorrowIst;
                var localDeliveryTime = tomorrowIst.ToDateTime(new TimeOnly(7, 0));
                var scheduledDeliveryTime = _time.ToUtc(localDeliveryTime);

                var newOrder = new Order
                {
                    UserId = userId,
                    UserMealId = pastOrder.UserMealId,
                    DeliveryAddressId = pastOrder.DeliveryAddressId,
                    IsPrepared = false,
                    OrderStatus = OrderStatus.Confirmed,
                    TotalPrice = price,
                    OrderDate = _time.UtcNow,
                    ScheduledFor = scheduledDeliveryTime,
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow
                };

                await _orderRepository.AddAsync(newOrder);
                await _unitOfWork.SaveChangesAsync();

                var walletBalanceAfter = await _walletService.GetUserBalanceAsync(userId);

                response = new OrderCreationResponseDto
                {
                    OrderId = newOrder.OrderId,
                    MealName = "Reorder",
                    OrderStatus = "Confirmed",
                    UserMealId = newOrder.UserMealId ?? 0,
                    TotalPrice = price,
                    WalletBalanceBefore = currentBalance,
                    WalletBalanceAfter = walletBalanceAfter,
                    OrderDate = newOrder.OrderDate,
                    ScheduledFor = newOrder.ScheduledFor
                };
            });

            return response;
        }
    }
}
