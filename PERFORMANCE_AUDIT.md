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
