"""Summarise ZzBase arms: alive / peak / starved / cold per group, and the rigs' per-100 lines."""
import re
import sys

for path in sys.argv[1:]:
    seen = {}
    rigs = []
    for line in open(path, encoding='utf-8', errors='replace'):
        m = re.search(r'ZZB (\w+) (\d+) (?:sites \d+ )?alive (\d+) peak (\d+) starved (\d+) cold (\d+) hash (\d+)', line)
        if m:
            seen[(m.group(1), m.group(2))] = tuple(int(x) for x in m.group(3, 4, 5, 6))
        if 'per 100 ticks worked' in line and ('landed' in line or 'hunter' in line) and not line.startswith('['):
            rigs.append(line.strip())
    groups = {}
    for (arm, _), v in seen.items():
        g = groups.setdefault(arm, [0, 0, 0, 0, 0])
        g[0] += 1
        for i in range(4):
            g[i + 1] += v[i]
    total = [sum(g[i] for g in groups.values()) for i in range(5)]
    print(f'== {path.split("/")[-1]}')
    for arm in sorted(groups):
        n, a, p, s, c = groups[arm]
        print(f'  {arm:8} n={n:2}  alive {a:4}  peak {p:4}  starved {s:4}  cold {c:4}')
    print(f'  {"ALL":8} n={total[0]:2}  alive {total[1]:4}  peak {total[2]:4}  starved {total[3]:4}  cold {total[4]:4}')
    for r in sorted(set(rigs)):
        print('  rig:', r[:260])
    import re as _r
    s=[int(m.group(1)) for m in (_r.search(r'ZZB \w+ \d+ sites (\d+)',l) for l in open(path,encoding='utf-8',errors='replace')) if m]
    if s: print('  unfinished sites at year 50 (sum):', sum(s), 'villages with any:', sum(1 for x in s if x))
