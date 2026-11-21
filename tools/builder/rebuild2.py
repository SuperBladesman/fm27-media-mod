"""Reference builder for the shipped format: base.ltf (SI's English through the template + possessive fix) +
lang_lines.tsv + append.ltf -> the full language."""
import sys, re
base, linesf, appf, name, out = sys.argv[1:6]
D = ['​', '‌', '‍', '⁠']; S = '⁣'
def mark(i):
    d = []
    while True:
        d.append(D[i % 4]); i //= 4
        if i == 0: break
    return S + ''.join(reversed(d)) + S
def units(s):
    b = s.encode('utf-16-le'); return [b[i] | (b[i+1] << 8) for i in range(0, len(b), 2)]
def ustr(u): return b''.join(x.to_bytes(2, 'little') for x in u).decode('utf-16-le')
def fnv(s):
    h = 0x811C9DC5
    for u in units(s): h ^= u; h = (h * 0x01000193) & 0xFFFFFFFF
    return h
def apply(b, ops):
    bu = units(b); out = []; p = 0
    for o in ops.split('\x1f'):
        if o[0] == '=': n = int(o[1:]); out += bu[p:p+n]; p += n
        elif o[0] == '-': p += int(o[1:])
        else: out += units(o[1:])
    assert p == len(bu); return ustr(out)
rows = {}; skipped = 0
for l in open(linesf, encoding='utf-8'):
    if l.startswith('#'): continue
    i, t, m, k, h, x = l.rstrip('\n').split('\t', 5); rows[int(i)] = (t, m == '1', k, h, x)
krx = re.compile(r'^KEY-(\d+)(-[A-Z]+)?:'); cur = None
with open(out, 'w', encoding='utf-8-sig', newline='\r\n') as fo:
    with open(base, encoding='utf-8-sig', newline='') as fb:
        for l in fb:
            l = l.rstrip('\r\n'); m = krx.match(l)
            if m: cur = int(m.group(1))
            elif l.startswith('LANGNAME:'): l = 'LANGNAME: ' + name
            elif l.startswith('STR-1: ') and cur in rows:
                t, mk, k, h, x = rows[cur]; b = l[7:]; txt = b
                if k == 't': txt = x
                elif k == 'e':
                    if f'{fnv(b):08x}' == h: txt = apply(b, x)
                    else: skipped += 1
                if t: txt = t + ' – ' + txt
                if mk: txt += mark(cur)
                l = 'STR-1: ' + txt
            fo.write(l + '\n')
    for l in open(appf, encoding='utf-8', newline=''):
        fo.write(l.rstrip('\r\n') + '\n')
print('edits skipped (SI line changed):', skipped)
