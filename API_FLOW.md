# SOVVA BACKEND — API FLOW DOCUMENTATION

**Generated:** 2026-05-22
**Covers:** All controller endpoints, authorization, request/response patterns

---

## CONTROLLER INVENTORY

| Controller | Route Prefix | Auth | Endpoints |
|-----------|-------------|------|-----------|
| `AuthController` | `/api/Auth` | Mixed | 4 |
| `DashboardController` | `/api/Dashboard` | JWT | 2 |
| `MealController` | `/api/Meal` | JWT | 8 |
| `IngredientController` | `/api/Ingredient` | JWT | 8 |
| `IngredientCategoryController` | `/api/IngredientCategory` | JWT | 5 |
| `OrderController` | `/api/Order` | JWT | 7 |
| `ScheduledOrderController` | `/api/ScheduledOrder` | JWT | 7 |
| `SubscriptionController` | `/api/Subscription` | JWT | 7 |
| `UserController` | `/api/User` | JWT | 8 |
| `UserAddressController` | `/api/UserAddress` | JWT | 6 |
| `UserMealController` | `/api/UserMeal` | JWT | 5 |
| `WalletTransactionController` | `/api/WalletTransactions` | JWT | 8 |
| `AdminController` | `/api/Admin` | Admin | 8+ |
| `DebugController` | `/api/Debug` | Admin | 3 |

---

## KEY BUSINESS FLOWS

### 1. User Registration + First Login

```
POST /api/Auth/register
  Body: { name, email, phone }
  │
  ├── JWT sub → Supabase AuthId (Guid)
  ├── Check existing UserAuthMapping
  │     ├── If exists + soft-deleted → reactivate
  │     └── If exists + active → return existing user
  │
  ├── Create User entity (Name, Email, Phone, Role=Customer)
  ├── Create UserAuthMapping (AuthId → UserId)
  └── Return UserDto
```

### 2. Create Order (Real-Time Meal Builder)

```
POST /api/Order/create-from-builder
  Body: { mealId, selectedIngredients: [{ingredientId, quantity}], scheduledFor? }
  Auth: JWT (sovva_user_id)
  │
  ├── Validate meal exists (soft-delete guard)
  ├── Validate primary address exists + serviceable
  ├── Calculate meal price (MealService.CalculateMealPriceAsync)
  │     ├── Load Meal.BasePrice
  │     └── Sum(ingredient.Price * quantity)
  ├── Check wallet balance (ledger SUM)
  ├── TRANSACTION:
  │     ├── Create UserMeal record
  │     ├── Create UserMealIngredient records
  │     ├── Create Order (status=Pending)
  │     ├── DebitWalletAsync (advisory lock + balance check + INSERT ledger)
  │     ├── Transition order → Confirmed
  │     └── Return OrderCreationResponseDto (with balance before/after)
  └── ApiResponse.Ok(response)
```

### 3. Reorder Flow

```
POST /api/Order/reorder/{orderId}
  Auth: JWT (sovva_user_id)
  │
  ├── Validate order exists + belongs to user
  ├── Validate UserMealId not null
  ├── Idempotency: Check recent order with same UserMealId (30s window)
  ├── Check wallet balance
  ├── TRANSACTION:
  │     ├── DebitWalletAsync
  │     ├── Create new Order (tomorrow 7 AM IST, status=Confirmed)
  │     └── Return OrderCreationResponseDto
  └── ApiResponse.Ok(response)
```

### 4. Create Subscription

```
POST /api/Subscription
  Body: { mealId, frequency, startDate, endDate, weeklySchedule? }
  Auth: JWT (sovva_user_id)
  │
  ├── Validate user + meal + primary address
  ├── TRANSACTION:
  │     ├── Check for duplicate active subscription (same user + meal)
  │     ├── Get or create UserMeal
  │     │     ├── If UserMeal doesn't exist → create + copy MealOptions→UserMealIngredients
  │     │     └── If exists → reuse
  │     ├── Validate dates (start < end)
  │     ├── Validate weekly schedule (if frequency=Weekly)
  │     ├── Create Subscription entity
  │     ├── Create SubscriptionSchedule entries (for Weekly)
  │     ├── Create first ScheduledOrder (async, non-blocking on failure)
  │     └── Return SubscriptionDto (with optional warning)
  └── ApiResponse.Ok(response)
```

### 5. Scheduled Order Lifecycle

