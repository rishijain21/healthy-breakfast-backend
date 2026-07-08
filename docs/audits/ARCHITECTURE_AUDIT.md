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
