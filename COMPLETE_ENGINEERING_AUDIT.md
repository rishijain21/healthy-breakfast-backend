# SOVVA BACKEND — SYSTEM FLOW DOCUMENTATION

**Generated:** 2026-05-22
**Codebase:** .NET 9 / ASP.NET Core / PostgreSQL / Supabase Auth / Hangfire
**Architecture:** Clean Architecture (4 layers)

---

## 1. HIGH-LEVEL ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Sovva.WebAPI                                │
│  Program.cs → ServiceCollectionExtensions → WebApplicationExtensions│
│  Controllers (14) │ Middleware (3) │ Services (1) │ Infrastructure  │
└────────────────────────┬────────────────────────────────────────────┘
                         │ depends on
┌────────────────────────▼────────────────────────────────────────────┐
│                      Sovva.Application                              │
│  Services (18) │ Interfaces (36) │ DTOs │ Validators │ Exceptions   │
└────────────────────────┬────────────────────────────────────────────┘
                         │ depends on
┌────────────────────────▼────────────────────────────────────────────┐
│                       Sovva.Domain                                  │
│  Entities (19) │ Enums (5) │ Constants (5) │ BaseEntity             │
└────────────────────────▲────────────────────────────────────────────┘
                         │ depends on
┌────────────────────────┴────────────────────────────────────────────┐
│                    Sovva.Infrastructure                              │
│  Repositories (16) │ Data (AppDbContext, UoW, Interceptor)          │
│  Migrations │ EF Configurations (10)                                │
└─────────────────────────────────────────────────────────────────────┘
```

**Dependency Rule:** Domain ← Application ← Infrastructure/WebAPI. Domain has ZERO references.

---

## 2. STARTUP FLOW

```
Program.cs
  │
  ├── 1. Serilog Configuration
  │     ├── Console sink (with CorrelationId template)
  │     ├── File sink (rolling daily, 14 days retention)
  │     └── Seq sink (configurable URL + API key)
  │
  ├── 2. Service Registration (ServiceCollectionExtensions.cs)
  │     ├── AddAppConfiguration()     → Binds SupabaseOptions, HangfireOptions, DatabaseOptions, CorsOptions
  │     ├── AddDatabase()             → NpgsqlDataSource + AppDbContext + TimestampInterceptor + retry strategy
  │     ├── AddHangfireServices()     → PostgreSQL storage, 2 workers, 15s poll interval
  │     ├── AddApplicationServices()  → 16 repositories + 18 services + singletons (IAppTimeProvider, cache)
  │     ├── AddApiInfrastructure()    → JSON config + FluentValidation + Brotli/Gzip compression + 10MB upload limit
  │     ├── AddAppCors()              → Explicit origin allowlist from config
  │     ├── AddAppRateLimiting()      → "auth" (10/min) + "default" (100/min) fixed-window
  │     ├── AddAppAuth()              → Supabase JWT validation (audience="authenticated", issuer=Supabase URL)
  │     ├── AddAppSwagger()           → OpenAPI doc with Bearer auth definition
  │     └── AddAppHealthChecks()      → NpgSql + Hangfire + self checks
  │
  └── 3. Middleware Pipeline (WebApplicationExtensions.cs)
        ├── GlobalExceptionMiddleware    → Catches all exceptions, maps to ApiResponse
        ├── CorrelationIdMiddleware      → X-Correlation-Id header for tracing
        ├── UseCors("CorsPolicy")
        ├── UseSerilogRequestLogging()
        ├── UseResponseCompression()
        ├── UseRateLimiter()
        ├── UseSwagger() (dev only)
        ├── UseAuthentication()          → Supabase JWT Bearer validation
        ├── AuthMiddleware               → Maps Supabase sub → User → sovva_role + sovva_user_id claims
        ├── UseAuthorization()
        ├── MapControllers()
        ├── MapHealthChecks()            → /health/live, /health/ready, /health
        └── ScheduleHangfireJobs()       → 4 recurring jobs (IST timezone)
```

---

## 3. REQUEST LIFECYCLE

```
HTTP Request
  │
  ├─1─→ GlobalExceptionMiddleware (try/catch wrapper)
  ├─2─→ CorrelationIdMiddleware (adds X-Correlation-Id)
  ├─3─→ CORS Policy
  ├─4─→ Serilog Request Logging
  ├─5─→ Response Compression
  ├─6─→ Rate Limiter (429 if exceeded)
  ├─7─→ Swagger UI (dev only)
  ├─8─→ JWT Authentication (Supabase JWT validation)
  ├─9─→ AuthMiddleware
  │      ├── Extract "sub" claim from JWT
  │      ├── Lookup user by AuthId via IUserService.GetUserByAuthIdIncludingDeletedAsync()
  │      ├── If AccountStatus == Deleted → 401 ACCOUNT_DELETED
  │      ├── Inject sovva_role claim (from DB User.Role)
  │      ├── Inject sovva_user_id claim (from DB User.UserId)
  │      └── Store UserId, User, AuthId in HttpContext.Items
  ├─10→ Authorization ([Authorize], [Authorize(Roles="Admin")])
  └─11→ Controller Action
           ├── Get userId from ClaimsPrincipal (sovva_user_id)
           ├── Call Application Service
           │     ├── Business logic
           │     ├── Repository calls (EF Core → PostgreSQL)
           │     └── External services (Supabase Storage)
           └── Return ApiResponse.Ok() / ApiResponse.Fail()
```

---

## 4. AUTHENTICATION FLOW

```
┌──────────┐     ┌──────────────┐     ┌──────────────────────┐
│  Client  │────→│ Supabase Auth│────→│ JWT Token (sub=guid)  │
└──────────┘     └──────────────┘     └──────────┬───────────┘
                                                  │ Bearer token
                                                  ▼
                                      ┌───────────────────────┐
                                      │ ASP.NET JWT Middleware │
                                      │  Validates: issuer,    │
                                      │  audience, signature,  │
                                      │  expiry                │
                                      └───────────┬───────────┘
                                                  │ "sub" = Supabase AuthId (Guid)
                                                  ▼
                                      ┌───────────────────────┐
                                      │   AuthMiddleware       │
                                      │  1. sub → GetUserByAuth│
                                      │  2. Check !Deleted     │
                                      │  3. Add sovva_role     │
                                      │  4. Add sovva_user_id  │
                                      └───────────┬───────────┘
                                                  │ UserId (int) available
                                                  ▼
                                      ┌───────────────────────┐
                                      │     Controller         │
                                      │  User.GetSovvaUserId() │
                                      └───────────────────────┘
```

**Identity Model:**
- Supabase manages auth (login, signup, password reset, social OAuth)
- `UserAuthMapping` table bridges Supabase `sub` (Guid) → Sovva `UserId` (int)
- Roles stored in Sovva DB, NOT in Supabase — injected as claims by AuthMiddleware
- Two authorization policies: `AdminOnly`, `UserOnly`

---

## 5. HANGFIRE JOB PIPELINE (IST Timezone)

```
TIME (IST)    JOB                               PURPOSE
───────────   ─────────────────────────────────  ────────────────────────────────────
 23:50        expire-subscriptions               Deactivate subscriptions past EndDate
 23:55        sync-subscription-dates             Update NextScheduledDate for all active subs
 00:00        midnight-order-confirmation         Debit wallets + create Order rows from ScheduledOrders
 00:01        subscription-order-generation       Generate ScheduledOrders for tomorrow from active subs
```

### Midnight Order Confirmation Flow (00:00):

```
ConfirmAllScheduledOrdersAsync(null)
  │
  ├── Default targetDate = TomorrowIst (if null)
  ├── Fetch all ScheduledOrders for targetDate
  ├── Filter: Status ∈ {Scheduled, Processing, Failed}
  ├── Batch load Users by AuthIds
  │
  └── FOR EACH pending order:
        ConfirmSingleOrderAsync(order, usersByAuthId)
          │
          ├── STEP 1: Validate user exists
          ├── STEP 2: Validate delivery address exists + active
          ├── STEP 3: IDEMPOTENCY CHECK
          │     ├── Does Order row exist? (GetByScheduledOrderIdAsync)
          │     ├── Does WalletTransaction exist? (ExistsForScheduledOrderAsync)
          │     └── If both: skip. If order only: complete payment.
          │
          └── STEP 4-6: ExecuteInTransactionAsync
                ├── STEP 4: AtomicDebitAsync (INSERT INTO WalletTransactions WHERE balance >= amount)
                ├── STEP 5: ConfirmScheduledOrderAsync → INSERT Order row
                └── STEP 6: MarkAsProcessedAsync → UPDATE ScheduledOrder status
```

### Subscription Order Generation Flow (00:01):

```
GenerateScheduledOrdersFromSubscriptionsAsync()
  │
  ├── deliveryDay = today + 1 (tomorrow)
  ├── Fetch all active subscriptions
  ├── BATCH LOAD (5 queries total):
  │     ├── UserMeals by IDs
  │     ├── UserMealIngredients by UserMealIds
  │     ├── Users with AuthMapping by UserIds
  │     └── Primary Addresses by UserIds
  │
  └── FOR EACH subscription:
        ├── IsDueOnDate(sub, deliveryDay) → Daily/Weekly/Alternate/Monthly
        ├── EndDate guard
        ├── Duplicate guard (GetBySubscriptionIdAndDateAsync)
        ├── Resolve ingredients (UserMeal → Catalogue fallback)
        ├── Create ScheduledOrder
        └── Advance NextScheduledDate
```

---

## 6. WALLET SYSTEM ARCHITECTURE

```
SINGLE SOURCE OF TRUTH: WalletTransactions table (ledger)

Balance = SUM(CASE WHEN Type='Credit' THEN Amount ELSE -Amount END)

┌─────────────────────┐
│  WalletTransactions  │
│  TransactionId (PK)  │
│  UserId (FK)         │
│  Amount (decimal)    │
│  Type (Credit/Debit) │
│  Description         │
│  ScheduledOrderId    │
│  CreatedAt           │
└─────────────────────┘

ATOMIC OPERATIONS:
  ├── AtomicDebitAsync()  → INSERT...SELECT WHERE balance >= amount (single SQL)
  ├── AtomicCreditAsync() → INSERT...SELECT WHERE balance + amount <= MaxBalance (single SQL)
  └── CreateTransactionAsync() → Advisory lock + balance check + INSERT (transaction-scoped)

BALANCE QUERIES:
  ├── HasSufficientBalanceAsync()  → Queries ledger SUM directly
  ├── GetUserBalanceAsync()        → SUM(Credits) - SUM(Debits)
  └── User.WalletBalance           → COMPUTED PROPERTY (populated in UserRepository, NOT authoritative)
```

---

## 7. DATA MODEL — KEY ENTITY RELATIONSHIPS

```
User (1) ──── (N) UserAuthMapping          (1:1 in practice)
User (1) ──── (N) UserAddress
User (1) ──── (N) UserMeal
User (1) ──── (N) Order
User (1) ──── (N) ScheduledOrder
User (1) ──── (N) Subscription
User (1) ──── (N) WalletTransaction

Meal (1) ──── (N) MealOption
MealOption (1) ── (N) MealOptionIngredient ──── Ingredient
IngredientCategory (1) ── (N) Ingredient

UserMeal (1) ──── (N) UserMealIngredient ──── Ingredient
Subscription (1) ──── (1) UserMeal
Subscription (1) ──── (N) SubscriptionSchedule
Subscription (1) ──── (N) ScheduledOrder

ScheduledOrder (1) ──── (N) ScheduledOrderIngredient ──── Ingredient
ScheduledOrder (1) ──── (0..1) Order   (ConfirmedOrderId)
Order (0..1) ──── (1) ScheduledOrder   (SourceScheduledOrder)

