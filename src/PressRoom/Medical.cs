// FM27 Media Mod - Press Room 0.13.5: the press remembers medical calls (Treatment Room design 4.3).
// The Treatment Room (a separate plugin) writes "treatment press.tsv" in the mod's state folder: fresh, public medical
// stories about YOUR players (a recurring problem, a forecast overrun, a comeback, missing from training, a player not
// rested who then broke down), with the facts already in words. If that file is there, the press may open one question
// per conference with one of them, when the question names the player or is about fitness and injuries. Each story is
// raised once. Every fact is one the game showed; only the journalist's opening words are ours. Players are named,
// never he or she. If the Treatment Room isn't installed, there is no file and nothing happens.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace FM27MediaMod
{
    internal static class Medical
    {
        internal static bool Enabled = true;
        internal static int PerConference = 1;
        internal const string StateName = "press medical.tsv";   // ids already raised (kept per save, Memory.Files)
        static string Dir => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
        static readonly Dictionary<string, int> Raised = new Dictionary<string, int>();
        static int TalkConf = -1, TalkThis;

        class It { public string Id = "", Kind = "", Who = "", Player = "", Date = "", Area = "", Count = "", Month = "", Injury = "", Extra = ""; }

        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }
        static string Last(string n) { var p = (n ?? "").Trim().Split(' '); return p[p.Length - 1]; }
        static string Num(string n) { switch (n) { case "2": return "two"; case "3": return "three"; case "4": return "four"; case "5": return "five"; case "6": return "six"; default: return n; } }
        const RegexOptions RX = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        static readonly Regex Topic = new Regex(@"\b(injur\w*|fit|fitness|fully fit|knock\w*|medical|physio\w*|treatment|rest|rested|resting|return\w*|comeback|back in|niggle\w*|hamstring|groin|thigh|calf|ankle|knee|scan|strain\w*|absen\w*|missing|unavailable|available|availability|squad|selection|team news|workload|fatigue|tired)\b", RX);

        static readonly string[] Recurrence =
        {
            "That's {n} {area} problems for {P} since {month}.",
            "Another {area} problem for {P}: {n} since {month}.",
            "{P} has had {n} {area} injuries since {month}. Is something not being managed?",
        };
        static readonly string[] Overrun =
        {
            "{P} was expected back in {expected}. It's been {weeks} weeks.",
            "The word was {expected} for {S}. We're at {weeks} weeks now.",
        };
        static readonly string[] Comeback =
        {
            "{P} is back after {out} out with the {injury}.",
            "Good to see {S} back after {out}.",
        };
        static readonly string[] Training =
        {
            "{P} has been missing from training this week.",
            "{S} wasn't seen in training for a few days.",
        };
        static readonly string[] Pushed =
        {
            "{P} looked to be running on empty before the {injury}. Was {S} pushed too hard?",
            "There were signs {S} needed a break, and now it's the {injury}. Should {S} have been rested?",
        };

        // 0.13.7: the medical department (Treatment Room 0.5.2+): several players out at once, a department short of hands,
        // a head of department gone. Journalists ask; they never claim to know the club's staffing.
        static readonly string[] Crisis =
        {
            "That's {n} players out injured now, with {P} the latest. How worried are you?",
            "You've {n} players injured at the same time. Is something going wrong?",
        };
        static readonly string[] Stretched =
        {
            "That's {n} players out, with {P} the latest. Does the medical department have the people to cope?",
            "With {S} injured as well, you've {n} out. Is the club investing enough in the medical side?",
        };
        static readonly string[] StaffGone =
        {
            "{P} has left the medical department. How much of a loss is that?",
            "The club is without its {area} now that {P} has gone. Does that worry you?",
        };
        static readonly HashSet<string> Known = new HashSet<string> { "recurrence", "overrun", "comeback", "training", "pushed", "crisis", "stretched", "staffgone" };

        static List<It> Read()
        {
            var l = new List<It>();
            var p = Path.Combine(Dir, "treatment press.tsv");
            if (!File.Exists(p)) return l;
            bool first = true;
            foreach (var line in File.ReadAllLines(p))
            {
                if (first) { first = false; continue; }
                var a = line.Split('\t'); if (a.Length < 10) continue;
                l.Add(new It { Id = a[0], Kind = a[1], Who = a[2], Player = a[3], Date = a[4], Area = a[5], Count = a[6], Month = a[7], Injury = a[8], Extra = a[9] });
            }
            return l;
        }

        static string Fill(string t, It x) => t.Replace("{P}", x.Player).Replace("{S}", Last(x.Player)).Replace("{n}", Num(x.Count)).Replace("{area}", x.Area)
            .Replace("{month}", x.Month).Replace("{injury}", x.Injury).Replace("{expected}", x.Extra).Replace("{weeks}", x.Count).Replace("{out}", x.Extra);

        // the opening line for this question, or null; subject = the player, so the answer is kept on the record about them
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
                    if (Raised.ContainsKey(x.Id) || x.Player.Length == 0 || !Known.Contains(x.Kind)) continue;   // 0.13.7: an unknown kind no longer blocks the rest
                    bool named = vq.IndexOf(Last(x.Player), StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!named && !topic) continue;
                    int chance = named ? 90 : 60;
                    if (k == PressRoom.Kind.Broadsheet && !named) chance /= 2;
                    if (Hash(x.Id + "|" + journalist + "|" + conf) % 100 >= (uint)chance) continue;
                    best = x; break;
                }
                if (best == null) return null;
                string[] pool;
                switch (best.Kind)
                {
                    case "recurrence": pool = Recurrence; break;
                    case "overrun": pool = Overrun; break;
                    case "comeback": pool = Comeback; break;
                    case "training": pool = Training; break;
                    case "pushed": pool = Pushed; break;
                    case "crisis": pool = Crisis; break;
                    case "stretched": pool = Stretched; break;
                    case "staffgone": pool = StaffGone; break;
                    default: return null;
                }
                var line = Fill(pool[Hash(best.Id + outlet + conf) % (uint)pool.Length], best);
                if (line.IndexOf('{') >= 0) return null;   // a field was missing: say nothing rather than something broken
                Raised[best.Id] = conf; TalkThis++; Save();
                subject = best.Player;
                PressRoom.W("medical", "raised " + best.Kind + " " + k + " " + outlet + " / " + journalist, line);
                return line;
            }
            catch (Exception e) { PressRoom.Fail("medical talk", e); return null; }
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
            catch (Exception e) { PressRoom.Fail("medical load", e); }
        }
        static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in Raised) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                File.WriteAllText(Path.Combine(Dir, StateName), sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("medical save", e); }
        }
    }
}
