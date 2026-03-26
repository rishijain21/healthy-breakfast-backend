using System;
using System.Threading.Tasks;
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;

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

        public OrderService(
            IOrderRepository orderRepository,
            IMealService mealService,
            IWalletTransactionService walletService,
            IUserMealService userMealService,
            IUserMealIngredientService userMealIngredientService,
            IUserAddressRepository userAddressRepository,
            IUnitOfWork unitOfWork) // ✅ ADDED
        {
            _orderRepository = orderRepository;
            _mealService = mealService;
            _walletService = walletService;
            _userMealService = userMealService;
            _userMealIngredientService = userMealIngredientService;
            _userAddressRepository = userAddressRepository;
            _unitOfWork = unitOfWork;
        }

        // ✅ SECURE: Create order with userId from JWT token
        public async Task<int> CreateOrderAsync(CreateOrderDto dto, int userId)
        {
            var entity = new Order
            {
                UserId = userId,
                OrderStatus = OrderStatus.Pending,
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

        // ✅ EXISTING: Simple methods for backward compatibility
        public async Task<IEnumerable<OrderDto>> GetAllOrderHistoryAsync()
        {
            var orders = await _orderRepository.GetAllAsync();
            
            return orders.Select(order => new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            }).OrderByDescending(o => o.CreatedAt);
        }

        public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId)
        {
            var orders = await _orderRepository.GetByUserIdAsync(userId);
            
            return orders.Select(order => new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            }).OrderByDescending(o => o.CreatedAt);
        }

        // ✅ NEW: Enhanced methods with rich data
        public async Task<IEnumerable<EnhancedOrderHistoryDto>> GetUserOrdersWithDetailsAsync(int userId)
        {
            var orders = await _orderRepository.GetUserOrdersWithDetailsAsync(userId);
            return MapToEnhancedDto(orders);
        }

        public async Task<IEnumerable<EnhancedOrderHistoryDto>> GetAllOrderHistoryWithDetailsAsync()
        {
            var orders = await _orderRepository.GetAllOrdersWithDetailsAsync();
            return MapToEnhancedDto(orders);
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

                    // ✅ Meal name: UserMeal → ScheduledOrder snapshot → fallback
                    MealId = order.UserMeal?.MealId ?? order.SourceScheduledOrder?.MealId ?? 0,
                    MealName = order.UserMeal?.MealName
                            ?? order.SourceScheduledOrder?.MealName
                            ?? "Order",

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

        // ✅ SECURE: Create order from meal builder using userId from JWT token
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId)
        {
            // ✅ FIX 6: Guard against soft-deleted meals
            // Check this before the address validation so we fail fast on invalid meal
            if (dto.MealId > 0)  // MealId = 0 means custom meal with no catalogue entry
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
                throw new InvalidOperationException(
                    "Please add a delivery address before placing an order. Go to Profile → Manage Addresses."
                );
            }

            // ✅ Validate location is serviceable
            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new InvalidOperationException(
                    $"Sorry, we don't currently deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"}. " +
                    $"Please update your delivery address to a serviceable location."
                );
            }

            // ✅ STEP 1: Calculate meal price and validate ingredients
            // ✅ Use override price from scheduled order, or recalculate
            MealPriceResponseDto priceCalculation;
            if (dto.OverrideTotalPrice.HasValue)
            {
                // Use the price agreed at order creation time (from scheduled order snapshot)
                priceCalculation = new MealPriceResponseDto
                {
                    // ✅ Use meal name from scheduled order snapshot, or fallback
                    MealName = dto.MealName ?? "Scheduled Order",
                    TotalPrice = dto.OverrideTotalPrice.Value,
                    IngredientBreakdown = dto.SelectedIngredients.Select(i => new IngredientBreakdownDto
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
                    MealId = dto.MealId,
                    SelectedIngredients = dto.SelectedIngredients
                });
            }

            // ✅ STEP 2: Check wallet balance
            var walletBalanceBefore = await _walletService.GetUserBalanceAsync(userId);
            var hasSufficientBalance = await _walletService.HasSufficientBalanceAsync(userId, priceCalculation.TotalPrice);
            
            if (!hasSufficientBalance)
            {
                throw new InvalidOperationException(
                    $"Insufficient wallet balance. Required: ₹{priceCalculation.TotalPrice}, Available: ₹{walletBalanceBefore}"
                );
            }

            // ✅ All writes inside a single transaction
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // ✅ STEP 3: Create UserMeal record (UserId passed separately, not in DTO)
                var userMealDto = new CreateUserMealDto
                {
                    MealId = dto.MealId,
                    MealName = priceCalculation.MealName,
                    TotalPrice = priceCalculation.TotalPrice,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUserMealId = await _userMealService.CreateUserMealAsync(userMealDto, userId);

                // ✅ STEP 4: Create UserMealIngredient records for each selected ingredient
                foreach (var selectedIngredient in dto.SelectedIngredients)
                {
                    var ingredientDetail = priceCalculation.IngredientBreakdown
                        .FirstOrDefault(i => i.IngredientId == selectedIngredient.IngredientId);
                    
                    if (ingredientDetail != null)
                    {
                        var userMealIngredient = new CreateUserMealIngredientDto
                        {
                            UserMealId = createdUserMealId,
                            IngredientId = selectedIngredient.IngredientId,
                            Quantity = selectedIngredient.Quantity,
                            UnitPrice = ingredientDetail.UnitPrice,
                            TotalPrice = ingredientDetail.TotalPrice
                        };

                        await _userMealIngredientService.CreateUserMealIngredientAsync(userMealIngredient);
                    }
                }

                // ✅ STEP 5: Create Order with UserMeal link AND DeliveryAddressId
                var order = new Order
                {
                    UserId = userId,
                    UserMealId = createdUserMealId,
                    DeliveryAddressId = primaryAddress.Id,
                    OrderStatus = OrderStatus.Pending,
                    TotalPrice = priceCalculation.TotalPrice,
                    OrderDate = DateTime.UtcNow,
                    ScheduledFor = dto.ScheduledFor ?? DateTime.UtcNow.AddHours(2),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _orderRepository.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // ✅ STEP 6: Process payment via wallet
                var walletTransaction = await _walletService.DebitWalletAsync(
                    userId,
                    priceCalculation.TotalPrice,
                    $"Order #{order.OrderId} - {priceCalculation.MealName}"
                );

                // ✅ STEP 7: Confirm order after successful payment
                order.OrderStatus = OrderStatus.Confirmed;
                order.UpdatedAt = DateTime.UtcNow;
                _orderRepository.Update(order);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitAsync();   // ✅ All or nothing

                var walletBalanceAfter = await _walletService.GetUserBalanceAsync(userId);

                // ✅ STEP 8: Return comprehensive order creation response
                return new OrderCreationResponseDto
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
            }
            catch
            {
                await _unitOfWork.RollbackAsync();  // ✅ Undo everything on failure
                throw;
            }
        }

        // ✅ NEW: Overload with explicit DeliveryAddressId (for scheduled order confirmation)
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto, 
            int userId, 
            int? deliveryAddressId)
        {
            // ✅ If deliveryAddressId is provided, use it directly
            // Otherwise, fall back to getting primary address
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
                    throw new InvalidOperationException(
                        "Please add a delivery address before placing an order. Go to Profile → Manage Addresses."
                    );
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

            // ✅ STEP 1: Calculate meal price and validate ingredients
            // ✅ Use override price from scheduled order, or recalculate
            MealPriceResponseDto priceCalculation;
            if (dto.OverrideTotalPrice.HasValue)
            {
                // Use the price agreed at order creation time (from scheduled order snapshot)
                priceCalculation = new MealPriceResponseDto
                {
                    // ✅ Use meal name from scheduled order snapshot, or fallback
                    MealName = dto.MealName ?? "Scheduled Order",
                    TotalPrice = dto.OverrideTotalPrice.Value,
                    IngredientBreakdown = dto.SelectedIngredients.Select(i => new IngredientBreakdownDto
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
                    MealId = dto.MealId,
                    SelectedIngredients = dto.SelectedIngredients
                });
            }

            // ✅ STEP 2: Check wallet balance
            var walletBalanceBefore = await _walletService.GetUserBalanceAsync(userId);
            var hasSufficientBalance = await _walletService.HasSufficientBalanceAsync(userId, priceCalculation.TotalPrice);
            
            if (!hasSufficientBalance)
            {
                throw new InvalidOperationException(
                    $"Insufficient wallet balance. Required: ₹{priceCalculation.TotalPrice}, Available: ₹{walletBalanceBefore}"
                );
            }

            // ✅ All writes inside a single transaction
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // ✅ STEP 3: Create UserMeal record
                var userMealDto = new CreateUserMealDto
                {
                    MealId = dto.MealId,
                    MealName = priceCalculation.MealName,
                    TotalPrice = priceCalculation.TotalPrice,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUserMealId = await _userMealService.CreateUserMealAsync(userMealDto, userId);

                // ✅ STEP 4: Create UserMealIngredient records for each selected ingredient
                // Note: DB schema has no UnitPrice/TotalPrice on UserMealIngredients —
                // only UserMealId, IngredientId, Quantity. We save what the schema supports.
                foreach (var selectedIngredient in dto.SelectedIngredients)
                {
                    await _userMealIngredientService.CreateUserMealIngredientAsync(
                        new CreateUserMealIngredientDto
                        {
                            UserMealId   = createdUserMealId,
                            IngredientId = selectedIngredient.IngredientId,
                            Quantity     = selectedIngredient.Quantity
                            // UnitPrice/TotalPrice omitted — not in DB schema
                        });
                }

                // ✅ STEP 5: Create Order with explicit DeliveryAddressId
                var order = new Order
                {
                    UserId = userId,
                    UserMealId = createdUserMealId,
                    DeliveryAddressId = addressIdToUse.Value,
                    OrderStatus = OrderStatus.Pending,
                    TotalPrice = priceCalculation.TotalPrice,
                    OrderDate = DateTime.UtcNow,
                    ScheduledFor = dto.ScheduledFor ?? DateTime.UtcNow.AddHours(2),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _orderRepository.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // ✅ STEP 6: Process payment via wallet
                var walletTransaction = await _walletService.DebitWalletAsync(
                    userId,
                    priceCalculation.TotalPrice,
                    $"Order #{order.OrderId} - {priceCalculation.MealName}"
                );

                // ✅ STEP 7: Confirm order after successful payment
                order.OrderStatus = OrderStatus.Confirmed;
                order.UpdatedAt = DateTime.UtcNow;
                _orderRepository.Update(order);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitAsync();   // ✅ All or nothing

                var walletBalanceAfter = await _walletService.GetUserBalanceAsync(userId);

                // ✅ STEP 8: Return comprehensive order creation response
                return new OrderCreationResponseDto
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
            }
            catch
            {
                await _unitOfWork.RollbackAsync();  // ✅ Undo everything on failure
                throw;
            }
        }

        // ✅ NEW: Dedicated method for confirming scheduled orders
        // No catalogue lookup, no UserMeal creation, no price recalculation
        // Everything comes from the snapshot
        // NOTE: Wallet deduction is now done atomically in ScheduledOrderService.ConfirmAllScheduledOrdersAsync
        // before calling this method, to prevent race conditions
        public async Task<int> ConfirmScheduledOrderAsync(ScheduledOrder scheduledOrder)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Create Order directly from snapshot (wallet already deducted atomically)
                var order = new Order
                {
                    UserId            = scheduledOrder.UserId,
                    UserMealId        = null,
                    ScheduledOrderId  = scheduledOrder.ScheduledOrderId,
                    DeliveryAddressId = scheduledOrder.DeliveryAddressId!.Value,
                    OrderStatus       = OrderStatus.Confirmed,
                    TotalPrice        = scheduledOrder.TotalPrice,

                    // ✅ ScheduledFor comes from DATE column → Kind=Unspecified → force UTC
                    ScheduledFor = DateTime.SpecifyKind(scheduledOrder.ScheduledFor, DateTimeKind.Utc),

                    OrderDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _orderRepository.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // ✅ Write the wallet transaction record so the ledger matches the balance deduction
                // that was already applied atomically in ConfirmAllScheduledOrdersAsync
                await _walletService.WriteTransactionRecordAsync(
                    scheduledOrder.UserId,
                    scheduledOrder.TotalPrice,
                    "Debit",
                    $"Order #{order.OrderId} - {scheduledOrder.MealName}"
                );

                await _unitOfWork.CommitAsync();

                return order.OrderId;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
