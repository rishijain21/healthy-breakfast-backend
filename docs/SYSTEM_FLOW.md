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