```
CREATION (3 paths):
  ├── Path A: Subscription nightly job → GenerateScheduledOrdersFromSubscriptionsAsync
  ├── Path B: Subscription create → CreateFirstScheduledOrderAsync
  └── Path C: Manual/Direct → CreateScheduledOrderAsync

MODIFICATION:
  PUT /api/ScheduledOrder/{id}/modify
  ├── Validates CanModify = true
  ├── Validates OrderStatus = Scheduled
  ├── Replaces ingredients, recalculates price
  └── Updates entity

DUPLICATION:
  POST /api/ScheduledOrder/{id}/duplicate
  ├── Copies all fields except ID
  ├── Sets new delivery date (tomorrow)
  └── Creates new ScheduledOrder

CANCELLATION:
  DELETE /api/ScheduledOrder/{id}
  ├── Only if not processed
  └── Hard deletes entity + ingredients

CONFIRMATION (midnight job):
  ├── AtomicDebitAsync → single SQL INSERT WHERE balance >= amount
  ├── Create Order row (status=Confirmed)
  ├── MarkAsProcessedAsync → UPDATE ScheduledOrder
  └── Link: ScheduledOrder.ConfirmedOrderId → Order.OrderId
```

### 6. Wallet Operations

```
CREDIT:
  POST /api/WalletTransactions/topup
  Body: { amount, description? }
  ├── Min topup: WalletConstants.MinTopUpAmount
  ├── Max balance: WalletConstants.MaxWalletBalance
  ├── Advisory lock → balance check → INSERT ledger
  └── Return WalletTransactionDto

ADMIN CREDIT:
  POST /api/Admin/wallet/credit
  Body: { userId, amount, description }
  ├── Bypasses min topup check (IsAdminCredit=true)
  └── Same flow as regular credit

DEBIT (automatic):
  ├── Real-time orders: OrderService → DebitWalletAsync (advisory lock path)
  └── Midnight job: ScheduledOrderService → AtomicDebitAsync (single SQL path)

BALANCE QUERY:
  GET /api/WalletTransactions/my-balance
  └── Returns ledger SUM(Credits) - SUM(Debits)
```

### 7. Dashboard Bootstrap (Single API Call)

```
GET /api/Dashboard/summary
  Auth: JWT (sovva_user_id)
  │
  └── Returns COMPOSITE DTO:
        ├── Profile (UserDto, cached 5min)
        ├── WalletBalance (ledger SUM)
        ├── RecentTransactions (last 20)
        ├── ActiveSubscriptions (filtered by date + active)
        └── TomorrowOrders (ScheduledOrders for tomorrow IST)
```

---

## AUTHORIZATION MODEL

```
Claim-Based Authorization:
  ├── "sovva_role" = "Admin"    → [Authorize(Roles = "Admin")]
  ├── "sovva_role" = "Customer" → Standard endpoints
  └── "sovva_user_id" = int     → Used to scope queries to current user

Controller-Level:
  ├── [Authorize]                → Requires valid JWT
  ├── [Authorize(Roles = "Admin")] → Admin-only endpoints
  └── [AllowAnonymous]           → Health checks, Swagger

User Scoping Pattern:
  var userId = User.GetSovvaUserId();  // Extension method reading sovva_user_id claim
  // All queries scoped: WHERE UserId = @userId
```

---

## RESPONSE FORMAT

All API responses use a consistent wrapper:

```json
// Success
{
  "success": true,
  "data": { ... },
  "message": null
}

// Error
{
  "success": false,
  "code": "INSUFFICIENT_BALANCE",
  "message": "You don't have enough balance to complete this order."
}
```

**Error Codes:**
| Code | HTTP Status | Source Exception |
|------|------------|-----------------|
| `INSUFFICIENT_BALANCE` | 400 | `InsufficientBalanceException` |
| `NO_DELIVERY_ADDRESS` | 400 | `AddressNotFoundException` |
| `DUPLICATE_SUBSCRIPTION` | 409 | `DuplicateSubscriptionException` |
| `NOT_FOUND` | 404 | `OrderNotFoundException`, `KeyNotFoundException` |
| `CONFLICT` | 409 | `OrderAlreadyPreparedException`, `DbUpdateConcurrencyException` |
| `VALIDATION_ERROR` | 400 | `FluentValidation.ValidationException` |
| `INVALID_OPERATION` | 400 | `InvalidOperationException` |
| `INVALID_ARGUMENT` | 400 | `ArgumentException` |
| `FORBIDDEN` | 403 | `UnauthorizedAccessException` |
| `ACCOUNT_DELETED` | 401 | AuthMiddleware soft-delete check |
| `INTERNAL_ERROR` | 500 | Default fallback |

---

## RATE LIMITING

| Policy | Limit | Window | Applied To |
|--------|-------|--------|-----------|
| `auth` | 10 requests | 1 minute | Auth endpoints |
| `default` | 100 requests | 1 minute | All other endpoints |

---

## PAGINATION PATTERN

```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50
}
```

Applied to: Orders, WalletTransactions, Users (admin), Meals, Subscriptions.
Default pageSize: 50 (clamped to max 50 for most, max 100 for transactions).
