# Case Study: Refactoring the Sovva Subscription Engine

## 1. The Context
Sovva is a highly dynamic health-focused meal delivery application. A core feature of the platform is allowing users to subscribe to recurring meal deliveries (e.g., daily or weekly). As the product evolved, we identified critical scalability limitations in how subscriptions were originally modeled and managed.

---

## 2. The Problem (Before)

Initially, the subscription architecture was built exclusively around a "Meal Builder" concept:
- **Rigid Data Model:** The `Subscriptions` table was strictly tied to a `UserMealId` (a custom meal created by the end-user).
- **Admin Limitations:** If an Admin curated a new fixed meal (e.g., "Dates & Walnut Oats") and added it to the master `Meals` menu, users could not easily subscribe to it because the database expected a custom `UserMealId`.
- **Silent Price Hikes:** If an Admin updated the price of a master meal, subscribed users would be silently charged the new, higher amount during the nightly cron job, leading to potential customer trust issues and chargebacks.
- **Frontend Split-Brain:** On the client side, the Angular application managed subscription state in two separate places—`AppStore` and `SubscriptionService.localSubscriptions`. This caused UI bugs where a user might subscribe from the Menu, but the Dashboard wouldn't update because it was reading from a stale cache.

---

## 3. The Solution (How We Fixed It)

To prepare Sovva for massive scale, we engineered a complete overhaul of the subscription flow across the full stack.

### Phase 1: Dual-Target Backend Architecture
We redesigned the database schema to support **Dual-Target Subscriptions**.
- We updated the `Subscriptions` table to accept either a `MealId` (Fixed Admin Menu) or a `UserMealId` (Custom User Menu). 
- The backend `SubscriptionService` was refactored to dynamically resolve the correct meal name, image, and price depending on which foreign key was present. 
- *Impact:* Users can now subscribe to anything—curated admin meals or their own custom creations.

### Phase 2: Enterprise-Grade Price Protection
To build trust mirroring giants like Swiggy and Zomato, we implemented a robust Price Protection system.
- We introduced an `AgreedPrice` column. When a user subscribes, their current price is locked in.
- The `ScheduledOrderBackgroundService` (the nightly cron job that generates tomorrow's orders) was updated with a smart detection mechanism. If it detects that the current master meal price is strictly greater than the user's `AgreedPrice`, it **halts the order**.
- The system automatically transitions the subscription to a `Paused` state and logs a `PauseReason` (e.g., *"Price increased from ₹99 to ₹149"*).

### Phase 3: Frontend State Consolidation
We eliminated the "split-brain" state architecture in the Angular frontend.
- `SubscriptionService` was stripped of all local state arrays and BehaviorSubjects, converting it into a pure HTTP API wrapper.
- All subscription data was migrated to the `AppStore`, utilizing Angular Signals for a reactive, single source of truth.
- When a user mutates a subscription (e.g., pausing a meal), the `AppStore` handles the optimistic UI update instantly across the entire application without requiring redundant HTTP fetches.

### Phase 4: UI/UX Price Protection Flow
We surfaced the backend's automatic pauses gracefully to the user.
- If a subscription is auto-paused due to a price hike, a prominent warning banner appears on the subscription card detailing the exact `PauseReason`.
- The UI displays the original `AgreedPrice` alongside the crossed-out new market price.
- If the user clicks "Resume", a browser confirmation dialog intercepts the action: *"The price for X has updated... By resuming, you agree to the new price."* This guarantees explicit user consent.

---

## 4. The Result

The refactored Subscription Engine transformed Sovva's recurring billing logic into a highly scalable, enterprise-ready system. 

- **Business Scalability:** Admins can now rapidly expand the fixed menu, knowing users can subscribe to new offerings instantly.
- **Consumer Trust:** Zero surprise charges. Users maintain complete control over their billing, significantly reducing the risk of chargebacks or platform abandonment.
- **Frontend Performance:** Code complexity was heavily reduced, UI sync bugs were eradicated, and 9 unused legacy files were deleted, resulting in a cleaner, faster-compiling Angular application.
