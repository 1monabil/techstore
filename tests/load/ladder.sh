#!/usr/bin/env bash
# Rate ladder: runs each script at increasing RPS and prints one summary line per run.
# Usage: K6=/path/to/k6 tests/load/ladder.sh [label]
set -u
K6=${K6:-k6}
LABEL=${1:-run}
DURATION=${DURATION:-60s}
OUT=tests/load/results
mkdir -p "$OUT"

summarize() { # $1 = summary json, $2 = scenario, $3 = rate
  python - "$1" "$2" "$3" <<'EOF'
import json, sys
path, scen, rate = sys.argv[1:]
m = json.load(open(path))['metrics']
def v(name, key):
    x = m.get(name);
    if not x: return float('nan')
    x = x.get('values', x)
    return x.get(key, float('nan'))
d = f'http_req_duration{{scenario:{scen}}}'
print(f"{scen:13s} target {rate:>4s} rps | achieved {v('iterations','rate'):6.1f} rps | "
      f"p50 {v(d,'med'):6.1f} ms | p95 {v(d,'p(95)'):6.1f} ms | p99 {v(d,'p(99)'):6.1f} ms | "
      f"es_took p95 {v('es_took_ms','p(95)'):6.1f} ms | failed {v('http_req_failed','rate')*100:4.1f}% | dropped {v('dropped_iterations','count') if 'dropped_iterations' in m else 0}")
EOF
}

run() { # $1 script, $2 scenario, $3 rate, extra env...
  local script=$1 scen=$2 rate=$3; shift 3
  local json="$OUT/${LABEL}-${scen}-${rate}rps$( [ $# -gt 0 ] && echo -nocache ).json"
  "$K6" run -q --no-color -e RATE="$rate" -e DURATION="$DURATION" "$@" --summary-export "$json" "tests/load/$script" >/dev/null 2>&1
  summarize "$json" "$scen" "$rate"
}

# warm-up (not reported)
"$K6" run -q --no-color -e RATE=20 -e DURATION=20s tests/load/search.js >/dev/null 2>&1
"$K6" run -q --no-color -e RATE=20 -e DURATION=15s tests/load/facets.js >/dev/null 2>&1

for r in ${SEARCH_RATES:-15 25 40}; do run search.js search "$r"; done
for r in ${FACET_RATES:-15 25 40}; do run facets.js facets "$r"; done
for r in ${AC_RATES:-50 100 200}; do run autocomplete.js autocomplete "$r"; done
for r in ${AC_RATES:-50 100 200}; do run autocomplete.js autocomplete "$r" -e NOCACHE=1; done
