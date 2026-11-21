"""Our language changes as data: every STR-1 line of v0.64 that differs from the pure-SI baseline, classified as
our text / a team-talk tag on SI's text / an invisible marker; plus the blocks appended after SI's text.
usage: delta.py base.ltf ours.ltf tags.json markers.json outdir"""
import json, sys, re, os, collections
base, ours, tagf, markf, outd = sys.argv[1:6]
tags = {int(k): v for k, v in json.load(open(tagf, encoding='utf-8')).items()}
marks = set(int(x) for x in json.load(open(markf, encoding='utf-8')))
D = ['​', '‌', '‍', '⁠']; S = '⁣'
def mark(i):
    d = []
    while True:
        d.append(D[i % 4]); i //= 4
        if i == 0: break
    return S + ''.join(reversed(d)) + S
krx = re.compile(r'^KEY-(\d+)(-[A-Z]+)?:')
def lines(p):
    with open(p, encoding='utf-8-sig', newline='') as f:
        for l in f:
            yield l.rstrip('\r\n')
per = collections.OrderedDict()   # id -> list of (key, tag, mark, text) for changed STR lines
keys_of = collections.defaultdict(list)
cur = None; n = 0; nb = 0; other = 0
bi = lines(base); oi = lines(ours); app = []
for b in bi:
    o = next(oi); nb += 1
    m = krx.match(b)
    if m: cur = (int(m.group(1)), m.group(2) or '')
    if b.startswith('STR-1:') and cur is not None: keys_of[cur[0]].append(cur[1])
    if b == o: continue
    if b.startswith('LANGNAME:') and o.startswith('LANGNAME:'): continue
    if not (b.startswith('STR-1: ') and o.startswith('STR-1: ')): other += 1; print('NON-STR DIFF', nb, b[:80], '|', o[:80]); continue
    i, k = cur; B = b[7:]; O = o[7:]; t = ''; mk = 0
    if i in marks and O.endswith(mark(i)): O = O[:-len(mark(i))]; mk = 1
    if i in tags and O.startswith(tags[i] + ' – '): t = tags[i]; O = O[len(tags[i]) + 3:]
    txt = None if O == B else O
    per.setdefault(i, []).append((k, t, mk, txt)); n += 1
for o in oi: app.append(o)
os.makedirs(outd, exist_ok=True)
rows = 0; star = 0
with open(os.path.join(outd, 'lines.tsv'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('# FM27 Media Mod language data: id, key (* = every key of the id), tag, marker, text (empty = SI\'s own text)\n')
    for i, lst in per.items():
        allk = keys_of[i]
        same = len(set((t, mk, txt) for _, t, mk, txt in lst)) == 1 and sorted(k for k, *_ in lst) == sorted(allk)
        items = [('*',) + lst[0][1:]] if same else lst
        if same: star += 1
        for k, t, mk, txt in items:
            assert txt is None or ('\t' not in txt and '\n' not in txt and '\r' not in txt)
            f.write(f"{i}\t{k}\t{t}\t{mk}\t{'' if txt is None else txt}\n"); rows += 1
with open(os.path.join(outd, 'append.ltf'), 'w', encoding='utf-8', newline='\n') as f:
    for a in app: f.write(a + '\n')
print(f'base lines {nb}; changed STR lines {n} in {len(per)} ids ({star} as *); rows {rows}; non-STR diffs {other}; appended lines {len(app)}')
