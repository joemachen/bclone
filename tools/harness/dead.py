import re,sys,glob
rows=[]
for path in sys.argv[1:]:
    alive=[];peak=[];st=[];co=[]
    for line in open(path,encoding='utf-8',errors='replace'):
        m=re.search(r'ZZB \w+ (\d+) (?:sites \d+ )?alive (\d+) peak (\d+) starved (\d+) cold (\d+)',line)
        if m:
            a,p,s,c=map(int,m.group(2,3,4,5)); alive.append(a);peak.append(p);st.append(s);co.append(c)
    n=len(alive)
    print(f"{path.split('/')[-1]:22} n={n:3} alive {sum(alive):4} dead-valleys {sum(1 for a in alive if a==0):3} peak {sum(peak):4} starved {sum(st):4} cold {sum(co):3}")
