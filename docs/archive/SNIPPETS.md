# SNIPPETS.md — Sovva Code Patterns & Templates

> Extracted from actual codebase. Copy these patterns when adding new features.

---

## JSON Serialization Notes

- Enums are serialized as **strings** (via `JsonStringEnumConverter`) so the API returns `"Daily"`, `"Weekly"`, `"Monthly"`, etc.
- This avoids frontend/backend numeric enum mismatches.

---

## 1. Domain Entity

**Source**: `Sovva.Domain/Entities/Subscription.cs`

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sovva.Domain.Enums;

namespace Sovva.Domain.Entities
{
    public class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("UserMeal")]
        public int UserMealId { get; set; }

        public int? DeliveryAddressId { get; set; }

        public SubscriptionFrequency Frequency { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool Active { get; set; }
        public DateOnly? NextScheduledDate { get; set; }

        public DateTime CreatedAt { get; set; }   // Auto-set by TimestampInterceptor
        public DateTime UpdatedAt { get; set; }   // Auto-set by TimestampInterceptor

        // Navigation properties
        public User User { get; set; } = null!;
        public UserMeal UserMeal { get; set; } = null!;
        public UserAddress? DeliveryAddress { get; set; }
        public ICollection<SubscriptionSchedule> WeeklySchedule { get; set; } = new List<SubscriptionSchedule>();
    }
}
```

**Pattern notes**:
- PK named `{EntityName}Id`
- FK uses `[ForeignKey("NavigationProp")]`
- Nullable FK for optional relationships (`int?`)
- `CreatedAt`/`UpdatedAt` always present — `TimestampInterceptor` sets them
- Navigation properties: required ones use `= null!;`, optional use `?`

---

## 2. Repository Interface

**Source**: `Sovva.Application/Interfaces/ISubscriptionRepository.cs`

```csharp
namespace Sovva.Application.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<IEnumerable<Subscription>> GetAllAsync();
        Task<Subscription?> GetByIdAsync(int subscriptionId);
        Task<IEnumerable<Subscription>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync();
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription> UpdateAsync(Subscription subscription);
        Task<bool> DeleteAsync(int subscriptionId);
        Task UpdateBatchAsync(IEnumerable<Subscription> subscriptions);
        // Domain-specific queries
        Task<Subscription?> GetAnyActiveSubscriptionByMealIdAsync(int userId, int mealId);
    }
}
```

---

## 3. Repository Implementation

**Source**: `Sovva.Infrastructure/Repositories/SubscriptionRepository.cs`

```csharp
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sovva.Infrastructure.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppTimeProvider _time;

        public SubscriptionRepository(AppDbContext context, IAppTimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<IEnumerable<Subscription>> GetAllAsync()
        {
            return await _context.Subscriptions
                .AsNoTracking()                    // ← Read-only queries: always AsNoTracking
                .Include(s => s.User)              // ← Eager load navigations
                .Include(s => s.UserMeal)
                    .ThenInclude(um => um.Meal)    // ← Nested eager load
                .Include(s => s.WeeklySchedule)
                .ToListAsync();
        }

        public async Task<Subscription> CreateAsync(Subscription entity)
        {
            _context.Subscriptions.Add(entity);    // ← No manual timestamp
            await _context.SaveChangesAsync();     // ← TimestampInterceptor runs here
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Subscriptions
                .Include(s => s.WeeklySchedule)    // ← Include children for cascade
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);
            if (entity == null) return false;

            _context.Subscriptions.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
```

**Pattern notes**:
- Always `AsNoTracking()` for read-only queries
- Eager load navigations with `Include/ThenInclude`
- Each repo calls `_context.SaveChangesAsync()` directly
- Use `IAppTimeProvider` for IST date comparisons (e.g., active subscription checks)

---

## 4. Service Interface

**Source**: `Sovva.Application/Interfaces/IOrderService.cs`

```csharp
namespace Sovva.Application.Interfaces
{
    public interface IOrderService
    {
        Task<long> CreateOrderAsync(CreateOrderDto dto, int userId);
        Task<OrderDto?> GetOrderByIdAsync(long id);
        Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId);
        Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto, int userId);
    }
}
```

---

## 5. Service Implementation (with UnitOfWork)

**Source**: `Sovva.Application/Services/OrderService.cs`

```csharp
namespace Sovva.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUnitOfWork _unitOfWork;
        // ... other dependencies

        public OrderService(
            IOrderRepository orderRepository,
            IUnitOfWork unitOfWork /* ... */)
        {
            _orderRepository = orderRepository;
            _unitOfWork = unitOfWork;
        }

        // ✅ Pattern: UnitOfWork for multi-step writes
        public async Task<OrderCreationResponseDto> CreateOrderFromMealBuilderAsync(
            CreateOrderFromMealBuilderDto dto, int userId)
        {
            // Step 1: Validations (before transaction)
            var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
            if (primaryAddress == null)
                throw new InvalidOperationException("Please add a delivery address...");

            // Step 2: Transaction for all writes
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // ... create entities, debit wallet, etc.
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return response;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        // ✅ Pattern: Manual DTO mapping (private method)
        private static OrderDto MapToDto(Order order)
        {
            return new OrderDto
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            };
        }
    }
}
```

---

## 6. Service Implementation (without UnitOfWork)

**Source**: `Sovva.Application/Services/SubscriptionService.cs`

```csharp
// ✅ Pattern: Simple CRUD without UnitOfWork
public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionInternalDto dto)
{
    // 1. Validations — throw typed exceptions
    var user = await _userLoader.GetUserWithAuthMappingAsync(dto.UserId);
    if (user == null)
        throw new ArgumentException("User not found");

    // 2. Duplicate check — throw InvalidOperationException for 409 Conflict
    var existing = await _subscriptionRepository.GetAnyActiveSubscriptionByMealIdAsync(
        dto.UserId, dto.MealId);
    if (existing != null)
        throw new InvalidOperationException("You already have an active subscription...");

    // 3. Security check — throw UnauthorizedAccessException for 403
    if (userMeal.UserId != dto.UserId)
        throw new UnauthorizedAccessException("You can only subscribe to your own meals");

    // 4. Business logic
    var subscription = new Subscription { /* ... */ };
    var created = await _subscriptionRepository.CreateAsync(subscription);

    // 5. Map and return
    return MapToDto(created);
}
```

---

## 7. Controller

**Source**: `Sovva.WebAPI/Controllers/OrdersController.cs`

```csharp
using Sovva.Application.DTOs;
using Sovva.Application.Interfaces;
using Sovva.WebAPI.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sovva.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        // ✅ Pattern: GET with JWT userId
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<EnhancedOrderHistoryDto>>> GetAllOrderHistory()
        {
            try
            {
                var userId = User.GetSovvaUserId();
                if (userId is null) return Unauthorized("User not authenticated");

                var result = await _orderService.GetUserOrdersWithDetailsAsync(userId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllOrderHistory");
                return StatusCode(500, new { error = "An error occurred" });
            }
        }

        // ✅ Pattern: GET by ID with ownership check
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            var userId = User.GetSovvaUserId();
            if (userId is null) return Unauthorized();
            if (order.UserId != userId.Value) return Forbid();

            return Ok(order);
        }

        // ✅ Pattern: POST with typed exception handling
        [HttpPost("create-from-meal-builder")]
        public async Task<ActionResult<OrderCreationResponseDto>> CreateFromMealBuilder(
            [FromBody] CreateOrderFromMealBuilderDto dto)
        {
            try
            {
                var userId = User.GetSovvaUserId();
                if (userId is null) return Unauthorized(new { message = "User not authenticated" });

                var result = await _orderService.CreateOrderFromMealBuilderAsync(dto, userId.Value);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            // No generic catch — GlobalExceptionMiddleware handles unexpected errors
        }
    }
}
```

---

## 8. DTO Patterns

```csharp
// ✅ Create DTO — what the client sends
public class CreateSubscriptionDto
{
    public int MealId { get; set; }
    public SubscriptionFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool Active { get; set; } = true;
    public List<WeeklyScheduleDto>? WeeklySchedule { get; set; }
}

