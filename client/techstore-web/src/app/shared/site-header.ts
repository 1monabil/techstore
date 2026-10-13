import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SearchBox } from './search-box/search-box';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block border-b border-line bg-page/90 backdrop-blur supports-[backdrop-filter]:bg-page/75 sticky top-0 z-30' },
  template: `
    <div class="mx-auto flex h-16 max-w-[1320px] items-center gap-4 px-4 md:gap-8 md:px-6">
      <a routerLink="/" class="flex shrink-0 items-center gap-2.5" aria-label="TechStore home">
        <img src="favicon.svg" alt="" class="size-7" />
        <span class="type-title text-[19px] tracking-tight">TechStore</span>
      </a>
      @if (showSearch()) {
        <app-search-box class="min-w-0 flex-1 md:max-w-[620px]" [value]="query()" [category]="category()" />
      } @else {
        <span class="flex-1"></span>
      }
      <nav class="hidden shrink-0 items-center gap-6 text-[14px] md:flex" aria-label="Main">
        <a routerLink="/products" class="text-muted transition-colors hover:text-ink">All products</a>
        <a routerLink="/products" [queryParams]="{ category: 'components' }" class="text-muted transition-colors hover:text-ink">Components</a>
        <a routerLink="/products" [queryParams]="{ category: 'peripherals' }" class="text-muted transition-colors hover:text-ink">Peripherals</a>
      </nav>
    </div>
  `,
})
export class SiteHeader {
  readonly showSearch = input(true);
  readonly query = input('');
  readonly category = input<string | null>(null);
}
