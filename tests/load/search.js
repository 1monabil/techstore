// Text search: relevance + highlighting + facets, light filtering, some sorting and paging.
// Run: k6 run -e RATE=40 tests/load/search.js
import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE, BRANDS, QUERIES, pick, qs } from './lib.js';

const esTook = new Trend('es_took_ms', true);

export const options = {
  scenarios: {
    search: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 40),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: 40,
      maxVUs: 200,
    },
  },
  thresholds: {
    'http_req_duration{scenario:search}': ['p(95)<150'],
    http_req_failed: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const r = Math.random();
  const params = { q: pick(QUERIES) };
  if (r < 0.2) params.brands = pick(BRANDS);
  if (r > 0.85) params.sort = pick(['price_asc', 'price_desc', 'rating', 'bestselling']);
  if (Math.random() < 0.15) params.page = String(2 + Math.floor(Math.random() * 4));

  const res = http.get(`${BASE}/api/products/search?${qs(params)}`, { tags: { name: 'search' } });
  check(res, { 'status 200': (x) => x.status === 200 });
  if (res.status === 200) esTook.add(res.json('tookMs'));
}
