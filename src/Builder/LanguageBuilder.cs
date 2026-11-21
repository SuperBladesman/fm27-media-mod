// FM27 Media Mod builder: the language file. Makes SI's English into an .ltf through SI's own template (example.ltf),
// exactly as 02 Tools/v0.17/build_persona_ltf.py + fix_possessive_ltf.py do with no persona, then applies the mod's
// language data (lang_lines.tsv: our own lines, small edits of SI's lines, team-talk tags, invisible markers) and
// appends the mod's new lines (lang_append.ltf). Checked byte for byte against the language built on Nik's PC.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod.Builder
{
    internal static class LanguageBuilder
    {
        static readonly Regex KeyRx = new Regex(@"^KEY-(\d+)(-[A-Z]+)?: (.*)$", RegexOptions.CultureInvariant);
        static readonly Regex KeyIdRx = new Regex(@"^KEY-(\d+)(?:-[A-Z]+)?:", RegexOptions.CultureInvariant);
        static readonly Regex Possessive = new Regex(@"(\[%[^\]]+\])\{s\}", RegexOptions.CultureInvariant);
        static string Esc(string x) => x.Replace("\r\n", "\n").Replace("\n", "\\n");

        sealed class Row { public string Tag = ""; public bool Mark; public char Kind; public uint Hash; public string Payload = ""; }

        public sealed class Result { public int Keys, ShippedOnly, Rows, Applied, EditsSkipped, Appended; }

        // english: SI's english.ltc bytes; template: example.ltf path; data: folder with lang_lines.tsv and lang_append.ltf
        public static Result Build(byte[] english, string template, string dataDir, string langName, string outFile)
        {
            var res = new Result();
            var eng = new Dictionary<int, string>(); var pc = new Dictionary<int, string>();
            foreach (var (id, flags, text) in Ltc.Read(english))
            {
                if (flags == 0x10) eng[id] = text;
                else if (flags == 0x110) pc[id] = text;
            }
            foreach (var kv in pc) if (!eng.ContainsKey(kv.Key)) eng[kv.Key] = kv.Value;

            var rows = ReadRows(Path.Combine(dataDir, "lang_lines.tsv"));
            res.Rows = rows.Count;
            var seen = new HashSet<int>(); var tbase = new Dictionary<int, string>();
            string cur = null; int curId = -1;

            var tmp = outFile + ".tmp";
            using (var w = new StreamWriter(tmp, false, new UTF8Encoding(true)) { NewLine = "\r\n" })
            {
                using (var r = new StreamReader(template, new UTF8Encoding(false), true))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.StartsWith("LANGNAME:", StringComparison.Ordinal)) { w.WriteLine("LANGNAME: " + langName); continue; }
                        var m = KeyRx.Match(line);
                        if (m.Success)
                        {
                            int i = int.Parse(m.Groups[1].Value); seen.Add(i); res.Keys++; curId = i;
                            string b = m.Groups[3].Value; int c = b.LastIndexOf("[COMMENT", StringComparison.Ordinal); if (c != -1) b = b.Substring(0, c);
                            bool variant = m.Groups[2].Success;
                            if (!variant) tbase[i] = b;
                            if (!variant) cur = eng.TryGetValue(i, out var e1) ? e1 : b;
                            else cur = eng.TryGetValue(i, out var e2) ? e2 : null;
                            if (cur == null) cur = tbase.TryGetValue(i, out var tb) ? tb : b;
                            w.WriteLine(line); continue;
                        }
                        if (line.StartsWith("STR-1:", StringComparison.Ordinal) && cur != null)
                        {
                            w.WriteLine("STR-1: " + Apply(curId, Fix(Esc(cur)), rows, res)); cur = null; continue;
                        }
                        // a KEY line that the template regex did not take still sets the id for the data (as the Python tools do)
                        var mk = KeyIdRx.Match(line); if (mk.Success) curId = int.Parse(mk.Groups[1].Value);
                        // a template STR line kept as it is still gets the possessive fix and the data
                        if (line.StartsWith("STR-1: ", StringComparison.Ordinal)) { w.WriteLine("STR-1: " + Apply(curId, Fix(line.Substring(7)), rows, res)); continue; }
                        w.WriteLine(line);
                    }
                }
                var extra = new List<int>();
                foreach (var k in eng.Keys) if (!seen.Contains(k)) extra.Add(k);
                extra.Sort();
                foreach (var i in extra)
                {
                    w.WriteLine(""); w.WriteLine("KEY-" + i + ": " + Esc(eng[i]));
                    w.WriteLine("STR-1: " + Apply(i, Fix(Esc(eng[i])), rows, res));
                }
                res.ShippedOnly = extra.Count;
                using (var a = new StreamReader(Path.Combine(dataDir, "lang_append.ltf"), new UTF8Encoding(false), true))
                {
                    string line;
                    while ((line = a.ReadLine()) != null) { w.WriteLine(line); res.Appended++; }
                }
            }
            if (File.Exists(outFile)) File.Delete(outFile);
            File.Move(tmp, outFile);
            return res;
        }

        static string Fix(string s) => s.IndexOf("{s}", StringComparison.Ordinal) >= 0 ? Possessive.Replace(s, "$1's") : s;

        static string Apply(int id, string si, Dictionary<int, Row> rows, Result res)
        {
            if (!rows.TryGetValue(id, out var r)) return si;
            string txt = si;
            if (r.Kind == 't') txt = r.Payload;
            else if (r.Kind == 'e')
            {
                if (Fnv32(si) == r.Hash) txt = Edit(si, r.Payload); else res.EditsSkipped++;
            }
            if (r.Tag.Length > 0) txt = r.Tag + " – " + txt;
            if (r.Mark) txt += Marker(id);
            res.Applied++;
            return txt;
        }

        static Dictionary<int, Row> ReadRows(string file)
        {
            var d = new Dictionary<int, Row>();
            foreach (var l in File.ReadLines(file, new UTF8Encoding(false)))
            {
                if (l.Length == 0 || l[0] == '#') continue;
                var p = l.Split('\t');
                if (p.Length < 6) throw new InvalidDataException("bad row: " + l);
                var r = new Row { Tag = p[1], Mark = p[2] == "1", Kind = p[3].Length > 0 ? p[3][0] : '\0',
                    Hash = p[4].Length > 0 ? Convert.ToUInt32(p[4], 16) : 0, Payload = string.Join("\t", p, 5, p.Length - 5) };
                d[int.Parse(p[0])] = r;
            }
            return d;
        }

        // keep / delete / insert over UTF-16 code units, separated by U+001F
        static string Edit(string si, string ops)
        {
            var sb = new StringBuilder(); int p = 0;
            foreach (var o in ops.Split('\u001f'))
            {
                if (o.Length == 0) continue;
                if (o[0] == '=') { int n = int.Parse(o.Substring(1)); sb.Append(si, p, n); p += n; }
                else if (o[0] == '-') p += int.Parse(o.Substring(1));
                else sb.Append(o, 1, o.Length - 1);
            }
            if (p != si.Length) throw new InvalidDataException("edit does not fit SI's line");
            return sb.ToString();
        }

        public static uint Fnv32(string s)
        {
            uint h = 0x811C9DC5;
            foreach (char c in s) { h ^= c; h *= 0x01000193; }
            return h;
        }

        // the Voices tag channel: U+2063, the id in base 4 as U+200B U+200C U+200D U+2060, U+2063
        static readonly char[] D = { '​', '‌', '‍', '⁠' };
        static string Marker(int i)
        {
            var d = new List<char>();
            while (true) { d.Add(D[i % 4]); i /= 4; if (i == 0) break; }
            d.Reverse();
            return "⁣" + new string(d.ToArray()) + "⁣";
        }
    }
}
