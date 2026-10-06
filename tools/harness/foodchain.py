"""Summarise ZzFoodChain's ZZF lines per arm (D521, `specs/food-chain.md §8`).

    python tools/harness/foodchain.py <output of the dotnet test run>

Per arm: alive / peak / starved; per seat-year held (median, p10-p90) for forage, wheat and logs split;
the year cumulative reaped wheat first reaches 1k / 2k / 5k / 10k (median, p10-p90; '-' = never by Year 20);
wheat held in stores at Years 5 and 20; and the lockstep share.
"""
import re
import statistics
import sys
from collections import defaultdict


def pct(xs, p):
    xs = sorted(xs)
    if not xs:
        return None
    k = (len(xs) - 1) * p / 100
    lo, hi = int(k), min(int(k) + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)


def fmt(xs):
    if not xs:
        return "-"
    return f"{statistics.median(xs):.0f} ({pct(xs, 10):.0f}-{pct(xs, 90):.0f})"


arms = defaultdict(list)
for line in open(sys.argv[1], encoding="utf-8", errors="replace"):
    if "ZZF " not in line:
        continue
    line = line[line.index("ZZF "):]
    arm, seed = line.split()[1:3]
    row = {"seed": seed}
    for key in ("alive", "peak", "starved", "stood"):
        row[key] = int(re.search(rf"\b{key} (-?\d+)", line).group(1))
    for name, made, years, rate in re.findall(r"(\w+):(\d+)/([\d.]+)=(\d+)", line):
        row[name] = (int(made), float(years), int(rate))
    row["reaped"] = [int(x) for x in re.search(r"reapedBy \[([^\]]*)\]", line).group(1).split()]
    row["held"] = [int(x) for x in re.search(r"wheatHeld \[([^\]]*)\]", line).group(1).split()]
    row["lock"] = float(re.search(r"lockstep (-?[\d.]+)%", line).group(1))
    arms[arm].append(row)

for arm, rows in arms.items():
    print(f"== {arm}: {len(rows)} runs")
    print(f"   alive {fmt([r['alive'] for r in rows])}  peak {fmt([r['peak'] for r in rows])}  "
          f"starved {sum(r['starved'] for r in rows)} total, dead valleys {sum(r['alive'] == 0 for r in rows)}")
    for name in ("Produce", "Wheat", "LogsSplit"):
        rates = [r[name][2] for r in rows if name in r and r[name][1] >= 5]
        print(f"   {name:9} per seat-year {fmt(rates)}  over {len(rates)} runs with >= 5 seat-years")
    for threshold in (1000, 2000, 5000, 10000):
        years = [next((i + 1 for i, v in enumerate(r["reaped"]) if v >= threshold), None) for r in rows]
        reached = [y for y in years if y is not None]
        print(f"   reaped {threshold:>5} by year {fmt(reached)}  ({len(reached)}/{len(rows)} by Year 20)")
    for y in (5, 20):
        print(f"   wheat held at Year {y:>2}: {fmt([r['held'][y - 1] for r in rows if len(r['held']) >= y])}")
    print(f"   lockstep {fmt([r['lock'] for r in rows if r['lock'] >= 0])} %")