// ✅ Internal DTO — enriched by controller before passing to service
public class CreateSubscriptionInternalDto
{
    public int UserId { get; set; }   // From JWT, NOT from client
    public int MealId { get; set; }
    public int UserMealId { get; set; } // Set by service after lookup
    // ... rest same as CreateSubscriptionDto
}

// ✅ Response DTO — what the client receives
public class SubscriptionDto
{
    public int SubscriptionId { get; set; }
    public int UserId { get; set; }
    public string MealName { get; set; } = string.Empty;
    public decimal MealPrice { get; set; }
    // ... flattened from navigation properties
}

// ✅ Update DTO — nullable fields for partial updates
public class UpdateSubscriptionDto
{
    public SubscriptionFrequency? Frequency { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool? Active { get; set; }
}
```

---

## 9. FluentValidation

**Source**: `Sovva.Application/Validators/CreateSubscriptionDtoValidator.cs`

```csharp
using FluentValidation;
using Sovva.Application.DTOs;

namespace Sovva.Application.Validators
{
    public class CreateSubscriptionDtoValidator : AbstractValidator<CreateSubscriptionDto>
    {
        public CreateSubscriptionDtoValidator()
        {
            RuleFor(x => x.MealId).GreaterThan(0);
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).NotEmpty()
                .GreaterThan(x => x.StartDate)
                .WithMessage("End date must be after start date");
        }
    }
}
```

---

## 10. DI Registration (Program.cs)

```csharp
// ✅ Pattern: Always register interface → implementation, Scoped lifetime
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ✅ Singletons for stateless utilities
builder.Services.AddSingleton<IAppTimeProvider, AppTimeProvider>();
builder.Services.AddSingleton<TimestampInterceptor>();

