/**
 * SOVVA k6 Stress Test — 250 Users
 *
 * Purpose: Find the EXACT connection pool saturation point.
 * Expected: DB pool (MaxPoolSize=20) will saturate at ~15 simultaneous requests.
 * p(95) will climb to 1–3s. Error rate 3–8%.
 *
 * Usage:
 *   k6 run -e API_URL=https://your-api.onrender.com \
 *          -e JWT_TOKEN=<valid_customer_jwt> \
 *          stress-250-users.js
 *
 * ─── REQUIRED BEFORE RUNNING ───────────────────────────────────────────────
 * Switch DATABASE_URL to Supabase Transaction Pooler (pgBouncer) URL:
 *   postgresql://postgres.xxx:[password]@aws-0-ap-south-1.pooler.supabase.com:6543/postgres
 * Without pgBouncer this test WILL produce 503s at ~30+ concurrent VUs.
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate    = new Rate('errors');
const dashboardDur = new Trend('dashboard_duration_ms');
const walletDur    = new Trend('wallet_duration_ms');

const BASE_URL  = __ENV.API_URL   || 'http://localhost:10000';
const JWT_TOKEN = __ENV.JWT_TOKEN || 'REPLACE_WITH_VALID_JWT';
const headers   = { Authorization: `Bearer ${JWT_TOKEN}`, 'Content-Type': 'application/json' };

export const options = {
  stages: [
    { duration: '30s', target: 50  },   // warm up
    { duration: '1m',  target: 250 },   // ramp to 250
    { duration: '3m',  target: 250 },   // sustain
    { duration: '30s', target: 0   },   // ramp down
  ],
  thresholds: {
    // Relaxed thresholds — goal is observation, not pass/fail at this scale
    http_req_duration: ['p(95)<3000'],
    http_req_failed:   ['rate<0.10'],
    errors:            ['rate<0.10'],
  },
};

export default function () {
  group('dashboard', () => {
    const res = http.get(`${BASE_URL}/api/dashboard/summary`, { headers });
    dashboardDur.add(res.timings.duration);
    errorRate.add(res.status !== 200);
    check(res, {
      'dashboard 200 or 503': (r) => [200, 503, 429].includes(r.status),
    });
  });

  group('wallet balance', () => {
    const res = http.get(`${BASE_URL}/api/wallettransactions/my-balance`, { headers });
    walletDur.add(res.timings.duration);
    errorRate.add(res.status !== 200);
    check(res, {
      'balance non-500': (r) => r.status !== 500,
    });
  });

  group('orders paginated', () => {
    const res = http.get(`${BASE_URL}/api/orders/my-orders?page=1&pageSize=5`, { headers });
    check(res, { 'orders non-500': (r) => r.status !== 500 });
  });

  sleep(0.5); // shorter sleep = higher RPS = faster pool saturation
}

export function handleSummary(data) {
  const p95 = data.metrics['http_req_duration'].values['p(95)'];
  const errRate = data.metrics['errors'] ? data.metrics['errors'].values['rate'] : 0;

  console.log(`\n╔══════════════════════════════════════╗`);
  console.log(`║  SOVVA 250-User Stress Test Summary  ║`);
  console.log(`╠══════════════════════════════════════╣`);
  console.log(`║  p(95) latency : ${p95.toFixed(0).padStart(6)}ms            ║`);
  console.log(`║  Error rate    : ${(errRate * 100).toFixed(2).padStart(6)}%             ║`);
  console.log(`║  Status        : ${errRate < 0.05 ? '✅ PASS' : '❌ FAIL — Fix pgBouncer first'}  ║`);
  console.log(`╚══════════════════════════════════════╝\n`);

  return { stdout: JSON.stringify(data, null, 2) };
}