UserAddress (N) ──── (1) ServiceableLocation
```

---

## 8. DEPENDENCY INJECTION LIFETIME MAP

| Lifetime   | Services |
|-----------|---------|
| Singleton | `IAppTimeProvider`, `TimestampInterceptor`, `JobFailureAlertFilter`, `IMemoryCache` |
| Scoped    | All repositories (16), All application services (18), `ICurrentUserService`, `IUnitOfWork`, `AppDbContext` |
| HttpClient | `ISupabaseStorageService` (via `AddHttpClient`) |

---

## 9. CONFIGURATION STRUCTURE

| Config Class       | Section            | Key Properties |
|-------------------|--------------------|---------------|
| `SupabaseOptions`  | `Supabase`         | Url, AnonKey, ServiceRoleKey, StorageUrl |
| `HangfireOptions`  | `HangfireDashboard`| Username, Password |
| `DatabaseOptions`  | `Database`         | MaxPoolSize(10), CommandTimeout(30s), MaxRetryCount(3) |
| `CorsOptions`      | `Cors`             | AllowedOrigins, AllowedVercelSlugs |

**Connection String Resolution Order:**
1. `DATABASE_SESSION_URL` env var
2. `DATABASE_URL` env var
3. `ConnectionStrings:DefaultConnection` in appsettings
4. Throw `InvalidOperationException`

---

## 10. EXTERNAL SERVICES

| Service | Usage | Implementation |
|---------|-------|---------------|
| **Supabase Auth** | JWT authentication, user identity | JWT Bearer validation via `.well-known/openid-configuration` |
| **Supabase Storage** | Meal image uploads, signed URL generation | `SupabaseStorageService` via `HttpClient` |
| **PostgreSQL** | Primary data store, Hangfire storage | Npgsql via EF Core with retry strategy |
| **Seq** | Structured log aggregation | Serilog Seq sink (configurable URL) |

---

## 11. DOCKER / DEPLOYMENT

- **Build:** `mcr.microsoft.com/dotnet/sdk:8.0`
- **Runtime:** `mcr.microsoft.com/dotnet/aspnet:8.0`
- **Port:** 10000 (Render requirement)
- **Environment:** `ASPNETCORE_ENVIRONMENT=Production`
- **Security:** Non-root user (`dotnetuser:dotnetgroup`, UID 1001)
- **TFM:** All projects target `net8.0` — consistent with Docker images ✅


---

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


---

# SOVVA BACKEND — DATABASE SCHEMA DOCUMENTATION

**Generated:** 2026-05-22
**Database:** PostgreSQL (via Supabase)
**ORM:** Entity Framework Core 9.0 (Code-First with Migrations)

---

## 1. TABLE INVENTORY

| Table | PK | Key Relationships | Soft Delete | Audit Fields |
|-------|-----|-------------------|-------------|-------------|
| `Users` | `UserId` (int) | → UserAuthMapping, Orders, Subscriptions | `DeletedAt` | CreatedAt, UpdatedAt |
| `UserAuthMappings` | `UserAuthMappingId` | → Users | No | CreatedAt, UpdatedAt |
| `UserAddresses` | `Id` (int) | → Users, ServiceableLocations | No | CreatedAt, UpdatedAt |
| `ServiceableLocations` | `Id` (int) | → UserAddresses | `IsActive` flag | CreatedAt, UpdatedAt |
| `Meals` | `MealId` (int) | → MealOptions | `DeletedAt` | CreatedAt, UpdatedAt |
| `MealOptions` | `MealOptionId` | → Meals, MealOptionIngredients | No | CreatedAt, UpdatedAt |
| `MealOptionIngredients` | `Id` | → MealOptions, Ingredients | No | CreatedAt |
| `Ingredients` | `IngredientId` (int) | → IngredientCategories | `DeletedAt` | CreatedAt, UpdatedAt |
| `IngredientCategories` | `CategoryId` (int) | → Ingredients | No | CreatedAt, UpdatedAt |
| `UserMeals` | `UserMealId` (int) | → Users, Meals | No | CreatedAt, UpdatedAt |
| `UserMealIngredients` | `UserMealIngredientId` | → UserMeals, Ingredients | No | CreatedAt, UpdatedAt |
| `Orders` | `OrderId` (int) | → Users, UserMeals, ScheduledOrders, UserAddresses | No | CreatedAt, UpdatedAt |
| `ScheduledOrders` | `ScheduledOrderId` (int) | → Users, Subscriptions, UserAddresses | No | CreatedAt, UpdatedAt |
| `ScheduledOrderIngredients` | `Id` | → ScheduledOrders, Ingredients | No | CreatedAt |
| `Subscriptions` | `SubscriptionId` (int) | → Users, UserMeals, UserAddresses | No | CreatedAt, UpdatedAt |
| `SubscriptionSchedules` | `ScheduleId` | → Subscriptions | No | CreatedAt, UpdatedAt |
| `WalletTransactions` | `TransactionId` (long) | → Users | No | CreatedAt, UpdatedAt |

---

## 2. DETAILED SCHEMA

### Users

```sql
CREATE TABLE "Users" (
    "UserId"         SERIAL PRIMARY KEY,
    "Name"           VARCHAR(200) NOT NULL,
    "Email"          VARCHAR(300) NOT NULL,
    "Phone"          VARCHAR(20) NOT NULL,
    "AccountStatus"  VARCHAR(50) NOT NULL DEFAULT 'Active',    -- CHECK IN ('Active','Deactivated','Deleted')
    "WalletBalance"  DECIMAL(12,2) DEFAULT 0,                  -- COMPUTED (not authoritative), concurrency token
    "Role"           VARCHAR(50) NOT NULL DEFAULT 'Customer',  -- CHECK IN ('Customer','Admin','DeliveryPartner')
    "DeletedAt"      TIMESTAMPTZ,
    "CreatedAt"      TIMESTAMPTZ NOT NULL,
    "UpdatedAt"      TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT "CK_Users_WalletBalance" CHECK ("WalletBalance" >= 0),
    CONSTRAINT "CK_Users_Role" CHECK ("Role" IN ('Customer','Admin','DeliveryPartner')),
    CONSTRAINT "CK_Users_AccountStatus" CHECK ("AccountStatus" IN ('Active','Deactivated','Deleted'))
);

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
CREATE UNIQUE INDEX "IX_Users_Phone" ON "Users" ("Phone");
CREATE INDEX "IX_Users_Active" ON "Users" ("DeletedAt") WHERE "DeletedAt" IS NULL;
```

**Global Query Filter:** `HasQueryFilter(u => u.DeletedAt == null)`

---

### WalletTransactions (LEDGER — Source of Truth)

```sql
CREATE TABLE "WalletTransactions" (
    "TransactionId"    BIGSERIAL PRIMARY KEY,
    "UserId"           INT NOT NULL REFERENCES "Users"("UserId"),
    "Amount"           DECIMAL(12,2) NOT NULL,
    "Type"             VARCHAR(20) NOT NULL,           -- CHECK IN ('Credit','Debit')
    "Description"      VARCHAR(500) NOT NULL,
    "ReferenceType"    VARCHAR(50),                    -- CHECK IN ('Order','Subscription','TopUp','Refund','Manual') or NULL
    "ScheduledOrderId" INT,                            -- Nullable FK for idempotency checks
    "CreatedAt"        TIMESTAMPTZ NOT NULL,
    "UpdatedAt"        TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT "CK_WalletTransactions_Type" CHECK ("Type" IN ('Credit','Debit')),
    CONSTRAINT "CK_WalletTransactions_Amount" CHECK ("Amount" > 0)
);

CREATE INDEX "IX_WalletTransactions_UserId_CreatedAt" ON "WalletTransactions" ("UserId", "CreatedAt");
CREATE INDEX "IX_WalletTransactions_ScheduledOrderId" ON "WalletTransactions" ("ScheduledOrderId");
```

**Balance Formula:** `SUM(CASE WHEN "Type" = 'Credit' THEN "Amount" ELSE -"Amount" END)`

---

### Orders

```sql
CREATE TABLE "Orders" (
    "OrderId"            SERIAL PRIMARY KEY,
    "UserId"             INT NOT NULL REFERENCES "Users"("UserId"),
    "UserMealId"         INT REFERENCES "UserMeals"("UserMealId"),         -- NULL for scheduled-order-sourced orders
    "ScheduledOrderId"   INT REFERENCES "ScheduledOrders"("ScheduledOrderId") ON DELETE SET NULL,
    "DeliveryAddressId"  INT REFERENCES "UserAddresses"("Id") ON DELETE SET NULL,
    "IsPrepared"         BOOLEAN DEFAULT FALSE,
    "Status"             VARCHAR(50) NOT NULL DEFAULT 'Pending',
    "OrderDate"          TIMESTAMPTZ NOT NULL,
    "ScheduledFor"       TIMESTAMPTZ NOT NULL,
    "TotalPrice"         DECIMAL(12,2) NOT NULL,
    "Rating"             INT,                                               -- CHECK 1-5 or NULL
    "Review"             TEXT,
    "CreatedAt"          TIMESTAMPTZ NOT NULL,
    "UpdatedAt"          TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT "CK_Orders_Status" CHECK ("Status" IN ('Pending','Confirmed','Preparing','OutForDelivery','Delivered','Cancelled')),
    CONSTRAINT "CK_Orders_TotalPrice" CHECK ("TotalPrice" >= 0),
    CONSTRAINT "CK_Orders_Rating" CHECK ("Rating" IS NULL OR ("Rating" >= 1 AND "Rating" <= 5))
);

CREATE INDEX "IX_Orders_UserId" ON "Orders" ("UserId");
CREATE INDEX "IX_Orders_ScheduledFor" ON "Orders" ("ScheduledFor");
CREATE INDEX "IX_Orders_UserId_Status" ON "Orders" ("UserId", "Status");
```

**Order Status Machine:**
```
Pending → Confirmed → Preparing → OutForDelivery → Delivered
                                                  → Cancelled
```
Terminal states: `Delivered`, `Cancelled` (no outbound transitions).

---

### ScheduledOrders

```sql
CREATE TABLE "ScheduledOrders" (
    "ScheduledOrderId"   SERIAL PRIMARY KEY,
    "UserId"             INT NOT NULL REFERENCES "Users"("UserId") ON DELETE CASCADE,
    "AuthId"             UUID NOT NULL,
    "MealName"           VARCHAR(255) NOT NULL DEFAULT 'Custom Overnight Oats',
    "MealId"             INT,                                                -- Soft ref, no FK constraint
    "MealImageUrl"       TEXT,                                               -- Snapshot
    "ScheduledFor"       DATE NOT NULL,                                      -- IST delivery date
    "DeliveryTimeSlot"   VARCHAR(50) NOT NULL DEFAULT '10:00 AM',
    "TotalPrice"         DECIMAL(12,2) NOT NULL,
    "NutritionalSummary" TEXT,                                               -- JSON blob
    "OrderStatus"        VARCHAR(50) NOT NULL DEFAULT 'Scheduled',
    "CanModify"          BOOLEAN DEFAULT TRUE,
    "ConfirmedAt"        TIMESTAMPTZ,
    "ExpiresAt"          TIMESTAMPTZ NOT NULL,
    "IsProcessedToOrder" BOOLEAN DEFAULT FALSE,
    "ConfirmedOrderId"   INT,                                                -- Link to Orders.OrderId
    "DeliveryAddressId"  INT REFERENCES "UserAddresses"("Id") ON DELETE SET NULL,
    "SubscriptionId"     INT REFERENCES "Subscriptions"("SubscriptionId"),
    "CreatedAt"          TIMESTAMPTZ NOT NULL,
    "UpdatedAt"          TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT "CK_ScheduledOrders_Status" CHECK (
        "OrderStatus" IN ('Scheduled','Confirmed','Cancelled','Processed','Processing','Failed')
    ),
    CONSTRAINT "CK_ScheduledOrders_TotalPrice" CHECK ("TotalPrice" >= 0)
);

CREATE INDEX "IX_ScheduledOrders_UserId_ScheduledFor" ON "ScheduledOrders" ("UserId", "ScheduledFor");
CREATE INDEX "IX_ScheduledOrders_AuthId_ScheduledFor" ON "ScheduledOrders" ("AuthId", "ScheduledFor");
CREATE INDEX "IX_ScheduledOrders_ScheduledFor_Status" ON "ScheduledOrders" ("ScheduledFor", "OrderStatus");
CREATE UNIQUE INDEX "IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique" 
    ON "ScheduledOrders" ("SubscriptionId", "ScheduledFor") WHERE "SubscriptionId" IS NOT NULL;
```

**ScheduledOrder Status Machine:**
```
Scheduled → Processing → Processed (success)
                       → Failed    (insufficient balance / error)
         → Cancelled   (user cancel)
```

---

### Subscriptions

```sql
CREATE TABLE "Subscriptions" (
    "SubscriptionId"    SERIAL PRIMARY KEY,
    "UserId"            INT NOT NULL REFERENCES "Users"("UserId") ON DELETE CASCADE,
    "UserMealId"        INT NOT NULL REFERENCES "UserMeals"("UserMealId") ON DELETE RESTRICT,
    "Frequency"         INT NOT NULL,                    -- 0=Daily, 1=Weekly, 2=Monthly, 3=Alternate
    "StartDate"         DATE NOT NULL,
    "EndDate"           DATE NOT NULL,
    "IsActive"          BOOLEAN DEFAULT TRUE,
    "NextScheduledDate" DATE,
    "DeliveryAddressId" INT REFERENCES "UserAddresses"("Id") ON DELETE SET NULL,
    "CreatedAt"         TIMESTAMPTZ NOT NULL,
    "UpdatedAt"         TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT "CK_Subscriptions_Dates" CHECK ("EndDate" > "StartDate")
);

