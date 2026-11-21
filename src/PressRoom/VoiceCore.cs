// FM27 Media Mod - Press Room: the Voices tag channel and token filling (0.11.0; 0.12.0 adds many versions per key and a least-used pick). Pure .NET, tested offline.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class VoiceCore
    {
        internal const char S = '⁣';
        static readonly string Digits = "​‌‍⁠";
        static readonly Regex Mark = new Regex("⁣([​‌‍⁠]+)⁣", RegexOptions.Compiled);
        static readonly Regex Tok = new Regex(@"\[%[^\]]+\]", RegexOptions.Compiled);
        internal class Entry { public string Base; public Regex Rx; public List<string> Toks = new List<string>(); public Dictionary<string, List<string>> V = new Dictionary<string, List<string>>(); }
        internal static readonly Dictionary<int, Entry> E = new Dictionary<int, Entry>();
        internal static int Rows, Bad;

        internal static int Decode(string d)
        {
            int v = 0; foreach (var ch in d) { int k = Digits.IndexOf(ch); if (k < 0) return -1; v = v * 4 + k; }
            return v;
        }
        // remove every tag; id = the first one found (-1 if none)
        internal static string Strip(string txt, out int id)
        {
            id = -1;
            if (string.IsNullOrEmpty(txt) || txt.IndexOf(S) < 0) return txt;
            var m = Mark.Match(txt); if (m.Success) id = Decode(m.Groups[1].Value);
            return Mark.Replace(txt, "").Replace(S.ToString(), "");
        }
        internal static void LoadFile(string path)
        {
            E.Clear(); Rows = 0; Bad = 0;
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                var p = raw.Split('\t'); if (p.Length < 3 || !int.TryParse(p[0], out var id)) { Bad++; continue; }
                if (!E.TryGetValue(id, out var e)) { e = new Entry(); E[id] = e; }
                if (p[1] == "base") { e.Base = p[2]; e.Rx = Build(p[2], e.Toks); }
                else { if (!e.V.TryGetValue(p[1], out var l)) { l = new List<string>(); e.V[p[1]] = l; } l.Add(p[2]); }
                Rows++;
            }
        }
        static Regex Build(string b, List<string> toks)
        {
            toks.Clear();
            var sb = new StringBuilder("^\\s*"); int last = 0, g = 0;
            foreach (Match t in Tok.Matches(b))
            {
                sb.Append(Lit(b.Substring(last, t.Index - last)));
                sb.Append("(?<g" + g + ">.*?)"); toks.Add(t.Value); g++; last = t.Index + t.Length;
            }
            sb.Append(Lit(b.Substring(last))); sb.Append("\\s*$");
            try { return new Regex(sb.ToString(), RegexOptions.Singleline); } catch { return null; }
        }
        // a literal stretch of the template: {an} becomes the article the game writes ("a " or "an "), {upper} nothing
        static string Lit(string x) => Regex.Escape(x).Replace("\\{an}", "(?:an |a |An |A )?").Replace("\\{upper}", "");
        internal static bool Has(int id, string key) => E.TryGetValue(id, out var e) && e.V.ContainsKey(key);
        internal static int Count(string key) { int n = 0; foreach (var e in E.Values) if (e.V.ContainsKey(key)) n++; return n; }

        // the variant for key, its tokens filled from the shown text; null if none fits
        internal static string Fill(int id, string key, string shown, uint salt, out string why)
        {
            why = "";
            if (!E.TryGetValue(id, out var e) || e.Rx == null) { why = "no base"; return null; }
            if (!e.V.TryGetValue(key, out var list) || list.Count == 0) { why = "no variant"; return null; }
            var m = e.Rx.Match(shown);
            if (!m.Success) { why = "no match"; return null; }
            var vals = new Dictionary<string, string>(); var orig = new Dictionary<string, string>();
            for (int i = 0; i < e.Toks.Count; i++)
            {
                var k = e.Toks[i].ToLowerInvariant();
                if (!vals.ContainsKey(k)) { vals[k] = m.Groups["g" + i].Value; orig[k] = e.Toks[i]; }
            }
            var v = list[(int)(salt % (uint)list.Count)];
            bool ok = true;
            var res = Tok.Replace(v, t =>
            {
                var k = t.Value.ToLowerInvariant();
                if (!vals.TryGetValue(k, out var val)) { ok = false; return ""; }
                return Case(val, orig[k], t.Value);
            });
            if (!ok) { why = "token not in base"; return null; }
            return res;
        }

        // 0.12.0: the drawn token values for a marked line, or null if the shown text doesn't fit its base
        internal static Dictionary<string, string[]> Values(int id, string shown, out string why)
        {
            why = "";
            if (!E.TryGetValue(id, out var e) || e.Rx == null) { why = "no base"; return null; }
            var m = e.Rx.Match(shown);
            if (!m.Success) { why = "no match"; return null; }
            var vals = new Dictionary<string, string[]>();
            for (int i = 0; i < e.Toks.Count; i++)
            {
                var k = e.Toks[i].ToLowerInvariant();
                if (!vals.ContainsKey(k)) vals[k] = new[] { m.Groups["g" + i].Value, e.Toks[i] };
            }
            return vals;
        }
        // one version with its tokens filled; null if it uses a token the base doesn't have
        internal static string Apply(string v, Dictionary<string, string[]> vals)
        {
            bool ok = true;
            var res = Tok.Replace(v, t =>
            {
                var k = t.Value.ToLowerInvariant();
                if (!vals.TryGetValue(k, out var val)) { ok = false; return ""; }
                return Case(val[0], val[1], t.Value);
            });
            return ok ? res : null;
        }
        internal static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } h ^= h >> 15; h *= 0x2c1b3c6d; h ^= h >> 12; return h; }
        // every version of id under the first key of each chain that has any; the base wording is always a candidate ("" text)
        internal static List<KeyValuePair<string, string>> Candidates(int id, IEnumerable<string[]> chains, bool withBase)
        {
            var res = new List<KeyValuePair<string, string>>();
            if (!E.TryGetValue(id, out var e)) return res;
            foreach (var chain in chains)
                foreach (var k in chain)
                    if (e.V.TryGetValue(k, out var l) && l.Count > 0) { foreach (var v in l) res.Add(new KeyValuePair<string, string>(k, v)); break; }
            if (withBase && e.Base != null) res.Add(new KeyValuePair<string, string>("base", ""));
            return res;
        }
        // the least-used candidate (uses counted per save by the caller), ties broken by salt
        internal static int Pick(List<KeyValuePair<string, string>> c, Func<string, int> used, uint salt)
        {
            int best = -1, bu = int.MaxValue; uint bh = 0;
            for (int i = 0; i < c.Count; i++)
            {
                int u = used(c[i].Value); uint h = Hash(c[i].Value) ^ salt;
                if (u < bu || (u == bu && h < bh)) { best = i; bu = u; bh = h; }
            }
            return best;
        }
        // same token, different capital on its form (he / He): change the first letter of the drawn text to match
        static string Case(string val, string baseTok, string wantTok)
        {
            if (baseTok == wantTok || val.Length == 0) return val;
            int i = baseTok.IndexOf('-'), j = wantTok.IndexOf('-');
            if (i < 0 || j < 0 || i + 1 >= baseTok.Length || j + 1 >= wantTok.Length) return val;
            bool bu = char.IsUpper(baseTok[i + 1]), wu = char.IsUpper(wantTok[j + 1]);
            if (bu == wu) return val;
            int c = 0; while (c < val.Length && !char.IsLetter(val[c])) c++;   // skip link or colour markup
            if (c >= val.Length) return val;
            return val.Substring(0, c) + (wu ? char.ToUpperInvariant(val[c]) : char.ToLowerInvariant(val[c])) + val.Substring(c + 1);
        }
    }

}
