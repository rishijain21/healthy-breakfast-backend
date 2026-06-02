# FRONTEND_API_REFERENCE.md — Sovva Backend Integration Reference

═══════════════════════════════════════════════════════════
SECTION 1: Overview
══════════════════════════════
### Base URLs
- **Production:** `https://api.sovva.in`
- **Development:** `http://localhost:5169`

### Authentication
Sovva uses **Supabase Auth**. Most endpoints require a **Bearer JWT** token.
- **Header:** `Authorization: Bearer <token>`
- **Identity:** The backend extracts `sub` (AuthId) and `email` from the JWT claims.

### Response Structure
All responses are wrapped in a standard envelope:
- **Success:** `{ "success": true, "data": { ... }, "message": "..." }`
- **Error:** `{ "success": false, "error": { "code": "...", "message": "..." } }`

═══════════════════════════════════════════════════════════
SECTION 2: Auth Endpoints
══════════════════════════════

### GET /api/auth/check-user-exists
Check if an email is already registered.
**Auth:** None
**Query Parameters:**
| Field | Type | Default | Constraints |
|---|---|---|---|
| email | string | - | Required, valid email |

**Success Response:**
```json
{
  "success": true,
  "data": { "exists": true }
}
```
**CURL Example:**
```bash
curl -X GET "http://localhost:5169/api/auth/check-user-exists?email=user@example.com"
```

---

### POST /api/auth/register
Finalize user registration after Supabase signup.
**Auth:** Bearer JWT
**Request Body:**
| Field | Type | Req | Validation | Example |
|---|---|---|---|---|
| name | string | Yes | 2-255 chars | "Rahul Sharma" |
| phone | string | No | - | "+919876543210" |

**Success Response:**
```json
{
  "success": true,
  "data": {
    "user": { "userId": 1, "name": "Rahul Sharma", "email": "rahul@example.com", "walletBalance": 0.0 },
    "isNewUser": true
  }
}
```
**CURL Example:**
```bash
curl -X POST "http://localhost:5169/api/auth/register" \
     -H "Authorization: Bearer <JWT>" \
     -H "Content-Type: application/json" \
     -d '{"name": "Rahul Sharma", "phone": "+919876543210"}'
```

═══════════════════════════════════════════════════════════
SECTION 3: User & Wallet Endpoints
══════════════════════════════

### GET /api/users/profile
Get current user profile.
**Auth:** Bearer JWT
**Success Response:** `UserDto`

---

### GET /api/users/dashboard-summary
Get consolidated dashboard data (Profile + Balance + Recent Orders + Active Subs).
**Auth:** Bearer JWT
**Success Response:** `DashboardSummaryDto`

---

### GET /api/wallettransactions/my-balance
Get current wallet balance.
**Auth:** Bearer JWT
**Success Response:** `{ "balance": 500.0, "userId": 1 }`

---

### POST /api/wallettransactions/topup
Add balance to wallet.
**Auth:** Bearer JWT
**Request Body:**
| Field | Type | Req | Validation | Example |
|---|---|---|---|---|
| amount | decimal | Yes | 1 to 10000 | 500.00 |
| description | string | No | - | "Topup" |

**CURL Example:**
```bash
curl -X POST "http://localhost:5169/api/wallettransactions/topup" \
     -H "Authorization: Bearer <JWT>" \
     -H "Content-Type: application/json" \
     -d '{"amount": 500.0}'
```

═══════════════════════════════════════════════════════════
SECTION 4: Meal Endpoints
══════════════════════════════

### GET /api/meals/public
List all active meals for browsing.
**Auth:** None
**Query Parameters:** `page`, `pageSize`

---

### GET /api/meals/{id}/details
Get rich meal data for the builder (includes options/ingredients).
**Auth:** Bearer JWT
**Success Response:** `MealWithDetailsDto`

---

### POST /api/meals/batch-details
Fetch multiple meal details (max 20).
**Auth:** Bearer JWT
**Request Body:** `{ "mealIds": [1, 5] }`

═══════════════════════════════════════════════════════════
SECTION 5: Order Endpoints
══════════════════════════════

### POST /api/orders/create-from-meal-builder
Place a new customized order.
**Auth:** Bearer JWT
**Request Body:**
| Field | Type | Req | Validation | Example |
|---|---|---|---|---|
| mealId | int | Yes | - | 5 |
| mealName | string | No | - | "My Poha" |
| selectedIngredients | array | Yes | min 1 | `[{"ingredientId": 1, "quantity": 1}]` |
| scheduledFor | datetime | No | UTC | "2024-05-06T07:00:00Z" |

