import re, difflib, collections
krx = re.compile(r'^KEY-(\d+)(-[A-Z]+)?:'); B = {}; cur = None
with open('base.ltf', encoding='utf-8-sig', newline='') as f:
    for l in f:
        l = l.rstrip('\r\n'); m = krx.match(l)
        if m: cur = int(m.group(1))
        elif l.startswith('STR-1: ') and cur is not None and cur not in B: B[cur] = l[7:]
rows = []
for l in open('out/lines.tsv', encoding='utf-8'):
    if l.startswith('#'): continue
    i, k, t, m, x = l.rstrip('\n').split('\t', 4); rows.append((int(i), t, m, x))
norm = lambda s: re.sub(r'[^a-z0-9%#\[\] ]', '', re.sub(r'\s+', ' ', s.lower())).strip()
band = collections.Counter(); hi = []
notext = sum(1 for r in rows if not r[3])
for i, t, m, x in rows:
    if not x: continue
    b = B.get(i, '')
    r = difflib.SequenceMatcher(None, norm(b), norm(x), autojunk=False).ratio() if b else 0
    bandk = '1.00 identical' if norm(b) == norm(x) else '>=0.90' if r >= 0.9 else '0.75-0.90' if r >= 0.75 else '<0.75'
    band[bandk] += 1
    if r >= 0.9: hi.append((r, i, b, x))
print('rows', len(rows), '| tag or marker only (no text shipped):', notext)
for k in ['1.00 identical', '>=0.90', '0.75-0.90', '<0.75']: print(' ', k, band[k])
hi.sort(reverse=True)
import json; json.dump([[r, i, b, x] for r, i, b, x in hi], open('out/near_si.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
for r, i, b, x in hi[:6]: print(round(r, 3), i, '\n   SI :', b[:150], '\n   OUR:', x[:150])
