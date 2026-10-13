const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
const usdWhole = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
const count = new Intl.NumberFormat('en-US');

export const formatPrice = (n: number): string => usd.format(n);
export const formatPriceWhole = (n: number): string => usdWhole.format(n);
export const formatCount = (n: number): string => count.format(n);

/** "1,243 results" or "10,000+ results" when ES stopped counting. */
export function formatTotal(total: number, approximate: boolean): string {
  const n = `${formatCount(total)}${approximate ? '+' : ''}`;
  return `${n} ${total === 1 && !approximate ? 'result' : 'results'}`;
}

export interface TextSegment {
  text: string;
  match: boolean;
}

/**
 * Splits text into matched / unmatched segments for every query word (case-insensitive),
 * so templates can tape the matches without innerHTML.
 */
export function matchSegments(text: string, query: string): TextSegment[] {
  const words = query
    .toLowerCase()
    .split(/\s+/)
    .filter((w) => w.length > 0)
    .map((w) => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  if (words.length === 0) return [{ text, match: false }];

  const re = new RegExp(`(${words.sort((a, b) => b.length - a.length).join('|')})`, 'gi');
  const segments: TextSegment[] = [];
  let last = 0;
  for (const m of text.matchAll(re)) {
    const start = m.index ?? 0;
    if (start > last) segments.push({ text: text.slice(last, start), match: false });
    segments.push({ text: m[0], match: true });
    last = start + m[0].length;
  }
  if (last < text.length) segments.push({ text: text.slice(last), match: false });
  return segments;
}
