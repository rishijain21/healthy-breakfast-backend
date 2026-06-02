# DEBUGGING_PLAYBOOK.md — Sovva Backend

> Step-by-step runbooks for every common failure mode in this codebase.
> Every section references actual file paths, actual error messages, and actual code.

---

## 1. When I Get a 500 on Render (Production)

### How the error pipeline works

```
Request
  → CorrelationIdMiddleware      generates 8-char ID, pushes to Serilog context
  → GlobalExceptionMiddleware    catches unhandled exceptions
  → AuthMiddleware               resolves Supabase JWT → sovva UserId
  → Controller                   your code
```

**Middleware pipeline order** (Program.cs L410-426):
```
app.UseMiddleware<GlobalExceptionMiddleware>();   // 1. catch all
app.UseMiddleware<CorrelationIdMiddleware>();      // 2. correlation ID
app.UseCors("CorsPolicy");                        // 3. CORS
app.UseSerilogRequestLogging();                    // 4. request logging
app.UseResponseCompression();                      // 5. compress
app.UseRateLimiter();                              // 6. rate limit
app.UseAuthentication();                           // 7. JWT validation
app.UseMiddleware<AuthMiddleware>();                // 8. resolve user
app.UseAuthorization();                            // 9. policy check
```

### Step-by-step to find the error

**Step 1**: Get the CorrelationId from the response header.

The `CorrelationIdMiddleware` (Sovva.WebAPI/Middleware/CorrelationIdMiddleware.cs) adds an
`X-Correlation-Id` header to every response — even 500s. The ID is an 8-character hex string.

```
# In browser DevTools → Network → failed request → Response Headers:
X-Correlation-Id: a1b2c3d4
```

> ⚠️ If `X-Correlation-Id` header is MISSING, the crash happened BEFORE CorrelationIdMiddleware
> ran. This means `GlobalExceptionMiddleware` itself crashed, or the exception is in Kestrel/CORS.

**Step 2**: Open Render dashboard → your service → **Logs** tab.

Serilog console output format (Program.cs L34-35):
```
[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message}{NewLine}{Exception}
```

**Step 3**: Search for the CorrelationId in logs.
```
Ctrl+F → a1b2c3d4
```

You'll find:
```
[14:23:01 ERR] [a1b2c3d4] Sovva.WebAPI.Middleware.GlobalExceptionMiddleware: 
  Unhandled exception on POST /api/Orders/create-from-meal-builder
  System.InvalidOperationException: Insufficient wallet balance
    at Sovva.Application.Services.OrderService.CreateOrderInternalAsync(...)
```

**Step 4**: Decode the exception type vs HTTP status.

`GlobalExceptionMiddleware` maps exceptions to HTTP codes (L40-72):

| Exception type | HTTP status | Error code | Notes |
|----------------|-------------|------------|-------|
| `KeyNotFoundException` | 404 | `NOT_FOUND` | Entity not found |
| `InvalidOperationException` containing "wallet" | 400 | `INSUFFICIENT_BALANCE` | Wallet issues |
| `InvalidOperationException` containing "address" | 400 | `NO_DELIVERY_ADDRESS` | Missing delivery address |
| `InvalidOperationException` containing "subscription" | 400 | `SUBSCRIPTION_NOT_FOUND` | Subscription issues |
| `InvalidOperationException` containing "order" | 400 | `INVALID_OPERATION` | Order issues |
| `InvalidOperationException` (other) | 400 | `INVALID_OPERATION` | Catch-all for business logic |
| `UnauthorizedAccessException` | 403 | `FORBIDDEN` | Access denied |
| `ArgumentException` | 400 | `INVALID_ARGUMENT` | Bad input |
| **Everything else** | **500** | **`INTERNAL_ERROR`** | **This is the real 500** |

> **Key insight**: If you see a 500, the exception was NOT one of the above types.
> It's likely a `NullReferenceException`, `DbUpdateException`, `NpgsqlException`, or similar.

**Step 5**: If it's a 500 in production, the response body will only say:
```json
{ "success": false, "code": "INTERNAL_ERROR", "message": "An unexpected error occurred" }
```
The `Detail` field (with `ex.Message`) is only included in `#if DEBUG` builds (L82-84).
The real message is ONLY in Render logs.

### Common 500 causes

