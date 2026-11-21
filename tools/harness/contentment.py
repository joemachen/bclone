"""Sums ZzContentment's ZZC lines (D799, `morale.md §13` step 1).

python -X utf8 -I tools/harness/contentment.py <log> [<log> ...]

Per mode (valley / save): adult-seasons, each cause as a share of them, the share of adult-seasons by how many causes
held at once, and per k = 1..4 the distinct adults who would be warned (a year at met >= k) and would leave (two years),
as a share of the adults seen. Then one line a run for the saves, so a single village can be read.
"""
import re
import sys

CAUSES = ["noroof", "cabin", "cold", "fireless", "commute", "dry", "death"]


def parse(line):
    words = line.split()
    i = words.index("ZZC")
    w = words[i:]
    out = {"mode": w[1], "id": w[2]}
    j = 3
    while j < len(w) - 1:
        key, val = w[j], w[j + 1]
        if key in ("met", "walk", "chill", "held"):
            out[key] = [int(x) for x in val.split(",")]
        elif re.fullmatch(r"-?\d+", val):
            out[key] = int(val)
        else:
            out[key] = val
        j += 2
    return out


def main(paths):
    runs = []
    for p in paths:
        with open(p, encoding="utf-8", errors="replace") as f:
            for line in f:
                if "ZZC " in line and "refused" not in line:
                    runs.append(parse(line))
                elif "ZZC refused" in line:
                    print(line.strip())
    for mode in ("valley", "save"):
        rs = [r for r in runs if r["mode"] == mode]
        if not rs:
            continue
        a = sum(r["aseasons"] for r in rs)
        adults = sum(r["adults"] for r in rs)
        print(f"== {mode}: {len(rs)} runs, {adults} adults, {a} adult-seasons, peak median "
              f"{sorted(r['peak'] for r in rs)[len(rs) // 2]}")
        print("   causes (share of adult-seasons): " + "  ".join(
            f"{c} {100 * sum(r[c] for r in rs) / max(a, 1):.1f}%" for c in CAUSES))
        met = [sum(r["met"][k] for r in rs) for k in range(7)]
        print("   causes held at once: " + "  ".join(f"{k}{'+' if k == 6 else ''}: {100 * m / max(a, 1):.1f}%"
                                                  for k, m in enumerate(met)))
        if all("walk" in r for r in rs):
            wk = [sum(r["walk"][k] for r in rs) for k in range(5)]
            print("   the walk's note, by tiles: " + "  ".join(f"{lab} {100 * m / max(a, 1):.1f}%" for lab, m in
                  zip(["none", "8-11", "12-15", "16-23", "24+"], wk)))
        if all("chill" in r for r in rs):
            ch = [sum(r["chill"][k] for r in rs) for k in range(5)]
            w = max(sum(ch), 1)
            print("   adult-winters by days at the get-warm line: " + "  ".join(f"{lab} {100 * m / w:.1f}%" for lab, m in
                  zip(["0", "1-2", "3-5", "6-10", "11+"], ch)))
            for l in range(2, 7):
                warn = sum(r[f"rwarn{l}"] for r in rs)
                leave = sum(r[f"rleave{l}"] for r in rs)
                print(f"   rolling year, causes >= {l}: warned {warn} ({100 * warn / max(adults, 1):.1f}%), "
                      f"would leave {leave} ({100 * leave / max(adults, 1):.1f}%), in {sum(1 for r in rs if r[f'rleave{l}'] > 0)} of {len(rs)} runs")
        if all("held" in r for r in rs):
            he = [sum(r["held"][k] for r in rs) for k in range(5)]
            print("   AS RECOMMENDED, causes held at once: " + "  ".join(f"{k}: {100 * m / max(a, 1):.1f}%" for k, m in enumerate(he)))
            for k in range(1, 4):
                warn = sum(r[f"hwarn{k}"] for r in rs)
                leave = sum(r[f"hleave{k}"] for r in rs)
                print(f"   AS RECOMMENDED, held >= {k}: warned {warn} ({100 * warn / max(adults, 1):.1f}%), would leave {leave} "
                      f"({100 * leave / max(adults, 1):.1f}%), in {sum(1 for r in rs if r[f'hleave{k}'] > 0)} of {len(rs)} runs; "
                      + ", ".join(f"{r['id']} {r[f'hleave{k}']}" for r in rs if r[f'hleave{k}'] > 0 and mode == 'save'))
        for k in range(1, 5):
            warn = sum(r[f"warn{k}"] for r in rs)
            leave = sum(r[f"leave{k}"] for r in rs)
            runs_leaving = sum(1 for r in rs if r[f"leave{k}"] > 0)
            print(f"   line met>={k}: warned {warn} ({100 * warn / max(adults, 1):.1f}% of adults), "
                  f"would leave {leave} ({100 * leave / max(adults, 1):.1f}%), in {runs_leaving} of {len(rs)} runs")
        if mode == "save":
            for r in rs:
                print(f"   {r['id']}: {r['from']} +{r['years']}, alive {r['alive']}, adults {r['adults']}; "
                      + " ".join(f"{c} {100 * r[c] / max(r['aseasons'], 1):.0f}%" for c in CAUSES)
                      + " | leave1..4 " + ",".join(str(r[f'leave{k}']) for k in range(1, 5)))


if __name__ == "__main__":
    main(sys.argv[1:])
