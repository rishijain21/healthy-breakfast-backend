# API_CONTRACTS.md — Sovva Backend Endpoint Reference

> Every endpoint from every controller. For AI context.
> Auth levels: **Public** = no token, **User** = valid JWT, **Admin** = JWT + sovva_role=Admin

---

## AuthController — `/api/auth`

### GET /api/auth/check-user-exists
  Auth: Public (rate limited: 10/min)
  Request: query `email: string` (required)
  Response: `{ exists: bool }`
  Errors: 400 — email missing | 500 — internal error

### POST /api/auth/register
  Auth: User (rate limited: 10/min)
  Request:
    - authId: Guid (required — must match JWT `sub` claim)
    - name: string (required)
    - email: string (required)
    - phone: string (required)
  Response: `{ success: bool, data: { user: UserDto, isNewUser: bool }, message: string }`
  Errors: 403 — authId mismatch with token | 409 — email already registered | 500 — internal

### POST /api/auth/login
  Auth: Public (rate limited: 10/min)
  Request: `{ email: string, password: string }`
  Response: `{ success: bool, data: { message: string } }`
  Notes: Test endpoint only — real auth is via Supabase

---

## UsersController — `/api/users`

### GET /api/users
  Auth: Admin
  Response: `UserDto[]`

### POST /api/users
  Auth: Admin
  Request: CreateUserDto `{ name: string, email: string, phone: string }`
  Response: 201 Created (Location header)
  Errors: 400 — validation

### GET /api/users/{id}
  Auth: Admin
  Response: `UserDto { userId, name, email, phone, role, walletBalance, createdAt, updatedAt }`
  Errors: 404

### GET /api/users/profile
  Auth: User
  Response: `UserDto`
  Errors: 401 — invalid token | 404 — user not found

### GET /api/users/dashboard-summary
  Auth: User (cached 60s per Authorization header)
  Response: `DashboardSummaryDto { profile, walletBalance, recentTransactions, activeSubscriptions, tomorrowOrders }`
  Errors: 404 — user not found | 500

### PUT /api/users/profile
  Auth: User
  Request: `{ name: string? }`
  Response: `UserDto`
  Errors: 400 — empty name | 404 — user not found

### PATCH /api/users/{id}/role
  Auth: Admin
  Request: `{ role: string }` (must be "User" or "Admin")
  Response: `{ message: string }`
  Errors: 400 — invalid role or self-demotion | 404 — user not found

---

## OrdersController — `/api/orders`

### GET /api/orders/history
  Auth: User
  Response: `EnhancedOrderHistoryDto[]` (sorted by createdAt desc)
  - Each: `{ orderId, userId, orderStatus, totalPrice, scheduledFor, mealId, mealName, nutritionalInfo: { totalCalories, totalProtein, totalFiber }, ingredients: OrderIngredientDetailDto[] }`

### GET /api/orders/users/me/orders
  Auth: User
  Response: `EnhancedOrderHistoryDto[]` (same as /history)

### GET /api/orders/users/me/orders/simple
  Auth: User
  Response: `OrderDto[] { orderId, userId, orderStatus, totalPrice, createdAt, updatedAt }`

### POST /api/orders
  Auth: User
  Request: `{ totalPrice: decimal }`
  Response: 201 Created (Location → /api/orders/{id})

### GET /api/orders/{id}
  Auth: User (ownership check: order.UserId must match JWT userId)
  Response: `OrderDto`
  Errors: 401 | 403 — not your order | 404

### POST /api/orders/create-from-meal-builder
  Auth: User
  Request: `CreateOrderFromMealBuilderDto`
    - mealId: int (required)
    - mealName: string? (optional)
    - selectedIngredients: `[{ ingredientId: int, quantity: int, unitPrice: decimal?, totalPrice: decimal? }]` (required)
    - overrideTotalPrice: decimal? (optional — for scheduled order snapshots)
    - scheduledFor: DateTime? (optional — defaults to +2h)
  Response: `OrderCreationResponseDto { orderId, userMealId, mealName, totalPrice, walletBalanceBefore, walletBalanceAfter, orderStatus, transactionId, orderDate, scheduledFor, ingredientBreakdown }`
  Errors: 400 — insufficient balance, no address, meal unavailable, not serviceable

---

## SubscriptionsController — `/api/subscriptions`

