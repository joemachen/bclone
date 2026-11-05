"""Summarise ZzLodge's ZZL lines (D683): python -X utf8 -I tools/harness/lodge.py out.txt [other.txt ...]

Per (file, arm, limx): runs, alive / starved / frozen summed, the lodges' peak food (median, max), seeds whose lodge sat
above half at all and the years it did (sum), stall ticks (met limit + swollen lodge + nobody seated) summed and the
seeds that stalled, hunter seat-ticks under a met limit, hunters' carrying ticks, meat carried out, hunts.
"""
import re
import statistics
import sys
from collections import defaultdict


def read(path):
    rows = []
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = re.search(r"ZZL (\S+) (\S+) (\d+) (.*)", line)
            if not m:
                continue
            arm, limx, seed, rest = m.groups()
            kv = dict(re.findall(r"(\w+) (-?[\d.]+)", rest))
            kv = {k: float(v) for k, v in kv.items()}
            kv["seed"] = int(seed)
            rows.append((arm, limx, kv))
    return rows


def main(paths):
    for path in paths:
        groups = defaultdict(list)
        for arm, limx, kv in read(path):
            groups[(arm, limx)].append(kv)
        print(f"== {path}")
        for (arm, limx), rows in sorted(groups.items()):
            n = len(rows)
            alive = sum(r["alive"] for r in rows)
            starved = sum(r["starved"] for r in rows)
            frozen = sum(r["frozen"] for r in rows)
            peaks = [r["lodgePeak"] for r in rows]
            swollen = [r for r in rows if r["swollenTicks"] > 0]
            stalled = [r for r in rows if r["stallTicks"] > 0]
            print(f"{arm:6} {limx:4} runs {n:2} alive {alive:5.0f} starved {starved:4.0f} frozen {frozen:3.0f} | "
                  f"lodgePeak med {statistics.median(peaks):6.0f} max {max(peaks):6.0f} | "
                  f"swollen seeds {len(swollen):2} years {sum(r['swollenYears'] for r in rows):7.2f} | "
                  f"stall seeds {len(stalled):2} ticks {sum(r['stallTicks'] for r in rows):8.0f} | "
                  f"seatMet {sum(r['seatTicksMet'] for r in rows):8.0f} carrier {sum(r['carrierTicks'] for r in rows):8.0f} "
                  f"hunt {sum(r['huntTicks'] for r in rows):8.0f} out {sum(r['carriedOut'] for r in rows):9.0f} hunts {sum(r['hunts'] for r in rows):5.0f}")
            for r in sorted(stalled, key=lambda r: -r["stallTicks"])[:6]:
                print(f"      seed {r['seed']:6} stall {r['stallTicks']:6.0f} from Y{r['firstStallYear']:.0f} swollenYears {r['swollenYears']:5.2f} "
                      f"lodgePeak {r['lodgePeak']:5.0f} alive {r['alive']:3.0f} starved {r['starved']:3.0f}")


if __name__ == "__main__":
    main(sys.argv[1:])
