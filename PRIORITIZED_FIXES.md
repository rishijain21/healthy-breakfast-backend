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
