"""D864's summariser for ZzLibrary.cs (specs/library-borrowing.md §4).

    python library.py <logs-folder> base,recA,recB      the saves: per lived-in save and per arm, then the totals,
                                                       the library walk, spare beds, firewood and cold from `base`
    python library.py <logs-folder> --valleys vbase,vlet  the unattended valleys' ZZB lines, summed per arm

Logs are named <arm>.<shard>.log. A save is lived in at Year >= 10 with three or more homes.
"""
import collections
import glob
import re
import sys


def parse(folder, arm):
    loads, ends, fires, colds = {}, {}, collections.defaultdict(list), collections.defaultdict(list)
    for path in glob.glob(f'{folder}/{arm}.*.log'):
        for line in open(path, encoding='utf-8', errors='replace'):
            words = line.split()
            if not words or not words[0].startswith('ZZ'):
                continue
            tag, village, rest = words[0], words[1], words[3:]
            fields, j = {}, 0
            while j < len(rest):
                key, values = rest[j], []
                j += 1
                while j < len(rest) and re.fullmatch(r'-?\d+', rest[j]):
                    values.append(int(rest[j]))
                    j += 1
                fields[key] = values[0] if len(values) == 1 else values
            {'ZZA': loads, 'ZZE': ends}.get(tag, {})[village] = fields
            if tag == 'ZZF':
                fires[village].append(fields)
            elif tag == 'ZZC':
                colds[village].append(fields)
    return loads, ends, fires, colds


def saves(folder, arms):
    data = {arm: parse(folder, arm) for arm in arms}
    loads = data[arms[0]][0]
    lived = sorted((v for v, d in loads.items() if d['year'] >= 10 and d['homes'] >= 3), key=lambda v: -loads[v]['year'])
    print(f'{len(lived)} lived-in saves, {sum(1 for v in lived if loads[v]["libraries"])} with a library')
    print('village'.ljust(26) + ' start' + ''.join(f' | {a} rel/end/starved' for a in arms))
    totals = {a: collections.Counter() for a in arms}
    for v in lived:
        row = v[:26].ljust(26) + f' {loads[v]["people"]:5}'
        for a in arms:
            e = data[a][1].get(v)
            if e is None:
                row += ' | --'
                continue
            # D869: the real rule's relatives are `kin` (a posed arm's are `posed`); letters and answers standing at the end.
            row += f' | {e["posed"] + e.get("kin", 0):3}/{e["people"]:3}/{e["starved"]:3}'
            for k in ('people', 'nomads', 'posed', 'kin', 'letters', 'waiting', 'births', 'starved', 'cold', 'left'):
                totals[a][k] += e.get(k, 0)
        print(row)
    for a in arms:
        print(a.ljust(10), dict(totals[a]))

    base = data[arms[0]]
    walks = [0] * 6
    for v in lived:
        if loads[v]['libraries']:
            walks = [x + y for x, y in zip(walks, loads[v]['reach'])]
    print('homes by walk to a library <=8 <=12 <=16 <=24 >24:', walks[:5])
    print('spare beds:', sum(loads[v]['spare'] for v in lived), 'of', sum(loads[v]['homes'] for v in lived), 'homes')
    f = collections.Counter()
    for v in lived:
        for d in base[2][v]:
            for k in ('homes', 'winterburn', 'winterwanted', 'winternone', 'fall', 'fallburn'):
                f[k] += d[k]
    print('firewood:', dict(f))
    c = collections.Counter()
    for v in lived:
        for d in base[3][v]:
            c['fireoutdays'] += d['fireoutdays']
            c['colddeaths'] += d['colddeaths']
            for i, who in ((0, 'grad'), (1, 'other')):
                c[f'winters_{who}'] += d['adults'][i]
                c[f'colddays6_{who}'] += d['colddays6'][i]
                c[f'goingsin_{who}'] += d['goingsin'][i]
    print('cold:', dict(c))


def valleys(folder, arms):
    pattern = re.compile(r'ZZB (\w+) (\d+) alive (\d+) peak (\d+) starved (\d+) cold (\d+) other (\d+) '
                         r'nomads (\d+) posed (\d+) births (\d+) died (-?\d+)')
    for arm in arms:
        runs = []
        for path in glob.glob(f'{folder}/{arm}.*.log'):
            for line in open(path, encoding='utf-8', errors='replace'):
                m = pattern.search(line)
                if m:
                    runs.append([int(x) for x in m.groups()[1:]])
        total = lambda i: sum(r[i] for r in runs)
        print(f'{arm}: valleys {len(runs)} dead {sum(1 for r in runs if r[9] > 0)} alive {total(1)} '
              f'starved {total(3)} in {sum(1 for r in runs if r[3])} cold {total(4)} nomads {total(6)} '
              f'relatives {total(7)} in {sum(1 for r in runs if r[7])} births {total(8)}')


if __name__ == '__main__':
    if len(sys.argv) > 3 and sys.argv[2] == '--valleys':
        valleys(sys.argv[1], sys.argv[3].split(','))
    else:
        saves(sys.argv[1], sys.argv[2].split(','))