// ✅ HttpClient for external services
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();
```

---

## 11. Exception → HTTP Status Mapping

```csharp
// In Services (throw these):
throw new ArgumentException("Meal not found");                    // → 400
throw new InvalidOperationException("Insufficient balance...");   // → 400
throw new UnauthorizedAccessException("Not your meal");           // → 403
throw new KeyNotFoundException("Order not found");                // → 404

// In Controllers (catch these):
catch (InvalidOperationException ex) => BadRequest(new { error = ex.Message });
catch (ArgumentException ex) => BadRequest(new { error = ex.Message });
catch (UnauthorizedAccessException) => Forbid();
// Unhandled → GlobalExceptionMiddleware → 500 with ApiErrorDto
```

---

## 12. Batch Operations Pattern

**Source**: `SubscriptionService.UpdateNextScheduledDatesAsync()`

```csharp
// ✅ Pattern: Collect in memory, then batch update
var itemsToUpdate = new List<Subscription>();

foreach (var item in activeItems)
{
    var newValue = CalculateNewValue(item);
    if (item.CurrentValue != newValue)
    {
        item.CurrentValue = newValue;
        itemsToUpdate.Add(item);
    }
}

// Single DB call for all updates
if (itemsToUpdate.Count > 0)
{
    await _repository.UpdateBatchAsync(itemsToUpdate);
}
```

---

## 13. Idempotency Guard

**Source**: `OrderService.ConfirmScheduledOrderAsync()`

```csharp
// ✅ Pattern: Check if already processed before creating
var existing = await _orderRepository.GetByScheduledOrderIdAsync(scheduledOrder.ScheduledOrderId);
if (existing != null)
{
    return existing.OrderId;  // Already processed — return existing
}

// Safe to create new
var order = new Order { /* ... */ };
await _orderRepository.AddAsync(order);
await _orderRepository.SaveChangesAsync();
return order.OrderId;
```

---

## 14. N+1 Query Prevention

**Source**: `SubscriptionService.BuildScheduledOrder()`

```csharp
// ❌ BAD: N+1 — one query per ingredient
foreach (var item in ingredients)
{
    var ingredient = await _ingredientRepository.GetByIdAsync(item.IngredientId);
}

// ✅ GOOD: Batch load + dictionary lookup
var ingredientIds = ingredientList.Select(i => i.IngredientId).ToList();
var allIngredients = await _ingredientRepository.GetByIdsAsync(ingredientIds);
var ingredientMap = allIngredients.ToDictionary(i => i.IngredientId);

foreach (var item in ingredientList)
{
    if (ingredientMap.TryGetValue(item.IngredientId, out var ingredient))
    {
        // Use ingredient directly — no DB call
    }
}
```
