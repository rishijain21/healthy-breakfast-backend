# SOVVA BACKEND — SECURITY AUDIT

**Generated:** 2026-05-22
**Phase:** 4 — Security Analysis
**Scope:** Authentication, authorization, data access, injection, secrets, OWASP Top 10 coverage

---

## EXECUTIVE SUMMARY

The application demonstrates **solid security fundamentals**:
- ✅ JWT-based authentication via Supabase
- ✅ Server-side user scoping (userId from JWT, never from request body)
- ✅ Role-based authorization (Admin/Customer separation)
- ✅ SQL injection prevention via EF Core parameterized queries
- ✅ Parameterized raw SQL in AtomicDebitAsync/AtomicCreditAsync
- ✅ Rate limiting on auth endpoints
- ✅ Soft-delete query filters
- ✅ Non-root Docker user
- ✅ Input validation (FluentValidation + manual guards)

**Remaining concerns are P1-P2 level, not critical vulnerabilities.**

---

## OWASP TOP 10 — COVERAGE MATRIX

### A01: Broken Access Control ✅ (mostly covered)

| Check | Status | Notes |
|-------|--------|-------|
| JWT authentication on all endpoints | ✅ | Supabase JWT validation |
| UserId from token, not request body | ✅ | `User.GetSovvaUserId()` extension |
| Admin endpoints protected | ✅ | `[Authorize(Roles = "Admin")]` |
| Object-level access control | ⚠️ | See SEC-01 below |
| Rate limiting | ✅ | Fixed-window: auth(10/min), default(100/min) |

### A02: Cryptographic Failures ✅

| Check | Status | Notes |
|-------|--------|-------|
| Passwords hashed | ✅ | Managed by Supabase (bcrypt) |
| JWT signing keys | ✅ | Supabase manages key rotation |
| Sensitive data in transit | ✅ | HTTPS enforced by Render |
| Secrets in code | ✅ | All secrets via environment variables |
| Connection string security | ⚠️ | See SEC-02 |

### A03: Injection ✅

| Check | Status | Notes |
|-------|--------|-------|
| SQL injection | ✅ | EF Core parameterized queries |
| Raw SQL parameterized | ✅ | `ExecuteSqlRawAsync` uses `{0}` placeholders |
| NoSQL injection | N/A | PostgreSQL only |
| XSS | ✅ | API-only (no HTML rendering) |
| Command injection | N/A | No shell commands |

### A04: Insecure Design ⚠️

| Check | Status | Notes |
|-------|--------|-------|
| Rate limiting on financial ops | ⚠️ | See SEC-03 |
| Idempotency on payments | ✅ | AtomicDebitAsync + duplicate checks |
| Business logic guards | ✅ | Balance checks, min/max amounts |

### A05: Security Misconfiguration ⚠️

| Check | Status | Notes |
|-------|--------|-------|
| Error details in production | ⚠️ | See SEC-04 |
| Swagger in production | ⚠️ | See SEC-05 |
| CORS configuration | ✅ | Explicit origin allowlist |
| Hangfire dashboard auth | ✅ | Basic auth with env vars |
| Health check exposure | ⚠️ | See SEC-06 |

### A06-A10 ✅ (Low Risk)

These categories are well-covered by the stack (Supabase Auth, EF Core, ASP.NET Core defaults).

---

## FINDINGS

---

### SEC-01: Missing Object-Level Authorization in Some Admin Endpoints

**SEVERITY:** 🟠 MEDIUM
**CONFIDENCE:** HIGH

**Problem:**
Several admin endpoints accept userId or orderId in the path but don't verify the requesting admin has permission to act on that specific resource. While admin-only authorization is enforced (`[Authorize(Roles = "Admin")]`), a compromised admin token could operate on ANY user's data.

Currently this is acceptable for a small team with one admin, but becomes a concern as the admin team grows.

**Affected endpoints:**
- `PUT /api/Admin/orders/{orderId}/status`
- `POST /api/Admin/wallet/credit`
- `GET /api/Admin/users/{userId}`

**Recommendation:** For now, this is acceptable. When you have multiple admin roles (e.g., support vs. super-admin), implement resource-scoped policies.

**PRIORITY:** P3 (future consideration)

---

### SEC-02: Connection String Fallback Chain Logs Sensitive Data

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `ServiceCollectionExtensions.cs` (Lines 62-74)

**Problem:**
The connection string resolution logs the chosen source:
```csharp
Log.Information("Using DATABASE_SESSION_URL for connection string");
```

While the connection string value itself is not logged, the resolution path is. The connection string is read from environment variables and passed directly to Npgsql. If `DATABASE_URL` contains credentials in the URL format (`postgres://user:pass@host/db`), these are embedded in the connection string.

**Recommendation:** Ensure the connection string is never logged at any level. Consider using `NpgsqlConnectionStringBuilder` to mask credentials in any diagnostic output.

**PRIORITY:** P2

---

### SEC-03: No Rate Limiting on Financial Operations

**SEVERITY:** 🟠 HIGH
**CONFIDENCE:** HIGH

**Problem:**
The rate limiter applies two policies:
- `auth` → 10/min (on auth endpoints)
- `default` → 100/min (everything else)

Financial endpoints use the `default` policy:
- `POST /api/Order/create-from-builder` — 100 orders/min possible
- `POST /api/WalletTransactions/topup` — 100 top-ups/min possible
- `POST /api/Order/reorder/{id}` — 100 reorders/min possible

While the wallet has a max balance cap and balance checks prevent overdraw, 100 rapid-fire order creation attempts per minute could:
1. Create unnecessary DB load
2. Enable brute-force probing of balance amounts
3. Overwhelm Hangfire with downstream scheduled orders

