# FRONTEND_INTEGRATION.md — Dashboard ↔ Backend Contract

> Traces every API call the Angular dashboard component makes, what shape it expects,
> and where backend DTOs don't match. For AI context — be complete, not pretty.

---

## 1. Dashboard Bootstrap Flow

The dashboard does **NOT** call APIs directly for most data. It reads from `AppStore`,
which makes **one single API call** on login:

```
AppStore.load()
  → GET /api/Users/dashboard-summary     ← single aggregated call
```

Then dashboard reads store signals:
- `store.userName` (profile)
- `store.balance` (wallet)
- `store.upcomingOrders` (tomorrow's scheduled orders)
- `store.activeSubscriptions` (subscriptions)
- `store.currentStreak`, `store.bestStreak`, `store.loyaltyPoints`
- `store.averageCarbs`, `store.averageFats`
- `store.totalTransactions`

### Additional API calls from `ngOnInit()`:
1. `SubscriptionService.syncActiveSubscriptions()` → `GET /api/Subscriptions/active`
2. `MealService.getAllMealsForMenu()` → `GET /api/Meals/public`
3. `MealService.getMealsBatch(ids)` → `POST /api/Meals/batch-details`
4. `LocationService.getPrimaryAddress()` → `GET /api/UserAddresses/primary`

---

## 2. Endpoint-by-Endpoint Contract

### 2.1 `GET /api/Users/dashboard-summary`

**Caller**: `AppStore.load()` — runs once on login, cached 5 min (TTL)
**Backend response** (`DashboardSummaryDto`):

| Backend field (C#) | Type | Frontend field (TS) | Notes |
|---------------------|------|---------------------|-------|
| `Profile` | `UserDto` | `summary.profile` | See profile mapping below |
| `WalletBalance` | `decimal` | `summary.walletBalance` | ✅ Match |
| `RecentTransactions` | `WalletTransactionDto[]` | `summary.recentTransactions` | ✅ Match |
| `ActiveSubscriptions` | `SubscriptionDto[]` | `summary.activeSubscriptions` | ⚠️ See §3.2 |
| `TomorrowOrders` | `ScheduledOrderResponseDto[]` | `summary.tomorrowOrders` | ⚠️ See §3.3 |
| `TotalTransactions` | `int` | `summary.totalTransactions` | ✅ Match |
| `CurrentStreak` | `int` | `summary.currentStreak` | ✅ Match |
| `BestStreak` | `int` | `summary.bestStreak` | ✅ Match |
| `LoyaltyPoints` | `int` | `summary.loyaltyPoints` | ✅ Match |
| `AverageCarbs` | `decimal` | `summary.averageCarbs` | ✅ Match |
| `AverageFats` | `decimal` | `summary.averageFats` | ✅ Match |

**Profile mapping** (inside DashboardSummaryDto.Profile → `UserDto`):

| Backend `UserDto` | Frontend `UserProfile` | Status |
|--------------------|------------------------|--------|
| `UserId` | `userId` | ✅ (camelCase auto) |
| `Name` | `name` | ✅ |
| — | `fullName` | ⚠️ Frontend expects `fullName`, backend sends `Name`. AppStore uses `p?.fullName \|\| p?.name` — works but `fullName` is always undefined |
| `Email` | `email` | ✅ |
| `Role` | `role` | ✅ |
| `Phone` | `phone` | ✅ |
| `WalletBalance` | — | Not mapped to profile (mapped to `summary.walletBalance` instead) |

---

### 2.2 `GET /api/Subscriptions/active`

**Caller**: `SubscriptionService.syncActiveSubscriptions()` — runs on dashboard init
**Response**: `SubscriptionDto[]`

| Backend `SubscriptionDto` | Frontend `SubscriptionDto` | Status |
|---------------------------|---------------------------|--------|
| `SubscriptionId` | `subscriptionId` | ✅ |
| `UserId` | `userId` | ✅ |
| `UserMealId` | `userMealId` | ✅ |
| — | `mealId` | ❌ **MISSING from backend** — frontend declares `mealId: number` but backend `SubscriptionDto` does NOT have it. Frontend falls back: `sub.mealId ?? sub.userMealId` |
| `Frequency` | `frequency` | ⚠️ Backend sends enum int (0=Daily, 1=Weekly, 2=Monthly), frontend enum is (1=Daily, 7=Weekly, 30=Monthly). **Values don't match for Weekly/Monthly.** |
| `StartDate` | `startDate` | ✅ (DateOnly → string) |
| `EndDate` | `endDate` | ✅ |
| `Active` | `active` | ✅ |
| `NextScheduledDate` | `nextScheduledDate` | ✅ |
| `UserName` | `userName` | ✅ |
| `MealName` | `mealName` | ✅ |
| `MealPrice` | `mealPrice` | ✅ |
| `WeeklySchedule` | `weeklySchedule` | ✅ |
| — | `imageUrl` / `mealImageUrl` | ❌ **MISSING** — dashboard reads `sub.imageUrl \|\| sub.mealImageUrl` for subscription meal cards but backend doesn't send either |
| — | `name` / `price` | ❌ Dashboard also tries `sub.name` and `sub.price` as fallbacks (line 118-121) |

---

### 2.3 `GET /api/ScheduledOrders/tomorrow`

**Caller**: `AppStore.refreshOrders()` and `ScheduledOrderService.loadTomorrowOrders()`
**Response**: `ScheduledOrderResponseDto[]`

| Backend `ScheduledOrderResponseDto` | Frontend `ScheduledOrderResponse` | Status |
|--------------------------------------|-----------------------------------|--------|
| `ScheduledOrderId` | `scheduledOrderId` | ✅ |
| `MealName` | `mealName` | ✅ |
| `MealId` | `mealId` | ✅ |
| `MealImageUrl` | `mealImageUrl` | ✅ |
| `ScheduledFor` | `scheduledFor` | ⚠️ Backend sends `DateTime` (UTC timestamptz), frontend parses as date string. Works but timezone edge cases possible. |
| `DeliveryTimeSlot` | `deliveryTimeSlot` | ✅ |
| `TotalPrice` | `totalPrice` | ✅ |
| `OrderStatus` | `orderStatus` | ✅ (string: "Scheduled", "Confirmed", etc.) |
| `CanModify` | `canModify` | ✅ |
| `CreatedAt` | `createdAt` | ✅ |
| `ExpiresAt` | `expiresAt` | ✅ |
| `NutritionalSummary` | `nutritionalSummary` | ✅ `{ totalCalories, totalProtein, itemCount }` |
| `Ingredients[]` | `ingredients[]` | ✅ |
| `SubscriptionId` | — | ⚠️ Backend sends it, frontend interface doesn't declare it (silently ignored) |

---

### 2.4 `GET /api/Meals/public`

**Caller**: `MealService.getAllMealsForMenu()` — cached with `shareReplay(1)`
**Response**: `MealListItemDto[]` (backend) → transformed to `MenuItem[]` (frontend)

| Backend `MealDto` | Frontend `MealListItemDto` | Status |
|--------------------|---------------------------|--------|
| `MealId` | `mealId` | ✅ |
| `MealName` | `mealName` | ✅ |
| `Description` | `description` | ✅ |
| `BasePrice` | `basePrice` | ✅ |
| `ImageUrl` | `imageUrl` | ✅ |
| `IsComplete` | — | ❌ Frontend doesn't use it (backend filters incomplete in admin, public endpoint likely only sends complete) |

Frontend transforms to `MenuItem`:
```
mealId    → id
mealName  → name
basePrice → price
imageUrl  → imageUrl (converted to absolute URL if relative)
           image (emoji derived from name)
           tags (generated from name + price heuristics)
           nutrition: { calories: 0, protein: 0, carbs: 0, fats: 0 }  ← placeholder until enriched
```

---

### 2.5 `POST /api/Meals/batch-details`

**Caller**: `MealService.getMealsBatch(mealIds)` — background enrichment
**Request**: `{ mealIds: number[] }`
**Response**: `MealDetailDto[]`

| Backend `MealWithDetailsDto` | Frontend `MealDetailDto` | Status |
|------------------------------|--------------------------|--------|
| `MealId` | `mealId` | ✅ |
| `MealName` | `mealName` | ✅ |
| `ApproxCalories` | `approxCalories` | ✅ |
| `ApproxProtein` | `approxProtein` | ✅ |
| `ApproxCarbs` | `approxCarbs` | ✅ |
| `ApproxFats` | `approxFats` | ✅ |
| `Options[].Ingredients[]` | `mealOptions[].ingredients[]` | ✅ |

---

### 2.6 `GET /api/UserAddresses/primary`

**Caller**: `LocationService.getPrimaryAddress()` — prefetched on dashboard init
**Response**: `UserAddressDetailDto` or `null`

| Backend | Frontend `UserAddressDetailDto` | Status |
|---------|-------------------------------|--------|
| `Id` | `id` | ✅ |
| `UserId` | `userId` | ✅ |
| `FlatNumber` | `flatNumber` | ✅ |
| `Wing` | `wing` | ✅ |
| `Block` | `block` | ✅ |
| `Floor` | `floor` | ✅ |
| `Label` | `label` | ✅ |
| `IsPrimary` | `isPrimary` | ✅ |
| `IsActive` | `isActive` | ✅ |
| `ServiceableLocation` | `serviceableLocation` | ✅ (nested object) |
| `AdditionalInstructions` | `additionalInstructions` | ✅ |
| — | `completeAddress` | ⚠️ Frontend expects this field — backend may or may not compute it |

---

### 2.7 `POST /api/ScheduledOrders/create-from-meal-builder`

**Caller**: `ScheduledOrderService.createScheduledOrder()` — from "Add" button on featured meals
**Request** (`CreateScheduledOrderRequest`):

| Frontend sends | Backend `CreateScheduledOrderDto` expects | Status |
|----------------|------------------------------------------|--------|
| `mealName` | `MealName` | ✅ |
| `mealId` | `MealId` | ✅ |
| `mealImageUrl` | `MealImageUrl` | ⚠️ Backend may not have this field on the DTO — verify |
| `mealPrice` | — | ⚠️ Frontend sends `mealPrice`, backend may use `TotalPrice` calculated from ingredients |
| `selectedIngredients` | `SelectedIngredients` | ✅ |
| `scheduledFor` | `ScheduledFor` | ✅ (sent as ISO string with +05:30 offset) |
| `deliveryTimeSlot` | `DeliveryTimeSlot` | ✅ |
| `nutritionalSummary` | `NutritionalSummary` | ⚠️ Backend DTO may not have this — stored as JSON text in `NutritionalSummary` column |

---

### 2.8 `DELETE /api/ScheduledOrders/{id}/cancel`

**Caller**: `ScheduledOrderService.cancelScheduledOrder()` — from "-" button on featured meals
**Response**: `{ message: string }`

---

### 2.9 `GET /api/WalletTransactions/my-balance` (refresh path)

**Caller**: `AppStore.refreshBalance()` — after wallet top-up
**Response**: `{ balance: decimal, userId: int }`

| Backend sends | Frontend expects (`WalletBalance`) | Status |
|---------------|-----------------------------------|--------|
| `balance` | `balance` | ✅ |
| `userId` | `userId` | ✅ |
| — | `authId` | ⚠️ Frontend `BalanceResponse` declares `authId: string` but backend `my-balance` endpoint may not send it. Frontend code doesn't use it. |

---

## 3. Field Name Mismatches — Summary

### 3.1 ❌ Critical Mismatches (will cause bugs)

| Issue | Frontend expects | Backend sends | Impact |
|-------|-----------------|---------------|--------|
| Subscription `mealId` | `mealId: number` | **Not in SubscriptionDto** | Frontend uses `sub.mealId ?? sub.userMealId` as fallback — subscribed meal badge check may fail if userMealId ≠ mealId |
| Subscription Frequency enum | `Daily=1, Weekly=7, Monthly=30` | `Daily=0, Weekly=1, Monthly=2` | All frequency comparisons will be wrong. Dashboard displays incorrect frequency text. |

### 3.2 ⚠️ Soft Mismatches (frontend has fallbacks)

| Issue | Frontend expects | Backend sends | Impact |
|-------|-----------------|---------------|--------|
| Profile `fullName` | `fullName` | `Name` | AppStore uses `p?.fullName \|\| p?.name` — works, but `fullName` always undefined |
| Subscription `imageUrl` | `imageUrl` or `mealImageUrl` | Neither | Dashboard subscription card shows no image. Line 121: `sub.imageUrl \|\| sub.mealImageUrl \|\| null` |
| Subscription `name` / `price` | `sub.name`, `sub.price` | `MealName`, `MealPrice` | Dashboard line 118-120 tries `sub.mealName \|\| sub.name` — works via fallback |
| Balance `authId` | `authId: string` | Not always sent | Frontend interface declares it but never reads it |
| `ScheduledFor` timezone | Expects date string | Backend sends UTC DateTime | `formatDeliveryDate()` parses as `new Date(dateStr)` which uses local TZ — may show wrong date near midnight IST |

### 3.3 ℹ️ Backend sends, frontend ignores

| Field | Endpoint | Notes |
|-------|----------|-------|
| `SubscriptionId` | ScheduledOrders/tomorrow | Not in frontend interface, JSON property silently ignored |
| `IsComplete` | Meals/public | Not mapped to MenuItem |

---

## 4. CORS Configuration

**Location**: `Program.cs` line 216-249

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                var host = uri.Host.ToLower();

                // ✅ Local development
                if (host == "localhost" || host == "127.0.0.1")
                    return true;

                // ✅ Production frontend
                if (host == "sovva.vercel.app")
                    return true;

                // ✅ All Vercel preview deployments
                if (host.EndsWith(".vercel.app"))
                    return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### Allowed Origins
| Origin | Allowed |
|--------|---------|
| `localhost:*` (any port) | ✅ |
| `127.0.0.1:*` | ✅ |
| `sovva.vercel.app` | ✅ |
| `*.vercel.app` (any subdomain) | ✅ |
| Any other domain | ❌ |

### Headers & Methods
- **Any header** — `AllowAnyHeader()`
- **Any method** — `AllowAnyMethod()` (GET, POST, PUT, PATCH, DELETE, OPTIONS)
- **Credentials** — `AllowCredentials()` (required for JWT cookies)

### ⚠️ CORS Notes
- No explicit `WithExposedHeaders` — custom response headers (if any) won't be readable by frontend JS
- `AllowCredentials()` + `SetIsOriginAllowed()` instead of `AllowAnyOrigin()` — this is correct (browsers reject `*` with credentials)
- No rate limiting on CORS preflight — each `OPTIONS` hits the lambda check

---

## 5. Dashboard API Call Sequence (Timeline)

```
ngOnInit()
│
├── 1. SubscriptionService.syncActiveSubscriptions()
│       → GET /api/Subscriptions/active            [User auth]
│
├── 2. MealService.getAllMealsForMenu()
│       → GET /api/Meals/public                     [Public / cached]
│       └── then: MealService.getMealsBatch(ids)
│              → POST /api/Meals/batch-details      [User auth / background]
│
└── 3. LocationService.getPrimaryAddress()
        → GET /api/UserAddresses/primary            [User auth]

AppStore.load() (called by app-level guard, NOT by dashboard):
    → GET /api/Users/dashboard-summary              [User auth / cached 5min]
```

**Total HTTP calls on dashboard load**: 4-5 (dashboard-summary is usually cached from app init)

---

## 6. Response Caching Behavior

| Endpoint | Cache | TTL | Invalidation |
|----------|-------|-----|-------------|
| `/Users/dashboard-summary` | `AppStore._lastFetchedAt` | 5 min | `AppStore.load()` skips if within TTL |
| `/Subscriptions/active` | `SubscriptionService.activeSubscriptions$` | Session | `invalidateSubscriptionCache()` on create/delete |
| `/Meals/public` | `MealService.mealsCache$` | Session | `clearMealsCache()` |
| `/Meals/batch-details` | `MealService.mealDetailsCache` (Map) | 5 min per meal | `clearAllCaches()` |
| `/UserAddresses/primary` | `dashboard.cachedAddress` | Page lifetime | Cleared on address error or navigation |
| `/Users/dashboard-summary` (HTTP) | `ResponseCache` 60s | 60s (server) | Varies by `Authorization` header |

---

## 7. Audit Findings (Phase 6 Dashboard Review — 2026-04-22)

### 7.1 Known Frontend Bugs
<!-- Add known bugs here -->

### 7.2 Fields Frontend Uses But Backend Doesn't Send Yet

**Source**: Code review of `DashboardService.GetDashboardSummaryAsync()` (L81-101)

#### ❌ Placeholder Fields (always return `0`)

These fields exist in `DashboardSummaryDto` but are **never populated** in `DashboardService`:

| Field | DTO Type | Populated? | Frontend Signal | Action |
|-------|----------|------------|-----------------|--------|
| `totalTransactions` | `int` | ❌ Never set | `store.totalTransactions` | **Fix**: Add `TotalTransactions = transactions.Count()` |
| `currentStreak` | `int` | ❌ Never set | `store.currentStreak` | **Decision**: Implement or remove |
| `bestStreak` | `int` | ❌ Never set | `store.bestStreak` | **Decision**: Implement or remove |
| `loyaltyPoints` | `int` | ❌ Never set | `store.loyaltyPoints` | **Decision**: Implement or remove |
| `averageCarbs` | `decimal` | ❌ Never set | `store.averageCarbs` | **Decision**: Implement or remove |
| `averageFats` | `decimal` | ❌ Never set | `store.averageFats` | **Decision**: Implement or remove |

> `totalTransactions` is a trivial fix (1 line). The others require new service logic or should be removed from the DTO if not planned.

#### ⚠️ Missing Fields in `activeSubscriptions` (Dashboard Mapping)

`DashboardService.GetActiveSubscriptionsAsync()` (L144-173) maps subscriptions but **omits**:

| Missing Field | Frontend Uses It For | Fix |
|--------------|---------------------|-----|
| `mealId` | Meal detail navigation (`ViewMeal(mealId)`) | Add `MealId = s.UserMeal?.MealId ?? 0` to mapping |
| `imageUrl` | Subscription card image display | Add `ImageUrl = s.UserMeal?.Meal?.ImageUrl ?? ""` (requires `.Include(s => s.UserMeal.Meal)`) |

> **Note**: `tomorrowOrders` correctly includes `mealId` and `mealImageUrl` from the scheduled order snapshot. Only `activeSubscriptions` has this gap.

#### ⚠️ Cache Inconsistency

| Field | Source | Cache TTL | Risk |
|-------|--------|-----------|------|
| `walletBalance` (top-level) | `WalletTransactionRepository.GetUserBalanceAsync()` | None (fresh) | ✅ Always accurate |
| `profile.walletBalance` | `UserRepository.GetByIdAsync()` → `user.WalletBalance` | **5 minutes** | ⚠️ Stale after top-up/order |

**Frontend should use `walletBalance` (top-level), not `profile.walletBalance`**, to avoid showing stale balances after wallet operations.

### 7.3 Planned API Changes
<!-- Add upcoming backend changes that will affect frontend -->
