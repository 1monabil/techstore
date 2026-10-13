import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { HomePage as HomeData } from '../../core/api.models';
import { CatalogApi } from '../../core/catalog-api';
import { formatCount } from '../../core/format';
import { PartGlyph } from '../../shared/part-glyph';
import { ProductCardComponent } from '../../shared/product-card';
import { SearchBox } from '../../shared/search-box/search-box';
import { SiteHeader } from '../../shared/site-header';

/** Real queries that show off what search understands: model numbers, specs, jargon. */
const EXAMPLES = ['rtx 4070 laptop', 'ddr5 32gb 6000', '27" 240hz oled', 'wifi 7 router', 'nvme 2tb', 'mechanical keyboard wireless'];

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, SiteHeader, SearchBox, PartGlyph, ProductCardComponent],
  templateUrl: './home.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  private readonly api = inject(CatalogApi);

  protected readonly examples = EXAMPLES;
  protected readonly home = toSignal<HomeData | null>(this.api.home().pipe(catchError(() => of(null))));
  protected readonly loading = computed(() => this.home() === undefined);

  protected readonly totalParts = computed(() => {
    const cats = this.home()?.featuredCategories ?? [];
    return cats.reduce((n, c) => n + c.productCount, 0);
  });

  protected count = formatCount;
}
