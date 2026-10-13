import { Pipe, PipeTransform } from '@angular/core';

/**
 * Highlights arrive HTML-encoded from the API with <mark> as the only markup. This pipe is the
 * second line of defence: every "<" or ">" that is not part of an exact <mark>/</mark> tag is
 * escaped, so nothing but <mark> can ever reach [innerHTML] (which Angular sanitises again).
 */
@Pipe({ name: 'safeMark' })
export class SafeMarkPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    return value
      .split(/(<mark>|<\/mark>)/g)
      .map((part) => (part === '<mark>' || part === '</mark>' ? part : part.replace(/</g, '&lt;').replace(/>/g, '&gt;')))
      .join('');
  }
}
