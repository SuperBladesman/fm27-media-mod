"""Turn out/lines.tsv into the shipped language data: our own lines as text; lines that are mostly SI's as an edit
(keep/delete/insert over UTF-16 code units) checked against an FNV-1a hash of SI's line, so no SI sentence ships.
usage: make_data.py base.ltf out/lines.tsv lang_lines.tsv"""
import re, sys, difflib
base, src, dst = sys.argv[1:4]
krx = re.compile(r'^KEY-(\d+)(-[A-Z]+)?:'); B = {}; cur = None
with open(base, encoding='utf-8-sig', newline='') as f:
    for l in f:
        l = l.rstrip('\r\n'); m = krx.match(l)
        if m: cur = int(m.group(1))
        elif l.startswith('STR-1: ') and cur is not None and cur not in B: B[cur] = l[7:]
def units(s):
    b = s.encode('utf-16-le'); return [b[i] | (b[i+1] << 8) for i in range(0, len(b), 2)]
def ustr(u): return b''.join(x.to_bytes(2, 'little') for x in u).decode('utf-16-le')
def fnv(s):
    h = 0x811C9DC5
    for u in units(s): h ^= u; h = (h * 0x01000193) & 0xFFFFFFFF
    return h
US = '\x1f'; n_txt = n_ops = n_none = 0
with open(dst, 'w', encoding='utf-8', newline='\n') as fo:
    fo.write('# FM27 Media Mod language data v1: id, tag, marker, kind (t = our text, e = edit of SI\'s line, empty = SI\'s line), fnv1a32 of SI\'s line (for e), payload\n')
    for l in open(src, encoding='utf-8'):
        if l.startswith('#'): continue
        i, k, t, m, x = l.rstrip('\n').split('\t', 4); assert k == '*'
        if not x: fo.write(f'{i}\t{t}\t{m}\t\t\t\n'); n_none += 1; continue
        b = B[int(i)]; bu = units(b); xu = units(x)
        sm = difflib.SequenceMatcher(None, bu, xu, autojunk=False)
        W = lambda s: re.findall(r"\[%[^\]]*\]|[\w']+|[^\w\s]", s.lower())
        wr = difflib.SequenceMatcher(None, W(b), W(x), autojunk=False).ratio()
        if wr >= 0.85:
            ops = []
            for op, i1, i2, j1, j2 in sm.get_opcodes():
                if op == 'equal': ops.append(f'={i2 - i1}')
                else:
                    if i2 > i1: ops.append(f'-{i2 - i1}')
                    if j2 > j1:
                        ins = ustr(xu[j1:j2]); assert US not in ins and '\t' not in ins; ops.append('+' + ins)
            fo.write(f'{i}\t{t}\t{m}\te\t{fnv(b):08x}\t{US.join(ops)}\n'); n_ops += 1
        else:
            fo.write(f'{i}\t{t}\t{m}\tt\t\t{x}\n'); n_txt += 1
print(f'our text {n_txt}, edits of SI lines {n_ops}, tag/marker only {n_none}')
