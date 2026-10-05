"""B6: when the quarry is learned, per arm (ZzBase's `dug … learned … at100 … at200 … tpy` fields).

    python tools/harness/quarry.py harness-out/row-*.txt

Years are fifty-year runs' first crossings; '-' never. The harness never marks a quarry, so the
crossings of 100 and 200 stone dug by hand do not depend on `quarry_unlock_stone` -- only `learned` does.
"""
import re
import sys

PAT = re.compile(r'ZZB \w+ (\d+) .*?alive (\d+) .*? dug (\d+) learned (-?\d+) at100 (-?\d+) at200 (-?\d+) tpy (\d+)')


def spread(ticks, tpy, n):
    hit = sorted(t / tpy for t in ticks if t >= 0)
    if not hit:
        return f"   0/{n:3}            -"
    q = lambda f: hit[min(len(hit) - 1, int(f * len(hit)))]
    return f"{len(hit):4}/{n:3} p10 {q(.1):4.1f} med {q(.5):4.1f} p90 {q(.9):4.1f}"


for path in sys.argv[1:]:
    rows = [m for m in (PAT.search(line) for line in open(path, encoding='utf-8', errors='replace')) if m]
    if not rows:
        continue
    n, tpy = len(rows), int(rows[0].group(7))
    dug = sorted(int(r.group(3)) for r in rows)
    print(f"{path.replace(chr(92), '/').split('/')[-1]:26} n={n:3} dug med {dug[n // 2]:4} "
          f"| learned {spread([int(r.group(4)) for r in rows], tpy, n)} "
          f"| 100 {spread([int(r.group(5)) for r in rows], tpy, n)} "
          f"| 200 {spread([int(r.group(6)) for r in rows], tpy, n)}")
