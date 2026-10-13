// Autocomplete as typed: short prefixes, many repeats (as in real traffic). Set NOCACHE=1 to add
// a unique suffix per request so every call misses the 30 s output cache and hits Elasticsearch.
// Run: k6 run -e RATE=100 tests/load/autocomplete.js
import http from 'k6/http';
import { check } from 'k6';
import { BASE, PREFIXES, pick, qs } from './lib.js';

export const options = {
  scenarios: {
    autocomplete: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 100),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: 40,
      maxVUs: 200,
    },
  },
  thresholds: {
    'http_req_duration{scenario:autocomplete}': ['p(95)<50'],
    http_req_failed: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  let q = pick(PREFIXES);
  if (__ENV.NOCACHE) q = `${q} ${String.fromCharCode(97 + (__ITER % 26))}${__VU}`;
  const params = { q };
  if (Math.random() < 0.15) params.category = pick(['laptops', 'graphics-cards', 'ssds', 'monitors']);
  const res = http.get(`${BASE}/api/products/autocomplete?${qs(params)}`, { tags: { name: 'autocomplete' } });
  check(res, { 'status 200': (x) => x.status === 200 });
}
