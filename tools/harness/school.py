"""Summarise ZzSchool's ZZS lines (D756, `school-and-education.md §12` step 3): per arm, the mean of each figure over
its seeds, and dead valleys. Usage: python -X utf8 -I tools/harness/school.py <file of ZZS lines>"""
import sys
from collections import defaultdict

KEYS = ["alive", "peak", "starved", "cold", "born", "pupilyears", "graduates", "strongyears", "dangerdays", "shelterdays",
        "workyears", "vigourworkyears"]

arms = defaultdict(list)
order = []
with open(sys.argv[1], encoding="utf-8", errors="replace") as f:
    for line in f:
        if "ZZS " not in line:
            continue
        parts = line.split("ZZS ", 1)[1].split()
        arm = parts[0]
        values = dict(zip(parts[2::2], parts[3::2]))
        if arm not in arms:
            order.append(arm)
        arms[arm].append({k: int(values[k]) for k in KEYS if k in values})

print(f"{'arm':6} {'runs':>4} {'dead':>4} " + " ".join(f"{k[:9]:>9}" for k in KEYS))
for arm in order:
    runs = arms[arm]
    dead = sum(1 for r in runs if r["alive"] == 0)
    means = [sum(r[k] for r in runs) / len(runs) for k in KEYS]
    print(f"{arm:6} {len(runs):>4} {dead:>4} " + " ".join(f"{m:9.1f}" for m in means))
