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

## 10. MESSAGE QUEUE & ASYNC BOTTLENECKS (DEEP AUDIT)

### BL-18: O(N) Database Insertions in Nightly Subscription Job (Thread Starvation Risk)
- **Severity:** 🔴 P0 CRITICAL
- **Location:** `SubscriptionSchedulingService.GenerateScheduledOrdersFromSubscriptionsAsync`
- **Issue:** The method iterates over all active subscriptions in a sequential `foreach` loop. Inside the loop, it performs:
  1. `await _scheduledOrderRepo.CreateAsync(scheduledOrder)`
  2. `await _ingredientRepo.GetByIdsAsync(...)` (inside helper methods)
- **Impact:** For 10,000 subscriptions, this triggers 10,000 to 20,000 separate, synchronous database round-trips. This will hold a thread from the ThreadPool for minutes, causing ThreadPool starvation and likely pushing the Hangfire job past timeout limits or making the system unresponsive.
- **Fix Required:** 
  1. Bulk load all required `IngredientIds` BEFORE the loop.
  2. Accumulate all generated `ScheduledOrder` entities into a `List<ScheduledOrder>`.
  3. Add a new `CreateBatchAsync` method to `IScheduledOrderRepository`.
  4. Perform a single bulk insert after the loop via `AddRangeAsync`.

### BL-19: Unbounded Data Loading in Repositories
- **Severity:** 🟡 P1
- **Location:** `WalletTransactionRepository.GetAllAsync()`
- **Issue:** Uses `Take(500)` as a "safety limit". While bounded, this is a code smell. If an admin page expects pagination, it will silently drop records beyond 500.
- **Fix:** Ensure Admin UI uses `GetAllPagedAsync` properly.

