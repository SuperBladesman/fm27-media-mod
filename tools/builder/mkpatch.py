"""Patches for our media.fmf / simatch.fmf changes, carrying none of SI's wording.
usage: mkpatch.py pairs.txt vanilla_media_dir vanilla_simatch_dir out.patch"""
import sys, os, subprocess, tempfile; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from canon import *
pairsf, vmd, vsd, outp = sys.argv[1:5]
pairs = [l.split('\t') for l in open(pairsf, encoding='utf-8').read().split('\n') if l]
known = set()
for d in (vmd, vsd):
    for root, ds, fs in os.walk(d):
        for f in fs:
            if f.endswith('.xml'): prose_strings(parse(open(os.path.join(root, f), 'rb').read().decode('utf-8')), known)
print('SI prose strings known:', len(known))
C = 3
def opcodes(a, b):
    with tempfile.NamedTemporaryFile('w', delete=False, encoding='utf-8') as fa, tempfile.NamedTemporaryFile('w', delete=False, encoding='utf-8') as fb:
        fa.write('\n'.join(a) + '\n'); fb.write('\n'.join(b) + '\n')
    d = subprocess.run(['diff', fa.name, fb.name], capture_output=True, text=True, encoding='utf-8').stdout
    os.unlink(fa.name); os.unlink(fb.name)
    ops = []
    for l in d.split('\n'):
        m = re.match(r'^(\d+)(?:,(\d+))?([acd])(\d+)(?:,(\d+))?$', l)
        if not m: continue
        a1, a2, op, b1, b2 = int(m.group(1)), int(m.group(2) or m.group(1)), m.group(3), int(m.group(4)), int(m.group(5) or m.group(4))
        if op == 'a': ops.append((a1, a1, b1 - 1, b2))          # insert after old line a1
        elif op == 'd': ops.append((a1 - 1, a2, b1, b1))         # delete old a1..a2
        else: ops.append((a1 - 1, a2, b1 - 1, b2))
    return ops
out = open(outp, 'w', encoding='utf-8', newline='\n')
out.write('# FM27 Media Mod media patches v1. Canonical lines (see canon rules); § = SI prose (masked), §<fnv64> = SI prose restored from the player\'s own files.\n')
nh = nadd = nship = 0
for a, b in pairs:
    arch = 'simatch.fmf' if a.startswith('svan/') else 'media.fmf'
    inner = a.split('/', 1)[1]
    V = parse(open(a, 'rb').read().decode('utf-8')); O = parse(open(b, 'rb').read().decode('utf-8'))
    Vm = lines(V, mode='mask'); Om = lines(O, mode='mask'); Os = lines(O, mode='ship', known=known, fnv=fnv64); Orl = lines(O)
    assert len(Om) == len(Os) == len(Orl)
    ops = opcodes(Vm, Om)
    out.write(f'FILE\t{arch}\t{inner}\n')
    for k, (i1, i2, j1, j2) in enumerate(ops):
        prev_end = ops[k-1][1] if k else 0; next_start = ops[k+1][0] if k + 1 < len(ops) else len(Vm)
        cb = min(C, i1 - prev_end); ca = min(C, next_start - i2)
        out.write(f'HUNK\t{i1}\t{cb}\t{i2 - i1}\t{ca}\t{j2 - j1}\n')
        for x in Vm[i1 - cb:i1]: out.write(' ' + x + '\n')
        for x in Vm[i1:i2]: out.write('-' + x + '\n')
        for x in Vm[i2:i2 + ca]: out.write(' ' + x + '\n')
        for x in Os[j1:j2]:
            out.write('+' + x + '\n'); nadd += 1
            if MASK in x: nship += 1
        nh += 1
out.write('END\n'); out.close()
print(f'files {len(pairs)}, hunks {nh}, added lines {nadd} ({nship} with SI prose by reference)')
