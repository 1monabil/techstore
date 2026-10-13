import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AutocompleteResult } from '../../core/api.models';
import { SearchBox } from './search-box';

const suggestions: AutocompleteResult = {
  products: [{ text: 'ASUS ROG Strix G16', slug: 'asus-rog-strix-g16' }],
  brands: [{ value: 'asus', label: 'ASUS', count: 40 }],
  categories: [],
  instant: [{ id: 1, name: 'ASUS ROG Strix G16 RTX 4070', slug: 'x', price: 1499 }],
};

describe('SearchBox', () => {
  let fixture: ComponentFixture<SearchBox>;
  let http: HttpTestingController;
  let input: HTMLInputElement;
  let navigate: jasmine.Spy;

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 0, 1)); // debounceTime reads Date.now()
    TestBed.configureTestingModule({
      imports: [SearchBox],
      providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SearchBox);
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input');
    input.focus();
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    http.verify();
  });

  const type = (text: string) => {
    input.value = text;
    input.dispatchEvent(new Event('input'));
  };
  const key = (k: string) => input.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true }));

  it('debounces keystrokes and only queries the latest text', () => {
    type('ro');
    jasmine.clock().tick(100);
    type('rog');
    jasmine.clock().tick(100);
    http.expectNone((r) => r.url.includes('autocomplete'));

    jasmine.clock().tick(60);
    const req = http.expectOne((r) => r.url === '/api/products/autocomplete');
    expect(req.request.params.get('q')).toBe('rog');
    req.flush(suggestions);
  });

  it('cancels the in-flight request when the text changes', () => {
    type('rog');
    jasmine.clock().tick(160);
    const first = http.expectOne((r) => r.params.get('q') === 'rog');

    type('rog str');
    jasmine.clock().tick(160);
    const second = http.expectOne((r) => r.params.get('q') === 'rog str');

    expect(first.cancelled).toBeTrue();
    second.flush(suggestions);
  });

  it('does not query below two characters', () => {
    type('r');
    jasmine.clock().tick(200);
    http.expectNone((r) => r.url.includes('autocomplete'));
  });

  it('supports arrow keys and Enter, and tapes the matched text', async () => {
    type('rog');
    jasmine.clock().tick(160);
    http.expectOne((r) => r.url.includes('autocomplete')).flush(suggestions);
    await fixture.whenStable();

    const marks = [...fixture.nativeElement.querySelectorAll('[role=option] mark')].map((m: Element) => m.textContent);
    expect(marks).toContain('ROG');

    key('ArrowDown');
    key('ArrowDown');
    await fixture.whenStable();
    expect(input.getAttribute('aria-activedescendant')).toMatch(/-opt-1$/);

    key('Enter');
    expect(navigate).toHaveBeenCalledWith(['/products'], { queryParams: { brands: 'asus' } });
  });

  it('Enter without a highlighted option searches the typed text; Escape closes the list', async () => {
    type('rtx 4070');
    jasmine.clock().tick(160);
    http.expectOne((r) => r.url.includes('autocomplete')).flush(suggestions);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role=listbox]')).not.toBeNull();

    key('Escape');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role=listbox]')).toBeNull();

    key('Enter');
    expect(navigate).toHaveBeenCalledWith(['/products'], { queryParams: { q: 'rtx 4070' } });
  });
});
