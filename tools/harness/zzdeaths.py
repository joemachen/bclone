"""D646: classify every head lost in ZzPose's ZZ_DEATHS reports.

For each loss: a cull (the herdsman was culling) or a starving, and for a starving the run-length
of the seated herdsman's states over the ticks the herd wanted hay, so one line says where he was.
Usage: python -X utf8 -I zzdeaths.py <report.d> ...
"""
import re
import sys
from itertools import groupby

for path in sys.argv[1:]:
    blocks, cur = [], None
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if "head lost #" in line:
            cur = {"title": line.split("=====")[1].strip(), "lines": []}
            blocks.append(cur)
        elif cur is not None:
            cur["lines"].append(line[4:] if line.startswith("ZZD ") else line)
    name = path.replace("\\", "/").split("/")[-1].replace(".txt.d", "")
    culls = 0
    print(f"=== {name}: {len(blocks)} losses")
    for b in blocks:
        ls = b["lines"]
        states = [re.search(r" st=(\S+)", l).group(1) if re.search(r" st=(\S+)", l) else "-" for l in ls]
        if any(s == "Culling" for s in states[-3:]):
            culls += 1
            continue
        wanting = [(s, l) for s, l in zip(states, ls) if "wants=True" in l]
        runs = " ".join(f"{k}x{len(list(g))}" for k, g in groupby(s for s, _ in wanting))
        last = ls[-1]
        when = re.match(r"(\S+ d\d+)", last).group(1)
        out = re.search(r"out=(\d+)", last).group(1)
        home = re.search(r"home=(\S+)", last).group(1)
        print(f"  {b['title']:<22} {when:<10} out={out:<3} home={home:<10} wanting {len(wanting):>2} ticks: {runs}")
    print(f"  (+ {culls} culls)")
