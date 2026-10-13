/** Typed mirror of the TechStore API contract (see /openapi/v1.json). */

export interface Highlights {
  name?: string | null;
  description?: string[] | null;
}

export interface ProductCard {
  id: number;
  sku: string;
  name: string;
  slug: string;
  brand: string;
  category: string;
  price: number;
  discountPrice?: number | null;
  rating: number;
  reviewCount: number;
  inStock: boolean;
  imageUrl?: string | null;
  specs: string[];
  highlights?: Highlights | null;
}

export interface FacetBucket {
  value: string;
  label: string;
  count: number;
  selected: boolean;
}

export interface Facet {
  key: string;
  label: string;
  type: 'terms' | 'range';
  buckets: FacetBucket[];
  min?: number | null;
  max?: number | null;
}

export interface SearchResult {
  items: ProductCard[];
  total: number;
  totalIsApproximate: boolean;
  page: number;
  pageSize: number;
  nextCursor?: string | null;
  facets: Facet[];
  didYouMean?: string | null;
  tookMs: number;
}

export interface SuggestedProduct {
  text: string;
  slug: string;
}

export interface SuggestedTerm {
  value: string;
  label: string;
  count: number;
}

export interface InstantResult {
  id: number;
  name: string;
  slug: string;
  price: number;
  discountPrice?: number | null;
  imageUrl?: string | null;
}

export interface AutocompleteResult {
  products: SuggestedProduct[];
  brands: SuggestedTerm[];
  categories: SuggestedTerm[];
  instant: InstantResult[];
}

export interface CategoryNode {
  id: number;
  name: string;
  slug: string;
  path: string;
  productCount: number;
  children: CategoryNode[];
}

export interface HomePage {
  featuredCategories: CategoryNode[];
  bestSellers: ProductCard[];
  newArrivals: ProductCard[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/** Query-string parameters accepted by /api/products/search (all optional). */
export type SearchParams = Record<string, string>;

export const SORT_OPTIONS = [
  { value: 'relevance', label: 'Best match' },
  { value: 'bestselling', label: 'Best selling' },
  { value: 'price_asc', label: 'Price: low to high' },
  { value: 'price_desc', label: 'Price: high to low' },
  { value: 'rating', label: 'Top rated' },
  { value: 'newest', label: 'Newest' },
] as const;