| Error in logs | Root cause | Fix |
|---------------|-----------|-----|
| `NpgsqlException: connection pool exhausted` | Too many concurrent DB connections | Check connection string pooling, Supabase connection limits |
| `DbUpdateException: duplicate key` | Unique constraint violation | See §5 below |
| `NullReferenceException` in mapping | `.Include()` missing in repository | See §2 below |
| `TimeoutException` | Supabase Session Mode timeout | Check `CommandTimeout` in DatabaseOptions |
| `InvalidOperationException: ...user transactions...` | NpgsqlRetryingExecutionStrategy | See §6 below |

---

## 2. When EF Core Throws on SaveChangesAsync

### Error: `DbUpdateException` with inner `NpgsqlException`

**Check the inner exception message** — it tells you the exact constraint:

```csharp
catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException npgEx)
{
    // npgEx.SqlState tells you the PostgreSQL error code
    // npgEx.Message tells you the constraint name
}
```

### Common causes in this codebase

#### 2a. `NullReferenceException` in entity mapping
```
System.NullReferenceException: Object reference not set to an instance of an object
  at Sovva.Application.Services.OrderService.CreateOrderInternalAsync(...)
```
**Cause**: Navigation property not `.Include()`'d in repository query.

**Example**: `SubscriptionRepository` loads a subscription but forgets `.Include(s => s.UserMeal)`.
The service then tries `subscription.UserMeal.MealName` → null reference.

**Fix**: Add `.Include()` to the repository method:
```csharp
// ❌ Will crash if service accesses UserMeal
return await _context.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == id);

// ✅ Include the navigation property
return await _context.Subscriptions
    .Include(s => s.UserMeal)
    .FirstOrDefaultAsync(s => s.SubscriptionId == id);
```

#### 2b. CHECK constraint violation
```
23514: new row for relation "Users" violates check constraint "CK_Users_WalletBalance"
```
**Cause**: Attempted to set `WalletBalance < 0`. The DB blocks this.

**Fix**: Always check balance before deducting:
```csharp
if (user.WalletBalance < totalPrice)
    throw new InvalidOperationException("Insufficient wallet balance");
```

#### 2c. Foreign key violation (RESTRICT)
```
23503: update or delete on table "Meals" violates foreign key constraint on table "UserMeals"
```
**Cause**: Tried to delete a Meal that has UserMeals referencing it.

**Non-CASCADE FKs in this codebase** (see DB_SCHEMA.md):

| Table.Column | References | On Delete | Impact |
|--------------|-----------|-----------|--------|
| `UserMeals.MealId` | Meals | **RESTRICT** | Cannot delete meal with saved user meals |
| `Subscriptions.UserMealId` | UserMeals | **RESTRICT** | Cannot delete user meal with active subscription |
| `Orders.UserMealId` | UserMeals | (no action) | Cannot delete user meal with orders |
| `ScheduledOrders.SubscriptionId` | Subscriptions | (no action) | Cannot delete subscription with scheduled orders |

**Fix**: Use `IsDeleted` soft-delete flag on Meals (already implemented with global query filter).
For UserMeals/Subscriptions — deactivate instead of delete.

#### 2d. Concurrency conflict on WalletBalance
```
DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), 
but actually affected 0 row(s)
```
**Cause**: `WalletBalance` is a **concurrency token** (DB_SCHEMA.md, Users table).
Two requests tried to update the same user's balance simultaneously.

**Fix**: Retry the operation. The `NpgsqlRetryingExecutionStrategy` handles transient
DB errors but NOT concurrency conflicts. You must catch `DbUpdateConcurrencyException` explicitly.

#### 2e. Tracker conflict: "entity already being tracked"
```
InvalidOperationException: The instance of entity type 'Subscription' cannot be tracked 
because another instance with the key value '{SubscriptionId: 42}' is already being tracked
```
**Cause**: Two different code paths loaded the same entity in the same DbContext scope.
**Known instance**: SubscriptionRepository.UpdateAsync — see KNOWN_ISSUES #005.

**Fix**: Detach the existing tracked entity before updating:
```csharp
var local = _context.Set<Subscription>()
    .Local.FirstOrDefault(e => e.SubscriptionId == entity.SubscriptionId);
if (local != null)
    _context.Entry(local).State = EntityState.Detached;
_context.Subscriptions.Update(entity);
```

---

## 3. When a Hangfire Job Silently Fails

### Where to look

