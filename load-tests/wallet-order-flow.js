/**
 * SOVVA k6 Financial Flow Test — Wallet + Order Creation
 *
 * Purpose: Simulate the FULL financial lifecycle under load.
 * This is the most important test — it exercises the paths that handle real money.
 *
 * Pre-requisites:
 *   1. Seed 50 test users, each with ₹500 wallet balance (use DbSeeder or admin API)
 *   2. Each user must have at least one UserMeal and one DeliveryAddress
 *   3. Provide a JWT_TOKENS env var as a comma-separated list of 50 valid JWTs
 *
 * Usage:
 *   k6 run -e API_URL=https://your-api.onrender.com \
 *          -e JWT_TOKENS="token1,token2,...,token50" \
 *          wallet-order-flow.js
 *
 * ─── EXPECTED RESULTS ────────────────────────────────────────────────────────
 * VUs: 50 concurrent
 * Wallet top-up p(95): < 1000ms
 * Order creation p(95): < 2000ms
 * Error rate: < 2% (mostly 402 Insufficient Balance = expected)
 * ─────────────────────────────────────────────────────────────────────────────
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// ── Custom metrics ───────────────────────────────────────────────────────────
const errorRate        = new Rate('errors');
const topupDur         = new Trend('topup_duration_ms', true);
const orderCreateDur   = new Trend('order_create_duration_ms', true);
const insufficientBal  = new Counter('insufficient_balance_count');
const ordersCreated    = new Counter('orders_created_count');
const topupsSucceeded  = new Counter('topups_succeeded_count');

// ── Config ───────────────────────────────────────────────────────────────────
const BASE_URL    = __ENV.API_URL    || 'http://localhost:10000';
const tokensRaw   = __ENV.JWT_TOKENS || 'REPLACE_WITH_COMMA_SEPARATED_JWTS';
const JWT_TOKENS  = tokensRaw.split(',').filter(t => t.trim().length > 0);

// Each VU picks a token based on its __VU index (round-robin)
function getHeaders() {
  const token = JWT_TOKENS[(__VU - 1) % JWT_TOKENS.length];
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
}

export const options = {
  vus: 50,
  duration: '3m',
  thresholds: {
    topup_duration_ms:        ['p(95)<1200'],
    order_create_duration_ms: ['p(95)<2500'],
    http_req_failed:          ['rate<0.05'],  // 402s are NOT failures — see check below
    errors:                   ['rate<0.05'],
  },
};

export default function () {
  const h = getHeaders();

  // ── Step 1: Check current balance ───────────────────────────────────────
  group('1. wallet_balance', () => {
    const res = http.get(`${BASE_URL}/api/wallettransactions/my-balance`, { headers: h });
    check(res, { 'balance 200': (r) => r.status === 200 });
  });

  // ── Step 2: Top up wallet ────────────────────────────────────────────────
  group('2. wallet_topup', () => {
    const payload = JSON.stringify({ amount: 150 });
    const res = http.post(`${BASE_URL}/api/wallettransactions/topup`, payload, {
      headers: h,
      tags: { name: 'wallet-topup' },
    });
    topupDur.add(res.timings.duration);

    const ok = check(res, {
      'topup 200 or 400 (business rule)': (r) => [200, 400, 429].includes(r.status),
    });

    if (res.status === 200) topupsSucceeded.add(1);
    if (!ok) errorRate.add(1);
  });

  sleep(0.2);

  // ── Step 3: Attempt order creation ──────────────────────────────────────
  group('3. order_create', () => {
    // Use userMealId=1 as a placeholder — in a real staging run, each user
    // should have their own seeded userMealId passed via env or data file.
    const payload = JSON.stringify({
      userMealId: 1,
      deliveryAddressId: 1
    });
    const res = http.post(`${BASE_URL}/api/orders/create-from-meal-builder`, payload, {
      headers: h,
      tags: { name: 'order-create' },
    });
    orderCreateDur.add(res.timings.duration);

    if (res.status === 402 || res.status === 400) {
      // 402 Insufficient Balance is a VALID business outcome — not an error
      insufficientBal.add(1);
    } else if (res.status === 200 || res.status === 201) {
      ordersCreated.add(1);
    } else {
      // Genuine server error
      errorRate.add(1);
    }

    check(res, {
      'order: not 500': (r) => r.status !== 500,
      'order: not 503': (r) => r.status !== 503,
    });
  });

  sleep(1);
}

export function handleSummary(data) {
  return { stdout: JSON.stringify(data.metrics, null, 2) };
}
