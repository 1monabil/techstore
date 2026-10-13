import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Silkscreen reference designators, the way the part would be labelled on a board. */
const DESIGNATORS: Record<string, string> = {
  laptops: 'SYS1', tablets: 'TAB1', smartphones: 'MOB1', 'graphics-cards': 'VGA1', processors: 'U1',
  motherboards: 'MB1', ram: 'DIMM1', ssds: 'M.2', hdds: 'SATA0', 'power-supplies': 'PSU1', cases: 'CHS1',
  monitors: 'LCD1', keyboards: 'KB1', mice: 'MS1', headsets: 'AUD1', routers: 'RF1', switches: 'ETH1',
};

/** Line drawings on a 64×48 grid, stroked with currentColor so they follow the theme. */
const PATHS: Record<string, string> = {
  laptops: 'M14 10h36v22H14zM8 36h48l-3 4H11zM28 38h8',
  tablets: 'M22 6h20a2 2 0 0 1 2 2v32a2 2 0 0 1-2 2H22a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zM31 38h2',
  smartphones: 'M25 5h14a3 3 0 0 1 3 3v32a3 3 0 0 1-3 3H25a3 3 0 0 1-3-3V8a3 3 0 0 1 3-3zM29 8h6',
  'graphics-cards': 'M6 12h50v22H6zM56 10v26M21 23m-7 0a7 7 0 1 0 14 0a7 7 0 1 0-14 0M41 23m-7 0a7 7 0 1 0 14 0a7 7 0 1 0-14 0M12 34v4h28v-4',
  processors: 'M20 12h24v24H20zM25 17h14v14H25zM24 8v4M30 8v4M36 8v4M42 8v4M24 36v4M30 36v4M36 36v4M42 36v4M16 16h4M16 22h4M16 28h4M44 16h4M44 22h4M44 28h4',
  motherboards: 'M10 6h44v36H10zM15 11h12v12H15zM33 10v16M37 10v16M41 10v16M45 10v16M15 30h34M15 35h34M18 26v2',
  ram: 'M4 16h56v14H4zM9 19h7v8H9zM19 19h7v8h-7zM29 19h7v8h-7zM39 19h7v8h-7zM49 19h7v8h-7zM4 30v4h26v-4M34 30v4h26v-4',
  ssds: 'M6 18h48v12H6zM54 20v8M58 24m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0M14 21h12v6H14zM30 21h8v6h-8zM6 21v6',
  hdds: 'M12 6h40v36H12zM32 22m-11 0a11 11 0 1 0 22 0a11 11 0 1 0-22 0M32 22m-2 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0M46 36l-10-10M16 38h6',
  'power-supplies': 'M8 8h48v32H8zM28 24m-12 0a12 12 0 1 0 24 0a12 12 0 1 0-24 0M28 12v24M16 24h24M19.5 15.5l17 17M36.5 15.5l-17 17M46 14h6M46 34h6',
  cases: 'M18 4h28v40H18zM24 9h16M24 13h16M24 17h16M32 34m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0M22 44v2M42 44v2',
  monitors: 'M6 6h52v30H6zM28 36l-2 6h12l-2-6M22 42h20',
  keyboards: 'M4 14h56v22H4zM8 18h4M14 18h4M20 18h4M26 18h4M32 18h4M38 18h4M44 18h4M50 18h6M8 24h6M16 24h4M22 24h4M28 24h4M34 24h4M40 24h4M46 24h10M8 30h8M18 30h28M48 30h8',
  mice: 'M32 6c9 0 14 7 14 16v8c0 8-6 14-14 14s-14-6-14-14v-8c0-9 5-16 14-16zM32 6v14M32 11v5M18 20h28',
  headsets: 'M14 30v-6a18 18 0 0 1 36 0v6M10 28h8v14h-8zM46 28h8v14h-8z',
  routers: 'M8 28h48v12H8zM16 28L12 8M48 28l4-20M32 28V10M14 34h2M20 34h2M26 34h2M46 34h6',
  switches: 'M4 18h56v14H4zM9 22h5v6H9zM16 22h5v6h-5zM23 22h5v6h-5zM30 22h5v6h-5zM37 22h5v6h-5zM44 22h5v6h-5zM53 23h3',
};

const FALLBACK = 'M14 10h36v28H14zM20 16h24v16H20z';

@Component({
  selector: 'app-part-glyph',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative block bg-tile text-muted overflow-hidden' },
  template: `
    <svg viewBox="0 0 64 48" class="absolute inset-[18%] h-[64%] w-[64%]" fill="none" stroke="currentColor"
         stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <path [attr.d]="path()" />
    </svg>
    <span class="absolute left-2 top-1.5 font-mono text-[10px] tracking-wider opacity-70" aria-hidden="true">{{ designator() }}</span>
  `,
})
export class PartGlyph {
  /** Category slug, or an image URL like /assets/parts/{slug}.svg from the API. */
  readonly part = input<string | null | undefined>('');

  protected readonly slug = computed(() => {
    const p = this.part() ?? '';
    const m = /\/parts\/([a-z0-9-]+)\.svg$/.exec(p);
    return m ? m[1] : p;
  });
  protected readonly path = computed(() => PATHS[this.slug()] ?? FALLBACK);
  protected readonly designator = computed(() => DESIGNATORS[this.slug()] ?? 'J1');
}