**Step 1**: Open Hangfire dashboard at `https://your-render-url.onrender.com/hangfire`
- Auth: Basic Auth (credentials in `HangfireDashboard:Username` / `HangfireDashboard:Password` config)
- Navigate to **Failed Jobs** tab

**Step 2**: If the job isn't in Failed Jobs, check **Succeeded Jobs** — it may have
"succeeded" but done nothing (empty catch block).

### The four Hangfire jobs (Program.cs L487-532)

| Job ID | Schedule (IST) | Service method | What it does |
|--------|---------------|----------------|-------------|
| `expire-subscriptions` | 11:50 PM | `SubscriptionService.ExpireSubscriptionsAsync()` | Sets `Active=false` on subscriptions where `EndDate <= today` |
| `sync-subscription-dates` | 11:55 PM | `SubscriptionService.UpdateNextScheduledDatesAsync()` | Updates `NextScheduledDate` on active subscriptions |
| `midnight-order-confirmation` | 12:00 AM | `ScheduledOrderService.ConfirmAllScheduledOrdersAsync()` | Deducts wallet, creates Orders from ScheduledOrders |
| `subscription-order-generation` | 12:01 AM | `SubscriptionSchedulingService.GenerateScheduledOrdersFromSubscriptionsAsync()` | Creates next-day ScheduledOrders from active subscriptions |

### Order matters!
The jobs run in this sequence:
```
11:50 PM  expire-subscriptions        ← cleanup first
11:55 PM  sync-subscription-dates     ← safety net
12:00 AM  midnight-order-confirmation ← wallet deduction + Order creation
12:01 AM  subscription-order-generation ← create tomorrow's orders
```

If `midnight-order-confirmation` fails, all confirmed orders for that day are LOST.
If `subscription-order-generation` fails, tomorrow's subscription orders won't exist.

### Common Hangfire failure causes

#### 3a. IST vs UTC timezone confusion
**Symptom**: Job runs but processes wrong day's orders.
**Cause**: Job code uses `DateTime.UtcNow` instead of `IAppTimeProvider.TodayIst`.

**Check**: Every Hangfire job service method should use:
```csharp
var todayIst = _time.TodayIst;  // DateOnly in IST
```
NOT:
```csharp
var today = DateOnly.FromDateTime(DateTime.UtcNow);  // ❌ Wrong after 6:30 PM IST
```

#### 3b. Exception swallowed by empty catch
**Symptom**: Job shows as "Succeeded" but nothing happened.
**Cause**: Service method has `catch (Exception) { }` without logging.

**Fix**: Always log in job catch blocks:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process scheduled order {Id}", orderId);
    throw; // Let Hangfire retry
}
```

#### 3c. Connection pool exhaustion
**Symptom**: `NpgsqlException: The connection pool has been exhausted`
**Cause**: Hangfire creates its own scope, and batch jobs open many connections.
**Config**: Hangfire uses `DATABASE_SESSION_URL` (port 5432, Session Mode — not PgBouncer).
Worker count is capped at **2** (Program.cs L124).

#### 3d. Idempotency not working
**Symptom**: Duplicate orders created for same subscription + date.
**Cause**: `SubscriptionId + ScheduledFor` unique index should prevent this, but only works
when `SubscriptionId IS NOT NULL`.

**Check**: `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` has filter
`"SubscriptionId" IS NOT NULL`. Manual (non-subscription) orders bypass this guard.

---

## 4. When Auth Returns 401 Unexpectedly

### The Sovva auth flow

```
Client sends:  Authorization: Bearer <supabase_jwt>
  ↓
1. JwtBearerMiddleware         validates JWT against Supabase JWKS
  ↓                            (ValidIssuer = supabaseUrl/auth/v1)
  ↓                            (ValidAudience = "authenticated")
  ↓                            (ClockSkew = 1 minute)
  ↓
2. AuthMiddleware              extracts "sub" claim → authId (Guid)
  ↓                            calls UserService.GetUserByAuthIdAsync(authGuid)
  ↓                            looks up user_auth_mapping table
  ↓                            sets HttpContext.Items["UserId"]
  ↓                            adds "sovva_role" claim
  ↓                            adds "sovva_user_id" claim
  ↓
3. Authorization               checks [Authorize] and policies
  ↓