CREATE UNIQUE INDEX "UX_Subscriptions_ActiveUserMeal" 
    ON "Subscriptions" ("UserId", "UserMealId") WHERE "Active" = true;   -- ⚠️ Column name mismatch (see ARCH-NEW-02)
CREATE INDEX "IX_Subscriptions_UserId_Active" ON "Subscriptions" ("UserId", "IsActive");
CREATE INDEX "IX_Subscriptions_Active_NextScheduledDate" 
    ON "Subscriptions" ("IsActive", "NextScheduledDate") WHERE "Active" = true;
```

---

## 3. INDEX COVERAGE ANALYSIS

| Query Pattern | Index Used | Coverage |
|--------------|-----------|---------|
| User by Email | `IX_Users_Email` (unique) | ✅ Full |
| User by Phone | `IX_Users_Phone` (unique) | ✅ Full |
| Active users | `IX_Users_Active` (filtered) | ✅ Full |
| Wallet by User+Date | `IX_WalletTransactions_UserId_CreatedAt` | ✅ Full |
| Wallet by ScheduledOrderId | `IX_WalletTransactions_ScheduledOrderId` | ✅ Full |
| Orders by User | `IX_Orders_UserId` | ✅ Full |
| Orders by User+Status | `IX_Orders_UserId_Status` | ✅ Full |
| Orders by ScheduledFor | `IX_Orders_ScheduledFor` | ✅ Full |
| ScheduledOrders by User+Date | `IX_ScheduledOrders_UserId_ScheduledFor` | ✅ Full |
| ScheduledOrders by AuthId+Date | `IX_ScheduledOrders_AuthId_ScheduledFor` | ✅ Full |
| ScheduledOrders by Date+Status | `IX_ScheduledOrders_ScheduledFor_Status` | ✅ Full |
| Subscription dedup | `UX_Subscriptions_ActiveUserMeal` (unique, filtered) | ⚠️ Column name issue |
| Subscription by Active+Next | `IX_Subscriptions_Active_NextScheduledDate` | ⚠️ Column name issue |
| **Balance SUM by UserId** | `IX_WalletTransactions_UserId_CreatedAt` | 🟡 Partial (index scan, no covering) |
| **GetByUserIdAndTypeAsync** | None specific | 🔴 Missing (full table scan filtered by UserId+Type) |

### Missing Indexes (Recommendations):

```sql
-- 1. Wallet balance queries are the #1 most frequent query — add covering index
CREATE INDEX "IX_WalletTransactions_UserId_Type_Amount" 
    ON "WalletTransactions" ("UserId", "Type") INCLUDE ("Amount");

-- 2. ScheduledOrder by SubscriptionId (used in subscription delete + duplicate check)
-- Already covered by unique index, but non-null filter may exclude some queries

-- 3. Orders by ScheduledOrderId (idempotency check in midnight job)
CREATE INDEX "IX_Orders_ScheduledOrderId" ON "Orders" ("ScheduledOrderId") WHERE "ScheduledOrderId" IS NOT NULL;
```

---

## 4. CHECK CONSTRAINTS SUMMARY

| Table | Constraint | Expression |
|-------|-----------|-----------|
| Users | `CK_Users_WalletBalance` | `"WalletBalance" >= 0` |
| Users | `CK_Users_Role` | `IN ('Customer','Admin','DeliveryPartner')` |
| Users | `CK_Users_AccountStatus` | `IN ('Active','Deactivated','Deleted')` |
| Orders | `CK_Orders_Status` | `IN ('Pending','Confirmed',...)` |
| Orders | `CK_Orders_TotalPrice` | `>= 0` |
| Orders | `CK_Orders_Rating` | `IS NULL OR (1-5)` |
| ScheduledOrders | `CK_ScheduledOrders_Status` | `IN ('Scheduled','Confirmed',...)` |
| ScheduledOrders | `CK_ScheduledOrders_TotalPrice` | `>= 0` |
| WalletTransactions | `CK_WalletTransactions_Type` | `IN ('Credit','Debit')` |
| WalletTransactions | `CK_WalletTransactions_Amount` | `> 0` |
| WalletTransactions | `CK_WalletTransactions_ReferenceType` | `IS NULL OR IN (...)` |
| Subscriptions | `CK_Subscriptions_Dates` | `"EndDate" > "StartDate"` |

---

## 5. SOFT DELETE STRATEGY

| Entity | Strategy | Filter |
|--------|---------|--------|
| `User` | `DeletedAt` nullable timestamp | `HasQueryFilter(u => u.DeletedAt == null)` |
| `Meal` | `DeletedAt` nullable timestamp | `HasQueryFilter(m => m.DeletedAt == null)` |
| `Ingredient` | `DeletedAt` nullable timestamp | `HasQueryFilter(i => i.DeletedAt == null)` |
| `ServiceableLocation` | `IsActive` boolean | Manual WHERE filter |
| All others | Hard delete | N/A |

**Bypass:** `IgnoreQueryFilters()` used in `GetUserByAuthIdIncludingDeletedAsync` (AuthMiddleware needs to detect deleted accounts).

---

## 6. TIMESTAMP MANAGEMENT

All entities extend `BaseEntity` with `CreatedAt` and `UpdatedAt`.

**TimestampInterceptor** (`SavingChangesAsync`):
- `EntityState.Added` → Sets `CreatedAt = UtcNow`, `UpdatedAt = UtcNow`
- `EntityState.Modified` → Sets `UpdatedAt = UtcNow`

⚠️ Some services manually set timestamps (redundant, overwritten by interceptor).

---

## 7. CONCURRENCY CONTROL

| Mechanism | Where Used |
|-----------|-----------|
| `IsConcurrencyToken()` on `WalletBalance` | `UserConfiguration` — ⚠️ now dead since balance is ledger-based |
| `pg_advisory_xact_lock(userId)` | `WalletTransactionRepository.AcquireUserWalletLockAsync` — used by `CreateTransactionAsync` |
| `INSERT...SELECT WHERE balance >= amount` | `AtomicDebitAsync` / `AtomicCreditAsync` — single-statement atomicity |
| `NpgsqlRetryingExecutionStrategy` | Global retry strategy for transient PostgreSQL errors |
| Unique indexes | `SubscriptionId+ScheduledFor`, `Email`, `Phone` |


---

# SOVVA BACKEND — ARCHITECTURE AUDIT

**Generated:** 2026-05-22
**Phase:** 3 — Code Quality + Architecture Deep Dive
**Scope:** Issues NOT already documented in CLAUDE.md

---

## NEW FINDINGS (beyond CLAUDE.md)

---

### ~~ARCH-NEW-01: Dockerfile .NET Version Mismatch~~ ✅ VERIFIED CONSISTENT

**FILE:** `Dockerfile` (Lines 2, 12) + all `.csproj` files
**STATUS:** ✅ NO ISSUE — All `.csproj` files target `net8.0`, matching the `dotnet/sdk:8.0` and `dotnet/aspnet:8.0` Docker images.

**NOTE:** Earlier CLAUDE.md referenced .NET 9, but verified all projects are on `net8.0`. Dockerfile is correct.

**PRIORITY:** N/A — no fix needed

---

### ~~ARCH-NEW-02: SubscriptionConfiguration References Non-Existent Column `"Active"`~~ ✅ VERIFIED

**FILE:** `Sovva.Infrastructure/Data/Configurations/SubscriptionConfiguration.cs` (Lines 42, 51)
**STATUS:** ✅ NO ISSUE

**Verdict:** `AppDbContextModelSnapshot.cs` Line 611-613 confirms:
```csharp
b.Property<bool>("IsActive")
    .HasColumnType("boolean")
    .HasColumnName("Active");  // ← Explicit mapping!
```

The C# property `IsActive` maps to DB column `"Active"` via `HasColumnName`. The filter expressions `"Active" = true` are CORRECT and match the actual database column.

**PRIORITY:** N/A — no fix needed

---

### ARCH-NEW-03: `User.WalletBalance` Has `IsConcurrencyToken()` — Dead Configuration

**FILE:** `Sovva.Infrastructure/Data/Configurations/UserConfiguration.cs` (Line 23)
**SEVERITY:** 🟡 LOW (no functional impact since WalletBalance is computed)

**Problem:**
```csharp
builder.Property(e => e.WalletBalance)
    .HasColumnType("decimal(12,2)")
    .IsConcurrencyToken();  // ← Concurrency token on a computed value
```

Since P0-3 fix made WalletBalance a computed property (never written to directly), the concurrency token is meaningless. It will cause `DbUpdateConcurrencyException` if any code path ever tries to update a User entity after the computed balance changes between reads.

**FIX:** Remove `.IsConcurrencyToken()` since balance is now ledger-based.

**RISK:** LOW — only triggers if User entity is updated while balance changes concurrently
**PRIORITY:** P2

---

### ARCH-NEW-04: `AuthMiddleware` Makes a DB Call on EVERY Authenticated Request

**FILE:** `Sovva.WebAPI/Middleware/AuthMiddleware.cs` (Line 65)
**SEVERITY:** 🟠 HIGH (performance at scale)
**CONFIDENCE:** HIGH

**Problem:**
```csharp
var userDto = await userService.GetUserByAuthIdIncludingDeletedAsync(authGuid);
```

Every single authenticated API request triggers:
1. `GetUserByAuthIdIncludingDeletedAsync` → `UserRepository.GetUserByAuthIdIncludingDeletedAsync` → 2 SQL queries (user + wallet balance SUM)
2. This means **every request pays 2 DB roundtrips** just for auth enrichment

At 100 concurrent users, each making 5 requests/minute = 1000 DB queries/minute just for auth middleware.

**FIX Options:**
1. **Cache user claims in memory** for 30-60 seconds per AuthId (invalidate on role/status change)
2. **Use JWT custom claims** — add sovva_user_id and sovva_role to the Supabase JWT via a Supabase Auth Hook, eliminating the middleware DB lookup entirely
3. **Cache in distributed cache (Redis)** if multi-instance deployment planned

**RISK:** P1 — direct latency impact on every API call
**PRIORITY:** P1

---

### ARCH-NEW-05: `AuthMiddleware.ExtractUserInfoFromToken()` Is Never Called

**FILE:** `Sovva.WebAPI/Middleware/AuthMiddleware.cs` (Lines 155-161)
**SEVERITY:** 🟢 LOW
**CONFIDENCE:** HIGH

Dead code — `ExtractUserInfoFromToken` is defined but never invoked anywhere.

**FIX:** Delete the method.
**PRIORITY:** P3

---

### ARCH-NEW-06: `OrderService.RateOrderAsync` Uses `DateTime.UtcNow` Directly

**FILE:** `Sovva.Application/Services/OrderService.cs` (Line 586)
**SEVERITY:** 🟡 LOW
**CONFIDENCE:** HIGH

```csharp
order.UpdatedAt = DateTime.UtcNow;  // ← Should use _time.UtcNow
```

Same pattern as P1-2 from CLAUDE.md but in a different method.

**FIX:** Replace with `_time.UtcNow`.
**PRIORITY:** P2

---

### ARCH-NEW-07: `SubscriptionService` Has Unused `using` Statements (Entities Framework Attributes)

**FILE:** `Sovva.Application/Services/SubscriptionService.cs` (Lines 5-6)
**SEVERITY:** 🟢 LOW
**CONFIDENCE:** HIGH

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
```

These are entity-level concerns, not needed in a service class.

**FIX:** Remove the unused imports.
**PRIORITY:** P3

---

### ARCH-NEW-08: `ScheduledOrderService.DuplicateScheduledOrderAsync` Doesn't Copy ExpiresAt

**FILE:** `Sovva.Application/Services/ScheduledOrderService.cs` (Lines 287-303)
**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** MEDIUM

The duplicate order creation block doesn't set `ExpiresAt`:
```csharp
var duplicateOrder = new ScheduledOrder
{
    // ... (no ExpiresAt set)
    // CreatedAt/UpdatedAt handled by TimestampInterceptor
};
```

`ExpiresAt` will default to `DateTime.MinValue` (0001-01-01). While `ExpiresAt` is never enforced at the application level (noted in CLAUDE.md P2), it creates inconsistent data.

**FIX:** Add `ExpiresAt = _time.ToUtc(originalOrder.ScheduledFor.AddDays(1).ToDateTime(TimeOnly.MinValue))`.
**PRIORITY:** P2

---

### ARCH-NEW-09: `DashboardService.GetProfileAsync` Triggers Extra Wallet SUM Query

**FILE:** `Sovva.Application/Services/DashboardService.cs` (Lines 116, 66)
**SEVERITY:** 🟡 MEDIUM (wasteful)
**CONFIDENCE:** HIGH

