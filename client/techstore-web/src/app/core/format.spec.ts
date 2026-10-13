import { formatTotal, matchSegments } from './format';

describe('matchSegments', () => {
  it('marks every query word, case-insensitively', () => {
    const segs = matchSegments('ASUS ROG Strix G16', 'rog strix');
    expect(segs.filter((s) => s.match).map((s) => s.text)).toEqual(['ROG', 'Strix']);
    expect(segs.map((s) => s.text).join('')).toBe('ASUS ROG Strix G16');
  });

  it('treats regex characters in the query literally', () => {
    const segs = matchSegments('Samsung 990 PRO (2TB)', '(2tb');
    expect(segs.some((s) => s.match && s.text === '(2TB')).toBeTrue();
  });

  it('returns the text untouched for an empty query', () => {
    expect(matchSegments('Keyboard', '  ')).toEqual([{ text: 'Keyboard', match: false }]);
  });
});

describe('formatTotal', () => {
  it('shows exact counts', () => expect(formatTotal(1243, false)).toBe('1,243 results'));
  it('shows a floor when ES stopped counting', () => expect(formatTotal(10000, true)).toBe('10,000+ results'));
  it('is singular for one result', () => expect(formatTotal(1, false)).toBe('1 result'));
});