4. Controller                  User.GetSovvaUserId() reads "sovva_user_id" claim
```

### Debugging checklist

#### ☐ Step 1: Is the JWT expired?
Supabase tokens expire after **1 hour** by default.

Decode the JWT at [jwt.io](https://jwt.io):
```json
{
  "exp": 1745328000,    ← Unix timestamp. Is this in the past?
  "sub": "abc-123-...", ← Supabase auth UUID
  "aud": "authenticated",
  "role": "authenticated"  ← Supabase role (NOT Sovva role)
}
```

If `exp` is in the past → token expired. Frontend must refresh via `supabase.auth.getSession()`.

**Clock skew**: Program.cs L315 allows 1 minute of skew: `ClockSkew = TimeSpan.FromMinutes(1)`.

#### ☐ Step 2: Is the JWT issuer correct?
```
ValidIssuer = "{supabaseUrl}/auth/v1"
```
If your Supabase URL changed, or you're testing against a different project, the issuer won't match.

Check Render env var: `Supabase__Url` must match the project URL in Supabase dashboard.

#### ☐ Step 3: Does the user exist in `user_auth_mapping`?
AuthMiddleware L59: `var userDto = await userService.GetUserByAuthIdAsync(authGuid);`

If this returns `null` → the user has a valid Supabase JWT but NO row in `user_auth_mapping`.
This means they never completed registration via `POST /api/Auth/register`.

**What happens**: AuthMiddleware sets `context.Items["IsNewUser"] = true` (L105) and does NOT
set `UserId`. Any controller calling `User.GetSovvaUserId()` will get `null` → returns 401.

**Log signature**:
```
🆕 AuthMiddleware: New user detected (authId: abc-123) - awaiting registration
```

#### ☐ Step 4: Is the `sovva_user_id` claim missing?
AuthMiddleware L84-91 adds the `sovva_user_id` claim to the ClaimsIdentity.
`User.GetSovvaUserId()` reads this claim.

If `userDto` was null (Step 3), this claim is never added → `GetSovvaUserId()` returns null.

#### ☐ Step 5: Is the endpoint in the public endpoints list?
AuthMiddleware L23-30 skips authentication for:
```csharp
var publicEndpoints = new[]
{
    "/swagger",
    "/api/auth/login",
    "/api/auth/register",
    "/api/auth/check-user-exists",
    "/api/scheduledorders/time-until-midnight"
};
```

> ⚠️ Case sensitivity: `path.ToLower()` is used (L22), so this is case-insensitive.
> BUT: If you add a new public endpoint and forget to add it here, it will require auth.

#### ☐ Step 6: Is it a role-based 403 (not 401)?
If the JWT is valid and user exists, but they get 403:
- Check if the endpoint has `[Authorize(Policy = "AdminOnly")]`
- The user's role in the `Users` table must be `"Admin"` (not `"Customer"`)
- AuthMiddleware L77-80 adds `sovva_role` claim from `userDto.Role`
- Program.cs L348-349: `AdminOnly` policy requires `sovva_role = "Admin"`

#### ☐ Step 7: Check Render logs for JWT validation failures
Program.cs L323-327 logs authentication failures:
```
JWT auth failed on /api/Orders/users/me/orders: IDX10223: Lifetime validation failed. 
The token is expired.
```

---

## 5. When I Get a Unique Constraint Violation

```
23505: duplicate key value violates unique constraint "IX_..."
```

### All unique constraints in this codebase

| Constraint | Table | Columns | Filter | What triggers it |
|-----------|-------|---------|--------|------------------|
| `IX_Users_Email` | Users | `Email` | — | Two users registered with same email |
| `IX_Users_Phone` | Users | `Phone` | — | Two users with same phone number |
| `IX_user_auth_mapping_UserId` | user_auth_mapping | `user_id` | — | Same DB user linked to two Supabase auth IDs (should be impossible) |
| `UX_UserMeals_UserId_MealId` | UserMeals | `UserId, MealId` | — | User tried to save the same meal twice. Service should check first with `GetByUserAndMealAsync()` |
| `UX_Subscriptions_ActiveUserMeal` | Subscriptions | `UserId, UserMealId` | `Active = true` | User tried to subscribe to the same meal while an active subscription already exists. Service checks with `GetAnyActiveSubscriptionByMealIdAsync()` (KNOWN_ISSUES #F001) |
| `IX_Orders_ScheduledOrderId` | Orders | `ScheduledOrderId` | — | Same scheduled order confirmed twice → duplicate Order row. Idempotency guard in `ConfirmScheduledOrderAsync` should prevent this. |
| `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` | ScheduledOrders | `SubscriptionId, ScheduledFor` | `SubscriptionId IS NOT NULL` | Subscription generated two scheduled orders for the same day. `GenerateScheduledOrdersFromSubscriptionsAsync` should prevent this. |

### How to fix each one

**`UX_UserMeals_UserId_MealId`** — Most common in practice:
```csharp
// ❌ Creates duplicate
var userMeal = new UserMeal { UserId = userId, MealId = mealId };
_context.UserMeals.Add(userMeal);