```csharp
var profile = await GetProfileAsync(userId, ct);      // Line 60 — calls GetByIdAsync → includes wallet SUM
var walletBalance = await ...GetUserBalanceAsync(userId);  // Line 66 — another SUM query
```

`GetProfileAsync` calls `_userRepository.GetByIdAsync(userId)` which computes `User.WalletBalance` via a SUM query. Then immediately after, `GetUserBalanceAsync` runs the SAME SUM query again. The wallet balance is computed twice.

**FIX:** Either:
1. Use the balance from the already-loaded User entity in the profile query
2. Or skip the wallet SUM inside `GetByIdAsync` when called from dashboard context

**PRIORITY:** P2

---

### ARCH-NEW-10: `ScheduledOrderRepository.GetByIdAndAuthIdAsync` Uses AsNoTracking But Caller Modifies Entity

**FILE:** `Sovva.Infrastructure/Repositories/ScheduledOrderRepository.cs` (Lines 84-92)
**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

```csharp
public async Task<ScheduledOrder?> GetByIdAndAuthIdAsync(int scheduledOrderId, Guid authId)
{
    return await _context.ScheduledOrders
        .AsNoTracking()  // ← Not tracked
        .Include(so => so.Ingredients)
        ...
}
```

This is called by `ModifyScheduledOrderAsync` which then mutates the entity (`scheduledOrder.Ingredients.Clear()`, `scheduledOrder.TotalPrice = newTotalPrice`) and calls `UpdateAsync`. Since the entity is not tracked, `UpdateAsync` must re-fetch it from DB (which it does — Line 207-212 in UpdateAsync).

The `AsNoTracking` is technically correct since UpdateAsync re-fetches, but it means:
1. The modify flow does 3 DB calls instead of 2 (fetch AsNoTracking → re-fetch tracked → save)
2. Between the two fetches, another request could modify the order (no optimistic concurrency)

**FIX:** Consider removing `AsNoTracking` for `GetByIdAndAuthIdAsync` since it's used in write flows, or add a separate tracked query for write operations.
**PRIORITY:** P2

---

### ARCH-NEW-11: `ScheduledOrderRepository.UpdateAsync` Doesn't Update Ingredients

**FILE:** `Sovva.Infrastructure/Repositories/ScheduledOrderRepository.cs` (Lines 205-231)
**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

`UpdateAsync` copies scalar fields from the input entity to the existing tracked entity, but does NOT update the `Ingredients` collection:
```csharp
existing.OrderStatus        = scheduledOrder.OrderStatus;
existing.CanModify          = scheduledOrder.CanModify;
// ... but NO: existing.Ingredients = scheduledOrder.Ingredients
```

However, `ModifyScheduledOrderAsync` calls `scheduledOrder.Ingredients.Clear()` on the AsNoTracking entity and then adds new ingredients. Since this entity is NOT tracked, these changes are lost.

The modify flow works only because EF Core's `SaveChangesAsync` in `UpdateAsync` will persist the changes to the tracked `existing` entity — but the new ingredients from the `scheduledOrder` parameter are never copied to `existing`.

**FIX:** The modify flow should either:
1. Fetch tracked and modify in-place, or
2. `UpdateAsync` must also handle ingredient replacement

**RISK:** HIGH — ingredient modifications may silently not persist
**PRIORITY:** P1

---

### ARCH-NEW-12: `GetAllAsync` in `WalletTransactionRepository` Still Used Without Pagination

**FILE:** `Sovva.Infrastructure/Repositories/WalletTransactionRepository.cs` (Lines 22-31)
**SEVERITY:** Already documented in CLAUDE.md (P1-5)
**CONFIDENCE:** HIGH

Already paginated at the controller level per CLAUDE.md remediation log. Confirmed fixed.

---

### ARCH-NEW-13: Multiple Services Set `CreatedAt`/`UpdatedAt` Manually Despite TimestampInterceptor

**FILES:**
- `SubscriptionSchedulingService.cs` Lines 195-199, 309-313
- `SubscriptionService.cs` Lines 135-136, 198, 204, 372-373, 396

**SEVERITY:** 🟢 LOW (redundant, not harmful)
**CONFIDENCE:** HIGH

`TimestampInterceptor` automatically sets `CreatedAt`/`UpdatedAt` for Added/Modified entities. Setting them manually is redundant — the interceptor will overwrite with its own value.

**FIX:** Remove manual timestamp assignments in services. Let the interceptor handle it.
**PRIORITY:** P3

---

## DEPENDENCY ANALYSIS — SERVICE COUPLING

### Constructor Injection Count (coupling indicator):

| Service | Dependencies | Assessment |
|---------|-------------|-----------|
| `ScheduledOrderService` | 10 | 🟠 HIGH — borderline god service |
| `SubscriptionSchedulingService` | 11 | 🔴 HIGHEST — too many concerns |
| `SubscriptionService` | 12 | 🔴 HIGHEST — god service territory |
| `OrderService` | 9 | 🟡 MODERATE |
| `DashboardService` | 7 | ✅ ACCEPTABLE |
| `WalletTransactionService` | 4 | ✅ GOOD |
| `MealService` | ~8 | 🟡 MODERATE |

**Recommendation:** `SubscriptionService` (12 deps) and `SubscriptionSchedulingService` (11 deps) should be decomposed. The subscription creation flow mixes:
- User lookup
- Meal resolution
- UserMeal creation
- Ingredient copying
- Address validation
- Order generation
- Schedule management

These are separate concerns that could be orchestrated via a Mediator pattern or broken into sub-services.

---

## CONSISTENCY CHECK — PATTERN VIOLATIONS

| Pattern | Expected | Violations |
|---------|---------|-----------|
| Time abstraction | `_time.UtcNow` everywhere | `DateTime.UtcNow` in `OrderService.RateOrderAsync`, `WebApplicationExtensions` root endpoint |
| ApiResponse wrapper | All controller returns | `DeactivateSubscription` (FIXED per CLAUDE.md) |
| Repository internal | All repos `internal class` | ✅ Consistent |
| FluentValidation | All DTOs validated | Some DTOs may lack validators (needs grep) |
| AsNoTracking for reads | All read-only queries | ✅ Mostly consistent |
| Structured logging | No string interpolation | Multiple instances of `$"..."` in log calls (should use log templates) |


---

# SOVVA BACKEND — SECURITY AUDIT

**Generated:** 2026-05-22
**Phase:** 4 — Security Analysis
**Scope:** Authentication, authorization, data access, injection, secrets, OWASP Top 10 coverage

---

## EXECUTIVE SUMMARY

The application demonstrates **solid security fundamentals**:
- ✅ JWT-based authentication via Supabase
- ✅ Server-side user scoping (userId from JWT, never from request body)
- ✅ Role-based authorization (Admin/Customer separation)
- ✅ SQL injection prevention via EF Core parameterized queries
- ✅ Parameterized raw SQL in AtomicDebitAsync/AtomicCreditAsync
- ✅ Rate limiting on auth endpoints
- ✅ Soft-delete query filters
- ✅ Non-root Docker user
- ✅ Input validation (FluentValidation + manual guards)

**Remaining concerns are P1-P2 level, not critical vulnerabilities.**

---

## OWASP TOP 10 — COVERAGE MATRIX

### A01: Broken Access Control ✅ (mostly covered)

| Check | Status | Notes |
|-------|--------|-------|
| JWT authentication on all endpoints | ✅ | Supabase JWT validation |
| UserId from token, not request body | ✅ | `User.GetSovvaUserId()` extension |
| Admin endpoints protected | ✅ | `[Authorize(Roles = "Admin")]` |
| Object-level access control | ⚠️ | See SEC-01 below |
| Rate limiting | ✅ | Fixed-window: auth(10/min), default(100/min) |

### A02: Cryptographic Failures ✅

| Check | Status | Notes |
|-------|--------|-------|
| Passwords hashed | ✅ | Managed by Supabase (bcrypt) |
| JWT signing keys | ✅ | Supabase manages key rotation |
| Sensitive data in transit | ✅ | HTTPS enforced by Render |
| Secrets in code | ✅ | All secrets via environment variables |
| Connection string security | ⚠️ | See SEC-02 |

### A03: Injection ✅

| Check | Status | Notes |
|-------|--------|-------|
| SQL injection | ✅ | EF Core parameterized queries |
| Raw SQL parameterized | ✅ | `ExecuteSqlRawAsync` uses `{0}` placeholders |
| NoSQL injection | N/A | PostgreSQL only |
| XSS | ✅ | API-only (no HTML rendering) |
| Command injection | N/A | No shell commands |

### A04: Insecure Design ⚠️

| Check | Status | Notes |
|-------|--------|-------|
| Rate limiting on financial ops | ⚠️ | See SEC-03 |
| Idempotency on payments | ✅ | AtomicDebitAsync + duplicate checks |
| Business logic guards | ✅ | Balance checks, min/max amounts |

### A05: Security Misconfiguration ⚠️

| Check | Status | Notes |
|-------|--------|-------|
| Error details in production | ⚠️ | See SEC-04 |
| Swagger in production | ⚠️ | See SEC-05 |
| CORS configuration | ✅ | Explicit origin allowlist |
| Hangfire dashboard auth | ✅ | Basic auth with env vars |
| Health check exposure | ⚠️ | See SEC-06 |

### A06-A10 ✅ (Low Risk)

These categories are well-covered by the stack (Supabase Auth, EF Core, ASP.NET Core defaults).

---

## FINDINGS

---

### SEC-01: Missing Object-Level Authorization in Some Admin Endpoints

**SEVERITY:** 🟠 MEDIUM
**CONFIDENCE:** HIGH

**Problem:**
Several admin endpoints accept userId or orderId in the path but don't verify the requesting admin has permission to act on that specific resource. While admin-only authorization is enforced (`[Authorize(Roles = "Admin")]`), a compromised admin token could operate on ANY user's data.

Currently this is acceptable for a small team with one admin, but becomes a concern as the admin team grows.

**Affected endpoints:**
- `PUT /api/Admin/orders/{orderId}/status`
- `POST /api/Admin/wallet/credit`
- `GET /api/Admin/users/{userId}`

**Recommendation:** For now, this is acceptable. When you have multiple admin roles (e.g., support vs. super-admin), implement resource-scoped policies.

**PRIORITY:** P3 (future consideration)

---

### SEC-02: Connection String Fallback Chain Logs Sensitive Data

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `ServiceCollectionExtensions.cs` (Lines 62-74)

**Problem:**
The connection string resolution logs the chosen source:
```csharp
Log.Information("Using DATABASE_SESSION_URL for connection string");
```

While the connection string value itself is not logged, the resolution path is. The connection string is read from environment variables and passed directly to Npgsql. If `DATABASE_URL` contains credentials in the URL format (`postgres://user:pass@host/db`), these are embedded in the connection string.

**Recommendation:** Ensure the connection string is never logged at any level. Consider using `NpgsqlConnectionStringBuilder` to mask credentials in any diagnostic output.

**PRIORITY:** P2

---

### SEC-03: No Rate Limiting on Financial Operations

**SEVERITY:** 🟠 HIGH
**CONFIDENCE:** HIGH

**Problem:**
The rate limiter applies two policies:
- `auth` → 10/min (on auth endpoints)
- `default` → 100/min (everything else)

Financial endpoints use the `default` policy:
- `POST /api/Order/create-from-builder` — 100 orders/min possible
- `POST /api/WalletTransactions/topup` — 100 top-ups/min possible
- `POST /api/Order/reorder/{id}` — 100 reorders/min possible

While the wallet has a max balance cap and balance checks prevent overdraw, 100 rapid-fire order creation attempts per minute could:
1. Create unnecessary DB load
2. Enable brute-force probing of balance amounts
3. Overwhelm Hangfire with downstream scheduled orders

**Recommendation:** Add a `financial` rate limit policy (10-20/min) applied to:
- Order creation endpoints
- Wallet top-up endpoints
- Reorder endpoints

**PRIORITY:** P1

---

### SEC-04: `InvalidOperationException` Messages Leak to Client

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `GlobalExceptionMiddleware.cs` (Line 62-63)

**Problem:**
```csharp
InvalidOperationException ioe =>
    (StatusCodes.Status400BadRequest, ErrorCodes.InvalidOperation, ioe.Message),
```

`InvalidOperationException` is used for both business logic errors AND internal errors. The handler passes `ioe.Message` directly to the client. Some internal exception messages may leak implementation details:

```
"ScheduledOrder #123 has no DeliveryAddressId. Cannot create Order without a delivery address."
"User 456 has no AuthMapping — cannot create ScheduledOrder."
```

These messages reveal internal entity IDs, column names, and architecture details.

**Recommendation:**
1. Create specific domain exceptions for user-facing errors (e.g., `DeliveryAddressRequiredException`)
2. Map `InvalidOperationException` to a generic message in production
3. Keep the detailed message in server logs only

