import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProductCard as Product } from '../core/api.models';
import { formatCount, formatPrice } from '../core/format';
import { PartGlyph } from './part-glyph';
import { SafeMarkPipe } from './safe-mark.pipe';

@Component({
  selector: 'app-product-card',
  imports: [PartGlyph, SafeMarkPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <article class="group flex h-full flex-col overflow-hidden rounded-[var(--radius-card)] border border-line bg-card transition-colors hover:border-trace">
      <app-part-glyph class="aspect-[4/3] w-full" [part]="product().imageUrl" />
      <div class="flex flex-1 flex-col gap-2 p-3.5">
        <p class="type-label !text-[10px]">{{ product().brand }} · {{ product().category }}</p>
        <h3 class="text-[14.5px] font-semibold leading-snug [overflow-wrap:anywhere]">
          @if (product().highlights?.name; as name) {
            <span [innerHTML]="name | safeMark"></span>
          } @else {
            {{ product().name }}
          }
        </h3>
        @if (product().specs.length) {
          <p class="font-mono text-[11.5px] leading-relaxed text-muted">{{ product().specs.join(' · ') }}</p>
        }
        @if (showSnippet() && snippet(); as s) {
          <p class="line-clamp-2 text-[13px] leading-snug text-muted" [innerHTML]="s | safeMark"></p>
        }
        <div class="mt-auto flex items-end justify-between gap-2 pt-2">
          <div>
            @if (product().discountPrice; as sale) {
              <p class="font-mono text-[17px] font-semibold text-ink">{{ price(sale) }}</p>
              <p class="font-mono text-[12px] text-muted line-through">{{ price(product().price) }}</p>
            } @else {
              <p class="font-mono text-[17px] font-semibold text-ink">{{ price(product().price) }}</p>
            }
          </div>
          <div class="text-right">
            <p class="font-mono text-[12px] text-ink" [attr.aria-label]="'Rated ' + product().rating + ' out of 5'">
              ★ {{ product().rating.toFixed(1) }} <span class="text-muted">({{ reviews() }})</span>
            </p>
            @if (product().inStock) {
              <p class="mt-0.5 inline-flex items-center gap-1.5 text-[12px] text-mask"><span class="size-1.5 rounded-full bg-mask"></span>In stock</p>
            } @else {
              <p class="mt-0.5 text-[12px] text-muted">Out of stock</p>
            }
          </div>
        </div>
      </div>
    </article>
  `,
})
export class ProductCardComponent {
  readonly product = input.required<Product>();
  readonly showSnippet = input(true);

  protected readonly snippet = computed(() => this.product().highlights?.description?.[0] ?? null);
  protected readonly reviews = computed(() => formatCount(this.product().reviewCount));
  protected price = formatPrice;
}
