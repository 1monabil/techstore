// Facet-heavy browsing: category pages with several multi-select filters, price ranges and
// often no text at all — every request computes all facet aggregations.
// Run: k6 run -e RATE=30 tests/load/facets.js
import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE, BRANDS, CATEGORY_FILTERS, pick, qs } from './lib.js';

const esTook = new Trend('es_took_ms', true);

export const options = {
  scenarios: {
    facets: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 30),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: 40,
      maxVUs: 200,
    },
  },
  thresholds: {
    'http_req_duration{scenario:facets}': ['p(95)<250'],
    http_req_failed: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const f = pick(CATEGORY_FILTERS);
  const params = { category: f.category, ...f.spec() };
  if (Math.random() < 0.6) params.brands = [pick(BRANDS), pick(BRANDS)].join(',');
  if (Math.random() < 0.4) { params.minPrice = pick(['100', '250', '500']); params.maxPrice = pick(['1000', '2000']); }
  if (Math.random() < 0.3) params.minRating = '4';
  if (Math.random() < 0.3) params.inStock = 'true';
  if (Math.random() < 0.25) params.q = pick(['gaming', 'wireless', 'oled', 'rgb', 'white']);

  const res = http.get(`${BASE}/api/products/search?${qs(params)}`, { tags: { name: 'facets' } });
  check(res, { 'status 200': (x) => x.status === 200 });
  if (res.status === 200) esTook.add(res.json('tookMs'));
}