**PRIORITY:** P1

---

### SEC-05: Swagger Accessible in Production (Conditionally)

**SEVERITY:** 🟢 LOW
**CONFIDENCE:** MEDIUM

**FILE:** `WebApplicationExtensions.cs`

Swagger is conditionally enabled:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

This is correct — Swagger is NOT accessible in production. ✅ No action needed.

---

### SEC-06: Health Endpoints Expose System Details

**SEVERITY:** 🟢 LOW
**CONFIDENCE:** HIGH

**FILE:** `WebApplicationExtensions.cs` (Health check mapping)

Health endpoints (`/health/live`, `/health/ready`, `/health`) are unauthenticated. The `/health/ready` endpoint checks PostgreSQL connectivity and Hangfire availability — if it returns details about failures, it could reveal infrastructure information.

**Recommendation:** Ensure health check responses use `Healthy/Unhealthy` status only, without exposing connection strings or error details.

**PRIORITY:** P3

---

### SEC-07: Hangfire Dashboard Basic Auth Credentials

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `WebApplicationExtensions.cs` (Hangfire dashboard config)

Hangfire dashboard uses HTTP Basic Auth with credentials from environment variables. Basic Auth sends credentials as Base64 (not encrypted) on every request. While HTTPS encrypts the transport layer, Basic Auth is considered a weak mechanism.

**Recommendation:** Acceptable for internal-only access. If the Hangfire dashboard is publicly accessible (e.g., on Render), consider:
1. IP allowlisting at the reverse proxy level
2. OAuth-based Hangfire auth filter

**PRIORITY:** P2

---

### SEC-08: No CSRF Protection

**SEVERITY:** 🟢 LOW (API-only)
**CONFIDENCE:** HIGH

Since this is a stateless JWT API (no cookies for auth), CSRF protection is not needed. JWT tokens are sent via `Authorization: Bearer` header, which cannot be triggered by cross-site form submissions.

**Status:** ✅ Not applicable — correctly handled.

---

### SEC-09: Wallet Amount Validation — Minimum Amounts

**SEVERITY:** ✅ RESOLVED
**CONFIDENCE:** HIGH

Wallet amounts are validated:
- `Amount > 0` (database CHECK constraint)
- `MinTopUpAmount` (service-level check for customer top-ups)
- `MaxWalletBalance` (cap on total balance)
- Admin credits bypass `MinTopUpAmount` but still respect `MaxWalletBalance`

**Status:** ✅ Correctly implemented.

---

## SECRETS MANAGEMENT

| Secret | Storage | Accessed Via |
|--------|---------|-------------|
| Database Connection String | Environment Variable | `DATABASE_SESSION_URL` / `DATABASE_URL` |
| Supabase URL | Environment Variable | `Supabase__Url` |
| Supabase Anon Key | Environment Variable | `Supabase__AnonKey` |
| Supabase Service Role Key | Environment Variable | `Supabase__ServiceRoleKey` |
| Hangfire Dashboard Credentials | Environment Variable | `HangfireDashboard__Username/Password` |
| Seq API Key | Environment Variable | `Logging__SeqApiKey` |

**Assessment:** All secrets are externalized via environment variables. No hardcoded secrets found in source code. `appsettings.json` contains only empty placeholder values. ✅

---

## DATA ACCESS SCOPING VERIFICATION

| Operation | User Scoping | Method |
|-----------|-------------|--------|
| Get my orders | ✅ userId from JWT | `GetUserOrdersAsync(userId)` |
| Get my wallet | ✅ userId from JWT | `GetUserTransactionsAsync(userId)` |
| Get my subscriptions | ✅ userId from JWT | `GetSubscriptionsByUserIdAsync(userId)` |
| Get my scheduled orders | ✅ authId from JWT | `GetByAuthIdAndDateAsync(authId, date)` |
| Create order | ✅ userId from JWT | `CreateOrderFromMealBuilderAsync(dto, userId)` |
| Top up wallet | ✅ userId from JWT | `TopUpWalletAsync(userId, dto)` |
| Modify scheduled order | ✅ authId from JWT | `ModifyScheduledOrderAsync(id, authId, dto)` |
| Rate order | ✅ userId + ownership check | `order.UserId != userId → denied` |
| Reorder | ✅ userId + ownership check | `pastOrder.UserId != userId → denied` |
| Admin: credit wallet | ✅ Admin role + target userId | `[Authorize(Roles = "Admin")]` |
| Admin: update order status | ✅ Admin role | `[Authorize(Roles = "Admin")]` |


---

# SOVVA BACKEND — PERFORMANCE AUDIT

**Generated:** 2026-05-22
**Phase:** 5 — Performance Analysis
**Focus:** Database query patterns, N+1, hot paths, caching, middleware overhead

---

## HOT PATH ANALYSIS

The most frequently executed code paths, ranked by estimated frequency:

| Rank | Path | Frequency | DB Queries | Assessment |
|------|------|-----------|-----------|-----------|
| 1 | **AuthMiddleware (every request)** | Every API call | 2 queries (user + wallet SUM) | 🔴 CRITICAL bottleneck |
| 2 | **Dashboard summary** | Every app open | 5 sequential queries | 🟡 Improvable |
| 3 | **Wallet balance check** | Every order/topup | 1 query (SUM) | ✅ OK but could use covering index |
| 4 | **Get scheduled orders by date** | Every dashboard + cart view | 1 query with 3 includes | ✅ OK |
| 5 | **Midnight confirmation job** | Once/day, N orders | 4+ queries per order | 🟡 Batch-optimizable |
| 6 | **Subscription generation job** | Once/day, N subs | 5 batch + 3 per sub | ✅ Already batch-optimized |

---

## FINDINGS

---

### PERF-01: AuthMiddleware — 2 DB Queries Per Request

**SEVERITY:** 🔴 CRITICAL (latency at scale)
**CONFIDENCE:** HIGH
**FILE:** `AuthMiddleware.cs` Line 65 → `UserRepository.GetUserByAuthIdIncludingDeletedAsync`

**Current Cost:**
```
Every authenticated request:
  Query 1: SELECT Users + AuthMapping WHERE AuthId = @authId        (~2ms)
  Query 2: SELECT SUM(WalletTransactions) WHERE UserId = @userId    (~3ms)
  Total: ~5ms added to EVERY request
```

**Impact at Scale:**
- 50 users × 10 requests/min = 500 requests/min = 1000 unnecessary queries/min
- 200 users × 10 requests/min = 4000 unnecessary queries/min

**Root Cause:** `UserRepository.GetByIdAsync` and `GetUserByAuthIdIncludingDeletedAsync` both compute `User.WalletBalance` by running a SUM query on WalletTransactions. The AuthMiddleware doesn't need the wallet balance — it only needs UserId, Role, and AccountStatus.

**Fix Options (choose one):**

1. **Quick Fix — Cache auth enrichment in-memory (30s TTL):**
```csharp
// In AuthMiddleware:
var cacheKey = $"auth:{authGuid}";
if (!_cache.TryGetValue(cacheKey, out AuthUserInfo info))
{
    info = await userService.GetAuthInfoAsync(authGuid); // NEW: lightweight query
    _cache.Set(cacheKey, info, TimeSpan.FromSeconds(30));
}
```

2. **Better Fix — Lightweight auth-only repository method:**
```csharp
// New method: no WalletBalance SUM, no Include(AuthMapping)
public async Task<(int UserId, UserRole Role, AccountStatus Status)?> GetAuthInfoByAuthIdAsync(Guid authId)
{
    return await _context.UserAuthMappings
        .Where(m => m.AuthId == authId)
        .Select(m => new { m.User.UserId, m.User.Role, m.User.AccountStatus })
        .FirstOrDefaultAsync();
}
```
This reduces to 1 query, no wallet SUM, no navigation loading.

**PRIORITY:** P0 — Fix before scaling past 50 users

---

### PERF-02: Dashboard — Sequential Queries with Redundant Wallet SUM

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH
**FILE:** `DashboardService.cs` Lines 60-69

**Current Flow:**
```
GetProfileAsync(userId)           → 2 queries (user + wallet SUM)   ← SUM #1
GetUserBalanceAsync(userId)       → 1 query (wallet SUM)            ← SUM #2 (DUPLICATE)
GetByUserIdAsync(userId, 1, 20)   → 2 queries (COUNT + SELECT)
GetActiveSubscriptionsAsync       → 1 query
GetTomorrowOrdersAsync            → 1 query
                                    ─────────
                                    7 queries total (should be 6)
```

The wallet balance is computed twice — once inside `GetByIdAsync` (called by `GetProfileAsync`) and once explicitly.

**Fix:** Either:
1. Remove wallet SUM from `GetByIdAsync` and use the explicit balance query
2. Or extract balance from the already-loaded User entity

**Savings:** 1 SUM query per dashboard load (~3ms)

**PRIORITY:** P2

---

### PERF-03: `GetUserBalanceAsync` — Table Scan on Large Transaction History

**SEVERITY:** 🟡 MEDIUM (grows with data)
**CONFIDENCE:** HIGH
**FILE:** `WalletTransactionRepository.cs` Lines 60-65

**Current Implementation:**
```csharp
var credits = await _context.WalletTransactions
    .Where(wt => wt.UserId == userId && wt.Type == WalletConstants.Credit)
    .SumAsync(wt => (decimal?)wt.Amount) ?? 0m;

var debits = await _context.WalletTransactions
    .Where(wt => wt.UserId == userId && wt.Type == WalletConstants.Debit)
    .SumAsync(wt => (decimal?)wt.Amount) ?? 0m;
```

This runs **2 separate SUM queries**. Each requires scanning ALL wallet transactions for that user. As transaction history grows (e.g., 365 days of daily orders = 365+ transactions per user), this becomes increasingly expensive.

**Fix Options:**

1. **Combine into single query:**
```sql
SELECT 
    COALESCE(SUM(CASE WHEN "Type" = 'Credit' THEN "Amount" ELSE -"Amount" END), 0) as Balance
FROM "WalletTransactions"
WHERE "UserId" = @userId
```
2. **Add covering index:**
```sql
CREATE INDEX "IX_WalletTransactions_UserId_Type_Amount" 
    ON "WalletTransactions" ("UserId", "Type") INCLUDE ("Amount");
```
3. **Long-term: Materialized balance with periodic reconciliation** (only if 10k+ transactions per user)

**PRIORITY:** P1 — combine the 2 queries into 1 immediately

---

### PERF-04: `GetUserWalletSummaryAsync` — 4 Separate Queries

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH
**FILE:** `WalletTransactionRepository.cs` Lines 86-107

Four separate queries for what should be one:
```csharp
var totalCredits = await ...SumAsync(...)  // Query 1
var totalDebits  = await ...SumAsync(...)  // Query 2
var count        = await ...CountAsync()   // Query 3
var lastDate     = await ...FirstOrDefaultAsync() // Query 4
```

**Fix — Single aggregate query:**
```sql
SELECT 
    COALESCE(SUM(CASE WHEN "Type" = 'Credit' THEN "Amount" END), 0) AS TotalCredits,
    COALESCE(SUM(CASE WHEN "Type" = 'Debit'  THEN "Amount" END), 0) AS TotalDebits,
    COUNT(*) AS TransactionCount,
    MAX("CreatedAt") AS LastTransactionDate
FROM "WalletTransactions"
WHERE "UserId" = @userId
```

**Savings:** 3 DB roundtrips per summary request (~9ms)
**PRIORITY:** P2

---

### PERF-05: Midnight Job — Per-Order Transaction Pattern

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH
**FILE:** `ScheduledOrderService.cs` → `ConfirmSingleOrderAsync`

**Current pattern per order:**
```
1. Check existing Order (SELECT)
2. Check existing WalletTransaction (SELECT)
3. AtomicDebitAsync (INSERT...SELECT)
4. ConfirmScheduledOrderAsync → INSERT Order
5. MarkAsProcessedAsync → UPDATE ScheduledOrder
```

= **5 queries per order** in the midnight job

For 100 daily orders: 500 queries in a single job run.

**Fix Options:**
1. **Batch the reads** — pre-load existing Orders and WalletTransactions for all ScheduledOrderIds
2. **Pipeline the writes** — use EF Core `SaveChanges` batching for Order inserts
3. **Most impactful:** Pre-load step 1+2 in batch before the loop

The subscription generation job already does this (batch loads users, meals, addresses). The confirmation job should follow the same pattern.

**PRIORITY:** P1

---

### PERF-06: `SubscriptionService.CreateSubscriptionAsync` — N+1 on MealOptionIngredients

**SEVERITY:** 🟢 LOW (happens once per subscription create)
**CONFIDENCE:** HIGH
**FILE:** `SubscriptionService.cs` Lines 144-159

