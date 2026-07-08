---

# SOVVA — Frontend Developer Crux
> Generated: May 2026 | Status: Production Ready
> This is the only file you need. Everything else is outdated.

## 1. Setup (5 minutes)

### Environment Variables (frontend)
NEXT_PUBLIC_SUPABASE_URL=
NEXT_PUBLIC_SUPABASE_ANON_KEY=
NEXT_PUBLIC_API_URL=http://localhost:5257  # change per env

### Auth Setup
- Install Supabase JS SDK
- User signs in via Supabase (OTP or password)
- Supabase returns a session with access_token (JWT)
- Send JWT as: Authorization: Bearer {access_token}
- Tokens expire in 1 hour — use Supabase session.refresh()
- On 401 from API → refresh token → retry request

## 2. Universal Response Contract

All backend responses are wrapped in a standard envelope.

**Success**
```typescript
interface ApiResponse<T> {
  success: true;
  data: T;
  message?: string;
}
```

**Error**
```typescript
interface ApiErrorResponse {
  success: false;
  error: {
    code: string;
    message: string;
  };
}
```

**Paginated Success**
```typescript
interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

## 3. Error Handling (copy this into your API client)

```typescript
export async function handleApiResponse<T>(response: Response): Promise<T> {
  const data = await response.json();
  
  if (!response.ok || !data.success) {
    const errorData = data as ApiErrorResponse;
    const errorCode = errorData.error?.code || 'UNKNOWN_ERROR';
    const errorMessage = errorData.error?.message || 'An unexpected error occurred';
    
    // Map of every error code → what to show the user
    const userFriendlyMessage = ErrorMessageMap[errorCode] || errorMessage;
    throw new Error(userFriendlyMessage);
  }
  
  return (data as ApiResponse<T>).data;
}

const ErrorMessageMap: Record<string, string> = {
  'INSUFFICIENT_BALANCE': 'Your wallet balance is too low to complete this order.',
  'NO_DELIVERY_ADDRESS': 'Please set a primary delivery address before ordering.',
  'SUBSCRIPTION_NOT_FOUND': 'We could not find this subscription. It may have expired.',
  'DUPLICATE_SUBSCRIPTION': 'You already have an active subscription for this meal.',
  'ORDER_ALREADY_PROCESSED': 'This order has already been processed and cannot be changed.',
  'ORDER_CANNOT_MODIFY': 'Orders cannot be modified past the midnight cutoff.',
  'NOT_FOUND': 'The requested resource could not be found.',
  'UNAUTHORIZED': 'Please log in again to continue.',
  'FORBIDDEN': 'You do not have permission to perform this action.',
  'VALIDATION_ERROR': 'Please check your inputs and try again.',
  'INTERNAL_ERROR': 'An unexpected error occurred. We are looking into it.'
};
```

## 4. API Client Setup

```typescript
import { createClient } from '@supabase/supabase-js';

const supabase = createClient(
  process.env.NEXT_PUBLIC_SUPABASE_URL!,
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!
);

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export async function fetchApi<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const { data: { session }, error } = await supabase.auth.getSession();
  
  if (error || !session) {
    // Handle unauthenticated state (e.g., redirect to login)
    throw new Error('Not authenticated');
  }

  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${session.access_token}`,
    ...options.headers,
  };

  const response = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    // Attempt refresh or logout user
    throw new Error('UNAUTHORIZED');
  }

  return handleApiResponse<T>(response);
}
```

## 5. User Flows (what screens to build and in what order)

1. **Auth & Profile:** Login via Supabase → `GET /api/auth/check-user-exists` → (If new) `POST /api/auth/register` → App Dashboard.
2. **Dashboard:** Fetch `GET /api/users/dashboard-summary` to display profile, wallet, recent transactions, and active subscriptions.
3. **Meal Browsing:** `GET /api/meals/public` → User selects meal → `GET /api/meals/{id}/details` for builder.
4. **Ordering:** User customizes ingredients → `POST /api/orders/create-from-meal-builder` → Show confirmation.
5. **Subscription:** Similar to ordering, but user chooses frequency (Daily/Weekly/Monthly) → `POST /api/subscriptions`.
6. **Wallet Top-Up:** View balance → `POST /api/wallettransactions/topup` → Refetch balance.

## 6. Every Endpoint (condensed table format)

For full details, see `FRONTEND_API_REFERENCE.md`.

### Auth & User
| Method | Path | Auth | Request Body (key fields) | Returns |
|---|---|---|---|---|
| GET | `/api/auth/check-user-exists?email=X` | Public | - | `{ exists: boolean }` |
| POST | `/api/auth/register` | User | `name`, `phone` | `{ user: UserDto, isNewUser: boolean }` |
| GET | `/api/users/profile` | User | - | `UserDto` |
| GET | `/api/users/dashboard-summary` | User | - | `DashboardSummaryDto` |

### Wallet & Address
| Method | Path | Auth | Request Body (key fields) | Returns |
|---|---|---|---|---|
| GET | `/api/wallettransactions/my-balance` | User | - | `{ balance: number, userId: number }` |
| POST | `/api/wallettransactions/topup` | User | `amount` | `WalletTransactionDto` |
| GET | `/api/useraddresses` | User | - | `UserAddressDetailDto[]` |
| POST | `/api/useraddresses` | User | `addressLine1`, `pincode`, `serviceableLocationId` | `UserAddressDetailDto` |
| PUT | `/api/useraddresses/{id}/set-primary` | User | - | `UserAddressDetailDto` |

