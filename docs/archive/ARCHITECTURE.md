# ARCHITECTURE.md — Sovva Backend Architecture

## System Overview

```
┌──────────────┐     JWT      ┌──────────────┐    JWKS     ┌──────────────┐
│   Angular    │ ──────────── │   .NET 8     │ ──────────  │   Supabase   │
│  (Vercel)    │   REST API   │  Web API     │  Auth       │   Auth       │
└──────────────┘              └──────┬───────┘             └──────────────┘
                                     │
                              ┌──────┴───────┐
                              │  PostgreSQL  │
                              │  (Supabase)  │
                              └──────────────┘
```

## Clean Architecture Layers

```
┌───────────────────────────────────────────────────────────┐
│  Sovva.WebAPI        — Controllers, Middleware, Program.cs │
├───────────────────────────────────────────────────────────┤
│  Sovva.Application   — Services, Interfaces, DTOs, Valid. │
├───────────────────────────────────────────────────────────┤
│  Sovva.Infrastructure — Repos, DbContext, UnitOfWork      │
├───────────────────────────────────────────────────────────┤
│  Sovva.Domain        — Entities, Enums (ZERO deps)        │
└───────────────────────────────────────────────────────────┘
```

**Dependency flow**: WebAPI → Application → Domain ← Infrastructure

---

## Core Entity Relationships

```
User (1) ──── (*) UserAuthMapping     (Supabase UUID ↔ internal UserId)
  │
  ├── (*) UserMeal ──── (*) UserMealIngredient ──── Ingredient ──── IngredientCategory
  │         └── linked from Order.UserMealId
  │
  ├── (*) Subscription ──── (*) SubscriptionSchedule (weekly day/qty)
  │         └── generates → ScheduledOrder
  │
  ├── (*) ScheduledOrder ──── (*) ScheduledOrderIngredient ──── Ingredient
  │         └── confirmed at midnight → Order (via ScheduledOrderId FK)
  │
  ├── (*) Order
  │         ├── UserMeal? (real-time orders)
  │         ├── SourceScheduledOrder? (subscription orders)
  │         └── UserAddress (delivery)
  │
  ├── (*) WalletTransaction (credit/debit ledger)
  └── (*) UserAddress ──── ServiceableLocation

Meal ──── (*) MealOption ──── (*) MealOptionIngredient ──── Ingredient
```

---

## Request Lifecycle (Middleware Pipeline)

```
HTTP Request
  │
  ├── GlobalExceptionMiddleware   → catches unhandled → ApiErrorDto
  ├── CorrelationIdMiddleware     → X-Correlation-Id header
  ├── CORS Policy                 → validates Origin
  ├── Serilog Request Logging     → method, path, status, duration
  ├── Response Compression        → Brotli/Gzip
  ├── Rate Limiter                → 10/min (auth), 100/min (default)
  ├── JWT Authentication          → Supabase JWKS validation
  ├── AuthMiddleware (custom)     → sub → UserAuthMapping → sovva_user_id claim
  ├── Authorization               → [Authorize], Roles
  └── Controller Action           → Service → Repository → DB
  │
HTTP Response
```

---

## Data Flow: Order Creation (Meal Builder)

```
1. POST /api/orders/create-from-meal-builder
2. OrdersController → extract userId from JWT
3. OrderService (inside UnitOfWork transaction):
   ├── Validate meal exists (soft-delete guard)
   ├── Validate primary address + serviceable location
   ├── Calculate price (MealService)
   ├── Check wallet balance
   ├── BEGIN TRANSACTION
   │   ├── Create UserMeal + UserMealIngredients
   │   ├── Create Order (Pending)
   │   ├── Debit wallet
   │   ├── Update Order → Confirmed
   │   └── COMMIT
   └── Return OrderCreationResponseDto
4. On failure → ROLLBACK
```

---

## Data Flow: Subscription Lifecycle

```
User creates subscription
        │
        ▼
CreateSubscriptionAsync
  ├── Validate user, meal, duplicates, address
  ├── Create Subscription + WeeklySchedule
  └── CreateFirstScheduledOrderAsync (immediate visibility)

Nightly Hangfire Jobs (IST):
  11:50 PM  →  ExpireSubscriptions (deactivate past EndDate)
  11:55 PM  →  SyncDates (recalculate NextScheduledDate)
  12:00 AM  →  ConfirmOrders (wallet debit + create Order row)
  12:01 AM  →  GenerateNextDay (create tomorrow's ScheduledOrder)
```

---

## Authentication Flow

```
1. Frontend: Supabase Auth → JWT { sub: "uuid", aud: "authenticated" }
2. Backend:
   ├── JwtBearerMiddleware → validates signature via JWKS
   └── AuthMiddleware:
       ├── Extract `sub` claim (auth UUID)
       ├── Lookup UserAuthMapping → get User.UserId
       └── Add claims: sovva_user_id (int), sovva_role (string)
3. Controllers: User.GetSovvaUserId() → reads sovva_user_id
```

---

## Key Infrastructure Decisions

| Decision | Rationale |
|----------|-----------|
| Session Mode (port 5432) | EF Core needs real connections for transactions; PgBouncer breaks them |
| Manual Mapping (no AutoMapper) | Explicit control, avoids hidden N+1 queries |
| FluentValidation | Declarative, testable, auto-wired before controller actions |
| `IAppTimeProvider` | IST business logic, UTC storage, mockable for tests |
| `TimestampInterceptor` | Consistent timestamps across all entities automatically |

---

## Key Files Quick Reference

| Purpose | File |
|---------|------|
| DI & Startup | `Sovva.WebAPI/Program.cs` |
| Database Context | `Sovva.Infrastructure/Data/AppDbContext.cs` |
| Auth Enrichment | `Sovva.WebAPI/Middleware/AuthMiddleware.cs` |
| userId Extraction | `Sovva.WebAPI/Extensions/ClaimsPrincipalExtensions.cs` |
| Error Handler | `Sovva.WebAPI/Middleware/GlobalExceptionMiddleware.cs` |
| Transactions | `Sovva.Infrastructure/Data/UnitOfWork.cs` |
| Auto Timestamps | `Sovva.Infrastructure/Data/TimestampInterceptor.cs` |
| IST Time | `Sovva.Application/Helpers/AppTimeProvider.cs` |

---

## Deployment

| Component | Platform | URL |
|-----------|----------|-----|
| Backend | Render (Docker) | `https://<service>.onrender.com` |
| Frontend | Vercel | `https://sovva.vercel.app` |
| Database | Supabase | PostgreSQL Session mode (5432) |
| Auth | Supabase Auth | JWKS endpoint |
| Jobs | Hangfire | `/hangfire` (Basic Auth) |

### Environment Variables

| Variable | Purpose |
|----------|---------|
| `DATABASE_SESSION_URL` | PostgreSQL connection (session mode) |
| `DATABASE_URL` | Fallback connection |
| `Supabase__Url` | Supabase project URL |
| `HangfireDashboard__Username` | Hangfire login |
| `HangfireDashboard__Password` | Hangfire password |
which