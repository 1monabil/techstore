import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AutocompleteResult, HomePage, SearchParams, SearchResult } from './api.models';

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  search(params: SearchParams): Observable<SearchResult> {
    return this.http.get<SearchResult>('/api/products/search', { params: new HttpParams({ fromObject: params }) });
  }

  autocomplete(q: string, category?: string | null): Observable<AutocompleteResult> {
    let params = new HttpParams().set('q', q);
    if (category) params = params.set('category', category);
    return this.http.get<AutocompleteResult>('/api/products/autocomplete', { params });
  }

  home(): Observable<HomePage> {
    return this.http.get<HomePage>('/api/home');
  }
}
