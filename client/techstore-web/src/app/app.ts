import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ErrorState } from './core/error-state';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (errors.current(); as error) {
      <div role="alert" class="fixed inset-x-4 bottom-4 z-50 mx-auto flex max-w-xl items-start gap-3 rounded-[var(--radius-card)] border border-danger/40 bg-card px-4 py-3 shadow-lg">
        <span class="mt-1.5 size-2 shrink-0 rounded-full bg-danger" aria-hidden="true"></span>
        <div class="flex-1 text-[14px]">
          <p class="font-semibold">{{ error.title }}</p>
          @if (error.detail) { <p class="text-muted">{{ error.detail }}</p> }
        </div>
        <button type="button" class="text-[13px] text-muted underline-offset-2 hover:underline" (click)="errors.dismiss()">Dismiss</button>
      </div>
    }
    <router-outlet />
    <footer class="mt-24 border-t border-line">
      <div class="mx-auto flex max-w-[1320px] flex-wrap items-center justify-between gap-3 px-4 py-8 text-[13px] text-muted md:px-6">
        <p>TechStore — laptops, components and peripherals.</p>
        <p class="font-mono text-[11.5px]">Prices in USD · Stock updates live</p>
      </div>
    </footer>
  `,
})
export class App {
  protected readonly errors = inject(ErrorState);
}