### GET /api/subscriptions
  Auth: User
  Response: `SubscriptionDto[]` (current user's subscriptions)

### GET /api/subscriptions/{id}
  Auth: User (ownership check)
  Response: `SubscriptionDto { subscriptionId, userId, userMealId, frequency, startDate, endDate, active, nextScheduledDate, userName, mealName, mealPrice, weeklySchedule: [{ dayOfWeek: int, quantity: int }] }`
  Errors: 403 — not yours | 404

### GET /api/subscriptions/user/me
  Auth: User
  Response: `SubscriptionDto[]`

### GET /api/subscriptions/active
  Auth: User
  Response: `SubscriptionDto[]` (filtered: active=true)

### POST /api/subscriptions
  Auth: User
  Request: `CreateSubscriptionDto`
    - mealId: int (required)
    - frequency: enum (Daily=0, Weekly=1, Monthly=2) (required)
    - startDate: DateOnly (required)
    - endDate: DateOnly (required)
    - active: bool (default true)
    - weeklySchedule: `[{ dayOfWeek: int 0-6, quantity: int }]`? (required if Weekly)
  Response: 201 `SubscriptionDto`
  Errors: 400 — invalid args | 403 — not your meal | 409 — duplicate subscription

### PUT /api/subscriptions/{id}
  Auth: User (ownership check)
  Request: `UpdateSubscriptionDto`
    - frequency: enum? (optional)
    - startDate: DateOnly? (optional)
    - endDate: DateOnly? (optional)
    - active: bool? (optional)
    - weeklySchedule: `[{ dayOfWeek, quantity }]`? (optional)
  Response: `SubscriptionDto`
  Errors: 400 — validation | 403 | 404

### DELETE /api/subscriptions/{id}
  Auth: User
  Response: 204 No Content
  Errors: 404
  Notes: Keeps processed ScheduledOrders, deletes pending ones

### PATCH /api/subscriptions/{id}/activate
  Auth: User (ownership check)
  Response: `{ message: string }`
  Notes: Also generates tomorrow's order immediately

### PATCH /api/subscriptions/{id}/deactivate
  Auth: User (ownership check)
  Response: `{ message: string }`
  Notes: Also cancels tomorrow's pending order

### POST /api/subscriptions/sync-dates
  Auth: Admin
  Response: `{ success: bool, message: string, timestamp: DateTime }`

---

## ScheduledOrdersController — `/api/scheduledorders`

### POST /api/scheduledorders/create-from-meal-builder
  Auth: User
  Request: `CreateScheduledOrderDto`
    - mealId: int
    - mealName: string
    - selectedIngredients: `[{ ingredientId, quantity, unitPrice, totalPrice }]`
    - scheduledFor: DateOnly
    - deliveryTimeSlot: string
    - totalPrice: decimal
  Response: `ScheduledOrderResponseDto { scheduledOrderId, mealName, scheduledFor, deliveryTimeSlot, totalPrice, orderStatus, canModify, ingredients }`
  Errors: 400 — invalid operation | 401 | 500

### POST /api/scheduledorders/{id}/duplicate
  Auth: User
  Response: `ScheduledOrderResponseDto`
  Errors: 400 | 401 | 500

### GET /api/scheduledorders/tomorrow
  Auth: User
  Response: `ScheduledOrderResponseDto[]` (filtered: status=Scheduled only)

### PUT /api/scheduledorders/{id}/modify
  Auth: User
  Request: `ModifyScheduledOrderDto { selectedIngredients: [{ ingredientId, quantity }] }`
  Response: `{ message: string }`
  Errors: 400 — past cutoff | 401 — not your order | 500

### DELETE /api/scheduledorders/{id}/cancel
  Auth: User
  Response: `{ message: string }`
  Errors: 400 — past cutoff | 401 — not your order | 500

### GET /api/scheduledorders/time-until-midnight
  Auth: Public
  Response: `int` (minutes until midnight IST)

### POST /api/scheduledorders/process-today-manual
  Auth: Admin
  Response: `ProcessOrdersResponseDto { success, message, deliveryDate, ordersFound, ordersPending, ordersAlreadyConfirmed, ordersConfirmed, ordersFailed, timestamp, note }`

### POST /api/scheduledorders/process-yesterday-manual
  Auth: Admin
  Response: `ProcessOrdersResponseDto`

### POST /api/scheduledorders/process-tomorrow-manual
  Auth: Admin
  Response: `ProcessOrdersResponseDto`

---

## MealsController — `/api/meals`

### GET /api/meals/public
  Auth: Public
  Response: `MealDto[] { mealId, mealName, description, basePrice, imageUrl, isComplete }`

### GET /api/meals/{id}/details
  Auth: User
  Response: `MealWithDetailsDto { mealId, mealName, description, basePrice, imageUrl, options: [{ optionId, optionName, ingredients: [{ ingredientId, ingredientName, price, calories, protein, fiber, iconEmoji, available }] }] }`
  Errors: 404

### POST /api/meals/batch-details
  Auth: User
  Request: `{ mealIds: int[] }` (max 20)
  Response: `MealWithDetailsDto[]`
  Errors: 400 — empty or >20

### POST /api/meals
  Auth: Admin
  Request: `CreateMealDto { mealName, description, basePrice }`
  Response: 201 Created

### GET /api/meals/{id}
  Auth: Admin
  Response: `MealDto`
  Errors: 404

### POST /api/meals/calculate-price
  Auth: User
  Request: `{ mealId: int, selectedIngredients: [{ ingredientId, quantity }] }`
  Response: `MealPriceResponseDto { mealName, totalPrice, ingredientBreakdown: [{ ingredientId, quantity, unitPrice, totalPrice }] }`
  Errors: 400

### POST /api/meals/validate-selection
  Auth: User
  Request: `{ mealId: int, selectedIngredients: [{ ingredientId, quantity }] }`
  Response: `{ isValid: bool, message: string }`

### POST /api/meals/nutritional-summary
  Auth: User
  Request: `[{ ingredientId: int, quantity: int }]`
  Response: `{ totalCalories, totalProtein, totalFiber, ingredientCount }`

### GET /api/meals/admin/all
  Auth: Admin
  Request: query `page: int (default 1)`, `pageSize: int (default 20)`
  Response: `PagedResult<AdminMealListDto> { items, totalCount, page, pageSize, totalPages }`

### GET /api/meals/admin/{id}
  Auth: Admin
  Response: `AdminMealDetailDto`
  Errors: 404

### POST /api/meals/admin
  Auth: Admin
  Request: `AdminCreateMealDto { mealName, description, basePrice, imageUrl?, options: [{ optionName, ingredients: [{ ingredientId }] }] }`
  Response: 201 `{ mealId, message }`

### PUT /api/meals/admin/{id}
  Auth: Admin
  Request: `UpdateMealDto { mealName?, description?, basePrice?, imageUrl?, isComplete? }`
  Response: `{ message }`
  Errors: 404

### DELETE /api/meals/admin/{id}
  Auth: Admin
  Response: `{ message }`
  Errors: 404
  Notes: Soft delete (sets IsDeleted=true, global query filter hides it)

### PATCH /api/meals/admin/{id}/status
  Auth: Admin
  Request: `{ isComplete: bool }`
  Response: `{ id, isComplete, message }`
  Errors: 404

### GET /api/meals/admin/categories-with-ingredients
  Auth: Admin
  Response: `CategoryWithIngredientsDto[] { categoryId, categoryName, ingredients }`

### GET /api/meals/admin/all-with-details
  Auth: User
  Response: `AdminMealListDto[]`

### POST /api/meals/admin/{id}/image
  Auth: Admin
  Request: `multipart/form-data` — field `image: IFormFile` (jpg/png/webp, max 10MB)
  Response: `{ imageUrl, message }`
  Errors: 400 — no image, wrong type, too large | 404 — meal not found

### DELETE /api/meals/admin/{id}/image
  Auth: Admin
  Response: `{ message }`
  Errors: 404

---

## WalletTransactionsController — `/api/wallettransactions`

### GET /api/wallettransactions/my-balance
  Auth: User
  Response: `{ balance: decimal, userId: int }`

### GET /api/wallettransactions/my-transactions
  Auth: User
  Response: `WalletTransactionDto[] { transactionId, userId, amount, type, description, referenceType, createdAt }`

### POST /api/wallettransactions/topup
  Auth: User
  Request: `{ amount: decimal, referenceType: string?, referenceId: string? }`
  Response: `WalletTransactionDto`
  Errors: 400 — amount <= 0

### GET /api/wallettransactions/check-balance
  Auth: User
  Request: query `amount: decimal` (required)
  Response: `{ hasSufficientBalance: bool, currentBalance: decimal, requiredAmount: decimal, shortfall: decimal }`

### GET /api/wallettransactions/admin/all
  Auth: Admin
  Response: `WalletTransactionDto[]`

### GET /api/wallettransactions/admin/user/{userId}/balance
  Auth: Admin
  Response: `{ userId, balance }`

### GET /api/wallettransactions/admin/user/{userId}/transactions
  Auth: Admin
  Response: `WalletTransactionDto[]`

### POST /api/wallettransactions/admin/user/{userId}/credit
  Auth: Admin
  Request: `{ amount: decimal }`
  Response: `WalletTransactionDto`
  Errors: 400 — amount <= 0

### GET /api/wallettransactions/admin/{id}
  Auth: Admin
  Response: `WalletTransactionDto`
  Errors: 404

---

## UserAddressesController — `/api/useraddresses`

### GET /api/useraddresses
  Auth: User
  Response: `UserAddressDetailDto[] { id, userId, label, addressLine1, addressLine2, city, state, pincode, isPrimary, serviceableLocationId, serviceableLocation: { area, locality, landmark } }`

### GET /api/useraddresses/{id}
  Auth: User (ownership check)
  Response: `UserAddressDetailDto`
  Errors: 403 | 404

### GET /api/useraddresses/primary
  Auth: User
  Response: `UserAddressDetailDto`
  Errors: 404 — no primary address set

### POST /api/useraddresses
  Auth: User
  Request: `CreateUserAddressDto { label, addressLine1, addressLine2?, city, state, pincode, serviceableLocationId, isPrimary? }`
  Response: 201 `UserAddressDetailDto`
  Errors: 400 — invalid location

### PUT /api/useraddresses/{id}
  Auth: User (ownership check)
  Request: `UpdateUserAddressDto { label?, addressLine1?, addressLine2?, city?, state?, pincode?, serviceableLocationId?, isPrimary? }`
  Response: `UserAddressDetailDto`
  Errors: 403 | 404

### PUT /api/useraddresses/{id}/set-primary
  Auth: User (ownership check)
  Response: `UserAddressDetailDto`
  Errors: 403 | 404

### DELETE /api/useraddresses/{id}
  Auth: User (ownership check)
  Response: `{ message }`
  Errors: 400 — can't delete primary | 403 | 404

### GET /api/useraddresses/{id}/validate
  Auth: User
  Response: `ValidateAddressDto { isValid, message }`

---

## ServiceableLocationsController — `/api/serviceablelocations`

### GET /api/serviceablelocations
  Auth: Public
  Response: `ServiceableLocationDto[] { id, city, area, locality, landmark, pincode, isActive, deliveryFee, estimatedDeliveryMinutes }`

### GET /api/serviceablelocations/{id}
  Auth: Public
  Response: `ServiceableLocationDto`
  Errors: 404

### GET /api/serviceablelocations/search/pincode/{pincode}
  Auth: Public
  Response: `ServiceableLocationDto[]` (active only)

### GET /api/serviceablelocations/search/city/{city}
  Auth: Public
  Response: `ServiceableLocationDto[]` (active only)

### GET /api/serviceablelocations/search
  Auth: Public
  Request: query `query: string?`, `city: string?`, `area: string?`
  Response: `ServiceableLocationDto[]`
  Notes: Free-text search across city/area/locality/landmark/pincode. Falls back to city+area, then all active.

### GET /api/serviceablelocations/validate/{locationId}
  Auth: Public
  Response: `ValidateAddressDto { isValid, message }`

### GET /api/serviceablelocations/admin/all
  Auth: Admin
  Response: `ServiceableLocationDto[]` (includes inactive)

### POST /api/serviceablelocations
  Auth: Admin
  Request: `CreateServiceableLocationDto { city, area, locality?, landmark?, pincode, isActive?, deliveryFee?, estimatedDeliveryMinutes? }`
  Response: 201 `ServiceableLocationDto`

### PUT /api/serviceablelocations/{id}
  Auth: Admin
  Request: `UpdateServiceableLocationDto { city?, area?, locality?, landmark?, pincode?, isActive?, deliveryFee?, estimatedDeliveryMinutes? }`
  Response: `ServiceableLocationDto`
  Errors: 404

### DELETE /api/serviceablelocations/{id}
  Auth: Admin
  Response: `{ message }`
  Errors: 404
  Notes: Soft-deletes if addresses are linked (sets IsActive=false)

---

## IngredientsController — `/api/ingredients`

### GET /api/ingredients
  Auth: User
  Response: `IngredientDto[] { ingredientId, ingredientName, categoryId, price, calories, protein, fiber, iconEmoji, available }`

### GET /api/ingredients/category/{categoryId}
  Auth: User
  Response: `IngredientDto[]`

### GET /api/ingredients/{id}
  Auth: User
  Response: `IngredientDto`
  Errors: 404

### POST /api/ingredients
  Auth: Admin
  Request: `CreateIngredientDto { ingredientName, categoryId, price, calories?, protein?, fiber?, iconEmoji? }`
  Response: 201 `{ ingredientId, message }`

### PUT /api/ingredients/{id}
  Auth: Admin
  Request: `UpdateIngredientDto { ingredientName?, categoryId?, price?, calories?, protein?, fiber?, iconEmoji?, available? }`
  Response: `{ message }`
  Errors: 404

### PATCH /api/ingredients/{id}/toggle-availability
  Auth: Admin
  Response: `{ message, ingredientId, available }`
  Errors: 404

### DELETE /api/ingredients/{id}
  Auth: Admin
  Response: `{ message }`
  Errors: 400 — in use | 404

---

## IngredientCategoriesController — `/api/ingredientcategories`

### GET /api/ingredientcategories
  Auth: Public
  Response: `IngredientCategoryDto[] { categoryId, categoryName }`

### POST /api/ingredientcategories
  Auth: Admin
  Request: `{ categoryName: string }`
  Response: 201 Created

### GET /api/ingredientcategories/{id}
  Auth: Public
  Response: `IngredientCategoryDto`
  Errors: 404

---

## KitchenController — `/api/kitchen`

### GET /api/kitchen/today
  Auth: Admin
  Response: `KitchenOrderDto[]` (orders confirmed for today's delivery)

### GET /api/kitchen/tomorrow
  Auth: Admin
  Response: `KitchenOrderDto[]` (orders confirmed for tomorrow's delivery)

### GET /api/kitchen/date/{dateString}
  Auth: Admin
  Request: path `dateString: string` (format YYYY-MM-DD)
  Response: `KitchenOrderDto[]`
  Errors: 400 — invalid date format

### PUT /api/kitchen/{orderId}/mark-prepared
  Auth: Admin
  Response: `{ success, message, orderId, timestamp }`
  Errors: 400 — invalid operation | 500

### GET /api/kitchen/stats/today
  Auth: Admin
  Response: kitchen stats object (totalOrders, prepared, pending, etc.)

### GET /api/kitchen/stats/tomorrow
  Auth: Admin
  Response: kitchen stats object

---

## UserMealsController — `/api/usermeals`

### POST /api/usermeals
  Auth: User
  Request: `CreateUserMealDto { mealId, mealName, totalPrice, selectedIngredients?: [{ ingredientId, quantity }] }`
  Response: `{ userMealId, message }`
  Notes: Also triggers scheduled order generation if user has active subscription for this meal

### GET /api/usermeals/{id}
  Auth: User
  Response: `UserMealDto`
  Errors: 404

### GET /api/usermeals/my-meals
  Auth: User
  Response: `UserMealDto[]`

---

## UserMealIngredientsController — `/api/usermealingredients`

### POST /api/usermealingredients
  Auth: User
  Request: `{ userMealId: int, ingredientId: int, quantity: int }`
  Response: 201 Created

### GET /api/usermealingredients/{id}
  Auth: User
  Response: `UserMealIngredientDto`
  Errors: 404

---

## MealOptionsController — `/api/mealoptions`

### POST /api/mealoptions
  Auth: Admin
  Request: `CreateMealOptionDto { mealId, optionName }`
  Response: 201 Created

---

## MealOptionIngredientsController — `/api/mealoptioningredients`

### POST /api/mealoptioningredients
  Auth: Admin
  Request: `CreateMealOptionIngredientDto { mealOptionId, ingredientId }`
  Response: 201 Created

---

## Non-Controller Endpoints (Program.cs)

### GET /
  Auth: Public
  Response: `{ service: "Sovva API", version: "1.0", status: "Running", environment, timestamp }`

### GET /ping
  Auth: Public
  Response: `"pong"`

### GET /health/live
  Auth: Public
  Response: `{ status, timestamp, checks }` (liveness — no DB)

### GET /health/ready
  Auth: Public
  Response: `{ status, timestamp, checks }` (readiness — includes DB)

### GET /health
  Auth: Public
  Response: `{ status, timestamp, checks }` (combined)

### /hangfire/*
  Auth: Basic Auth (separate credentials)
  Notes: Hangfire dashboard — not JWT protected
