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
