import { Injectable, signal } from '@angular/core';

export interface AppError {
  title: string;
  detail?: string;
}

/** The single app-wide error banner. The interceptor reports; the shell renders. */
@Injectable({ providedIn: 'root' })
export class ErrorState {
  private readonly _current = signal<AppError | null>(null);
  readonly current = this._current.asReadonly();

  report(error: AppError): void {
    this._current.set(error);
  }

  dismiss(): void {
    this._current.set(null);
  }
}
