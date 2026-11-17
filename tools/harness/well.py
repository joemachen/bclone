"""Summarise ZzWell's ZZW lines (D777, `water-need.md §5` step 1): per arm, the mean of each figure and dead valleys, and
the paired difference in alive from the first arm named (same seeds), with its standard error — so a cost can be told
from the noise of a perturbed run. Usage: python -X utf8 -I tools/harness/well.py <base-arm> <files of ZZW lines>"""
import math
import sys
from collections import defaultdict

KEYS = ["alive", "peak", "starved", "cold", "born", "trips", "adultyears", "wellticks", "restyears", "drydays", "homedays"]

base = sys.argv[1]
arms = defaultdict(dict)
order = []
for path in sys.argv[2:]:
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if "ZZW " not in line:
                continue
            parts = line.split("ZZW ", 1)[1].split()
            arm, seed = parts[0], int(parts[1])
            values = dict(zip(parts[2::2], parts[3::2]))
            if arm not in order:
                order.append(arm)
            arms[arm][seed] = {k: int(values[k]) for k in KEYS if k in values}

print(f"{'arm':6} {'runs':>4} {'dead':>4} " + " ".join(f"{k[:8]:>8}" for k in KEYS) + "   Δalive vs " + base + " (± s.e.)")
for arm in order:
    runs = arms[arm]
    dead = sum(1 for r in runs.values() if r["alive"] == 0)
    means = [sum(r.get(k, 0) for r in runs.values()) / len(runs) for k in KEYS]
    paired = [runs[s]["alive"] - arms[base][s]["alive"] for s in runs if s in arms[base]]
    if paired and arm != base:
        mean = sum(paired) / len(paired)
        sd = math.sqrt(sum((d - mean) ** 2 for d in paired) / max(1, len(paired) - 1))
        changed = sum(1 for d in paired if d != 0)
        tail = f"   {mean:+6.2f} ± {sd / math.sqrt(len(paired)):4.2f}  ({changed} of {len(paired)} valleys differ)"
    else:
        tail = ""
    print(f"{arm:6} {len(runs):>4} {dead:>4} " + " ".join(f"{m:8.1f}" for m in means) + tail)
