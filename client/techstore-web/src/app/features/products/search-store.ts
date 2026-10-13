import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { Facet, SearchParams, SearchResult } from '../../core/api.models';
import { CatalogApi } from '../../core/catalog-api';
import { formatPriceWhole } from '../../core/format';

export const PAGE_SIZE = 24;
export const MAX_PAGE = 50;

/** URL keys that are not facet filters. */
const NON_FILTER_KEYS = new Set(['q', 'sort', 'page']);
const CORE_FILTER_KEYS = new Set(['category', 'brands', 'minPrice', 'maxPrice', 'minRating', 'inStock']);

export interface FilterChip {
  /** Query keys this chip clears. */
  keys: string[];
  /** For list-valued keys, the single value this chip clears. */
  value?: string;
  label: string;
}

export function paramsFrom(map: ParamMap): SearchParams {
  const params: SearchParams = {};
  for (const key of map.keys) {
    const value = map.get(key);
    if (value !== null && value !== '') params[key] = value;
  }
  return params;
}

const list = (value: string | undefined): string[] => (value ? value.split(',').filter(Boolean) : []);

/**
 * State for the products page. The URL query string is the single source of truth: every action
 * navigates, and results are derived from the current params, so refresh, sharing a link and
 * back/forward all reproduce the same view.
 */
@Injectable()
export class SearchStore {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly params = toSignal(this.route.queryParamMap.pipe(map(paramsFrom)), { initialValue: {} as SearchParams });

  private readonly _result = signal<SearchResult | null>(null);
  readonly result = this._result.asReadonly();
  readonly loading = signal(true);
  readonly failed = signal(false);
  /** Query text of the results currently shown (to decide skeleton vs. dimmed refresh). */
  private readonly shownQuery = signal<string | null>(null);

  readonly q = computed(() => this.params()['q'] ?? '');
  readonly category = computed(() => this.params()['category'] ?? null);
  readonly sort = computed(() => this.params()['sort'] ?? 'relevance');
  readonly page = computed(() => Number(this.params()['page'] ?? 1) || 1);
  readonly facets = computed<Facet[]>(() => this._result()?.facets ?? []);
  readonly pageCount = computed(() => Math.min(MAX_PAGE, Math.ceil((this._result()?.total ?? 0) / PAGE_SIZE)));
  /** Skeletons only when the text query changed; filter tweaks dim the current grid instead. */
  readonly showSkeleton = computed(() => this.loading() && (this._result() === null || this.shownQuery() !== this.q()));

  readonly chips = computed<FilterChip[]>(() => {
    const p = this.params();
    const facets = this.facets();
    const labelOf = (key: string, value: string) =>
      facets.find((f) => f.key === key)?.buckets.find((b) => b.value === value)?.label ?? value;
    const chips: FilterChip[] = [];

    if (p['category']) chips.push({ keys: ['category'], label: labelOf('category', p['category']) });
    for (const b of list(p['brands'])) chips.push({ keys: ['brands'], value: b, label: labelOf('brands', b) });
    if (p['minPrice'] || p['maxPrice']) {
      const min = p['minPrice'] ? formatPriceWhole(+p['minPrice']) : null;
      const max = p['maxPrice'] ? formatPriceWhole(+p['maxPrice']) : null;
      chips.push({ keys: ['minPrice', 'maxPrice'], label: min && max ? `${min} – ${max}` : min ? `${min} & up` : `Under ${max}` });
    }
    if (p['minRating']) chips.push({ keys: ['minRating'], label: `★ ${p['minRating']} & up` });
    if (p['inStock'] === 'true') chips.push({ keys: ['inStock'], label: 'In stock' });

    for (const key of Object.keys(p).filter((k) => !NON_FILTER_KEYS.has(k) && !CORE_FILTER_KEYS.has(k))) {
      const facet = facets.find((f) => f.key === key);
      for (const v of list(p[key])) chips.push({ keys: [key], value: v, label: `${facet?.label ?? key}: ${labelOf(key, v)}` });
    }
    return chips;
  });

  constructor() {
    toObservable(this.params)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap((p) =>
          this.api.search({ pageSize: `${PAGE_SIZE}`, ...p }).pipe(
            map((r) => ({ r, q: p['q'] ?? '' })),
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((res) => {
        if (res) {
          this._result.set(res.r);
          this.shownQuery.set(res.q);
        }
        this.loading.set(false);
      });
  }

  isSelected(key: string, value: string): boolean {
    return list(this.params()[key]).includes(value);
  }

  // ---------- actions (all navigate; state follows the URL) ----------

  search(q: string): void {
    this.navigate({ q: q || null }, { keepFilters: false });
  }

  toggle(key: string, value: string): void {
    const current = list(this.params()[key]);
    const next = current.includes(value) ? current.filter((v) => v !== value) : [...current, value];
    this.navigate({ [key]: next.length ? next.join(',') : null });
  }

  setCategory(slug: string | null): void {
    // Spec filters belong to a category; drop them when the category changes.
    const specKeys = Object.keys(this.params()).filter((k) => !NON_FILTER_KEYS.has(k) && !CORE_FILTER_KEYS.has(k));
    this.navigate({ category: slug, ...Object.fromEntries(specKeys.map((k) => [k, null])) });
  }

  setPrice(min: number | null, max: number | null): void {
    this.navigate({ minPrice: min !== null ? `${min}` : null, maxPrice: max !== null ? `${max}` : null });
  }

  setSingle(key: 'minRating' | 'inStock', value: string | null): void {
    this.navigate({ [key]: value });
  }

  setSort(sort: string): void {
    this.navigate({ sort: sort === 'relevance' ? null : sort });
  }

  goToPage(page: number): void {
    this.navigate({ page: page > 1 ? `${page}` : null }, { resetPage: false });
  }

  remove(chip: FilterChip): void {
    if (chip.value !== undefined) this.toggle(chip.keys[0], chip.value);
    else this.navigate(Object.fromEntries(chip.keys.map((k) => [k, null])));
  }

  clearFilters(): void {
    const p = this.params();
    this.router.navigate([], { relativeTo: this.route, queryParams: { q: p['q'] ?? null, sort: p['sort'] ?? null } });
  }

  private navigate(patch: Record<string, string | null>, opts: { resetPage?: boolean; keepFilters?: boolean } = {}): void {
    const { resetPage = true, keepFilters = true } = opts;
    const base = keepFilters ? this.params() : { sort: this.params()['sort'] };
    const next: Record<string, string | null> = { ...base, ...patch };
    if (resetPage && !('page' in patch)) next['page'] = null;
    const queryParams = Object.fromEntries(Object.entries(next).filter(([, v]) => v !== null && v !== undefined && v !== ''));
    this.router.navigate([], { relativeTo: this.route, queryParams });
  }
}
