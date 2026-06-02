# DB_SCHEMA.md — Sovva PostgreSQL Schema (EF Core Snapshot)

> Source of truth: `Sovva.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` + listed migrations.
> Intended for AI codegen & migration safety checks.

---

## Ingredients

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| IngredientId | `int` | `integer` | No | Identity | PK |
| Available | `bool` | `boolean` | No |  |  |
| Calories | `int` | `integer` | No |  |  |
| CategoryId | `int` | `integer` | No |  | FK → `IngredientCategories(CategoryId)` ON DELETE **CASCADE** |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| Description | `string` | `text` | No |  |  |
| Fiber | `decimal` | `numeric` | No |  |  |
| IconEmoji | `string` | `text` | No |  |  |
| IngredientName | `string` | `text` | No |  |  |
| Price | `decimal` | `numeric` | No |  |  |
| Protein | `decimal` | `numeric` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- `IX_Ingredients_CategoryId` (non-unique) on (`CategoryId`)

**Foreign Keys**
- `CategoryId` → `IngredientCategories(CategoryId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## IngredientCategories

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| CategoryId | `int` | `integer` | No | Identity | PK |
| CategoryName | `string` | `text` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- None

**Foreign Keys**
- None

**Check constraints**
- None

---

## Meals

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| MealId | `int` | `integer` | No | Identity | PK |
| ApproxCalories | `int?` | `integer` | Yes |  |  |
| ApproxCarbs | `decimal?` | `decimal(5,1)` | Yes |  |  |
| ApproxFats | `decimal?` | `decimal(5,1)` | Yes |  |  |
| ApproxProtein | `decimal?` | `decimal(5,1)` | Yes |  |  |
| BasePrice | `decimal` | `decimal(10,2)` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| Description | `string` | `text` | No |  |  |
| ImageUrl | `string?` | `text` | Yes |  |  |
| IsComplete | `bool` | `boolean` | No |  |  |
| IsDeleted | `bool` | `boolean` | No |  |  |
| MealName | `string` | `text` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- None

**Foreign Keys**
- None

**Check constraints**
- None

---

## MealOptions

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| MealOptionId | `int` | `integer` | No | Identity | PK |
| CategoryId | `int` | `integer` | No |  | FK → `IngredientCategories(CategoryId)` ON DELETE **CASCADE** |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| IsRequired | `bool` | `boolean` | No |  |  |
| MaxSelectable | `int` | `integer` | No |  |  |
| MealId | `int` | `integer` | No |  | FK → `Meals(MealId)` ON DELETE **CASCADE** |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- `IX_MealOptions_CategoryId` (non-unique) on (`CategoryId`)
- `IX_MealOptions_MealId` (non-unique) on (`MealId`)

**Foreign Keys**
- `CategoryId` → `IngredientCategories(CategoryId)` ON DELETE **CASCADE**
- `MealId` → `Meals(MealId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## MealOptionIngredients

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| MealOptionIngredientId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| IngredientId | `int` | `integer` | No |  | FK → `Ingredients(IngredientId)` ON DELETE **CASCADE** |
| MealOptionId | `int` | `integer` | No |  | FK → `MealOptions(MealOptionId)` ON DELETE **CASCADE** |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- `IX_MealOptionIngredients_IngredientId` (non-unique) on (`IngredientId`)
- `IX_MealOptionIngredients_MealOptionId` (non-unique) on (`MealOptionId`)

**Foreign Keys**
- `IngredientId` → `Ingredients(IngredientId)` ON DELETE **CASCADE**
- `MealOptionId` → `MealOptions(MealOptionId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## Orders

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| OrderId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DeliveryAddressId | `int?` | `integer` | Yes |  | FK → `UserAddresses(Id)` ON DELETE **SET NULL** |
| IsPrepared | `bool` | `boolean` | No |  |  |
| OrderDate | `DateTime` | `timestamp with time zone` | No |  |  |
| Status (OrderStatus) | `string` | `character varying(50)` | No |  | Column name in DB is `Status` |
| ScheduledFor | `DateTime` | `timestamp with time zone` | No |  |  |
| ScheduledOrderId | `int?` | `integer` | Yes |  | FK → `ScheduledOrders(ScheduledOrderId)` ON DELETE **SET NULL** (one-to-one) |
| TotalPrice | `decimal` | `decimal(12,2)` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |
| UserMealId | `int?` | `integer` | Yes |  | FK → `UserMeals(UserMealId)` (delete behavior not specified in snapshot) |

**Indexes / Uniques**
- `IX_Orders_UserId` (non-unique) on (`UserId`)
- `IX_Orders_UserId_Status` (non-unique) on (`UserId`, `Status`)
- `IX_Orders_ScheduledFor` (non-unique) on (`ScheduledFor`)
- `IX_Orders_DeliveryAddressId` (non-unique) on (`DeliveryAddressId`)
- `IX_Orders_UserMealId` (non-unique) on (`UserMealId`)
- `IX_Orders_ScheduledOrderId` (**unique**) on (`ScheduledOrderId`) — one order per scheduled order

**Foreign Keys**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**
- `DeliveryAddressId` → `UserAddresses(Id)` ON DELETE **SET NULL**
- `ScheduledOrderId` → `ScheduledOrders(ScheduledOrderId)` ON DELETE **SET NULL**
- `UserMealId` → `UserMeals(UserMealId)` (ON DELETE not specified; EF default depends on requiredness)

**Check constraints**
- `CK_Orders_Status`: `"Status" IN ('Pending','Confirmed','Preparing','OutForDelivery','Delivered','Cancelled')`
- `CK_Orders_TotalPrice`: `"TotalPrice" >= 0`

---

## OrderItems

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| OrderItemId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| OrderId | `int` | `integer` | No |  | FK → `Orders(OrderId)` ON DELETE **CASCADE** |
| Price | `decimal` | `numeric` | No |  |  |
| Quantity | `int` | `integer` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserMealId | `int` | `integer` | No |  | FK → `UserMeals(UserMealId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_OrderItems_OrderId` (non-unique) on (`OrderId`)
- `IX_OrderItems_UserMealId` (non-unique) on (`UserMealId`)

**Foreign Keys**
- `OrderId` → `Orders(OrderId)` ON DELETE **CASCADE**
- `UserMealId` → `UserMeals(UserMealId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## ScheduledOrders

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| ScheduledOrderId | `int` | `integer` | No | Identity | PK |
| AuthId | `Guid` | `uuid` | No |  |  |
| CanModify | `bool` | `boolean` | No |  |  |
| ConfirmedAt | `DateTime?` | `timestamp with time zone` | Yes |  |  |
| ConfirmedOrderId | `int?` | `integer` | Yes |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DeliveryAddressId | `int?` | `integer` | Yes |  | FK → `UserAddresses(Id)` ON DELETE **SET NULL** |
| DeliveryTimeSlot | `string` | `character varying(50)` | No |  |  |
| ExpiresAt | `DateTime` | `timestamp with time zone` | No |  |  |
| IsProcessedToOrder | `bool` | `boolean` | No | `false` | Default (`HasDefaultValue(false)`) |
| MealId | `int?` | `integer` | Yes |  |  |
| MealImageUrl | `string?` | `text` | Yes |  |  |
| MealName | `string` | `character varying(255)` | No |  |  |
| NutritionalSummary | `string?` | `text` | Yes |  |  |
| OrderStatus | `string` | `character varying(50)` | No |  |  |
| ScheduledFor | `DateOnly` | `date` | No |  |  |
| SubscriptionId | `int?` | `integer` | Yes |  | FK → `Subscriptions(SubscriptionId)` (ON DELETE not specified) |
| TotalPrice | `decimal` | `decimal(12,2)` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_ScheduledOrders_DeliveryAddressId` (non-unique) on (`DeliveryAddressId`)
- `IX_ScheduledOrders_UserId_ScheduledFor` (non-unique) on (`UserId`, `ScheduledFor`)
- `IX_ScheduledOrders_AuthId_ScheduledFor` (non-unique) on (`AuthId`, `ScheduledFor`)
- `IX_ScheduledOrders_ScheduledFor_Status` (non-unique) on (`ScheduledFor`, `OrderStatus`)
- `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` (**unique**) on (`SubscriptionId`, `ScheduledFor`) WHERE `SubscriptionId IS NOT NULL`

**Foreign Keys**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**
- `DeliveryAddressId` → `UserAddresses(Id)` ON DELETE **SET NULL**
- `SubscriptionId` → `Subscriptions(SubscriptionId)` (ON DELETE not specified)

**Check constraints**
- `CK_ScheduledOrders_Status`: `"OrderStatus" IN ('Scheduled', 'Confirmed', 'Cancelled', 'Processed')`
- `CK_ScheduledOrders_TotalPrice`: `"TotalPrice" >= 0`

---

## ScheduledOrderIngredients

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| Id | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| IngredientId | `int` | `integer` | No |  | FK → `Ingredients(IngredientId)` ON DELETE **CASCADE** |
| Quantity | `int` | `integer` | No |  |  |
| ScheduledOrderId | `int` | `integer` | No |  | FK → `ScheduledOrders(ScheduledOrderId)` ON DELETE **CASCADE** |
| TotalPrice | `decimal` | `numeric` | No |  |  |
| UnitPrice | `decimal` | `numeric` | No |  |  |

**Indexes / Uniques**
- `IX_ScheduledOrderIngredients_IngredientId` (non-unique)
- `IX_ScheduledOrderIngredients_ScheduledOrderId` (non-unique)

**Foreign Keys**
- `IngredientId` → `Ingredients(IngredientId)` ON DELETE **CASCADE**
- `ScheduledOrderId` → `ScheduledOrders(ScheduledOrderId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## ServiceableLocations

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| Id | `int` | `integer` | No | Identity | PK |
| Area | `string` | `character varying(100)` | No |  |  |
| City | `string` | `character varying(100)` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DeliveryTimeSlot | `string?` | `character varying(100)` | Yes |  |  |
| IsActive | `bool` | `boolean` | No |  |  |
| LandmarkOrSociety | `string` | `character varying(200)` | No |  |  |
| Latitude | `decimal?` | `numeric` | Yes |  |  |
| Locality | `string` | `character varying(200)` | No |  |  |
| Longitude | `decimal?` | `numeric` | Yes |  |  |
| Pincode | `string` | `character varying(10)` | No |  |  |
| UpdatedAt | `DateTime?` | `timestamp with time zone` | Yes |  |  |

**Indexes / Uniques**
- None

**Foreign Keys**
- None

**Check constraints**
- None

---

## Subscriptions

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| SubscriptionId | `int` | `integer` | No | Identity | PK |
| Active | `bool` | `boolean` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DeliveryAddressId | `int?` | `integer` | Yes |  | FK → `UserAddresses(Id)` ON DELETE **SET NULL** |
| EndDate | `DateOnly` | `date` | No |  |  |
| Frequency | `int` (enum) | `integer` | No |  | Stored as int (`HasConversion<int>()`) |
| NextScheduledDate | `DateOnly?` | `date` | Yes |  |  |
| StartDate | `DateOnly` | `date` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |
| UserMealId | `int` | `integer` | No |  | FK → `UserMeals(UserMealId)` ON DELETE **RESTRICT** |

**Indexes / Uniques**
- `IX_Subscriptions_UserMealId` (non-unique)
- `IX_Subscriptions_DeliveryAddressId` (non-unique)
- `IX_Subscriptions_UserId_Active` (non-unique)
- `IX_Subscriptions_Active_NextScheduledDate` (non-unique) WHERE `Active = true`
- `UX_Subscriptions_ActiveUserMeal` (**unique**) on (`UserId`, `UserMealId`) WHERE `Active = true`

**Foreign Keys**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**
- `UserMealId` → `UserMeals(UserMealId)` ON DELETE **RESTRICT**
- `DeliveryAddressId` → `UserAddresses(Id)` ON DELETE **SET NULL**

**Check constraints**
- `CK_Subscriptions_Dates`: `"EndDate" > "StartDate"`

---

## SubscriptionSchedules

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| ScheduleId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DayOfWeek | `int` | `integer` | No |  |  |
| Quantity | `int` | `integer` | No |  |  |
| SubscriptionId | `int` | `integer` | No |  | FK → `Subscriptions(SubscriptionId)` ON DELETE **CASCADE** |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |

**Indexes / Uniques**
- `IX_SubscriptionSchedules_SubscriptionId` (non-unique)

**Foreign Keys**
- `SubscriptionId` → `Subscriptions(SubscriptionId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## Users

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| UserId | `int` | `integer` | No | Identity | PK |
| AccountStatus | `string` | `character varying(50)` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| DeletedAt | `DateTime?` | `timestamp with time zone` | Yes |  | Soft-delete timestamp |
| Email | `string` | `character varying(300)` | No |  |  |
| Name | `string` | `character varying(200)` | No |  |  |
| Phone | `string` | `character varying(20)` | No |  |  |
| Role | `string` | `character varying(50)` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| WalletBalance | `decimal` | `decimal(12,2)` | No |  | Concurrency token |

**Indexes / Uniques**
- `IX_Users_Active` (non-unique) WHERE `DeletedAt IS NULL`
- `IX_Users_Email` (**unique**) on (`Email`)
- `IX_Users_Phone` (**unique**) on (`Phone`)

**Foreign Keys**
- None

**Check constraints**
- `CK_Users_AccountStatus`: `"AccountStatus" IN ('Active', 'Deactivated', 'Deleted')`
- `CK_Users_Role`: `"Role" IN ('Customer', 'Admin', 'DeliveryPartner')`
- `CK_Users_WalletBalance`: `"WalletBalance" >= 0`

---

## UserAddresses

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| Id | `int` | `integer` | No | Identity | PK |
| AdditionalInstructions | `string?` | `character varying(500)` | Yes |  |  |
| Block | `string?` | `character varying(50)` | Yes |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| FlatNumber | `string` | `character varying(50)` | No |  |  |
| Floor | `string?` | `character varying(20)` | Yes |  |  |
| IsActive | `bool` | `boolean` | No |  |  |
| IsPrimary | `bool` | `boolean` | No |  |  |
| Label | `string?` | `character varying(50)` | Yes |  |  |
| ServiceableLocationId | `int` | `integer` | No |  | FK → `ServiceableLocations(Id)` ON DELETE **CASCADE** |
| UpdatedAt | `DateTime?` | `timestamp with time zone` | Yes |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |
| Wing | `string?` | `character varying(50)` | Yes |  |  |

**Indexes / Uniques**
- `IX_UserAddresses_ServiceableLocationId` (non-unique)
- `IX_UserAddresses_UserId` (non-unique)

**Foreign Keys**
- `ServiceableLocationId` → `ServiceableLocations(Id)` ON DELETE **CASCADE**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## user_auth_mapping

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| mapping_id (MappingId) | `int` | `integer` | No | Identity | PK |
| auth_id (AuthId) | `Guid` | `uuid` | No |  |  |
| created_at (CreatedAt) | `DateTime` | `timestamp with time zone` | No |  |  |
| user_id (UserId) | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_user_auth_mapping_UserId` (**unique**) on (`user_id`)

**Foreign Keys**
- `user_id` → `Users(UserId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## UserMeals

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| UserMealId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| MealId | `int` | `integer` | No |  | FK → `Meals(MealId)` ON DELETE **RESTRICT** |
| MealName | `string` | `character varying(200)` | No |  |  |
| TotalPrice | `decimal` | `decimal(12,2)` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_UserMeals_MealId` (non-unique)
- `UX_UserMeals_UserId_MealId` (**unique**) on (`UserId`, `MealId`)

**Foreign Keys**
- `MealId` → `Meals(MealId)` ON DELETE **RESTRICT**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## UserMealIngredients

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| UserMealIngredientId | `int` | `integer` | No | Identity | PK |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| IngredientId | `int` | `integer` | No |  | FK → `Ingredients(IngredientId)` ON DELETE **CASCADE** |
| Quantity | `int` | `integer` | No |  |  |
| UpdatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| UserMealId | `int` | `integer` | No |  | FK → `UserMeals(UserMealId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_UserMealIngredients_IngredientId` (non-unique)
- `IX_UserMealIngredients_UserMealId` (non-unique)

**Foreign Keys**
- `IngredientId` → `Ingredients(IngredientId)` ON DELETE **CASCADE**
- `UserMealId` → `UserMeals(UserMealId)` ON DELETE **CASCADE**

**Check constraints**
- None

---

## WalletTransactions

| Column | C# type | PostgreSQL type | Nullable | Default | Notes |
|---|---|---|---:|---|---|
| TransactionId | `int` | `integer` | No | Identity | PK |
| Amount | `decimal` | `decimal(12,2)` | No |  |  |
| CreatedAt | `DateTime` | `timestamp with time zone` | No |  |  |
| Description | `string` | `character varying(500)` | No |  |  |
| ReferenceId | `int?` | `integer` | Yes |  |  |
| ReferenceType | `string?` | `character varying(50)` | Yes |  |  |
| Type | `string` | `character varying(20)` | No |  |  |
| UserId | `int` | `integer` | No |  | FK → `Users(UserId)` ON DELETE **CASCADE** |

**Indexes / Uniques**
- `IX_WalletTransactions_UserId_CreatedAt` (non-unique) on (`UserId`, `CreatedAt`)

**Foreign Keys**
- `UserId` → `Users(UserId)` ON DELETE **CASCADE**

**Check constraints**
- `CK_WalletTransactions_Amount`: `"Amount" > 0`
- `CK_WalletTransactions_ReferenceType`: `"ReferenceType" IS NULL OR "ReferenceType" IN ('Order', 'Subscription', 'TopUp', 'Refund', 'Manual')`
- `CK_WalletTransactions_Type`: `"Type" IN ('Credit', 'Debit')`

---

## Unique Constraints (All)

| Index name | Table | Columns | Filter / Partial |
|---|---|---|---|
| `IX_Orders_ScheduledOrderId` | `Orders` | (`ScheduledOrderId`) |  |
| `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique` | `ScheduledOrders` | (`SubscriptionId`, `ScheduledFor`) | `WHERE "SubscriptionId" IS NOT NULL` |
| `UX_Subscriptions_ActiveUserMeal` | `Subscriptions` | (`UserId`, `UserMealId`) | `WHERE "Active" = true` |
| `UX_UserMeals_UserId_MealId` | `UserMeals` | (`UserId`, `MealId`) |  |
| `IX_Users_Email` | `Users` | (`Email`) |  |
| `IX_Users_Phone` | `Users` | (`Phone`) |  |
| `IX_user_auth_mapping_UserId` | `user_auth_mapping` | (`user_id`) |  |

---

## Dangerous Operations

These are operations that commonly violate constraints, based on current schema + known data patterns.

1. **Creating duplicate `UserMeals` per (UserId, MealId)**
   - Violates unique index `UX_UserMeals_UserId_MealId`.
   - Mitigation: always `GetByUserIdAndMealId` before insert.

2. **Creating multiple active subscriptions for the same (UserId, UserMealId)**
   - Violates partial unique index `UX_Subscriptions_ActiveUserMeal` when `Active=true`.
   - Mitigation: deactivate old subscription before creating a new one, or update existing.

3. **Creating more than one ScheduledOrder for the same subscription on the same date**
   - Violates `IX_ScheduledOrders_SubscriptionId_ScheduledFor_Unique`.
   - Mitigation: check existence before insert; treat operation as idempotent.

4. **Deleting Meals that are referenced by UserMeals**
   - `UserMeals.MealId` FK uses ON DELETE **RESTRICT**.
   - Mitigation: use soft delete (`Meals.IsDeleted`) rather than hard delete.

5. **Deleting UserMeals that are referenced by Subscriptions**
   - `Subscriptions.UserMealId` FK uses ON DELETE **RESTRICT**.
   - Mitigation: delete/deactivate subscriptions first.

6. **Inserting invalid enum/status strings**
   - `Orders.Status`, `ScheduledOrders.OrderStatus`, `Users.Role`, `Users.AccountStatus`, `WalletTransactions.Type` are protected by CHECK constraints.

7. **Inserting negative prices / invalid amounts**
   - `Orders.TotalPrice >= 0`, `ScheduledOrders.TotalPrice >= 0`, `WalletTransactions.Amount > 0`, `Users.WalletBalance >= 0`.