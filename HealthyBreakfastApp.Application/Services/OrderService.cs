using System;
using System.Threading.Tasks;
using HealthyBreakfastApp.Application.DTOs;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Domain.Entities;
using HealthyBreakfastApp.Domain.Enums;

namespace HealthyBreakfastApp.Application.Services
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
            return orders.Select(order => new EnhancedOrderHistoryDto
            {
                OrderId = order.OrderId,
                MealId = order.UserMeal?.MealId ?? 0,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                ScheduledFor = order.ScheduledFor,
                
                // ✅ IMPROVED: Better fallback text for legacy orders
                MealName = order.UserMeal?.MealName ?? "Legacy Order",
                
                NutritionalInfo = new NutritionalInfoDto
                {
                    TotalCalories = order.UserMeal?.UserMealIngredients?
                        .Sum(umi => umi.Ingredient.Calories * umi.Quantity) ?? 0,
                    TotalProtein = order.UserMeal?.UserMealIngredients?
                        .Sum(umi => umi.Ingredient.Protein * umi.Quantity) ?? 0,
                    TotalFiber = order.UserMeal?.UserMealIngredients?
                        .Sum(umi => umi.Ingredient.Fiber * umi.Quantity) ?? 0
                },
                
                // ✅ ENHANCED: Better handling for missing ingredients with fallback
                Ingredients = order.UserMeal?.UserMealIngredients?.Select(umi => new OrderIngredientDetailDto
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
                }).ToList() ?? new List<OrderIngredientDetailDto>
                {
                    // ✅ FALLBACK: Show placeholder ingredient for legacy orders
                    new OrderIngredientDetailDto
                    {
                        IngredientId = 0,
                        IngredientName = "Historical Order Items",
                        Quantity = 1,
                        UnitPrice = order.TotalPrice,
                        TotalPrice = order.TotalPrice,
                        IconEmoji = "📜",
                        Calories = 0,
                        Protein = 0,
                        Fiber = 0
                    }
                }
            });
        }

        // ✅ SECURE: Create order from meal builder using userId from JWT token
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(CreateOrderFromMealBuilderDto dto, int userId)
        {
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
            var priceCalculation = await _mealService.CalculateMealPriceAsync(new MealPriceCalculationDto
            {
                MealId = dto.MealId,
                SelectedIngredients = dto.SelectedIngredients
            });

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
                var address = await _userAddressRepository.GetByIdAsync(addressIdToUse.Value);
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
            var priceCalculation = await _mealService.CalculateMealPriceAsync(new MealPriceCalculationDto
            {
                MealId = dto.MealId,
                SelectedIngredients = dto.SelectedIngredients
            });

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
    }
}