```csharp
foreach (var option in mealWithDetails.MealOptions)
{
    foreach (var moi in option.MealOptionIngredients)
    {
        await _userMealIngredientRepository.AddAsync(new UserMealIngredient { ... });
    }
}
await _userMealIngredientRepository.SaveChangesAsync();
```

Each `AddAsync` is a separate call (though EF Core batches on `SaveChangesAsync`). This is actually fine because EF Core will batch the inserts. The `SaveChangesAsync` at the end sends all inserts in a single round-trip.

**Status:** ✅ Acceptable — EF Core batching handles this efficiently.

---

### PERF-07: `MapToEnhancedDto` — Heavy Include Chain

**SEVERITY:** 🟡 MEDIUM (admin endpoint)
**CONFIDENCE:** HIGH
**FILE:** `OrderService.cs` Lines 169-248

The enhanced order history mapping requires deep include chains:
```
Order → UserMeal → UserMealIngredients → Ingredient
Order → SourceScheduledOrder → Ingredients → Ingredient
```

These queries load the full ingredient tree for every order. For admin views showing 50 orders, this is expensive.

**Fix:** Consider a read-only projection query:
```csharp
.Select(o => new EnhancedOrderHistoryDto { ... })
```
This avoids loading full entities and lets PostgreSQL compute the projection.

**PRIORITY:** P2

---

### PERF-08: EF Core Change Tracker Overhead in Batch Jobs

**SEVERITY:** 🟢 LOW
**CONFIDENCE:** MEDIUM
**FILE:** `ScheduledOrderRepository.cs` Lines 151-161

The `GetScheduledOrdersForDateAsync(DateOnly date)` method for the midnight job does NOT use `AsNoTracking`:
```csharp
return await _context.ScheduledOrders
    .Include(so => so.Ingredients)
    .Where(...)
    .ToListAsync();
```

This loads all scheduled orders for the day into the change tracker. For 100+ orders with 5+ ingredients each, this creates 600+ tracked entities.

**Fix:** Since the midnight job uses raw SQL for updates (`MarkAsProcessedAsync`, `MarkAsAsync`), the tracked entities are never used for writes. Add `AsNoTracking()`.

**PRIORITY:** P2

---

## CACHING ANALYSIS

| Layer | Current Caching | Assessment |
|-------|----------------|-----------|
| AuthMiddleware | ❌ None | 🔴 Critical — cache user auth info |
| Dashboard profile | ✅ IMemoryCache (5min TTL) | ✅ Good |
| Wallet balance | ❌ None | ⚠️ Acceptable — real-time accuracy needed |
| Meal catalogue | ❌ None | 🟡 Consider caching (changes infrequently) |
| Ingredients | ❌ None | 🟡 Consider caching (changes infrequently) |
| Subscription list | ❌ None | ✅ Acceptable — user-specific, changes on write |

---

## CONNECTION POOL ANALYSIS

**Configuration:**
```json
{
    "MaxPoolSize": 10,
    "MinPoolSize": 0,
    "ConnectionIdleLifetime": 60,
    "Keepalive": 30,
    "CommandTimeout": 30
}
```

**Assessment:**
- MaxPoolSize=10 is LOW for a web application with Hangfire workers
- Hangfire uses 2 workers, each holding a connection during job execution
- Leaves 8 connections for web requests
- At peak (50+ concurrent users), connection pool exhaustion is possible

**Recommendation:**
- Increase to `MaxPoolSize=20` for Render free/hobby tier
- Ensure Hangfire uses a separate connection string (or lower worker count)
- Monitor with Npgsql connection pool metrics

**PRIORITY:** P2

---

## PERFORMANCE SCORECARD

| Area | Score | Notes |
|------|-------|-------|
| Authentication path | 🔴 D | 2 DB queries per request |
| Wallet operations | 🟡 C+ | Dual SUM queries, but atomic |
| Order creation | ✅ B+ | Well-transactioned, one-shot |
| Subscription generation | ✅ A | Batch-loaded, efficient |
| Dashboard | 🟡 B- | Sequential but functional |
| Midnight job | 🟡 C+ | Per-order queries, no batching |
| Index coverage | ✅ B+ | Good, 2 missing recommended |
| Caching | 🟡 C | Only profile cached |


---

# SOVVA BACKEND — BUSINESS LOGIC AUDIT

**Generated:** 2026-05-22
**Phase:** 7 — Business Logic Integrity Analysis
**Scope:** Edge cases, race conditions, data consistency, idempotency, state machines

---

## 1. ORDER LIFECYCLE INTEGRITY

### Order State Machine — Verified ✅

```
Pending → Confirmed → Preparing → OutForDelivery → Delivered
                                                  → Cancelled
```

**Guard Implementation:** `Order.TransitionTo(OrderStatus newStatus)` (Domain layer)
- ✅ Blocks transitions OUT of `Cancelled` and `Delivered` (terminal states)
- ⚠️ No guard against invalid forward transitions (e.g., `Pending → Delivered` skipping `Confirmed`)

**BL-01: Missing Intermediate State Validation**
- **Severity:** 🟢 LOW
- **Issue:** `TransitionTo` only blocks exits from terminal states, but allows any forward jump
- **Risk:** An admin could accidentally set `Pending → Delivered` via admin status update endpoint
- **Fix:** Add allowed transition map: `Pending→Confirmed, Confirmed→Preparing, ...`
- **Priority:** P3 — Currently only admin can change status, and the flow works correctly in automated paths

---

### ScheduledOrder State Machine — Verified ✅

```
Scheduled → Processing → Processed (success)
                       → Failed    (insufficient balance / error)
         → Cancelled   (user cancel)
```

**Guard Implementation:** `MarkAsAsync` uses raw SQL — no domain-level guard. Status transitions are enforced by the midnight job's control flow rather than domain invariants.

**BL-02: ScheduledOrder Status Can Be Set to Arbitrary String**
- **Severity:** 🟡 MEDIUM
- **Issue:** `MarkAsAsync(int id, string status)` accepts any string. If code passes `"cancelled"` (lowercase) instead of `"Cancelled"`, it bypasses the CHECK constraint and fails at DB level
- **Evidence:** Line 731 in ScheduledOrderService: `await _scheduledOrderRepository.MarkAsAsync(scheduledOrder.ScheduledOrderId, "cancelled");` — lowercase "c"!
- **Fix:** Use `ScheduledOrderStatus` enum and `.ToString()` in all status assignments
- **Priority:** P1 — This will cause a PostgreSQL CHECK constraint violation at runtime

---

## 2. WALLET INTEGRITY

### Double-Debit Prevention — Verified ✅

The midnight job has a robust 3-layer idempotency check:

```
Layer 1: Check if Order row exists for this ScheduledOrderId
Layer 2: Check if WalletTransaction exists for this ScheduledOrderId
Layer 3: AtomicDebitAsync = INSERT...SELECT WHERE balance >= amount (single SQL)
```

**Assessment:**
- ✅ If both Order + WalletTransaction exist → skip (already processed)
- ✅ If Order exists but no WalletTransaction → complete payment only
- ✅ If neither exists → full flow (debit + create Order + mark processed)
- ✅ AtomicDebitAsync is a single SQL statement — PostgreSQL guarantees atomicity
- ✅ Advisory locks used for manual wallet operations (topup, CreateTransactionAsync)

### Balance Consistency Verification

**BL-03: `User.WalletBalance` Column Diverges from Ledger**
- **Severity:** 🟡 MEDIUM
- **Issue:** The `Users.WalletBalance` column is populated by `UserRepository.GetByIdAsync` from a live SUM query, but it's a physical column in the DB. If any code path reads `User.WalletBalance` directly (without going through the repository), it will see stale/zero data.
- **Current Mitigation:** Service layer uses `IWalletTransactionRepository.GetUserBalanceAsync()` for all financial decisions. `User.WalletBalance` is only for backward compatibility in DTOs.
- **Risk:** A future developer adds code that reads `User.WalletBalance` directly, trusting it as authoritative
- **Fix:** Consider making `WalletBalance` a computed column or removing it entirely and always using the ledger SUM
- **Priority:** P2

**BL-04: `CK_Users_WalletBalance >= 0` Can Block Legitimate Operations**
- **Severity:** 🟡 MEDIUM
- **Issue:** The CHECK constraint `"WalletBalance" >= 0` exists on the Users table. Since `WalletBalance` is computed on read (not written to directly anymore), this shouldn't fire. But if any EF Core operation touches the User entity and SaveChanges writes back the computed value, a race condition could write a negative balance to this column.
- **Current Protection:** Wallet operations use `AtomicDebitAsync` which never touches the Users table. Manual operations go through `CreateTransactionAsync` which uses advisory locks.
- **Risk:** LOW — the column is effectively dead for writes
- **Priority:** P3

---

## 3. SUBSCRIPTION LOGIC INTEGRITY

### Duplicate Subscription Prevention — Verified ✅

Two layers of protection:
1. **Application layer:** `GetAnyActiveSubscriptionByMealIdAsync` check before creation
2. **Database layer:** Unique partial index `UX_Subscriptions_ActiveUserMeal` on `(UserId, UserMealId) WHERE Active = true`

**~~BL-05: Subscription Unique Index Filter Column Name~~** ✅ VERIFIED
- **Severity:** ✅ RESOLVED
- **Verdict:** `AppDbContextModelSnapshot.cs` Line 611-613 confirms `Subscription.IsActive` maps to DB column `"Active"` via explicit `HasColumnName("Active")`. The filter `"Active" = true` is CORRECT.
- **Priority:** N/A

### Subscription Expiry — Verified ✅

```
ExpireSubscriptionsAsync() runs at 23:50 IST:
  1. Fetch all active subscriptions
  2. Filter where EndDate <= today
  3. Set IsActive = false
  4. Batch update
```

✅ Runs BEFORE sync-subscription-dates (23:55) and midnight confirm (00:00) — correct ordering.

### Weekly Schedule Edge Cases

**BL-06: FindNextWeeklyDate — Sunday (DayOfWeek=0) Handling**
- **Severity:** ✅ RESOLVED
- **Evidence:** Both `SubscriptionService.FindNextWeeklyDate` and `SubscriptionSchedulingService.FindNextWeeklyDate` use `Cast<int?>().FirstOrDefault(d => d > current)` — nullable int prevents Sunday=0 from being confused with default(int)
- **Status:** ✅ Correctly handled

### Subscription Order Generation — One Order Per Day Per Subscription

**BL-07: Duplicate Guard in Subscription Generation**
- **Severity:** ✅ VERIFIED
- **Evidence:**
  1. Application-level check: `GetBySubscriptionIdAndDateAsync(subId, deliveryDay)` — if non-null, skip
  2. DB-level: Unique index `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` with `WHERE SubscriptionId IS NOT NULL`
- **Status:** ✅ Two-layer protection

---

## 4. MIDNIGHT JOB EDGE CASES

