# Sums ZzCemetery.cs's logs (D891, specs/cemetery.md 3). Usage: python -X utf8 -I tools/harness/cemetery.py <logs>
import re
import statistics as st
import sys
from collections import defaultdict


def fields(parts):
    out = {}
    for i in range(0, len(parts) - 1, 2):
        out[parts[i]] = parts[i + 1]
    return out


def pct(values, p):
    s = sorted(values)
    return s[min(len(s) - 1, int(len(s) * p / 100))] if s else None


A, P, G, Y, C, seen = {}, defaultdict(dict), [], [], [], set()
for path in sys.argv[1:]:
    for raw in open(path, encoding="utf-8", errors="replace"):
        m = re.search(r"\b(ZZ[APGYC]) (.*)$", raw.strip())
        if not m or (m[1], m[2]) in seen and m[1] not in ("ZZG",):
            continue
        seen.add((m[1], m[2]))
        t = m[2].split()
        if m[1] == "ZZA":
            A[t[1]] = dict(kind=t[0], **fields(t[2:]))
        elif m[1] == "ZZP":
            P[t[0]][t[1]] = fields(t[3:])
        elif m[1] == "ZZG":
            G.append(dict(village=t[0], src=t[1], **fields(t[2:])))
        elif m[1] == "ZZY":
            Y.append(dict(kind=t[0], village=t[1], **fields(t[2:])))
        else:
            C.append(dict(kind=t[0], village=t[1], arm=t[2], **fields(t[3:])))

for kind in ("save", "valley"):
    names = {v for v, a in A.items() if a["kind"] == kind}
    if not names:
        continue
    print(f"== {kind}: {len(names)} villages")
    ys = [y for y in Y if y["village"] in names]
    gs = [g for g in G if g["village"] in names]
    per = defaultdict(list)
    for y in ys:
        per[min(9, int(y["people"]) // 10)].append(int(y["deaths"]))
    print("  deaths a village-year by people (decade of people: years, mean, max, share with none):")
    for k in sorted(per):
        d = per[k]
        print(f"    {k * 10:3}-{k * 10 + 9}: {len(d):5} yrs  mean {st.mean(d):.2f}  max {max(d)}  none {100 * sum(x == 0 for x in d) // len(d)}%")
    first = defaultdict(lambda: 10 ** 9)
    byv = defaultdict(list)
    for g in gs:
        byv[g["village"]].append(int(g["year"]))
        first[g["village"]] = min(first[g["village"]], int(g["year"]))
    fl = [f for f in first.values()]
    if fl:
        print(f"  first death year: n {len(fl)} p10 {pct(fl, 10)} median {pct(fl, 50)} p90 {pct(fl, 90)} min {min(fl)} max {max(fl)}")
    for cap in (20, 40, 60, 80):
        fill, notyet = [], 0
        for v in names:
            d = sorted(byv[v])
            if len(d) >= cap:
                fill.append(d[cap - 1] - d[0])
            else:
                notyet += 1
        print(f"  {cap} graves fill: {len(fl and fill)} villages, years after the first death p10 {pct(fill, 10)} median {pct(fill, 50)} p90 {pct(fill, 90)}; not full {notyet}")
    causes = defaultdict(int)
    for g in gs:
        causes[g["cause"]] += 1
    print("  causes:", dict(causes), " ages median", pct([int(g["age"]) for g in gs], 50))
    print("  lie at home (0 tiles):", sum(g["tohome"] == "0" for g in gs), "within 2:", sum(0 <= int(g["tohome"]) <= 2 for g in gs),
          "further:", sum(int(g["tohome"]) > 2 for g in gs), "no home:", sum(g["tohome"] == "-1" for g in gs))
    for plot in ("near", "edge"):
        t = [int(g[plot]) for g in gs if int(g[plot]) >= 0]
        un = sum(int(g[plot]) < 0 for g in gs)
        if t:
            print(f"  plot {plot} -> the dead, ticks one way: p10 {pct(t, 10)} median {pct(t, 50)} p90 {pct(t, 90)} max {max(t)}; unreachable {un}")
        w = [float(P[v][plot]["mean"]) for v in names if plot in P[v] and float(P[v][plot].get("mean", -1)) >= 0]
        if w:
            print(f"  plot {plot} <- homes, mean ticks one way: median {pct(w, 50)} max {max(w)} (n {len(w)})")
    print("  living adult kin a death:", {k: sum(int(g["kin"]) == k for g in gs) for k in range(0, 6)})
    seasons = defaultdict(int)
    for g in gs:
        seasons[(g["village"], g["year"], g["season"])] += 1
    for k in (2, 3, 5, 10):
        print(f"  seasons with >= {k} deaths: {sum(x >= k for x in seasons.values())} (worst {max(seasons.values()) if seasons else 0})")
    print("  arms: unsettled adult-seasons %, thinking, would go, couples unsettled %")
    arms = defaultdict(lambda: defaultdict(int))
    for c in C:
        if c["village"] in names:
            for f in ("adultseasons", "unsettled", "thinking", "gone", "coupleseasons", "coupleunsettled"):
                arms[c["arm"]][f] += int(c[f])
    for arm, a in arms.items():
        print(f"    {arm:9} {100 * a['unsettled'] / max(1, a['adultseasons']):5.2f}%  thinking {a['thinking']:4}  gone {a['gone']:4}  "
              f"couples {100 * a['coupleunsettled'] / max(1, a['coupleseasons']):5.2f}%  (adult-seasons {a['adultseasons']})")
    really = {c["village"]: int(c["reallyleft"]) for c in C if c["village"] in names}
    print("  really left by the run's end (history included):", sum(really.values()))
    if kind == "save":
        for v in sorted(names):
            d = sorted(byv[v])
            last = [y for y in ys if y["village"] == v]
            print(f"    {v:28} year {A[v]['year']:>3} people {A[v]['people']:>3} dead at load {A[v]['dead']:>2} -> first {first[v] if d else '-'} "
                  f"dead after {len(d)} people then {last[-1]['people'] if last else '-'}; 20th grave year {d[19] if len(d) > 19 else '-'}")
