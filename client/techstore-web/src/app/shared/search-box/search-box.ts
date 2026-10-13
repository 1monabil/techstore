import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { AutocompleteResult } from '../../core/api.models';
import { CatalogApi } from '../../core/catalog-api';
import { formatPrice, matchSegments } from '../../core/format';
import { PartGlyph } from '../part-glyph';

type OptionKind = 'product' | 'brand' | 'category' | 'instant';

export interface SuggestionOption {
  kind: OptionKind;
  label: string;
  value: string;
  meta?: string;
  image?: string | null;
}

let nextId = 0;

@Component({
  selector: 'app-search-box',
  imports: [PartGlyph, NgTemplateOutlet],
  templateUrl: './search-box.html',
  styleUrl: './search-box.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.hero]': "size() === 'hero'" },
})
export class SearchBox {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly size = input<'hero' | 'compact'>('compact');
  /** Current query from the URL; keeps the box in sync on back/forward. */
  readonly value = input<string>('');
  /** Scope suggestions to a category when one is selected. */
  readonly category = input<string | null>(null);

  protected readonly uid = `sb${nextId++}`;
  protected readonly query = signal('');
  protected readonly results = signal<AutocompleteResult | null>(null);
  protected readonly open = signal(false);
  protected readonly active = signal(-1);
  private readonly inputEl = viewChild.required<ElementRef<HTMLInputElement>>('field');
  private readonly typed$ = new Subject<string>();

  /** Flat, keyboard-ordered list: products, brands, categories, then instant results. */
  protected readonly options = computed<SuggestionOption[]>(() => {
    const r = this.results();
    if (!r) return [];
    return [
      ...r.products.map((p) => ({ kind: 'product' as const, label: p.text, value: p.text })),
      ...r.brands.map((b) => ({ kind: 'brand' as const, label: b.label, value: b.value, meta: `${b.count}` })),
      ...r.categories.map((c) => ({ kind: 'category' as const, label: c.label, value: c.value, meta: `${c.count}` })),
      ...r.instant.map((i) => ({
        kind: 'instant' as const, label: i.name, value: i.name,
        meta: formatPrice(i.discountPrice ?? i.price), image: i.imageUrl,
      })),
    ];
  });

  private readonly indexed = computed(() => this.options().map((o, index) => ({ ...o, index })));

  /** Text suggestions in display order; empty groups are omitted. */
  protected readonly textGroups = computed(() => {
    const byKind = (kind: OptionKind) => this.indexed().filter((o) => o.kind === kind);
    return [
      { key: 'products', label: 'Products', options: byKind('product') },
      { key: 'brands', label: 'Brands', options: byKind('brand') },
      { key: 'categories', label: 'Categories', options: byKind('category') },
    ].filter((g) => g.options.length > 0);
  });

  protected readonly instant = computed(() => this.indexed().filter((o) => o.kind === 'instant'));

  protected readonly activeId = computed(() => (this.open() && this.active() >= 0 ? `${this.uid}-opt-${this.active()}` : null));

  constructor() {
    effect(() => this.query.set(this.value()));

    this.typed$
      .pipe(
        map((q) => q.trim()),
        debounceTime(150),
        distinctUntilChanged(),
        // switchMap cancels the in-flight request for the previous keystroke (the API then
        // cancels its Elasticsearch call through the propagated CancellationToken).
        switchMap((q) => (q.length < 2 ? of(null) : this.api.autocomplete(q, this.category()).pipe(catchError(() => of(null))))),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((r) => {
        this.results.set(r);
        this.active.set(-1);
        this.open.set(this.options().length > 0 && this.isFocused());
      });
  }

  protected segments(text: string) {
    return matchSegments(text, this.query());
  }

  protected onInput(value: string): void {
    this.query.set(value);
    this.typed$.next(value);
    if (value.trim().length < 2) this.open.set(false);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.options().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open() && count) this.open.set(true);
        this.active.set(count ? (this.active() + 1) % count : -1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (!this.open() && count) this.open.set(true);
        this.active.set(count ? (this.active() - 1 + count) % count : -1);
        break;
      case 'Enter': {
        event.preventDefault();
        const option = this.open() ? this.options()[this.active()] : undefined;
        if (option) this.choose(option);
        else this.submit();
        break;
      }
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.close();
        } else if (this.query()) {
          this.query.set('');
          this.typed$.next('');
        }
        break;
      case 'Tab':
        this.close();
        break;
    }
  }

  protected onFocus(): void {
    if (this.options().length && this.query().trim().length >= 2) this.open.set(true);
  }

  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (!next || !this.host.nativeElement.contains(next)) this.close();
  }

  protected submit(): void {
    const q = this.query().trim();
    this.close();
    this.inputEl().nativeElement.blur();
    this.router.navigate(['/products'], { queryParams: q ? { q } : {} });
  }

  protected choose(option: SuggestionOption): void {
    this.close();
    this.inputEl().nativeElement.blur();
    switch (option.kind) {
      case 'brand':
        this.query.set('');
        this.router.navigate(['/products'], { queryParams: { brands: option.value } });
        break;
      case 'category':
        this.query.set('');
        this.router.navigate(['/products'], { queryParams: { category: option.value } });
        break;
      default:
        this.query.set(option.value);
        this.router.navigate(['/products'], { queryParams: { q: option.value } });
    }
  }

  private close(): void {
    this.open.set(false);
    this.active.set(-1);
  }

  private isFocused(): boolean {
    return document.activeElement === this.inputEl().nativeElement;
  }
}