### Meals & Ordering
| Method | Path | Auth | Request Body (key fields) | Returns |
|---|---|---|---|---|
| GET | `/api/meals/public` | Public | - | `MealDto[]` |
| GET | `/api/meals/{id}/details` | User | - | `MealWithDetailsDto` |
| POST | `/api/orders/create-from-meal-builder` | User | `mealId`, `selectedIngredients` | `OrderCreationResponseDto` |
| GET | `/api/orders/users/me/orders` | User | - | `EnhancedOrderHistoryDto[]` |
| POST | `/api/orders/{id}/reorder` | User | - | `OrderCreationResponseDto` |

### Subscriptions & Scheduled Orders
| Method | Path | Auth | Request Body (key fields) | Returns |
|---|---|---|---|---|
| GET | `/api/subscriptions/active` | User | - | `SubscriptionDto[]` |
| POST | `/api/subscriptions` | User | `mealId`, `frequency`, `startDate`, `endDate`, `weeklySchedule` | `SubscriptionDto` |
| PATCH | `/api/subscriptions/{id}/activate` | User | - | `{ message: string }` |
| PATCH | `/api/subscriptions/{id}/deactivate` | User | - | `{ message: string }` |
| GET | `/api/scheduledorders/tomorrow` | User | - | `ScheduledOrderResponseDto[]` |
| PUT | `/api/scheduledorders/{id}/modify` | User | `selectedIngredients` | `{ message: string }` |

## 7. Key Business Rules (things that will burn you if you miss them)

- Price is ALWAYS server-computed. Never send a price. The server ignores it.
- Wallet minimum top-up: ₹50. Max balance: ₹50,000.
- User must have a primary address before placing any order or creating a subscription.
- Weekly subscriptions REQUIRE `weeklySchedule` array. Other frequencies ignore it.
- DayOfWeek: 0=Sunday, 1=Monday ... 6=Saturday
- Quantity per day: 1-10. Ingredient quantity per order: 1-100.
- Duplicate ingredientIds in one order → 400 error.
- Subscription response may have message field with a warning (not an error) — show it to the user.
- Paginated endpoints: always send page and pageSize. Default pageSize = 20. Never exceed max (50 for meals, 100 for wallet).
- After account deletion, the JWT is immediately rejected (401). Clear local storage and redirect to login.
- Token expiry (exp claim) ≠ ACCOUNT_DELETED. Handle them differently.
- Serviceable location check BEFORE address creation. Pincode must be in a serviceable area.
- Reorder uses the CURRENT prices from DB, not historical prices.
- ScheduledOrders are read-only for users. They cannot be created or modified directly by the frontend (use `/modify` for ingredients).
- The midnight job runs at 12:00 AM IST. Between 11:50 PM and 12:01 AM IST the wallet balance may be temporarily lower than expected — this is normal.

## 8. TypeScript Types (copy-paste ready)

```typescript
export interface UserDto {
  userId: number;
  name: string;
  email: string;
  phone: string;
  role: string;
  walletBalance: number;
  accountStatus: string;
  isProfileComplete: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SubscriptionDto {
  subscriptionId: number;
  userId: number;
  userMealId: number;
  frequency: number; // 0=Daily, 1=Weekly, 2=Monthly
  startDate: string; // YYYY-MM-DD
  endDate: string; // YYYY-MM-DD
  isActive: boolean;
  nextScheduledDate?: string;
  userName: string;
  mealName: string;
  mealPrice: number;
  weeklySchedule: WeeklyScheduleDto[];
}

export interface WeeklyScheduleDto {
  dayOfWeek: number; // 0-6
  quantity: number;
}

export interface CreateUserAddressDto {
  label: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  state: string;
  pincode: string;
  serviceableLocationId: number;
  isPrimary: boolean;
}

export interface CreateSubscriptionDto {
  mealId: number;
  frequency: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  weeklySchedule?: WeeklyScheduleDto[];
}

export interface OrderDto {
  orderId: number;
  userId: number;
  orderStatus: string;
  totalPrice: number;
  createdAt: string;
  updatedAt: string;
}

export interface WalletTransactionDto {
  transactionId: number;
  userId: number;
  amount: number;
  type: string; // "Credit" | "Debit"
  description: string;
  referenceType?: string;
  referenceId?: number;
  createdAt: string;
}
```

## 9. Supabase Auth Integration

```typescript
import { createClient } from '@supabase/supabase-js';

const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

// 1. Sign In
const { data, error } = await supabase.auth.signInWithOtp({
  email: 'user@example.com'
});

// 2. Verify OTP
const { data: sessionData, error: verifyError } = await supabase.auth.verifyOtp({
  email: 'user@example.com',
  token: '123456',
  type: 'email'
});

if (sessionData.session) {
  const token = sessionData.session.access_token;
  
  // 3. Check if user exists in backend
  const checkRes = await fetch(`${API_URL}/api/auth/check-user-exists?email=user@example.com`);
  const { data: { exists } } = await checkRes.json();
  
  if (!exists) {
    // 4. Register new user
    await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json' 
      },
      body: JSON.stringify({ name: 'John Doe', phone: '+919876543210' })
    });
  }
}
```

## 10. Admin Panel Reference

**Note:** Admin role is set via `PATCH /api/users/{id}/role` by an existing admin.

| Method | Path | Action |
|---|---|---|
| GET | `/api/users` | List all users |
| PATCH | `/api/users/{id}/role` | Promote/demote users |
| GET | `/api/kitchen/today` | View confirmed orders |
| PUT | `/api/kitchen/{id}/mark-prepared` | Mark order prepared |
| GET | `/api/wallettransactions/admin/all` | View ledger |
| POST | `/api/wallettransactions/admin/user/{id}/credit` | Issue refund |
| POST | `/api/meals/admin` | Create meal template |
| POST | `/api/meals/admin/{id}/image` | Upload meal image |

---
