"""D564: summarise a ZzNomads run (specs/nomads.md section 8).

    python tools/harness/nomads.py harness-out/nomads-100.txt

Per arm: the ZZB survival line (as dead.py), what a knock meets at Years 8/10/12 (ZZK), the settlers' fate (ZZS),
what stands N years after a death (ZZD) and the re-founders' survival (ZZR).
"""
import re, sys
from collections import defaultdict

def pct(xs, p):
    if not xs: return 0
    xs = sorted(xs); i = min(len(xs) - 1, max(0, int(round(p * (len(xs) - 1)))))
    return xs[i]

def summarise(path):
    B = defaultdict(list); K = defaultdict(list); S = defaultdict(list); D = defaultdict(list); R = defaultdict(list)
    for line in open(path, encoding='utf-8', errors='replace'):
        line = line.strip()
        m = re.match(r'ZZB (\w+) (\d+) alive (\d+) peak (\d+) starved (\d+) cold (\d+) dead-year (-?\d+)', line)
        if m: B[m.group(1)].append(tuple(int(x) for x in m.groups()[1:])); continue
        m = re.match(r'ZZK (\w+) (\d+) year (\d+) alive (\d+) hh (\d+) homed (\d+) empty-houses (\d+) food (\d+) per-head (\d+) firewood (\d+) logs (\d+) res-tiles (\d+) nowhere-to-build (\w+) band (\d+)', line)
        if m:
            g = m.groups(); K[(g[0], int(g[2]))].append(dict(alive=int(g[3]), hh=int(g[4]), homed=int(g[5]), empty=int(g[6]), food=int(g[7]), perhead=int(g[8]), firewood=int(g[9]), logs=int(g[10]), res=int(g[11]), nowhere=g[12] == 'True', band=int(g[13]))); continue
        m = re.match(r'ZZS (\w+) (\d+) settled (\d+) families (\d+) housed-years (\S+) died (\d+) starved (\d+) cold (\d+) deaths (\S+) alive-now (\d+)', line)
        if m:
            g = m.groups(); S[g[0]].append(dict(seed=int(g[1]), settled=int(g[2]), families=int(g[3]), housed=g[4].split(','), died=int(g[5]), starved=int(g[6]), cold=int(g[7]), deaths=g[8], alive=int(g[9]))); continue
        m = re.match(r'ZZD (\w+) (\d+) died-year (\d+) on (\d+) food (\d+) firewood (\d+) standing (\d+) sound (\d+) worn (\d+) failing (\d+) ruin (\d+) \| (.*)', line)
        if m:
            g = m.groups(); D[(g[0], int(g[3]))].append(dict(seed=int(g[1]), died=int(g[2]), food=int(g[4]), firewood=int(g[5]), standing=int(g[6]), sound=int(g[7]), worn=int(g[8]), failing=int(g[9]), ruin=int(g[10]), kinds=g[11])); continue
        m = re.match(r'ZZR (\w+) (\d+) on (\d+) alive (\d+)', line)
        if m: R[(m.group(1), int(m.group(3)))].append(int(m.group(4))); continue
        m = re.match(r'ZZR (\w+) (\d+) refound-year (\d+) cart (\w+) houses-standing (\d+) standing (\d+) sound (\d+) worn (\d+) failing (\d+) ruin (\d+)', line)
        if m: R[(m.group(1), 'at')].append(tuple(int(x) if x.isdigit() else x for x in m.groups()[1:]))

    for arm, rows in sorted(B.items()):
        n = len(rows); alive = sum(r[1] for r in rows); dead = sum(1 for r in rows if r[1] == 0)
        print(f"ZZB {arm:8} n={n:3} alive {alive:4} dead-valleys {dead:3} peak {sum(r[2] for r in rows):4} starved {sum(r[3] for r in rows):4} cold {sum(r[4] for r in rows):3}"
              f"  death-years p10/med/p90 {pct([r[5] for r in rows if r[5] > 0], .1)}/{pct([r[5] for r in rows if r[5] > 0], .5)}/{pct([r[5] for r in rows if r[5] > 0], .9)}")
    for (arm, year), rows in sorted(K.items()):
        if arm != 'knock': continue
        a = [r['alive'] for r in rows]; f = [r['perhead'] for r in rows]; bands = [r['band'] for r in rows]
        q = [max(2, min(8, r['alive'] // 4)) for r in rows]
        print(f"ZZK year {year:2} n={len(rows):3} alive p10/med/p90 {pct(a,.1)}/{pct(a,.5)}/{pct(a,.9)} households med {pct([r['hh'] for r in rows],.5)} "
              f"all-housed {sum(1 for r in rows if r['homed'] == r['hh'])} empty-houses {sum(r['empty'] for r in rows)} food/head p10/med/p90 {pct(f,.1)}/{pct(f,.5)}/{pct(f,.9)} "
              f"nowhere-to-build {sum(1 for r in rows if r['nowhere'])} band(fifth) med {pct(bands,.5)} max {max(bands)} band(quarter) med {pct(q,.5)} max {max(q)}")
    for arm, rows in sorted(S.items()):
        settled = sum(r['settled'] for r in rows); died = sum(r['died'] for r in rows)
        housed10 = sum(1 for r in rows for h in r['housed'] if h == '10'); never = sum(1 for r in rows for h in r['housed'] if h == 'never')
        fams = sum(r['families'] for r in rows)
        firstwinter = sum(1 for r in rows for d in r['deaths'].split(',') if d.endswith('@10') or d.endswith('@11'))
        print(f"ZZS {arm:8} valleys {len(rows)} settled {settled} families {fams} housed-in-year-10 {housed10} housed-later {fams - housed10 - never} never-housed {never} "
              f"died {died} (starved {sum(r['starved'] for r in rows)} cold {sum(r['cold'] for r in rows)}) in-years-10-11 {firstwinter} alive-at-50 {sum(r['alive'] for r in rows)}")
    for (arm, on), rows in sorted(D.items()):
        st = sum(r['standing'] for r in rows)
        print(f"ZZD {arm:8} +{on} years n={len(rows):2} standing {st} sound {sum(r['sound'] for r in rows)} worn {sum(r['worn'] for r in rows)} failing {sum(r['failing'] for r in rows)} ruin {sum(r['ruin'] for r in rows)} "
              f"food med {pct([r['food'] for r in rows],.5)} firewood med {pct([r['firewood'] for r in rows],.5)} died-year med {pct([r['died'] for r in rows],.5)}")
    for (arm, on), rows in sorted(R.items(), key=lambda kv: (kv[0][0], str(kv[0][1]))):
        if on == 'at':
            print(f"ZZR {arm:8} refounded n={len(rows)} year med {pct([r[1] for r in rows],.5)} cart-took med {pct([r[2] for r in rows],.5)} houses med {pct([r[3] for r in rows],.5)} standing med {pct([r[4] for r in rows],.5)} ruin med {pct([r[8] for r in rows],.5)}")
        else:
            print(f"ZZR {arm:8} +{on:2} years n={len(rows)} alive total {sum(rows)} valleys-alive {sum(1 for a in rows if a > 0)} alive p10/med/p90 {pct(rows,.1)}/{pct(rows,.5)}/{pct(rows,.9)}")

for p in sys.argv[1:]:
    print('==', p); summarise(p)