// ✅ Check first
var existing = await _context.UserMeals
    .FirstOrDefaultAsync(um => um.UserId == userId && um.MealId == mealId);
if (existing != null) return existing; // reuse
```

**`UX_Subscriptions_ActiveUserMeal`** — Fixed in #F001:
```csharp
// ✅ Already implemented in SubscriptionService.CreateSubscriptionAsync
var existingActive = await _subscriptionRepo
    .GetAnyActiveSubscriptionByMealIdAsync(userId, dto.MealId);
if (existingActive != null)
    throw new InvalidOperationException("Active subscription already exists for this meal");
```

**`IX_Orders_ScheduledOrderId`** — Idempotency guard:
```csharp
// ✅ Already implemented in OrderService.ConfirmScheduledOrderAsync
var existingOrder = await _orderRepo.GetByScheduledOrderIdAsync(scheduledOrderId);
if (existingOrder != null) return existingOrder; // already confirmed, skip
```

---

## 6. When NpgsqlRetryingExecutionStrategy Blocks a Transaction

### The exact error
```
InvalidOperationException: The configured execution strategy 
'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions. 
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()' 
to execute all the operations in the transaction as a retriable unit.
```

### Why it happens
Program.cs L88-92 enables retry-on-failure:
```csharp
npgsql.EnableRetryOnFailure(
    maxRetryCount: dbOptions.MaxRetryCount,  // default: 3
    maxRetryDelay: TimeSpan.FromSeconds(5),
    errorCodesToAdd: null
);
```

This means EF Core wraps every `SaveChangesAsync()` in its own retry logic.
When you try to manually call `_context.Database.BeginTransactionAsync()`, EF
says: "I can't retry a manual transaction — I don't know if it's safe to replay."

### KNOWN_ISSUES #004 reference
This is documented in KNOWN_ISSUES.md as `[WORKAROUND] #004`.

### The fix (two options)

**Option A: Single SaveChangesAsync (preferred in this codebase)**
```csharp
// ✅ Pattern used in ConfirmScheduledOrderAsync (OrderService.cs L549)
// All entity changes are batched, then one SaveChangesAsync call.
// EF Core treats this as a single atomic operation.

scheduledOrder.OrderStatus = "Confirmed";
scheduledOrder.IsProcessedToOrder = true;
user.WalletBalance -= totalPrice;
var order = new Order { ... };
_context.Orders.Add(order);
var transaction = new WalletTransaction { ... };
_context.WalletTransactions.Add(transaction);

await _context.SaveChangesAsync(); // ← single call, atomic
```

**Option B: Wrap in execution strategy (for complex multi-step operations)**
```csharp
// ✅ Only use this when you absolutely need explicit transaction control
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // ... multiple SaveChangesAsync calls ...
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});
```

> ⚠️ Option B has a caveat: the ENTIRE lambda is retried on failure, including
> operations that already succeeded. Make sure all operations are idempotent
> or use `IsProcessedToOrder` flags as guards.

### Where this matters in the codebase

| Service | Method | Uses transaction? | Pattern |
|---------|--------|-------------------|---------|
| `OrderService` | `ConfirmScheduledOrderAsync` | NO | Single `SaveChangesAsync` + idempotency guard |
| `OrderService` | `CreateOrderInternalAsync` | NO | Single `SaveChangesAsync` |
| `SubscriptionService` | `CreateSubscriptionAsync` | NO | Single `SaveChangesAsync` |
| `WalletTransactionService` | `TopUpAsync` | YES (via UnitOfWork) | ⚠️ Risk — uses `IUnitOfWork` which calls `BeginTransactionAsync` |

---

## 7. Migration Failed / DB Out of Sync

### Symptoms
```
# At startup
Npgsql.PostgresException: relation "XYZ" does not exist

# During dotnet ef migrations add
Build failed. Fix build errors and try again.

# During dotnet ef database update
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving...
42P01: relation "NewTable" does not exist
```

