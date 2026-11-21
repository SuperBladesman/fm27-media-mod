"""Reference patch applier (the C# builder does the same): canonical vanilla + patches -> canonical patched files."""
import sys, os, re; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from canon import *
A_RX = re.compile(r'="([^"]*)"')
def mask_line(l):
    s = l.lstrip('\t'); ind = l[:len(l) - len(s)]
    if s.startswith('<!--'):
        c = s[4:-3]; return ind + '<!--' + (MASK if prose(c) else c) + '-->'
    if not s.startswith('<'):
        return ind + (MASK if prose(unesc(s)) else s)
    def ra(m): return '="' + (MASK if prose(unesc(m.group(1))) else m.group(1)) + '"'
    m = re.match(r'^(<[^>]*>)(.*)(</[^>]+>)$', s)
    if m and not m.group(1).endswith('/>'):
        head = A_RX.sub(ra, m.group(1)); t = m.group(2)
        return ind + head + (MASK if prose(unesc(t)) else t) + m.group(3)
    return ind + A_RX.sub(ra, s)
def resolve(l, dic, miss):
    def rep(m):
        v = dic.get(m.group(1))
        if v is None: miss[0] += 1; v = ''
        pre = l[:m.start()]
        if pre.endswith('="'): return esc_attr(v)
        if pre.lstrip('\t') == '<!--': return v
        return esc_text(v)
    return re.sub('§([0-9a-f]{16})', rep, l)
def read_patches(p):
    files = []; cur = None; it = iter(open(p, encoding='utf-8').read().split('\n'))
    for l in it:
        if l.startswith('FILE\t'): _, arch, inner = l.split('\t'); cur = (arch, inner, []); files.append(cur)
        elif l.startswith('HUNK\t'):
            _, s, cb, no, ca, nn = l.split('\t'); s, cb, no, ca, nn = map(int, (s, cb, no, ca, nn))
            before = [next(it)[1:] for _ in range(cb)]; old = [next(it)[1:] for _ in range(no)]; after = [next(it)[1:] for _ in range(ca)]
            new = [next(it)[1:] for _ in range(nn)]
            cur[2].append((s, before, old, after, new))
    return files
def apply_file(real, hunks, dic, miss):
    cur = list(real); m = [mask_line(x) for x in cur]; off = 0; fails = 0
    for s, before, old, after, new in hunks:
        pat = before + old + after; exp = s + off - len(before); pos = None
        if 0 <= exp and m[exp:exp + len(pat)] == pat: pos = exp
        else:
            hits = [i for i in range(len(m) - len(pat) + 1) if m[i:i + len(pat)] == pat]
            if len(hits) == 1: pos = hits[0]
        if pos is None: fails += 1; continue
        at = pos + len(before); rl = [resolve(x, dic, miss) for x in new]
        cur[at:at + len(old)] = rl; m[at:at + len(old)] = [mask_line(x) for x in rl]
        off += len(new) - len(old) + (pos - exp)
    return cur, fails
if __name__ == '__main__':
    patchf, vmd, vsd, omd, osd = sys.argv[1:6]
    dic = {}
    for d in (vmd, vsd):
        for root, ds, fs in os.walk(d):
            for f in fs:
                if f.endswith('.xml'):
                    for v in prose_strings(parse(open(os.path.join(root, f), 'rb').read().decode('utf-8')), set()): dic[fnv64(v)] = v
    ok = bad = 0; miss = [0]; totf = 0
    for arch, inner, hunks in read_patches(patchf):
        vd, od = (vsd, osd) if arch == 'simatch.fmf' else (vmd, omd)
        real = lines(parse(open(os.path.join(vd, inner), 'rb').read().decode('utf-8')))
        got, fails = apply_file(real, hunks, dic, miss); totf += fails
        want = lines(parse(open(os.path.join(od, inner), 'rb').read().decode('utf-8')))
        if got == want and not fails: ok += 1
        else:
            bad += 1; d = next((i for i in range(min(len(got), len(want))) if got[i] != want[i]), None)
            print('MISMATCH', inner, 'fails', fails, 'len', len(got), len(want), 'first diff', d, repr(got[d][:120]) if d is not None else '', repr(want[d][:120]) if d is not None else '')
    print('files identical', ok, 'mismatched', bad, 'hunk failures', totf, 'unresolved SI references', miss[0])
