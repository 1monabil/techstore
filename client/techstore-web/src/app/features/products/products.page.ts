import { A11yModule } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { SORT_OPTIONS } from '../../core/api.models';
import { formatTotal } from '../../core/format';
import { ProductCardComponent } from '../../shared/product-card';
import { SiteHeader } from '../../shared/site-header';
import { FacetSidebar } from './facet-sidebar';
import { MAX_PAGE, SearchStore } from './search-store';

const POPULAR = ['rtx 4070', 'ddr5 32gb', 'nvme 2tb', 'oled monitor', 'wireless mouse'];

@Component({
  selector: 'app-products-page',
  imports: [A11yModule, RouterLink, SiteHeader, FacetSidebar, ProductCardComponent],
  providers: [SearchStore],
  templateUrl: './products.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'drawerOpen.set(false)' },
})
export class ProductsPage {
  protected readonly store = inject(SearchStore);
  private readonly title = inject(Title);

  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly popular = POPULAR;
  protected readonly maxPage = MAX_PAGE;
  protected readonly drawerOpen = signal(false);
  protected readonly skeletons = Array.from({ length: 12 }, (_, i) => i);

  protected readonly heading = computed(() => {
    const q = this.store.q();
    if (q) return q;
    const cat = this.store.facets().find((f) => f.key === 'category')?.buckets.find((b) => b.selected)?.label;
    return cat ?? (this.store.category() ? this.store.chips()[0]?.label : 'All products');
  });

  protected readonly totalLabel = computed(() => {
    const r = this.store.result();
    return r ? formatTotal(r.total, r.totalIsApproximate) : '';
  });

  /** Page links: first, last, current ±1, with gaps marked as null. */
  protected readonly pages = computed<(number | null)[]>(() => {
    const n = this.store.pageCount();
    const cur = this.store.page();
    const set = new Set([1, n, cur - 1, cur, cur + 1].filter((p) => p >= 1 && p <= n));
    const sorted = [...set].sort((a, b) => a - b);
    const out: (number | null)[] = [];
    sorted.forEach((p, i) => {
      if (i > 0 && p - sorted[i - 1] > 1) out.push(null);
      out.push(p);
    });
    return out;
  });

  constructor() {
    effect(() => {
      const q = this.store.q();
      this.title.setTitle(q ? `“${q}” — TechStore` : `${this.heading() ?? 'Products'} — TechStore`);
    });
  }

  protected go(page: number): void {
    this.store.goToPage(page);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
