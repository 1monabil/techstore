import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ProblemDetails } from './api.models';
import { ErrorState } from './error-state';

/**
 * Turns failed API calls into one readable banner. Validation errors (400) are left to the
 * caller; autocomplete failures stay silent because the user is still typing.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const errors = inject(ErrorState);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && !req.url.includes('/autocomplete') && err.status !== 400) {
        const problem = (err.error ?? {}) as ProblemDetails;
        errors.report(
          err.status === 0
            ? { title: "Can't reach TechStore", detail: 'Check your connection, then try again.' }
            : err.status === 429
              ? { title: 'Too many searches at once', detail: 'Wait a second, then try again.' }
              : { title: problem.title ?? 'Something went wrong', detail: problem.detail ?? 'Try again in a moment.' },
        );
      }
      return throwError(() => err);
    }),
  );
};
