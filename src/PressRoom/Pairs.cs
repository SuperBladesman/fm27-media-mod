// FM27 Media Mod - Press Room 0.13.6: the press notices your partnerships (Partnerships design, stage 5).
// The Partnerships plugin (a separate sister mod) writes "partnership press.tsv" in the mod's state folder: fresh stories
// about pairs SI recognises in YOUR side (split up or back together in the last match, newly recognised, starting to click,
// breaking down), with the pair, where they play and when, already in words. If that file is there, the press may open one
// question per conference with one of them, when the question names one of the pair or is about selection, the team or a
// part of the pitch. Each story is raised once. Every fact is one the game showed (the line-up, SI's own partnership lines);
// only the journalist's words are ours. Players are named, never he or she. Built the same way as Medical.cs (0.13.5).
// If Partnerships isn't installed, there is no file and nothing happens.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace FM27MediaMod
{
    internal static class Pairs
    {
        internal static bool Enabled = true;
        internal static int PerConference = 1;
        internal const string StateName = "press pairs.tsv";   // ids already raised (kept per save, Memory.Files)
        static string Dir => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
        static readonly Dictionary<string, int> Raised = new Dictionary<string, int>();
        static int TalkConf = -1, TalkThis;

        class It { public string Id = "", Kind = "", A = "", B = "", Zone = "", Date = "", When = "", Starts = "", Word = ""; }

        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }
        static string Last(string n) { var p = (n ?? "").Trim().Split(' '); return p[p.Length - 1]; }
        const RegexOptions RX = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        static readonly Regex Topic = new Regex(@"\b(selection|select\w*|team news|line-?up|starting|eleven|changes?|chang\w+|rotat\w*|partnership\w*|understanding|together|pairing\w*|paired|defen[cs]\w*|back (line|four|three|five)|centre-?backs?|central defen\w*|midfield\w*|attack\w*|strik\w*|forward\w*|shape|system|formation|tactic\w*|perform\w*|clean sheet\w*|solid)\b", RX);

        static readonly string[] Split =
        {
            "{A} and {B} didn't start together {zone} {when}.",
            "No {A} and {B} partnership {zone} {when}.",
            "{A} and {B} were split up {zone} {when}.",
        };
        static readonly string[] Reunited =
        {
            "{A} and {B} were paired {zone} again {when}.",
            "{A} and {B} were reunited {zone} {when}.",
        };
        static readonly string[] New =
        {
            "{A} and {B} seem to be building an understanding {zone}.",
            "There's an understanding forming between {A} and {B} {zone}.",
        };
        static readonly string[] Clicking =
        {
            "{A} and {B} are really starting to click {zone}.",
            "The partnership between {A} and {B} {zone} looks to be growing.",
        };
        static readonly string[] Telepathic =
        {
            "{A} and {B} look to know each other's game inside out {zone}.",
            "{A} and {B} {zone}: that looks like a partnership that barely needs words now.",
        };
        static readonly string[] Breaking =
        {
            "{A} and {B} haven't looked as comfortable together {zone} lately.",
            "Something seems to have gone between {A} and {B} {zone}.",
        };

        static List<It> Read()
        {
            var l = new List<It>();
            var p = Path.Combine(Dir, "partnership press.tsv");
            if (!File.Exists(p)) return l;
            bool first = true;
            foreach (var line in File.ReadAllLines(p))
            {
                if (first) { first = false; continue; }
                var a = line.Split('\t'); if (a.Length < 9) continue;
                l.Add(new It { Id = a[0], Kind = a[1], A = a[2], B = a[3], Zone = a[4], Date = a[5], When = a[6], Starts = a[7], Word = a[8] });
            }
            return l;
        }

        static string Fill(string t, It x) => t.Replace("{A}", x.A).Replace("{B}", x.B).Replace("{zone}", x.Zone).Replace("{when}", x.When);

        // the opening line for this question, or null; subject = the first of the pair, so the answer is kept on the record
        internal static string Talk(string q, string outlet, string journalist, PressRoom.Kind k, out string subject)
        {
            subject = null;
            try
            {
                if (!Enabled || PerConference <= 0) return null;
                int conf = PressRoom.Record.Conf;
                if (TalkConf != conf) { TalkConf = conf; TalkThis = 0; }
                if (TalkThis >= PerConference) return null;
                var items = Read();
                if (items.Count == 0) return null;
                var vq = PressRoom.Visible(q ?? "");
                bool topic = Topic.IsMatch(vq);
                It best = null;
                foreach (var x in items)   // the file is already in order: most pressing first
                {
                    if (Raised.ContainsKey(x.Id) || x.A.Length == 0 || x.B.Length == 0 || x.Zone.Length == 0) continue;
                    bool named = vq.IndexOf(Last(x.A), StringComparison.OrdinalIgnoreCase) >= 0 || vq.IndexOf(Last(x.B), StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!named && !topic) continue;
                    int chance = named ? 90 : 50;
                    if (k == PressRoom.Kind.Broadsheet && !named) chance /= 2;
                    if (Hash(x.Id + "|" + journalist + "|" + conf) % 100 >= (uint)chance) continue;
                    best = x; break;
                }
                if (best == null) return null;
                string[] pool;
                switch (best.Kind)
                {
                    case "split": pool = Split; break;
                    case "reunited": pool = Reunited; break;
                    case "new": pool = New; break;
                    case "clicking": pool = Clicking; break;
                    case "telepathic": pool = Telepathic; break;
                    case "breaking": pool = Breaking; break;
                    default: return null;
                }
                var line = Fill(pool[Hash(best.Id + outlet + conf) % (uint)pool.Length], best);
                if (line.IndexOf('{') >= 0) return null;   // a field was missing: say nothing rather than something broken
                Raised[best.Id] = conf; TalkThis++; Save();
                subject = best.A;
                PressRoom.W("pairs", "raised " + best.Kind + " " + k + " " + outlet + " / " + journalist, line);
                return line;
            }
            catch (Exception e) { PressRoom.Fail("pairs talk", e); return null; }
        }

        // ---------- state (per save) ----------
        internal static void Reset() { Raised.Clear(); TalkConf = -1; TalkThis = 0; }
        internal static void Load()
        {
            Raised.Clear();
            try
            {
                var p = Path.Combine(Dir, StateName);
                if (!File.Exists(p)) return;
                foreach (var line in File.ReadAllLines(p)) { var a = line.Split('\t'); if (a.Length >= 2 && int.TryParse(a[1], out var c)) Raised[a[0]] = c; }
            }
            catch (Exception e) { PressRoom.Fail("pairs load", e); }
        }
        static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in Raised) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                File.WriteAllText(Path.Combine(Dir, StateName), sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("pairs save", e); }
        }
    }
}