### BL-08: Job Processes Wrong Date on DST Change
- **Severity:** 🟢 LOW (India doesn't observe DST)
- **IST = UTC+5:30 (no DST transitions)**
- **Status:** ✅ Not applicable for current deployment

### BL-09: Midnight Job Failure Recovery
- **Severity:** ✅ VERIFIED
- **Implementation:**
  - Hangfire retries with `[AutomaticRetry(Attempts = 3)]`
  - `JobFailureAlertFilter` logs critical failures
  - Per-order isolation: each order has its own try/catch — one failure doesn't block others
  - Idempotency: safe to rerun the entire job — existing processed orders are skipped
- **Status:** ✅ Robust

### BL-10: ConfirmSingleOrderAsync — Partial Failure Scenario

```
ExecuteInTransactionAsync:
  Step 4: AtomicDebitAsync      → SQL: INSERT INTO WalletTransactions (single statement)
  Step 5: ConfirmScheduledOrder → SQL: INSERT INTO Orders
  Step 6: MarkAsProcessedAsync  → SQL: UPDATE ScheduledOrders
```

**Question:** If Step 5 fails after Step 4 succeeds, is the wallet debit rolled back?

**Answer:** YES — `ExecuteInTransactionAsync` wraps all three in a DB transaction. If Step 5 throws, the transaction rolls back, including the `AtomicDebitAsync` INSERT. ✅

**But:** `AtomicDebitAsync` uses `ExecuteSqlRawAsync` which creates its own implicit transaction if not inside a user transaction. Inside `ExecuteInTransactionAsync`, it participates in the outer transaction thanks to `NpgsqlRetryingExecutionStrategy`. ✅ Verified.

### BL-11: `MarkAsAsync` Uses String Status "cancelled" (Lowercase)

**FILE:** `ScheduledOrderService.cs` Line 731
**Severity:** 🔴 P1 BUG

```csharp
await _scheduledOrderRepository.MarkAsAsync(
    scheduledOrder.ScheduledOrderId, "cancelled");  // ← lowercase!
```

The CHECK constraint expects `'Cancelled'` (PascalCase). This will throw a PostgreSQL constraint violation error:
```
ERROR: new row for relation "ScheduledOrders" violates check constraint "CK_ScheduledOrders_Status"
```

This code path is hit when: existing Order row was found but wallet debit fails (insufficient balance during idempotent retry). The order would stay in `Scheduled`/`Processing` state instead of being marked as `Cancelled`.

**Fix:** Change to `ScheduledOrderStatus.Cancelled.ToString()` or `"Cancelled"`.

---

## 5. TIME MANAGEMENT INTEGRITY

### `IAppTimeProvider` Usage Audit

| Service | Uses `_time` | Uses `DateTime.UtcNow` Directly | Assessment |
|---------|-------------|--------------------------------|-----------|
| `ScheduledOrderService` | ✅ | ❌ | ✅ Clean |
| `SubscriptionSchedulingService` | ✅ | ❌ | ✅ Clean |
| `SubscriptionService` | ✅ | ❌ | ✅ Clean |
| `OrderService` | ✅ | ✅ (Line 586) | ⚠️ 1 violation |
| `WalletTransactionService` | ❌ (not injected) | ❌ | ✅ N/A (uses repos) |
| `DashboardService` | ✅ | ❌ | ✅ Clean |
| `UserMealIngredientService` | ❌ | ✅ (Lines 29,30,48,49) | 🔴 4 violations |
| `UserAddressService` | ❌ | ✅ (Lines 62,118) | 🔴 2 violations |
| `IngredientCategoryService` | ❌ | ✅ (Lines 36,37) | 🔴 2 violations |
| `MealOptionService` | ❌ | ✅ (Lines 26,27) | 🔴 2 violations |
| `UserMealService` | ❌ | ✅ (Lines 38,39,57) | 🔴 3 violations |
| `IngredientService` | ❌ | ✅ (Lines 55,56,83,98) | 🔴 4 violations |
| `ServiceableLocationService` | ❌ | ✅ (Lines 81,123) | 🔴 2 violations |
| `MealOptionIngredientService` | ❌ | ✅ (Lines 24,25) | 🔴 2 violations |

**BL-12: 21 instances of `DateTime.UtcNow` in Application Services**
- **Severity:** 🟡 MEDIUM
- **Issue:** 8 services bypass `IAppTimeProvider` and use `DateTime.UtcNow` directly. While functionally identical in production (since `AppTimeProvider.UtcNow` returns `DateTime.UtcNow`), this:
  1. Breaks testability — tests cannot mock time
  2. Violates the established pattern in critical services
  3. All are for `CreatedAt`/`UpdatedAt` which TimestampInterceptor overwrites anyway
- **Fix:** Either inject `IAppTimeProvider` into all services, or (better) rely entirely on TimestampInterceptor and remove manual timestamp assignments
- **Priority:** P2 (most are overwritten by interceptor — cosmetic inconsistency)

---

## 6. EXCEPTION HANDLING PATTERNS

### Broad `catch (Exception ex)` Audit

| Location | Behavior | Assessment |
|----------|---------|-----------|
| `SubscriptionService.CreateFirstScheduledOrderAsync` L296 | Logs + returns `(false, ex.Message)` | ✅ Correct — graceful degradation |
| `ScheduledOrderService.DuplicateScheduledOrderAsync` L334 | Wraps in `InvalidOperationException` + rethrows | ⚠️ Hides original exception type |
| `ScheduledOrderService.ConfirmSingleOrderAsync` L779 | Logs + marks as Failed (for balance errors) | ✅ Correct |
| `SubscriptionSchedulingService` L218 | Logs + increments failedCount | ✅ Correct — per-item isolation |
| `CurrentUserService.GetAuthId` L64 | Logs + returns null | ⚠️ Swallows all exceptions |
| `CurrentUserService.GetCurrentUserIdAsync` L106 | Logs + returns null | ⚠️ Swallows all exceptions |
| `CurrentUserService.InvalidateCacheAsync` L159 | Logs + swallows | ⚠️ Swallows (but acceptable for cache) |
| `MealService` L686 | Needs verification | — |

**BL-13: CurrentUserService Swallows All Exceptions**
- **Severity:** 🟡 MEDIUM
- **Issue:** `GetAuthId()` and `GetCurrentUserIdAsync()` catch `Exception` and return null. If the database is down or there's a configuration error, the user gets a silent null instead of a proper error.
- **Impact:** The caller sees "user not found" instead of "database connection failed"
- **Fix:** Only catch expected exceptions (e.g., `InvalidOperationException`). Let infrastructure exceptions propagate.
- **Priority:** P2

**BL-14: CurrentUserService Uses String Interpolation in LogError**
- **Severity:** 🟢 LOW
- **Evidence:** Lines 66, 108, 161 — `_logger.LogError($"❌ ... {ex.Message}")`
- **Issue:** String interpolation in structured logging prevents log aggregation by template
- **Fix:** Use `_logger.LogError(ex, "❌ CurrentUserService GetAuthId error")`
- **Priority:** P3

---

## 7. DATA CONSISTENCY — PRICE SNAPSHOT INTEGRITY

### Order Price Calculation

| Order Type | Price Source | Assessment |
|-----------|-------------|-----------|
| Real-time order | `MealService.CalculateMealPriceAsync` (live calculation) | ✅ Current prices |
| Scheduled order (manual) | Live ingredient prices at creation time | ✅ Snapshot at creation |
| Scheduled order (subscription) | `UserMeal.TotalPrice * quantity` | ⚠️ See BL-15 |
| Reorder | `pastOrder.TotalPrice` (historical) | ✅ Uses past price |

**BL-15: Subscription Order Price Uses UserMeal.TotalPrice — Stale if Ingredients Change**
- **Severity:** 🟡 MEDIUM
- **Issue:** `SubscriptionSchedulingService` Line 192: `TotalPrice = userMeal.TotalPrice * quantity`
  - If ingredient prices change after the subscription was created, the UserMeal.TotalPrice is stale
  - The ScheduledOrderIngredients have correct unit prices (fetched from `_ingredientRepo.GetByIdsAsync`), but the `TotalPrice` on the ScheduledOrder doesn't match the sum of ingredients
- **Evidence:** `BuildIngredientListAsync` computes per-ingredient totals from current prices, but the parent `TotalPrice` uses `userMeal.TotalPrice` (stale)
- **Fix:** Compute `TotalPrice` as `SUM(ingredient.UnitPrice * quantity)` in `BuildIngredientListAsync` instead of using `userMeal.TotalPrice`
- **Priority:** P1 — financial accuracy issue

---

## 8. CONCURRENCY EDGE CASES

### BL-16: Modify + Midnight Job Race Condition
- **Scenario:** User modifies a ScheduledOrder at 11:59:59 PM IST. Midnight job starts at 12:00:00 AM.
- **Analysis:**
  1. Midnight job fetches orders (AsNoTracking read)
  2. User's modify call updates ingredients via `UpdateAsync`
  3. Midnight job processes the old snapshot
- **Mitigation:** `CanModify` is set to `false` by `MarkAsAsync` during processing, but the check happens AFTER the job fetches orders
- **Risk:** LOW — the time window is <1 second, and `AtomicDebitAsync` will use the correct price from the scheduled order (which was already fetched)
- **Priority:** P3

### BL-17: Concurrent Subscription Create + Nightly Job
- **Scenario:** User creates a subscription at 12:00:30 AM while the nightly generation job runs
- **Analysis:**
  1. `CreateSubscriptionAsync` creates a ScheduledOrder for tomorrow
  2. `GenerateScheduledOrdersFromSubscriptionsAsync` also tries to create one
  3. Unique index `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` prevents duplicate
- **Mitigation:** ✅ DB-level unique constraint catches this. Application-level duplicate check also runs.
- **Priority:** N/A — correctly handled

---

## 9. BUSINESS RULE SUMMARY

| Rule | Implementation | Status |
|------|---------------|--------|
| Min top-up amount | `WalletConstants.MinTopUpAmount` | ✅ |
| Max wallet balance | `WalletConstants.MaxWalletBalance` | ✅ |
| Balance check before order | AtomicDebitAsync WHERE balance >= amount | ✅ |
| One active subscription per meal per user | Unique partial index + application check | ⚠️ Verify column name |
| One scheduled order per subscription per day | Unique partial index + application check | ✅ |
| Subscription EndDate > StartDate | CHECK constraint + application validation | ✅ |
| Order rating 1-5 | CHECK constraint + IsPrepared guard | ✅ |
| Soft-delete users can't log in | AuthMiddleware AccountStatus check | ✅ |
| Non-root Docker user | Dockerfile USER directive | ✅ |
| Serviceable location validation | Checked on order creation + midnight job | ✅ |

---

## TOP BUSINESS LOGIC FIXES (ordered by impact)

| # | Issue | Severity | Ref |
|---|-------|---------|-----|
| 1 | `"cancelled"` lowercase in MarkAsAsync — CHECK violation | 🔴 P1 | BL-02/BL-11 |
| 2 | Subscription price uses stale `UserMeal.TotalPrice` | 🟠 P1 | BL-15 |
| 3 | Subscription unique index may use wrong column name | 🟠 P1 | BL-05 |
| 4 | `User.WalletBalance` column is a liability | 🟡 P2 | BL-03 |
| 5 | 21 `DateTime.UtcNow` violations in services | 🟡 P2 | BL-12 |
| 6 | CurrentUserService swallows all exceptions | 🟡 P2 | BL-13 |
| 7 | Order state machine allows skip transitions | 🟢 P3 | BL-01 |


---

# SOVVA BACKEND — PRIORITIZED FIX LIST

**Generated:** 2026-05-22
**Phase:** 6 — Prioritized Action Plan
**Combines:** Architecture, Security, Performance, and Database audit findings

---

## PRIORITY LEGEND

| Priority | Meaning | SLA |
|----------|---------|-----|
| **P0** | Blocks production / data corruption / security vulnerability | Fix immediately |
| **P1** | Significant performance/correctness/security improvement | Fix this sprint |
| **P2** | Code quality / minor performance / maintainability | Fix next sprint |
| **P3** | Cleanup / cosmetic / future-proofing | Backlog |

---

## P0 — CRITICAL (Fix Immediately)

### ~~FIX-001: Dockerfile .NET Version Mismatch~~ ✅ VERIFIED
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-01
- **File:** `Dockerfile` Lines 2, 12 + all `.csproj` files
- **Issue:** Initially appeared as .NET 8/9 mismatch
- **Verdict:** All `.csproj` files target `net8.0`. Dockerfile is CORRECT.
- **Status:** ✅ NO ACTION NEEDED

---

## P1 — HIGH (Fix This Sprint)

### FIX-002: AuthMiddleware DB Queries on Every Request
- **Source:** PERFORMANCE_AUDIT → PERF-01, ARCHITECTURE_AUDIT → ARCH-NEW-04
- **File:** `AuthMiddleware.cs` Line 65, `UserRepository.cs`
- **Issue:** 2 DB queries (user + wallet SUM) on every authenticated request
- **Fix:** Create lightweight `GetAuthInfoByAuthIdAsync` returning only (UserId, Role, Status) + add 30s IMemoryCache
- **Impact:** Eliminates ~5ms latency per request, reduces DB load by 60-80%
- **Effort:** 2 hours
- **Status:** ⬜ TODO

### FIX-003: ScheduledOrder Ingredient Updates May Not Persist
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-11
- **File:** `ScheduledOrderRepository.cs` Lines 205-231
- **Issue:** `UpdateAsync` copies scalar fields but NOT ingredients collection. Modify flow uses AsNoTracking entity then calls UpdateAsync — ingredient changes may be silently lost
- **Fix:** Add ingredient replacement logic in UpdateAsync, or refactor modify flow to use tracked entities
- **Effort:** 3 hours
- **Status:** ⬜ TODO — **VERIFY IN PRODUCTION** if scheduled order modifications are actually persisting ingredients

### FIX-004: Wallet Balance — Combine Dual SUM into Single Query
- **Source:** PERFORMANCE_AUDIT → PERF-03
- **File:** `WalletTransactionRepository.cs` Lines 60-65
- **Issue:** 2 separate SUM queries for credits and debits
- **Fix:** Single raw SQL: `SUM(CASE WHEN Type='Credit' THEN Amount ELSE -Amount END)`
- **Impact:** Reduces wallet balance queries from 2 to 1 (affects every request via AuthMiddleware)
- **Effort:** 30 minutes
- **Status:** ⬜ TODO

### FIX-005: Add Rate Limiting on Financial Endpoints
- **Source:** SECURITY_AUDIT → SEC-03
- **File:** `ServiceCollectionExtensions.cs` (rate limiter config)
- **Issue:** Financial endpoints use default 100/min policy — too high for order/wallet operations
- **Fix:** Add `financial` policy (15/min) to order creation, wallet topup, reorder endpoints
- **Effort:** 1 hour
- **Status:** ⬜ TODO

### FIX-006: InvalidOperationException Messages Leak Internal Details to Client
- **Source:** SECURITY_AUDIT → SEC-04
- **File:** `GlobalExceptionMiddleware.cs` Lines 62-63
- **Issue:** `ioe.Message` passed directly to client — may contain entity IDs, column names
- **Fix:** Create specific domain exceptions for user-facing errors. Map generic `InvalidOperationException` to "An error occurred processing your request" in production
- **Effort:** 3 hours
- **Status:** ⬜ TODO

### FIX-007: Midnight Job Per-Order Query Pattern
- **Source:** PERFORMANCE_AUDIT → PERF-05
- **File:** `ScheduledOrderService.cs` → `ConfirmAllScheduledOrdersAsync`
- **Issue:** 5 DB queries per order in midnight job (idempotency checks + write operations)
- **Fix:** Batch-load existing Orders and WalletTransactions for all ScheduledOrderIds before the loop
- **Impact:** Reduces midnight job DB queries from 5N to N+2 (where N = number of orders)
- **Effort:** 4 hours
- **Status:** ⬜ TODO

### FIX-024: 🔴 `"cancelled"` Lowercase — CHECK Constraint Violation
- **Source:** BUSINESS_LOGIC_AUDIT → BL-02 / BL-11
- **File:** `ScheduledOrderService.cs` Line 731
- **Issue:** `MarkAsAsync(id, "cancelled")` uses lowercase "c". PostgreSQL CHECK constraint expects `'Cancelled'` (PascalCase). This will throw a constraint violation at runtime during idempotent retry of failed payments.
- **Fix:** Change to `ScheduledOrderStatus.Cancelled.ToString()` or `"Cancelled"`
- **Effort:** 5 minutes
- **Status:** ⬜ TODO

### FIX-025: Subscription Order Price Uses Stale `UserMeal.TotalPrice`
- **Source:** BUSINESS_LOGIC_AUDIT → BL-15
- **File:** `SubscriptionSchedulingService.cs` Line 192
- **Issue:** `TotalPrice = userMeal.TotalPrice * quantity` uses the price snapshot from when the subscription was created. If ingredient prices change, the ScheduledOrder.TotalPrice won't match SUM(ScheduledOrderIngredient.TotalPrice)
- **Fix:** Compute TotalPrice as `SUM(ingredient.UnitPrice * quantity)` from the resolved ingredients list
- **Effort:** 30 minutes
- **Status:** ⬜ TODO

### ~~FIX-026: Verify Subscription Unique Index Column Name~~ ✅ VERIFIED
- **Source:** BUSINESS_LOGIC_AUDIT → BL-05, ARCHITECTURE_AUDIT → ARCH-NEW-02
- **File:** `SubscriptionConfiguration.cs` Lines 42, 51 + `AppDbContextModelSnapshot.cs` Line 611-613
- **Verdict:** `IsActive` maps to DB column `"Active"` via explicit `HasColumnName("Active")`. Filter expressions are CORRECT.
- **Status:** ✅ NO ACTION NEEDED

---

## P2 — MEDIUM (Fix Next Sprint)

### ~~FIX-008: SubscriptionConfiguration Column Name Mismatch~~ ✅ VERIFIED
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-02
- **File:** `SubscriptionConfiguration.cs` + `AppDbContextModelSnapshot.cs`
- **Verdict:** Explicit `HasColumnName("Active")` mapping makes filter correct. No mismatch.
- **Status:** ✅ NO ACTION NEEDED

### FIX-009: Dashboard Redundant Wallet SUM Query
- **Source:** PERFORMANCE_AUDIT → PERF-02, ARCHITECTURE_AUDIT → ARCH-NEW-09
- **File:** `DashboardService.cs` Lines 60, 66
- **Issue:** Wallet balance computed twice (once in GetByIdAsync, once explicitly)
- **Fix:** Skip wallet SUM inside GetByIdAsync for dashboard context, or reuse computed value
- **Effort:** 1 hour
- **Status:** ⬜ TODO

### FIX-010: Remove `IsConcurrencyToken()` from `User.WalletBalance`
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-03
- **File:** `UserConfiguration.cs` Line 23
- **Issue:** Concurrency token on a computed property is meaningless and may cause spurious concurrency exceptions
- **Fix:** Remove `.IsConcurrencyToken()` + generate migration
- **Effort:** 30 minutes
- **Status:** ⬜ TODO

### FIX-011: `RateOrderAsync` Uses `DateTime.UtcNow` Directly
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-06
- **File:** `OrderService.cs` Line 586
- **Issue:** Bypasses `IAppTimeProvider` — inconsistent with rest of codebase
- **Fix:** Replace `DateTime.UtcNow` with `_time.UtcNow`
- **Effort:** 5 minutes
- **Status:** ⬜ TODO

### FIX-012: `DuplicateScheduledOrderAsync` Missing ExpiresAt
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-08
- **File:** `ScheduledOrderService.cs` Lines 287-303
- **Issue:** Duplicate order gets `ExpiresAt = DateTime.MinValue` (0001-01-01)
- **Fix:** Add `ExpiresAt = _time.ToUtc(...)` when duplicating
- **Effort:** 10 minutes
- **Status:** ⬜ TODO

### FIX-013: `GetUserWalletSummaryAsync` — 4 Queries → 1
- **Source:** PERFORMANCE_AUDIT → PERF-04
- **File:** `WalletTransactionRepository.cs` Lines 86-107
- **Issue:** 4 separate queries for credits, debits, count, lastDate
- **Fix:** Single aggregate raw SQL query
- **Effort:** 1 hour
- **Status:** ⬜ TODO

### FIX-014: Add Missing Database Indexes
- **Source:** DB_SCHEMA.md → Index Coverage Analysis
- **Issue:** Missing covering index on wallet balance queries, missing index on Orders.ScheduledOrderId
- **Fix:**
  ```sql
  CREATE INDEX IX_WalletTransactions_UserId_Type_Amount ON "WalletTransactions" ("UserId","Type") INCLUDE ("Amount");
  CREATE INDEX IX_Orders_ScheduledOrderId ON "Orders" ("ScheduledOrderId") WHERE "ScheduledOrderId" IS NOT NULL;
  ```
- **Effort:** 30 minutes (+ migration)
- **Status:** ⬜ TODO

### FIX-015: Connection String Credentials in Diagnostics
- **Source:** SECURITY_AUDIT → SEC-02
- **File:** `ServiceCollectionExtensions.cs`
- **Issue:** Connection string resolution path logged — ensure no credential leakage
- **Fix:** Use `NpgsqlConnectionStringBuilder` to mask credentials in any output
- **Effort:** 30 minutes
- **Status:** ⬜ TODO

### FIX-016: Midnight Job Change Tracker Overhead
- **Source:** PERFORMANCE_AUDIT → PERF-08
- **File:** `ScheduledOrderRepository.cs` Lines 151-161
- **Issue:** Midnight job loads entities into change tracker but uses raw SQL for writes
- **Fix:** Add `AsNoTracking()` to `GetScheduledOrdersForDateAsync(DateOnly date)`
- **Effort:** 5 minutes
- **Status:** ⬜ TODO

### FIX-017: Increase Connection Pool Size
- **Source:** PERFORMANCE_AUDIT → Connection Pool Analysis
- **File:** `appsettings.json` / environment config
- **Issue:** MaxPoolSize=10 may be insufficient with Hangfire workers + concurrent web requests
- **Fix:** Increase to 20 and monitor pool usage
- **Effort:** 5 minutes
- **Status:** ⬜ TODO

---

## P3 — LOW (Backlog)

### FIX-018: Remove Dead Code — `ExtractUserInfoFromToken`
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-05
- **File:** `AuthMiddleware.cs` Lines 155-161
- **Fix:** Delete the method
- **Status:** ⬜ TODO

### FIX-019: Remove Unused Imports in SubscriptionService
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-07
- **File:** `SubscriptionService.cs` Lines 5-6
- **Fix:** Remove `System.ComponentModel.DataAnnotations` imports
- **Status:** ⬜ TODO

### FIX-020: Remove Redundant Manual Timestamp Assignments
- **Source:** ARCHITECTURE_AUDIT → ARCH-NEW-13
- **Files:** `SubscriptionSchedulingService.cs`, `SubscriptionService.cs` (multiple locations)
- **Fix:** Let TimestampInterceptor handle timestamps exclusively
- **Status:** ⬜ TODO

### FIX-021: Use Structured Log Templates Instead of String Interpolation
- **Source:** ARCHITECTURE_AUDIT → Consistency Check
- **Files:** `SubscriptionService.cs` Lines 259, 273, 277, 325
- **Issue:** `_logger.LogInformation($"📦 ...")` — string interpolation defeats structured logging
- **Fix:** Replace with `_logger.LogInformation("📦 Creating first order for subscription #{Id}", subscription.SubscriptionId)`
- **Status:** ⬜ TODO

### FIX-022: Admin Endpoint Object-Level Authorization
- **Source:** SECURITY_AUDIT → SEC-01
- **Issue:** Admin endpoints don't enforce resource-scoped permissions
- **Fix:** Implement when admin team grows (future consideration)
- **Status:** ⬜ DEFERRED

### FIX-023: Decompose God Services (SubscriptionService 12 deps)
- **Source:** ARCHITECTURE_AUDIT → Dependency Analysis
- **Issue:** `SubscriptionService` (12 deps), `SubscriptionSchedulingService` (11 deps) are too large
- **Fix:** Extract sub-services (e.g., `SubscriptionOrderGenerator`, `SubscriptionValidator`)
- **Status:** ⬜ DEFERRED (non-trivial refactor)

---

## IMPLEMENTATION ORDER (Recommended Sprint Plan)

### Sprint 1 (This Week) — Impact: HIGH, Effort: LOW
```
Day 1: FIX-024 (cancelled lowercase)  — 5 min  ← 🔴 RUNTIME BUG
Day 1: FIX-004 (Wallet SUM)           — 30 min
Day 1: FIX-025 (Stale sub price)      — 30 min
Day 1: FIX-011 (DateTime.UtcNow)      — 5 min
Day 1: FIX-012 (ExpiresAt)            — 10 min
Day 1: FIX-016 (AsNoTracking)         — 5 min
Day 1: FIX-017 (Pool size)            — 5 min
Day 1: FIX-018 (Dead code)            — 5 min
Day 1: FIX-019 (Unused imports)       — 5 min
                                       ──────
                              Total: ~1.5 hours
```

### Sprint 2 (This Week) — Impact: HIGH, Effort: MEDIUM
```
Day 2: FIX-002 (AuthMiddleware cache)  — 2 hours
Day 2: FIX-005 (Rate limiting)         — 1 hour
Day 3: FIX-003 (Ingredient updates)    — 3 hours (verify first!)
Day 3: FIX-006 (Error message leak)    — 3 hours
                                        ──────
                              Total: ~9 hours
```

### Sprint 3 (Next Week) — Impact: MEDIUM, Effort: MEDIUM
```
FIX-007 (Midnight job batching)        — 4 hours
FIX-009 (Dashboard SUM)               — 1 hour
FIX-010 (Concurrency token)           — 30 min + migration
FIX-013 (Wallet summary)              — 1 hour
FIX-014 (Missing indexes)             — 30 min + migration
FIX-015 (Credential masking)          — 30 min
                                       ──────
                              Total: ~7.5 hours
```

---

## AUDIT DOCUMENT INDEX

| Document | Contents |
|----------|---------|
| `CLAUDE.md` | Original audit — P0 wallet bugs (RESOLVED), P1-P2 findings |
| `SYSTEM_FLOW.md` | Architecture diagrams, startup flow, Hangfire pipeline, data model |
| `API_FLOW.md` | All controller endpoints, business flows, auth model, error codes |
| `DB_SCHEMA.md` | Table schemas, indexes, constraints, concurrency control |
| `ARCHITECTURE_AUDIT.md` | NEW findings — code quality, coupling, pattern violations |
| `SECURITY_AUDIT.md` | OWASP Top 10 coverage, rate limiting, secrets, data scoping |
| `PERFORMANCE_AUDIT.md` | Hot paths, query efficiency, caching, connection pool |
| `BUSINESS_LOGIC_AUDIT.md` | State machines, wallet integrity, price consistency, race conditions |
| `PRIORITIZED_FIXES.md` | THIS FILE — consolidated action plan with sprint schedule |


---

