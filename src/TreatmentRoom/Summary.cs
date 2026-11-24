// FM27 Media Mod - Treatment Room: what the ledger says about this season (pure C#, tested offline).
// Shown in the panel on Season Summary (0.2.0). Every number comes from rows the game showed:
// Season Summary's list (injuries at the club), and each player's latest Risk Assessment row (SI's recurring injury, risk level).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class Summary
    {
        internal sealed class Line { public string Text = ""; public bool Quiet, Flag; public int Col; }   // Col 0 = the season, 1 = the players

        // design section 2 (respect): absences for personal or family reasons, which some injury mods add, are never counted,
        // listed or raised; nor is pregnancy (women's football)
        static readonly Regex PersonalRx = new Regex(@"\b(personal|family|compassionate|bereave\w*|paternity|maternity|pregnan\w*|leave of absence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal static bool Personal(string injury) => PersonalRx.IsMatch(injury ?? "");
        internal static IEnumerable<Ledger.Inj> Injuries() => Ledger.Injuries.Values.Where(i => !Personal(i.Injury));

        static readonly Regex DurRx = new Regex(@"^(\d+)\s*(day|days|week|weeks|month|months|year|years)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal static int Days(string outFor)
        {
            var m = DurRx.Match((outFor ?? "").Trim());
            return m.Success ? Parse.Days(m.Groups[1].Value, m.Groups[2].Value) : 0;
        }

        // body parts, most specific first; "muscle" = the soft-tissue ones
        static readonly string[] Parts = { "hamstring", "groin", "thigh", "calf", "abdominal", "achilles", "ankle", "knee", "foot", "toe", "hip", "back",
            "shoulder", "neck", "rib", "hand", "wrist", "arm", "elbow", "head", "face", "cheekbone", "nose", "eye", "shin", "leg" };
        internal static readonly HashSet<string> Muscle = new HashSet<string> { "hamstring", "groin", "thigh", "calf", "abdominal" };
        internal static string Part(string injury)
        {
            var i = (injury ?? "").ToLowerInvariant();
            foreach (var p in Parts) if (i.Contains(p)) return p;
            return "";
        }

        // the season the game date falls in: from the 1st of SeasonStartMonth (7 = July, the English season)
        internal static int SeasonStart(string gameDate, int startMonth)
        {
            var d = GameDate.Day(gameDate); if (d < 0) return -1;
            var p = gameDate.Split('/');
            int y = int.Parse(p[2], CultureInfo.InvariantCulture), m = int.Parse(p[1], CultureInfo.InvariantCulture);
            if (m < startMonth) y--;
            return GameDate.Day("1/" + startMonth + "/" + y);
        }

        static string Join(IEnumerable<string> items)
        {
            var l = items.ToList();
            if (l.Count == 0) return "";
            if (l.Count == 1) return l[0];
            return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        }
        static string Lower1(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        // 0.2.4: WHO TO WATCH, one row per player: at high or very high risk on the latest assessment, on the medical team's
        // recurring list, or injured three or more times at the club this season. Most at risk first.
        internal sealed class WatchRow { public string Who = "", Name = "", Recurring = "", Risk = "", Factor = "", FirstFlag = ""; public int Injuries, Days, Rank, FlagDays, AssessDays; }
        static int RiskRank(string r)
        {
            switch ((r ?? "").ToLowerInvariant()) { case "very high": return 0; case "high": return 1; case "increased": return 2; case "normal": return 3; case "low": return 4; case "very low": return 5; default: return 6; }
        }
        internal static List<WatchRow> Watch(string gameDate, int startMonth, int max)
        {
            int from = SeasonStart(gameDate, startMonth);
            var rows = Injuries().Where(i => ("," + i.Sources + ",").Contains(",season,") && (from < 0 || GameDate.Day(i.Date) >= from)).ToList();
            var inj = rows.GroupBy(i => i.Who).ToDictionary(g => g.Key, g => g.ToList());
            var risk = Ledger.LatestRisk();
            int last = risk.Count > 0 ? risk.Max(k => GameDate.Day(k.Date)) : -1;
            var byWho = new Dictionary<string, Ledger.Rk>();
            foreach (var k in risk) if (GameDate.Day(k.Date) == last) byWho[k.Who] = k;   // only the latest assessment day
            var who = new HashSet<string>(inj.Where(x => x.Value.Count >= 3).Select(x => x.Key));
            // 0.4.0: the department decides how far the list reaches (Dept.Reach): 2 adds players whose risk has stayed raised
            int reach = Dept.Reach(Dept.Now(Notes.StaffName("risk")));
            var prevBy = new Dictionary<string, Ledger.Rk>();
            if (reach >= 2)
            {
                int prev = Ledger.Risk.Values.Select(x => GameDate.Day(x.Date)).Where(d => d < last).DefaultIfEmpty(-1).Max();
                if (prev >= 0) foreach (var x in Ledger.Risk.Values) if (GameDate.Day(x.Date) == prev) prevBy[x.Who] = x;
            }
            foreach (var k in byWho.Values)
            {
                int rr = RiskRank(k.Risk);
                bool take = k.Recurring.Length > 0 || rr <= Math.Min(reach, 1)
                    || (reach >= 2 && rr == 2 && prevBy.TryGetValue(k.Who, out var pk) && RiskRank(pk.Risk) <= 2);
                if (take) who.Add(k.Who);
            }
            var list = new List<WatchRow>();
            foreach (var w in who)
            {
                var r = new WatchRow { Who = w };
                if (inj.TryGetValue(w, out var l)) { r.Injuries = l.Count; r.Days = l.Sum(i => Days(i.OutFor)); r.Name = l.OrderByDescending(i => i.Person.Length).First().Person; }
                if (byWho.TryGetValue(w, out var k)) { r.Recurring = k.Recurring; r.Risk = k.Risk; r.Factor = k.Factor; if (k.Person.Length > r.Name.Length) r.Name = k.Person; }
                if (Ledger.Squad.TryGetValue(w, out var q) && q.Name.Length > r.Name.Length) r.Name = q.Name;
                r.Rank = RiskRank(r.Risk);
                // memory: on how many assessment days this player was flagged high or very high, and since when
                var mine = Ledger.Risk.Values.Where(x => x.Who == w).OrderBy(x => GameDate.Day(x.Date)).ToList();
                r.AssessDays = mine.Select(x => x.Date).Distinct().Count();
                var flagged = mine.Where(x => RiskRank(x.Risk) <= 1).ToList();
                r.FlagDays = flagged.Select(x => x.Date).Distinct().Count();
                r.FirstFlag = flagged.Count > 0 ? flagged[0].Date : "";
                list.Add(r);
            }
            return list.OrderBy(r => r.Rank).ThenBy(r => r.Recurring.Length > 0 ? 0 : 1).ThenByDescending(r => r.Injuries).ThenByDescending(r => r.Days).ThenBy(r => r.Name)
                .Take(max).ToList();
        }

        internal static List<Line> Season(string gameDate, int startMonth)
        {
            var lines = new List<Line>();
            int from = SeasonStart(gameDate, startMonth);
            var rows = Injuries().Where(i => ("," + i.Sources + ",").Contains(",season,") && (from < 0 || GameDate.Day(i.Date) >= from)).ToList();
            if (rows.Count == 0)
            {
                lines.Add(new Line { Text = "Scroll the injury list below once and the Treatment Room will sum up the season here.", Quiet = true });
                return lines;
            }
            int n = rows.Count;
            var training = rows.Where(i => i.Received == "In training").ToList();
            var match = rows.Where(i => i.Received == "In match").ToList();
            int other = n - training.Count - match.Count;
            var where = new List<string>();
            if (training.Count > 0) where.Add(training.Count + " in training");
            if (match.Count > 0) where.Add(match.Count + (match.Count == 1 ? " in a match" : " in matches"));
            if (other > 0) where.Add(other + " non-football");
            lines.Add(new Line { Text = n + (n == 1 ? " injury" : " injuries") + " at the club this season: " + Join(where) + "." });

            int days = rows.Sum(i => Days(i.OutFor)), dTr = training.Sum(i => Days(i.OutFor)), dMa = match.Sum(i => Days(i.OutFor));
            if (days > 0)
            {
                if (dTr >= dMa && dTr > 0) lines.Add(new Line { Text = "Training injuries cost " + dTr + " of the " + days + " days lost.", Flag = dTr * 4 >= days * 3 });
                else if (dMa > 0) lines.Add(new Line { Text = "Match injuries cost " + dMa + " of the " + days + " days lost." });
            }

            var parts = rows.Select(i => Part(i.Injury)).Where(p => p.Length > 0).GroupBy(p => p).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
            int muscle = rows.Count(i => Muscle.Contains(Part(i.Injury)));
            int tight = rows.Count(i => i.Injury.StartsWith("A tight", StringComparison.OrdinalIgnoreCase));
            if (parts.Count > 0)
            {
                var top = parts.Take(3).Select(g => g.Key + " " + g.Count());
                var topMuscle = parts.Where(g => Muscle.Contains(g.Key)).Take(3).Select(g => g.Key + " " + g.Count());
                var t = muscle * 2 > n
                    ? muscle + " of the " + n + " are muscle injuries (" + string.Join(", ", topMuscle) + ")"
                    : "Most often: " + string.Join(", ", top);
                if (tight >= 2) t += "; " + tight + " were 'tight' niggles";
                lines.Add(new Line { Text = t + "." });
            }

            var groups = rows.GroupBy(i => i.Who).Select(g => new { Name = g.OrderByDescending(x => x.Person.Length).First().Person, N = g.Count() }).ToList();
            var three = groups.Where(g => g.N >= 3).OrderByDescending(g => g.N).ThenBy(g => g.Name).Take(5).Select(g => g.Name + " " + g.N).ToList();
            int twice = groups.Count(g => g.N == 2);
            if (three.Count > 0) lines.Add(new Line { Col = 1, Text = "Three or more injuries: " + string.Join(", ", three) + (twice > 0 ? "; " + twice + (twice == 1 ? " more player" : " more players") + " twice." : ".") });
            else if (twice > 0) lines.Add(new Line { Col = 1, Text = "Injured twice: " + Join(groups.Where(g => g.N == 2).OrderBy(g => g.Name).Take(5).Select(g => g.Name)) + (twice > 5 ? " and " + (twice - 5) + " more." : ".") });

            // SI's own Risk Assessment: recurring injuries, and who is at very high risk on the latest day it was read
            var risk = Ledger.LatestRisk();
            if (risk.Count == 0) lines.Add(new Line { Col = 1, Text = "Open Risk Assessment and the medical team's watch list of recurring problems will show here.", Quiet = true });
            else
            {
                var rec = risk.Where(k => k.Recurring.Length > 0).OrderBy(k => k.Person).Select(k => k.Person + " (" + Lower1(k.Recurring) + ")").ToList();
                lines.Add(new Line { Col = 1, Text = rec.Count > 0 ? "Recurring problems the medical team is watching: " + Join(rec) + "." : "The medical team has no recurring problems on its list.", Flag = rec.Count > 0 });
                var last = risk.Max(k => GameDate.Day(k.Date));
                var vh = risk.Where(k => GameDate.Day(k.Date) == last && k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.Person).ToList();
                if (vh.Count > 0)
                {
                    var date = vh[0].Date;
                    lines.Add(new Line { Col = 1, Text = (date == gameDate ? "Very high injury risk today: " : "Very high injury risk at the last assessment (" + date + "): ") + Join(vh.Select(k => k.Person + (k.Factor.Length > 0 ? " (" + k.Factor.ToLowerInvariant() + ")" : ""))) + ".", Quiet = true });
                }
            }
            return lines;
        }
    }
}