### Recovery steps

**Step 1**: Check what migration the DB thinks it's on
```bash
dotnet ef migrations list --project Sovva.Infrastructure --startup-project Sovva.WebAPI
```
Migrations with `(Applied)` are in the DB. Migrations without it are pending.

**Step 2**: Check `AppDbContextModelSnapshot.cs` matches last migration
```
Sovva.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
```
This file is the **source of truth** for what EF Core thinks the schema looks like.
If you edited a migration file manually, the snapshot may not match → corrupted state.

> ⚠️ **NEVER edit migration files manually** (KNOWN_ISSUES.md).
> If a migration is wrong, `dotnet ef migrations remove` and recreate it.

**Step 3**: If snapshot is corrupted
```bash
# Remove the bad migration (only if NOT applied to production)
dotnet ef migrations remove --project Sovva.Infrastructure --startup-project Sovva.WebAPI

# Verify snapshot matches DB
# Then recreate the migration
dotnet ef migrations add FixedMigration --project Sovva.Infrastructure --startup-project Sovva.WebAPI
```

**Step 4**: If production DB is ahead of your local code
This happens when someone applied a migration to Render but didn't commit the migration files.

```bash
# Generate a migration from current DB state
dotnet ef dbcontext scaffold "your-connection-string" Npgsql.EntityFrameworkCore.PostgreSQL --output-dir TempModels

# Compare TempModels/ with your entities, then delete TempModels/
# Create a migration that brings your code in line with the DB
```

**Step 5**: Last resort — reset migration history (dev only)
```bash
# ⚠️ NEVER do this in production
# 1. Delete all files in Sovva.Infrastructure/Migrations/
# 2. Create fresh initial migration
dotnet ef migrations add InitialCreate --project Sovva.Infrastructure --startup-project Sovva.WebAPI
# 3. Mark it as applied without running it (since DB already has the tables)
dotnet ef database update --project Sovva.Infrastructure --startup-project Sovva.WebAPI
```

### Key migration files in this project
```
Sovva.Infrastructure/Migrations/
  ├── 20260402193256_IndustryLevelSchemaHardening.cs     ← major hardening
  ├── 20260421182639_SubscriptionDataIntegrityFixes.cs   ← subscription fixes
  └── AppDbContextModelSnapshot.cs                       ← SOURCE OF TRUTH
```

### When asking AI for migration help

Always provide:
1. The exact error message (copy-paste)
2. The relevant entity class (e.g., `Subscription.cs`)
3. The `AppDbContextModelSnapshot.cs` section for that entity
4. The `OnModelCreating` configuration for that entity in `AppDbContext.cs`

---

## Quick Reference: Error Code → Likely Cause

| Error response `code` | HTTP | Most likely cause |
|----------------------|------|-------------------|
| `INSUFFICIENT_BALANCE` | 400 | User wallet < order total |
| `NO_DELIVERY_ADDRESS` | 400 | User has no address, or address was soft-deleted |
| `SUBSCRIPTION_NOT_FOUND` | 400 | Subscription deactivated or expired |
| `DUPLICATE_SUBSCRIPTION` | 400 | Active subscription for same meal already exists |
| `ORDER_ALREADY_PROCESSED` | 400 | `IsProcessedToOrder = true` — idempotency guard fired |
| `ORDER_CANNOT_MODIFY` | 400 | Past midnight IST — `CanModify = false` |
| `NOT_FOUND` | 404 | Entity doesn't exist (wrong ID, or soft-deleted Meal) |
| `UNAUTHORIZED` | 401 | JWT missing/expired/invalid |
| `FORBIDDEN` | 403 | Valid JWT but wrong role (Customer accessing Admin endpoint) |
| `INTERNAL_ERROR` | 500 | Unhandled exception — check Render logs with CorrelationId |

---

## Quick Reference: Log Search Patterns

```bash
# Find all errors for a specific user
grep "UserId.*42" render-logs.txt

# Find all errors for a specific correlation ID
grep "a1b2c3d4" render-logs.txt

# Find all 500s (unhandled exceptions)
grep "Unhandled exception" render-logs.txt

# Find all Hangfire job failures
grep "Failed to" render-logs.txt | grep -i "hangfire\|schedule\|subscription\|confirm"

# Find auth failures
grep "JWT auth failed\|AuthMiddleware error\|New user detected" render-logs.txt
```
