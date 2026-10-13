import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SearchResult } from '../../core/api.models';
import { SearchStore } from './search-store';

const result = (over: Partial<SearchResult> = {}): SearchResult => ({
  items: [], total: 0, totalIsApproximate: false, page: 1, pageSize: 24, facets: [], tookMs: 3, ...over,
});

describe('SearchStore', () => {
  let store: SearchStore;
  let router: Router;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(), provideHttpClientTesting(), SearchStore],
    });
    router = TestBed.inject(Router);
    http = TestBed.inject(HttpTestingController);
    await router.navigateByUrl('/?q=rtx%204070&brands=asus');
    store = TestBed.inject(SearchStore);
    TestBed.tick();
  });

  afterEach(() => http.verify());

  const flushSearch = (body = result()) => {
    const req = http.expectOne((r) => r.url === '/api/products/search');
    req.flush(body);
    return req;
  };

  it('requests exactly what is in the URL', () => {
    const req = flushSearch();
    expect(req.request.params.get('q')).toBe('rtx 4070');
    expect(req.request.params.get('brands')).toBe('asus');
    expect(req.request.params.get('pageSize')).toBe('24');
  });

  it('toggling a facet value rewrites the URL and resets the page', async () => {
    flushSearch();
    await router.navigateByUrl('/?q=rtx%204070&brands=asus&page=3');
    TestBed.tick();
    flushSearch();

    store.toggle('brands', 'msi');
    await TestBed.inject(Router).navigated;
    await new Promise((r) => setTimeout(r));
    TestBed.tick();

    expect(router.url).toContain('brands=asus,msi');
    expect(router.url).not.toContain('page=');
    flushSearch();
  });

  it('builds removable chips with facet labels', async () => {
    flushSearch(result({
      facets: [{ key: 'brands', label: 'Brand', type: 'terms', buckets: [{ value: 'asus', label: 'ASUS', count: 5, selected: true }] }],
    }));
    TestBed.tick();

    expect(store.chips().map((c) => c.label)).toEqual(['ASUS']);
  });

  it('clearing filters keeps the query text', async () => {
    flushSearch();
    store.clearFilters();
    await new Promise((r) => setTimeout(r));
    TestBed.tick();

    expect(router.url).toBe('/?q=rtx%204070');
    flushSearch();
  });

  it('a new text search drops the old filters', async () => {
    flushSearch();
    store.search('ddr5');
    await new Promise((r) => setTimeout(r));
    TestBed.tick();

    expect(router.url).toBe('/?q=ddr5');
    flushSearch();
  });
});