**Recommendation:** Add a `financial` rate limit policy (10-20/min) applied to:
- Order creation endpoints
- Wallet top-up endpoints
- Reorder endpoints

**PRIORITY:** P1

---

### SEC-04: `InvalidOperationException` Messages Leak to Client

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `GlobalExceptionMiddleware.cs` (Line 62-63)

**Problem:**
```csharp
InvalidOperationException ioe =>
    (StatusCodes.Status400BadRequest, ErrorCodes.InvalidOperation, ioe.Message),
```

`InvalidOperationException` is used for both business logic errors AND internal errors. The handler passes `ioe.Message` directly to the client. Some internal exception messages may leak implementation details:

```
"ScheduledOrder #123 has no DeliveryAddressId. Cannot create Order without a delivery address."
"User 456 has no AuthMapping — cannot create ScheduledOrder."
```

These messages reveal internal entity IDs, column names, and architecture details.

**Recommendation:**
1. Create specific domain exceptions for user-facing errors (e.g., `DeliveryAddressRequiredException`)
2. Map `InvalidOperationException` to a generic message in production
3. Keep the detailed message in server logs only

**PRIORITY:** P1

---

### SEC-05: Swagger Accessible in Production (Conditionally)

**SEVERITY:** 🟢 LOW
**CONFIDENCE:** MEDIUM

**FILE:** `WebApplicationExtensions.cs`

Swagger is conditionally enabled:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

This is correct — Swagger is NOT accessible in production. ✅ No action needed.

---

### SEC-06: Health Endpoints Expose System Details

**SEVERITY:** 🟢 LOW
**CONFIDENCE:** HIGH

**FILE:** `WebApplicationExtensions.cs` (Health check mapping)

Health endpoints (`/health/live`, `/health/ready`, `/health`) are unauthenticated. The `/health/ready` endpoint checks PostgreSQL connectivity and Hangfire availability — if it returns details about failures, it could reveal infrastructure information.

**Recommendation:** Ensure health check responses use `Healthy/Unhealthy` status only, without exposing connection strings or error details.

**PRIORITY:** P3

---

### SEC-07: Hangfire Dashboard Basic Auth Credentials

**SEVERITY:** 🟡 MEDIUM
**CONFIDENCE:** HIGH

**FILE:** `WebApplicationExtensions.cs` (Hangfire dashboard config)

Hangfire dashboard uses HTTP Basic Auth with credentials from environment variables. Basic Auth sends credentials as Base64 (not encrypted) on every request. While HTTPS encrypts the transport layer, Basic Auth is considered a weak mechanism.

**Recommendation:** Acceptable for internal-only access. If the Hangfire dashboard is publicly accessible (e.g., on Render), consider:
1. IP allowlisting at the reverse proxy level
2. OAuth-based Hangfire auth filter

**PRIORITY:** P2

---

### SEC-08: No CSRF Protection

**SEVERITY:** 🟢 LOW (API-only)
**CONFIDENCE:** HIGH

Since this is a stateless JWT API (no cookies for auth), CSRF protection is not needed. JWT tokens are sent via `Authorization: Bearer` header, which cannot be triggered by cross-site form submissions.

**Status:** ✅ Not applicable — correctly handled.

---

### SEC-09: Wallet Amount Validation — Minimum Amounts

**SEVERITY:** ✅ RESOLVED
**CONFIDENCE:** HIGH

Wallet amounts are validated:
- `Amount > 0` (database CHECK constraint)
- `MinTopUpAmount` (service-level check for customer top-ups)
- `MaxWalletBalance` (cap on total balance)
- Admin credits bypass `MinTopUpAmount` but still respect `MaxWalletBalance`

**Status:** ✅ Correctly implemented.

---

## SECRETS MANAGEMENT

| Secret | Storage | Accessed Via |
|--------|---------|-------------|
| Database Connection String | Environment Variable | `DATABASE_SESSION_URL` / `DATABASE_URL` |
| Supabase URL | Environment Variable | `Supabase__Url` |
| Supabase Anon Key | Environment Variable | `Supabase__AnonKey` |
| Supabase Service Role Key | Environment Variable | `Supabase__ServiceRoleKey` |
| Hangfire Dashboard Credentials | Environment Variable | `HangfireDashboard__Username/Password` |
| Seq API Key | Environment Variable | `Logging__SeqApiKey` |

**Assessment:** All secrets are externalized via environment variables. No hardcoded secrets found in source code. `appsettings.json` contains only empty placeholder values. ✅

---

## DATA ACCESS SCOPING VERIFICATION

| Operation | User Scoping | Method |
|-----------|-------------|--------|
| Get my orders | ✅ userId from JWT | `GetUserOrdersAsync(userId)` |
| Get my wallet | ✅ userId from JWT | `GetUserTransactionsAsync(userId)` |
| Get my subscriptions | ✅ userId from JWT | `GetSubscriptionsByUserIdAsync(userId)` |
| Get my scheduled orders | ✅ authId from JWT | `GetByAuthIdAndDateAsync(authId, date)` |
| Create order | ✅ userId from JWT | `CreateOrderFromMealBuilderAsync(dto, userId)` |
| Top up wallet | ✅ userId from JWT | `TopUpWalletAsync(userId, dto)` |
| Modify scheduled order | ✅ authId from JWT | `ModifyScheduledOrderAsync(id, authId, dto)` |
| Rate order | ✅ userId + ownership check | `order.UserId != userId → denied` |
| Reorder | ✅ userId + ownership check | `pastOrder.UserId != userId → denied` |
| Admin: credit wallet | ✅ Admin role + target userId | `[Authorize(Roles = "Admin")]` |
| Admin: update order status | ✅ Admin role | `[Authorize(Roles = "Admin")]` |
