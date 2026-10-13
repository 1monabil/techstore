import { SafeMarkPipe } from './safe-mark.pipe';

describe('SafeMarkPipe', () => {
  const pipe = new SafeMarkPipe();

  it('keeps <mark> highlights', () => {
    expect(pipe.transform('ASUS <mark>ROG</mark> Strix')).toBe('ASUS <mark>ROG</mark> Strix');
  });

  it('escapes any other markup, even if the API let it through', () => {
    const out = pipe.transform('<mark>Evil</mark> <img src=x onerror=alert(1)> <script>x</script>');
    expect(out).toBe('<mark>Evil</mark> &lt;img src=x onerror=alert(1)&gt; &lt;script&gt;x&lt;/script&gt;');
  });

  it('does not accept look-alike mark tags', () => {
    expect(pipe.transform('<mark onclick="x">a</mark>')).toBe('&lt;mark onclick="x"&gt;a</mark>');
  });

  it('leaves already-encoded text alone', () => {
    expect(pipe.transform('27&quot; &amp; 240Hz')).toBe('27&quot; &amp; 240Hz');
  });

  it('handles empty input', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
