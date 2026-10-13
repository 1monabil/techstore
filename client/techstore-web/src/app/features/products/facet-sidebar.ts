import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { Facet } from '../../core/api.models';
import { formatCount, formatPriceWhole } from '../../core/format';
import { SearchStore } from './search-store';

const VISIBLE = 6;

const toggled = (keys: Set<string>, key: string): Set<string> => {
  const next = new Set(keys);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
};

@Component({
  selector: 'app-facet-sidebar',
  templateUrl: './facet-sidebar.html',
  styleUrl: './facet-sidebar.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetSidebar {
  protected readonly store = inject(SearchStore);
  protected readonly visible = VISIBLE;
  protected readonly collapsed = signal(new Set<string>());
  protected readonly expanded = signal(new Set<string>());

  protected readonly termFacets = computed(() =>
    this.store.facets().filter((f) => f.type === 'terms' && f.key !== 'inStock' && f.key !== 'category' && f.buckets.length > 0),
  );
  protected readonly categoryFacet = computed(() => this.store.facets().find((f) => f.key === 'category'));
  protected readonly priceFacet = computed(() => this.store.facets().find((f) => f.key === 'price'));
  protected readonly ratingFacet = computed(() => this.store.facets().find((f) => f.key === 'minRating'));
  protected readonly stockFacet = computed(() => this.store.facets().find((f) => f.key === 'inStock'));

  // Price slider works on whole dollars between the cheapest and dearest match.
  protected readonly bounds = computed(() => {
    const f = this.priceFacet();
    const lo = Math.floor(f?.min ?? 0);
    const hi = Math.max(lo + 1, Math.ceil(f?.max ?? 1000));
    return { lo, hi };
  });
  protected readonly minDraft = signal<number | null>(null);
  protected readonly maxDraft = signal<number | null>(null);

  constructor() {
    // Re-seed the price inputs whenever the URL or bounds change.
    effect(() => {
      const p = this.store.params();
      const { lo, hi } = this.bounds();
      this.minDraft.set(p['minPrice'] ? +p['minPrice'] : lo);
      this.maxDraft.set(p['maxPrice'] ? +p['maxPrice'] : hi);
    });
  }

  protected toggleGroup(key: string): void {
    this.collapsed.update((keys) => toggled(keys, key));
  }

  protected toggleMore(key: string): void {
    this.expanded.update((keys) => toggled(keys, key));
  }

  protected shown(f: Facet) {
    if (this.expanded().has(f.key) || f.buckets.length <= VISIBLE + 1) return f.buckets;
    // Always keep selected values visible even when they sit past the cut.
    const head = f.buckets.slice(0, VISIBLE);
    return [...head, ...f.buckets.slice(VISIBLE).filter((b) => b.selected)];
  }

  protected applyPrice(): void {
    const { lo, hi } = this.bounds();
    let min = this.minDraft();
    let max = this.maxDraft();
    if (min !== null && max !== null && min > max) [min, max] = [max, min];
    this.store.setPrice(min !== null && min > lo ? Math.round(min) : null, max !== null && max < hi ? Math.round(max) : null);
  }

  /** Bucket keys look like "100-250", "0-100" (no lower bound) or "2000-" (no upper bound). */
  protected pickPriceBucket(value: string, selected: boolean): void {
    if (selected) {
      this.store.setPrice(null, null);
      return;
    }
    const [from, to] = value.split('-');
    this.store.setPrice(from && from !== '0' ? +from : null, to ? +to : null);
  }

  protected onMinSlider(v: string): void {
    this.minDraft.set(Math.min(+v, this.maxDraft() ?? +v));
  }

  protected onMaxSlider(v: string): void {
    this.maxDraft.set(Math.max(+v, this.minDraft() ?? +v));
  }

  protected readonly count = formatCount;
  protected readonly money = formatPriceWhole;

  protected pct(v: number | null): number {
    const { lo, hi } = this.bounds();
    return (((v ?? lo) - lo) / (hi - lo)) * 100;
  }
}
