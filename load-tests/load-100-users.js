/**
 * SOVVA k6 Load Test — Baseline (100 Users)
 *
 * Usage:
 *   k6 run -e API_URL=https://your-api.onrender.com \
 *          -e JWT_TOKEN=<valid_customer_jwt> \
 *          load-100-users.js
 *
 * Expected results at 100 VUs:
 *   p(50) < 150ms  p(95) < 400ms  p(99) < 800ms
 *   Error rate: < 0.5%
 *   Bottleneck: None — well within DB pool limits
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// ── Custom metrics ──────────────────────────────────────────────────────────
const errorRate    = new Rate('errors');
const dashboardDur = new Trend('dashboard_duration_ms');
const walletDur    = new Trend('wallet_duration_ms');
const ordersDur    = new Trend('orders_duration_ms');

// ── Config ───────────────────────────────────────────────────────────────────
const BASE_URL   = __ENV.API_URL   || 'http://localhost:10000';
const JWT_TOKEN  = __ENV.JWT_TOKEN || 'REPLACE_WITH_VALID_JWT';

const headers = {
  Authorization: `Bearer ${JWT_TOKEN}`,
  'Content-Type': 'application/json',
};

export const options = {
  stages: [
    { duration: '30s', target: 100 },   // ramp up
    { duration: '2m',  target: 100 },   // sustain at 100
    { duration: '30s', target: 0   },   // ramp down
  ],
  thresholds: {
    http_req_duration:    ['p(95)<500'],   // 95th percentile < 500ms
    http_req_failed:      ['rate<0.01'],   // < 1% failure rate
    dashboard_duration_ms: ['p(95)<600'],
    wallet_duration_ms:    ['p(95)<400'],
    errors:                ['rate<0.01'],
  },
};

export default function () {
  group('health', () => {
    const res = http.get(`${BASE_URL}/health/live`);
    check(res, { 'live 200': (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group('dashboard', () => {
    const res = http.get(`${BASE_URL}/api/dashboard/summary`, { headers });
    dashboardDur.add(res.timings.duration);
    check(res, { 'dashboard 200': (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group('wallet balance', () => {
    const res = http.get(`${BASE_URL}/api/wallettransactions/my-balance`, { headers });
    walletDur.add(res.timings.duration);
    check(res, { 'balance 200': (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group('my orders', () => {
    const res = http.get(`${BASE_URL}/api/orders/my-orders?page=1&pageSize=10`, { headers });
    ordersDur.add(res.timings.duration);
    check(res, { 'orders 200': (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  group('subscriptions', () => {
    const res = http.get(`${BASE_URL}/api/subscriptions/my-subscriptions`, { headers });
    check(res, { 'subs 200': (r) => r.status === 200 });
    errorRate.add(res.status !== 200);
  });

  sleep(1);
}
