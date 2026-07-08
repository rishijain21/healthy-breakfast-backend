# Removed Endpoints Log

> **Date:** 2026-04-22  
> **Reason:** Frontend API audit — endpoints confirmed unused by the frontend codebase (`/frontend/sovva/src`).  
> **Build status after removal:** ✅ 0 errors, 0 warnings

---

## Removals

### `MealsController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `POST` | `/api/Meals` | Generic create — frontend uses `POST /api/Meals/admin` instead |
| `GET` | `/api/Meals/{i``d}` | Generic get — frontend uses `GET /api/Meals/{id}/details` |
| `POST` | `/api/Meals/calculate-price` | Not called anywhere in frontend services |
| `POST` | `/api/Meals/validate-selection` | Not called anywhere in frontend services |
| `POST` | `/api/Meals/nutritional-summary` | Not called anywhere in frontend services |
| `PATCH` | `/api/Meals/admin/{id}/status` | Frontend uses `DELETE /api/Meals/admin/{id}` — status toggle not used |
| `GET` | `/api/Meals/admin/all-with-details` | Superseded by `GET /api/Meals/admin/all` (paginated) |

---

### `IngredientCategoriesController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `POST` | `/api/IngredientCategories` | No admin UI calls this — categories are static |
| `GET` | `/api/IngredientCategories/{id}` | Frontend always fetches all categories, never by ID |

---

### `OrdersController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `GET` | `/api/Orders/history` | Exact duplicate of `GET /api/Orders/users/me/orders` — same service call, different route |
| `POST` | `/api/Orders` | Generic create — frontend uses `POST /api/Orders/create-from-meal-builder` |

---

### `UserMealsController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `GET` | `/api/UserMeals/{id}` | Not called by frontend — `userMealId` returned on POST is used directly |
| `GET` | `/api/UserMeals/my-meals` | Not called by frontend |

**Bonus cleanup:** Removed 10 `Console.WriteLine` debug statements that were left in `UserMealsController.Create()` from development. Replaced with proper `_logger.LogInformation`.

---

### `WalletTransactionsController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `GET` | `/api/WalletTransactions/check-balance` | Not called by frontend — balance checks done client-side after fetching `/my-balance` |

---

### `MealOptionsController.cs` & `MealOptionIngredientsController.cs`

| Method | Route | Why Removed |
|--------|-------|-------------|
| `POST` | `/api/MealOptions` | Entire controller removed. Admin endpoints for strict meal templates not used by frontend. |
| `POST` | `/api/MealOptionIngredients` | Entire controller removed. Admin endpoints for strict meal templates not used by frontend. |

---

## Total Removed
**17 endpoints** removed across 7 controllers.

---

## Endpoints Still Flagged (NOT Removed — Need Action)

These are issues found during the audit but kept intentionally:

| Priority | Issue | Status |
|----------|-------|--------|
| 🔴 P0 | `DELETE /api/Users/account` — frontend calls this, backend MISSING | **Must add** |
| 🔴 P0 | `POST /api/Orders/{id}/rating` — frontend calls this, backend MISSING | **Must add** |
| 🔴 P0 | `POST /api/Orders/{id}/reorder` — frontend calls this, backend MISSING | **Must add** |
| 🟡 P1 | `PUT /api/Users/{id}/role` — frontend sends PUT, backend has PATCH | Fix method |
| 🟡 P1 | `GET /orders/user/{userId}` — legacy path frontend keeps for compat, backend MISSING | Add or remove from frontend |
| 🟡 P1 | `POST /Auth/register` — frontend still sends `authId`+`email` in body, backend now reads from JWT | Update frontend service |

---

## Controllers NOT Touched (All routes in use)
- `AuthController` ✅
- `IngredientsController` ✅
- `UsersController` ✅
- `SubscriptionsController` ✅
- `ScheduledOrdersController` ✅
- `KitchenController` ✅
- `ServiceableLocationsController` ✅ (admin endpoints kept even though not in frontend doc)
- `UserAddressesController` ✅
- `WalletTransactionsController` ✅ (admin endpoints kept)
