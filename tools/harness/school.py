"""Summarise ZzSchool's ZZS lines (D756, `school-and-education.md §12` step 3): per arm, the mean of each figure over
its seeds, and dead valleys. Usage: python -X utf8 -I tools/harness/school.py <file of ZZS lines>; with --states first,
the ZZT time budget (D758)"""
import sys
from collections import defaultdict

KEYS = ["alive", "peak", "starved", "cold", "born", "pupilyears", "graduates", "strongyears", "dangerdays", "shelterdays",
        "workyears", "vigourworkyears", "walkyears"]

if sys.argv[1] == "--states":
    # ZZT lines (D758): the able adults' time budget, summed over every run of each arm, as shares.
    budgets = defaultdict(lambda: defaultdict(int))
    with open(sys.argv[2], encoding="utf-8", errors="replace") as f:
        for line in f:
            if "ZZT " not in line:
                continue
            parts = line.split("ZZT ", 1)[1].split()
            for pair in parts[2:]:
                state, ticks = pair.rsplit(":", 1)
                budgets[parts[0]][state] += int(ticks)
    for arm, budget in budgets.items():
        total = sum(budget.values())
        print(f"{arm}: {total} able-adult ticks")
        for state, ticks in sorted(budget.items(), key=lambda kv: -kv[1]):
            print(f"  {state:28} {100 * ticks / total:5.1f} %")
    sys.exit(0)

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