**Success Response:** `OrderCreationResponseDto`

---

### GET /api/orders/users/me/orders
Get full order history with nutrient breakdowns.
**Auth:** Bearer JWT
**Success Response:** `EnhancedOrderHistoryDto[]`

---

### POST /api/orders/{id}/reorder
Repeat a previous order.
**Auth:** Bearer JWT
**Success Response:** `OrderCreationResponseDto`

---

### POST /api/orders/{id}/rating
Rate a completed order.
**Auth:** Bearer JWT
**Request Body:** `{ "rating": 5, "review": "Great!" }`

═══════════════════════════════════════════════════════════
SECTION 6: Subscription Endpoints
══════════════════════════════

### POST /api/subscriptions
Create a recurring meal plan.
**Auth:** Bearer JWT
**Request Body:** `CreateSubscriptionDto` (Frequency: Daily=0, Weekly=1, Monthly=2)

---

### PATCH /api/subscriptions/{id}/activate | /deactivate
Resume or pause a subscription.
**Auth:** Bearer JWT

═══════════════════════════════════════════════════════════
SECTION 7: Scheduled Order Endpoints
══════════════════════════════

### GET /api/scheduledorders/tomorrow
View upcoming orders for tomorrow's delivery.
**Auth:** Bearer JWT
**Success Response:** `ScheduledOrderResponseDto[]`

---

### PUT /api/scheduledorders/{id}/modify
Change ingredients for a future order (before midnight cutoff).
**Auth:** Bearer JWT
**Request Body:** `{ "selectedIngredients": [{ "ingredientId": 1, "quantity": 2 }] }`

═══════════════════════════════════════════════════════════
SECTION 8: Address & Serviceable Location Endpoints
══════════════════════════════

### GET /api/serviceablelocations
Get list of delivery areas and fees.
**Auth:** None

---

### POST /api/useraddresses
Add a new delivery address.
**Auth:** Bearer JWT
**Request Body:** `CreateUserAddressDto`

---

### PUT /api/useraddresses/{id}/set-primary
Set an address as primary for orders.
**Auth:** Bearer JWT

═══════════════════════════════════════════════════════════
SECTION 9: Ingredient Endpoints
══════════════════════════════

### GET /api/ingredients
Get all ingredients (prices, calories, proteins).
**Auth:** Bearer JWT

═══════════════════════════════════════════════════════════
SECTION 10: Kitchen Endpoints (Admin)
══════════════════════════════

### GET /api/kitchen/today
List of confirmed orders for today's delivery.
**Auth:** Bearer JWT + Admin Role

### PUT /api/kitchen/{id}/mark-prepared
Update order status to "Prepared".
**Auth:** Bearer JWT + Admin Role

═══════════════════════════════════════════════════════════
SECTION 11: Admin User Management
══════════════════════════════

### GET /api/users
List all registered users.
**Auth:** Admin Role

### PATCH /api/users/{id}/role
Change user access level.
**Auth:** Admin Role
**Request Body:** `{ "role": "Admin" }` (or "Customer")

═══════════════════════════════════════════════════════════
SECTION 12: Admin Meal Management
══════════════════════════════

### POST /api/meals/admin
Create a new meal template.
**Auth:** Admin Role

### POST /api/meals/admin/{id}/image
Upload photo for a meal (Multipart Form Data).
**Auth:** Admin Role

═══════════════════════════════════════════════════════════
SECTION 13: Error Code Appendix
══════════════════════════════

| Code | HTTP | Description |
|---|---|---|
| `INSUFFICIENT_BALANCE` | 400 | Wallet balance is too low. |
| `NO_DELIVERY_ADDRESS` | 400 | User has no primary address set. |
| `FORBIDDEN` | 403 | Admin role or ownership mismatch. |
| `NOT_FOUND` | 404 | ID does not exist. |
| `INVALID_ARGUMENT` | 400 | Validation failed. |

═══════════════════════════════════════════════════════════
SECTION 14: Recommended Integration Flows
══════════════════════════════

### Flow: One-Click Reorder
1. User views history → `GET /api/orders/users/me/orders`.
2. User taps "Reorder" → `POST /api/orders/{id}/reorder`.
3. Backend clones configuration and schedules for next delivery.

### Flow: Real-time Price Update
1. Enter Meal Builder → `GET /api/meals/{id}/details`.
2. Frontend calculates price locally using ingredient prices in DTO.
3. Order Placed → `POST /api/orders/create-from-meal-builder` (Recalculated server-side).
