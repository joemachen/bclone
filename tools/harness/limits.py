"""Summarise ZzLimits (D611): per arm and opening — runs, alive, dead valleys, peak, starved, froze, born, and fish and
meat produced, with each seed's change in alive between the arms so the spread is said, not hidden (D603's trap).

    python tools/harness/limits.py out-old-played.txt out-new-played.txt ...
"""
import re
import statistics
import sys
from collections import defaultdict

rows = defaultdict(dict)
for path in sys.argv[1:]:
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = re.search(r"ZZL (\w+) (\w+) (\d+) (.*)", line)
            if not m:
                continue
            arm, opening, seed, rest = m.group(1), m.group(2), int(m.group(3)), m.group(4)
            parts = rest.split()
            rows[(arm, opening)][seed] = {parts[i]: int(parts[i + 1]) for i in range(0, len(parts) - 1, 2)}

for (arm, opening), runs in sorted(rows.items(), key=lambda kv: (kv[0][1], kv[0][0])):
    v = list(runs.values())
    total = lambda k: sum(r[k] for r in v)
    print(f"{opening:12} {arm:4} runs {len(v):3}  alive {total('alive'):5}  dead valleys {sum(1 for r in v if r['alive'] == 0):3}  "
          f"peak {total('peak'):5}  starved {total('starved'):5}  froze {total('cold'):4}  born {total('born'):5}  "
          f"fish {total('fish'):8}  meat {total('meat'):8}  forage {total('forage'):8}  fish+meat held/yr {total('fishmeatheld'):7}")

for opening in sorted({o for _, o in rows}):
    old, new = rows.get(("old", opening), {}), rows.get(("new", opening), {})
    both = sorted(set(old) & set(new))
    if not both:
        continue
    d = [new[s]["alive"] - old[s]["alive"] for s in both]
    worse = [s for s in both if new[s]["alive"] == 0 and old[s]["alive"] > 0]
    better = [s for s in both if old[s]["alive"] == 0 and new[s]["alive"] > 0]
    sd = statistics.pstdev(d) if len(d) > 1 else 0
    t = statistics.mean(d) / (sd / len(d) ** 0.5) if sd > 0 else 0
    print(f"{opening}: alive new - old per seed: mean {statistics.mean(d):+.2f}, sd {sd:.2f}, t {t:+.2f}; "
          f"died only under new {worse}; lived only under new {better}")
