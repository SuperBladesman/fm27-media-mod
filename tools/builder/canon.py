"""Canonical form of FM's XML for patching: one node per line, tab indents, fixed escaping, comments on their own
line (newlines in comments become spaces), whitespace-only text dropped. The C# builder implements the same rules.
Prose (any attribute value, text or comment containing a space) is what carries SI's wording; it is masked as §."""
import re
MASK = '§'
def unesc(s):
    out = []; i = 0; n = len(s)
    while i < n:
        c = s[i]
        if c == '&':
            j = s.find(';', i)
            if j > i:
                e = s[i+1:j]
                if e == 'amp': out.append('&'); i = j + 1; continue
                if e == 'lt': out.append('<'); i = j + 1; continue
                if e == 'gt': out.append('>'); i = j + 1; continue
                if e == 'quot': out.append('"'); i = j + 1; continue
                if e == 'apos': out.append("'"); i = j + 1; continue
                if e.startswith('#x') or e.startswith('#X'):
                    try: out.append(chr(int(e[2:], 16))); i = j + 1; continue
                    except ValueError: pass
                elif e.startswith('#'):
                    try: out.append(chr(int(e[1:]))); i = j + 1; continue
                    except ValueError: pass
        out.append(c); i += 1
    return ''.join(out)
def esc_attr(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;').replace('\n', '&#10;').replace('\r', '&#13;').replace('\t', '&#9;')
def esc_text(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('\n', '&#10;').replace('\r', '&#13;')
def prose(s): return ' ' in s.strip()
NAME = re.compile(r'[^\s/>=]+')
ATTR = re.compile(r'\s*([^\s/>=]+)\s*=\s*("([^"]*)"|\'([^\']*)\')')
def parse(s):
    """-> list of nodes; node = ('e', name, [(k, v_unescaped)], children) | ('t', text_unescaped) | ('c', comment) | ('p', raw)"""
    if s.startswith('﻿'): s = s[1:]
    s = s.replace('\r\n', '\n').replace('\r', '\n')   # XML line-end normalisation
    root = []; stack = [root]; names = []; i = 0; n = len(s)
    while i < n:
        if s.startswith('<!--', i):
            j = s.find('-->', i + 4); assert j >= 0
            stack[-1].append(('c', s[i+4:j])); i = j + 3; continue
        if s.startswith('<?', i):
            j = s.find('?>', i); stack[-1].append(('p', s[i:j+2])); i = j + 2; continue
        if s.startswith('</', i):
            j = s.find('>', i); nm = s[i+2:j].strip(); assert names and names[-1] == nm, (nm, names[-3:])
            names.pop(); stack.pop(); i = j + 1; continue
        if s[i] == '<':
            m = NAME.match(s, i + 1); nm = m.group(0); p = m.end(); attrs = []
            while True:
                a = ATTR.match(s, p)
                if not a: break
                attrs.append((a.group(1), unesc(a.group(3) if a.group(3) is not None else a.group(4)))); p = a.end()
            while s[p] in ' \t\r\n': p += 1
            node = ('e', nm, attrs, [])
            stack[-1].append(node)
            if s.startswith('/>', p): i = p + 2; continue
            assert s[p] == '>', (nm, s[i:p+20]); i = p + 1
            stack.append(node[3]); names.append(nm); continue
        j = s.find('<', i); j = n if j < 0 else j
        t = s[i:j]
        if t.strip(): stack[-1].append(('t', unesc(t)))
        i = j
    assert not names, names
    return root
def lines(nodes, depth=0, out=None, mode='real', known=None, fnv=None):
    """mode: real | mask (prose -> §) | ship (prose -> § + fnv64 if known SI prose, else kept)"""
    if out is None: out = []
    def P(v):
        if mode == 'real' or not prose(v): return v
        if mode == 'mask': return MASK
        return (MASK + fnv(v)) if v in known else v
    for nd in nodes:
        ind = '\t' * depth
        if nd[0] == 'c':
            c = re.sub(r'\r\n|\r|\n', ' ', nd[1]); out.append(ind + '<!--' + P(c) + '-->')
        elif nd[0] == 'p': out.append(ind + nd[1])
        elif nd[0] == 't': out.append(ind + esc_text(P(nd[1])))
        else:
            _, nm, attrs, ch = nd
            a = ''.join(' %s="%s"' % (k, esc_attr(P(v))) for k, v in attrs)
            if not ch: out.append(ind + '<' + nm + a + '/>')
            elif len(ch) == 1 and ch[0][0] == 't': out.append(ind + '<' + nm + a + '>' + esc_text(P(ch[0][1])) + '</' + nm + '>')
            else:
                out.append(ind + '<' + nm + a + '>'); lines(ch, depth + 1, out, mode, known, fnv); out.append(ind + '</' + nm + '>')
    return out
def prose_strings(nodes, acc):
    for nd in nodes:
        if nd[0] == 'c':
            c = re.sub(r'\r\n|\r|\n', ' ', nd[1])
            if prose(c): acc.add(c)
        elif nd[0] == 't':
            if prose(nd[1]): acc.add(nd[1])
        elif nd[0] == 'e':
            for k, v in nd[2]:
                if prose(v): acc.add(v)
            prose_strings(nd[3], acc)
    return acc
def fnv64(s):
    h = 0xcbf29ce484222325
    b = s.encode('utf-16-le')
    for i in range(0, len(b), 2):
        h ^= b[i] | (b[i+1] << 8); h = (h * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF
    return '%016x' % h
