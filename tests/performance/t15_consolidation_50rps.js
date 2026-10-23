import http from 'k6/http';
import { Counter, Rate } from 'k6/metrics';

const warmup = __ENV.T15_PHASE === 'warmup';
const durationSeconds = warmup ? 10 : 120;
const targetRate = warmup ? 10 : 50;
const endpoint = `${__ENV.T15_BASE_URL}/consolidations/${__ENV.T15_BUSINESS_DATE}`;
const requestsSent = new Counter('t15_requests_sent');
const failures = new Counter('t15_failures');
const failureRate = new Rate('t15_failure_rate');

export const options = {
  scenarios: {
    consolidation: {
      executor: 'constant-arrival-rate',
      rate: targetRate,
      timeUnit: '1s',
      duration: `${durationSeconds}s`,
      preAllocatedVUs: 100,
      gracefulStop: '0s',
    },
  },
  summaryTrendStats: ['p(50)', 'p(95)', 'p(99)'],
  thresholds: warmup ? {} : {
    t15_requests_sent: ['count>=6000'],
    t15_failure_rate: ['rate<=0.05'],
    dropped_iterations: ['count==0'],
  },
};

function valid(body) {
  try {
    const value = JSON.parse(body);
    return value.businessDate === __ENV.T15_BUSINESS_DATE &&
      value.currency === 'BRL' &&
      value.totalCredits === '100.00' &&
      value.totalDebits === '20.00' &&
      value.balance === '80.00' &&
      value.transactionCount === 2 &&
      value.consistency === 'EVENTUAL' &&
      value.projectionState === 'AVAILABLE' &&
      value.completeness === 'UNKNOWN';
  } catch (_) {
    return false;
  }
}

export default function () {
  requestsSent.add(1);
  let failed = true;
  try {
    const response = http.get(endpoint, {
      headers: { Authorization: `Bearer ${__ENV.T15_TOKEN}` },
      timeout: '5s',
      redirects: 0,
    });
    failed = response.status !== 200 || !valid(response.body);
  } catch (_) {
    // A transport error or timeout counts once, like every other failed request.
  }
  if (failed) failures.add(1);
  failureRate.add(failed);
}

export function handleSummary(data) {
  const sent = data.metrics.t15_requests_sent?.values.count || 0;
  const failed = data.metrics.t15_failures?.values.count || 0;
  const dropped = data.metrics.dropped_iterations?.values.count || 0;
  const latency = data.metrics.http_req_duration?.values || {};
  const report = {
    phase: warmup ? 'warmup' : 'measured',
    endpoint,
    environment: 'local Docker Compose',
    expected_requests: targetRate * durationSeconds,
    requests_sent: sent,
    requests_per_second: sent / durationSeconds,
    failures: failed,
    failure_rate: sent ? failed / sent : 1,
    dropped_iterations: dropped,
    p50_ms: latency['p(50)'],
    p95_ms: latency['p(95)'],
    p99_ms: latency['p(99)'],
    duration_seconds: durationSeconds,
    pass: warmup ? null : sent >= 6000 && failed / sent <= 0.05 && dropped === 0,
    internal_latency_goals_met: latency['p(95)'] <= 500 && latency['p(99)'] <= 1000,
  };
  return { stdout: `T15_SUMMARY_JSON ${JSON.stringify(report)}\n` };
}
