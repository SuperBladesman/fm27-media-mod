// FM27 Media Mod builder: media.fmf / simatch.fmf. The mod's changes ship as patches on a canonical form of FM's XML
// (one node per line, tab indents, fixed escaping), carrying none of SI's wording: SI's prose is masked (U+00A7) in the
// patches and filled back from the player's own files by an FNV-1a 64 hash. Port of 02 Tools/builder/canon.py and
// applyref.py, checked file for file against them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod.Builder
{
    internal static class XmlCanon
    {
        public const char Mask = '§';
        public abstract class Node { }
        public sealed class El : Node { public string Name = ""; public List<(string k, string v)> Attrs = new List<(string, string)>(); public List<Node> Kids = new List<Node>(); }
        public sealed class Txt : Node { public string Text = ""; }
        public sealed class Com : Node { public string Text = ""; }
        public sealed class PI : Node { public string Raw = ""; }

        public static bool Prose(string s) => s.Trim().IndexOf(' ') >= 0;

        public static string Unesc(string s)
        {
            if (s.IndexOf('&') < 0) return s;
            var o = new StringBuilder(s.Length); int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '&')
                {
                    int j = s.IndexOf(';', i);
                    if (j > i)
                    {
                        string e = s.Substring(i + 1, j - i - 1);
                        string rep = null;
                        if (e == "amp") rep = "&"; else if (e == "lt") rep = "<"; else if (e == "gt") rep = ">"; else if (e == "quot") rep = "\""; else if (e == "apos") rep = "'";
                        else if (e.StartsWith("#x") || e.StartsWith("#X")) { if (TryHex(e.Substring(2), out int v)) rep = Chr(v); }
                        else if (e.StartsWith("#")) { if (TryDec(e.Substring(1), out int v)) rep = Chr(v); }
                        if (rep != null) { o.Append(rep); i = j + 1; continue; }
                    }
                }
                o.Append(c); i++;
            }
            return o.ToString();
        }
        static string Chr(int v) => v < 0x10000 ? ((char)v).ToString() : v <= 0x10FFFF ? char.ConvertFromUtf32(v) : null;   // as Python chr()
        static bool TryHex(string s, out int v) { v = 0; if (s.Length == 0) return false; foreach (char c in s) { int d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1; if (d < 0) return false; v = v * 16 + d; } return true; }
        static bool TryDec(string s, out int v) { v = 0; if (s.Length == 0) return false; foreach (char c in s) { if (c < '0' || c > '9') return false; v = v * 10 + (c - '0'); } return true; }
        public static string EscAttr(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("\n", "&#10;").Replace("\r", "&#13;").Replace("\t", "&#9;");
        public static string EscText(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", "&#10;").Replace("\r", "&#13;");

        static readonly Regex NameRx = new Regex(@"\G[^\s/>=]+", RegexOptions.CultureInvariant);
        static readonly Regex AttrRx = new Regex(@"\G\s*([^\s/>=]+)\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.CultureInvariant);

        public static List<Node> Parse(string s)
        {
            if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            var root = new List<Node>(); var stack = new List<List<Node>> { root }; var names = new List<string>();
            int i = 0, n = s.Length;
            while (i < n)
            {
                if (string.CompareOrdinal(s, i, "<!--", 0, 4) == 0)
                {
                    int j = s.IndexOf("-->", i + 4, StringComparison.Ordinal); if (j < 0) throw new InvalidDataException("unclosed comment");
                    stack[stack.Count - 1].Add(new Com { Text = s.Substring(i + 4, j - i - 4) }); i = j + 3; continue;
                }
                if (string.CompareOrdinal(s, i, "<?", 0, 2) == 0)
                {
                    int j = s.IndexOf("?>", i, StringComparison.Ordinal);
                    stack[stack.Count - 1].Add(new PI { Raw = s.Substring(i, j + 2 - i) }); i = j + 2; continue;
                }
                if (string.CompareOrdinal(s, i, "</", 0, 2) == 0)
                {
                    int j = s.IndexOf('>', i); string nm = s.Substring(i + 2, j - i - 2).Trim();
                    if (names.Count == 0 || names[names.Count - 1] != nm) throw new InvalidDataException("mismatched </" + nm + ">");
                    names.RemoveAt(names.Count - 1); stack.RemoveAt(stack.Count - 1); i = j + 1; continue;
                }
                if (s[i] == '<')
                {
                    var m = NameRx.Match(s, i + 1); string nm = m.Value; int p = m.Index + m.Length;
                    var el = new El { Name = nm };
                    while (true)
                    {
                        var a = AttrRx.Match(s, p);
                        if (!a.Success) break;
                        el.Attrs.Add((a.Groups[1].Value, Unesc(a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value))); p = a.Index + a.Length;
                    }
                    while (s[p] == ' ' || s[p] == '\t' || s[p] == '\r' || s[p] == '\n') p++;
                    stack[stack.Count - 1].Add(el);
                    if (string.CompareOrdinal(s, p, "/>", 0, 2) == 0) { i = p + 2; continue; }
                    if (s[p] != '>') throw new InvalidDataException("bad tag <" + nm);
                    i = p + 1; stack.Add(el.Kids); names.Add(nm); continue;
                }
                int k = s.IndexOf('<', i); if (k < 0) k = n;
                string t = s.Substring(i, k - i);
                if (t.Trim().Length > 0) stack[stack.Count - 1].Add(new Txt { Text = Unesc(t) });
                i = k;
            }
            if (names.Count != 0) throw new InvalidDataException("unclosed <" + names[names.Count - 1] + ">");
            return root;
        }

        static string NL(string c) => c.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

        // mode 0 = real, 1 = masked
        public static List<string> Lines(List<Node> nodes, int mode)
        {
            var o = new List<string>(); Emit(nodes, 0, mode, o); return o;
        }
        static string P(string v, int mode) => mode == 1 && Prose(v) ? Mask.ToString() : v;
        static void Emit(List<Node> nodes, int depth, int mode, List<string> o)
        {
            string ind = new string('\t', depth);
            foreach (var nd in nodes)
            {
                switch (nd)
                {
                    case Com c: o.Add(ind + "<!--" + P(NL(c.Text), mode) + "-->"); break;
                    case PI pi: o.Add(ind + pi.Raw); break;
                    case Txt t: o.Add(ind + EscText(P(t.Text, mode))); break;
                    case El e:
                        var sb = new StringBuilder();
                        foreach (var (k, v) in e.Attrs) sb.Append(' ').Append(k).Append("=\"").Append(EscAttr(P(v, mode))).Append('"');
                        string a = sb.ToString();
                        if (e.Kids.Count == 0) o.Add(ind + "<" + e.Name + a + "/>");
                        else if (e.Kids.Count == 1 && e.Kids[0] is Txt only) o.Add(ind + "<" + e.Name + a + ">" + EscText(P(only.Text, mode)) + "</" + e.Name + ">");
                        else { o.Add(ind + "<" + e.Name + a + ">"); Emit(e.Kids, depth + 1, mode, o); o.Add(ind + "</" + e.Name + ">"); }
                        break;
                }
            }
        }

        public static void ProseStrings(List<Node> nodes, Action<string> add)
        {
            foreach (var nd in nodes)
            {
                switch (nd)
                {
                    case Com c: { var t = NL(c.Text); if (Prose(t)) add(t); break; }
                    case Txt t: if (Prose(t.Text)) add(t.Text); break;
                    case El e:
                        foreach (var (k, v) in e.Attrs) if (Prose(v)) add(v);
                        ProseStrings(e.Kids, add); break;
                }
            }
        }

        public static string Fnv64(string s)
        {
            ulong h = 0xcbf29ce484222325UL;
            foreach (char c in s) { h ^= c; h *= 0x100000001b3UL; }
            return h.ToString("x16");
        }

        static readonly Regex ARx = new Regex("=\"([^\"]*)\"", RegexOptions.CultureInvariant);
        static readonly Regex InlineRx = new Regex(@"^(<[^>]*>)(.*)(</[^>]+>)$", RegexOptions.CultureInvariant);
        public static string MaskLine(string l)
        {
            string s = l.TrimStart('\t'); string ind = l.Substring(0, l.Length - s.Length);
            if (s.StartsWith("<!--", StringComparison.Ordinal))
            {
                string c = s.Substring(4, s.Length - 7); return ind + "<!--" + (Prose(c) ? Mask.ToString() : c) + "-->";
            }
            if (!s.StartsWith("<", StringComparison.Ordinal)) return ind + (Prose(Unesc(s)) ? Mask.ToString() : s);
            string RA(Match m) => "=\"" + (Prose(Unesc(m.Groups[1].Value)) ? Mask.ToString() : m.Groups[1].Value) + "\"";
            var im = InlineRx.Match(s);
            if (im.Success && !im.Groups[1].Value.EndsWith("/>", StringComparison.Ordinal))
            {
                string head = ARx.Replace(im.Groups[1].Value, RA); string t = im.Groups[2].Value;
                return ind + head + (Prose(Unesc(t)) ? Mask.ToString() : t) + im.Groups[3].Value;
            }
            return ind + ARx.Replace(s, RA);
        }
    }

    internal sealed class MediaPatches
    {
        public sealed class Hunk { public int Start; public List<string> Before = new List<string>(), Old = new List<string>(), After = new List<string>(), New = new List<string>(); }
        public sealed class FilePatch { public string Archive = "", Inner = ""; public List<Hunk> Hunks = new List<Hunk>(); }
        public readonly List<FilePatch> Files = new List<FilePatch>();
        public readonly HashSet<string> NeededHashes = new HashSet<string>(StringComparer.Ordinal);
        static readonly Regex HashRx = new Regex("§([0-9a-f]{16})", RegexOptions.CultureInvariant);

        public MediaPatches(string file)
        {
            var L = File.ReadAllLines(file, new UTF8Encoding(false)); int p = 0; FilePatch cur = null;
            while (p < L.Length)
            {
                var l = L[p++];
                if (l.StartsWith("FILE\t", StringComparison.Ordinal)) { var a = l.Split('\t'); cur = new FilePatch { Archive = a[1], Inner = a[2] }; Files.Add(cur); }
                else if (l.StartsWith("HUNK\t", StringComparison.Ordinal))
                {
                    var a = l.Split('\t'); var h = new Hunk { Start = int.Parse(a[1]) };
                    int cb = int.Parse(a[2]), no = int.Parse(a[3]), ca = int.Parse(a[4]), nn = int.Parse(a[5]);
                    for (int k = 0; k < cb; k++) h.Before.Add(L[p++].Substring(1));
                    for (int k = 0; k < no; k++) h.Old.Add(L[p++].Substring(1));
                    for (int k = 0; k < ca; k++) h.After.Add(L[p++].Substring(1));
                    for (int k = 0; k < nn; k++) { var x = L[p++].Substring(1); h.New.Add(x); foreach (Match m in HashRx.Matches(x)) NeededHashes.Add(m.Groups[1].Value); }
                    cur.Hunks.Add(h);
                }
            }
        }

        public static string Resolve(string l, Dictionary<string, string> dic, ref int missing)
        {
            if (l.IndexOf(XmlCanon.Mask) < 0) return l;
            int miss = 0;
            string r = HashRx.Replace(l, m =>
            {
                if (!dic.TryGetValue(m.Groups[1].Value, out var v)) { miss++; v = ""; }
                string pre = l.Substring(0, m.Index);
                if (pre.EndsWith("=\"", StringComparison.Ordinal)) return XmlCanon.EscAttr(v);
                if (pre.TrimStart('\t') == "<!--") return v;
                return XmlCanon.EscText(v);
            });
            missing += miss;
            return r;
        }

        // canonical vanilla lines -> patched lines; false if any hunk cannot be placed (the file is then left as SI's)
        public static bool Apply(List<string> real, FilePatch fp, Dictionary<string, string> dic, out List<string> result, ref int missing, out string why)
        {
            var cur = new List<string>(real); var m = new List<string>(real.Count);
            foreach (var x in cur) m.Add(XmlCanon.MaskLine(x));
            int off = 0; why = null; result = null;
            foreach (var h in fp.Hunks)
            {
                var pat = new List<string>(h.Before.Count + h.Old.Count + h.After.Count);
                pat.AddRange(h.Before); pat.AddRange(h.Old); pat.AddRange(h.After);
                int exp = h.Start + off - h.Before.Count, pos = -1;
                if (exp >= 0 && Matches(m, exp, pat)) pos = exp;
                else
                {
                    int hits = 0, at = -1;
                    for (int i = 0; i + pat.Count <= m.Count; i++) if (Matches(m, i, pat)) { hits++; at = i; if (hits > 1) break; }
                    if (hits == 1) pos = at;
                }
                if (pos < 0) { why = "a change near line " + h.Start + " no longer fits SI's file"; return false; }
                int ins = pos + h.Before.Count;
                var rl = new List<string>(h.New.Count); var rm = new List<string>(h.New.Count);
                foreach (var x in h.New) { var y = Resolve(x, dic, ref missing); rl.Add(y); rm.Add(XmlCanon.MaskLine(y)); }
                cur.RemoveRange(ins, h.Old.Count); cur.InsertRange(ins, rl);
                m.RemoveRange(ins, h.Old.Count); m.InsertRange(ins, rm);
                off += h.New.Count - h.Old.Count + (pos - exp);
            }
            result = cur; return true;
        }
        static bool Matches(List<string> m, int at, List<string> pat)
        {
            if (at < 0 || at + pat.Count > m.Count) return false;
            for (int k = 0; k < pat.Count; k++) if (!string.Equals(m[at + k], pat[k], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
