// FM27 Media Mod - Treatment Room 0.6.2: SPORTS SCIENCE, the evidence behind the guidance (pure C#, tested offline).
// Nik (23 Nov): "Sports Science tab still a bit of a mess - this needs a rethink in the same way as the Guidance tab... Or split
// the content over the two appropriately?" Guidance says what to do; this page shows why: the latest assessment, workload,
// this season's injuries, how good the flags have been, and the full assessment. The sketch is the Design canvas
// "Treatment Room: guidance from the team", artboard "Sports science: the evidence".
// The numbers are the game's own (the risk assessment, the injury record); "what it shows" is read from them, and only a
// Good or better head of sports science (or one not met yet) reads that much into them. Players are named, never he or she.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FM27MediaMod
{
    internal static class Science
    {
        internal sealed class Bar { public string Label = "", Note = ""; public int Count, Total; public int Level; }   // Level: 0 hot, 1 mid, 2 quiet
        internal sealed class Month { public string Label = ""; public int Training, Match, Other, Days; }
        internal sealed class Page
        {
            public string Assessed = "", AgeText = "", Signer = "";
            public int Age = -1, Players, VeryHigh, High, Increased, Normal, Low;
            public string Thread = "";
            public List<Bar> MatchLoad = new List<Bar>(), TrainLoad = new List<Bar>();
            public string Individual = "";
            public List<string> LoadShows = new List<string>();
            public int Injuries, Training, Match, Other, Days, TrainingDays, MuscleN, Tight;
            public string MuscleParts = "", TightNote = "";
            public List<Month> Months = new List<Month>();
            public List<string> SeasonShows = new List<string>();
            public int FlagDays, Flagged, FlaggedHurt, Others, OthersHurt;
            public string FlagNote = "", FlagWhy = "";
            public bool Reads;   // the department reads "what it shows"
            public string ReadsNot = "";
            public List<string[]> Table = new List<string[]>();
            public List<int> TableLevel = new List<int>();
        }

        static int Day(string d) => GameDate.Day(d);
        static string Lower(string s) => (s ?? "").ToLowerInvariant();
        static string Surname(string n) { var p = (n ?? "").Trim().Split(' '); return p.Length == 0 ? n : p[p.Length - 1]; }
        static string Join(List<string> l) => l.Count == 0 ? "" : l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        static int Rank(string r) => Guidance.Rank(r);
        static string Num(int n) => Guidance.Num(n);
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static int SusRank(string s) { switch (Lower(s)) { case "very high": return 0; case "high": return 1; case "above average": return 2; case "average": return 3; case "below average": return 4; case "low": return 5; case "very low": return 6; default: return 7; } }
        static (int Matches, int Got, int Of) Minutes(Ledger.Rk k)
        {
            var m = System.Text.RegularExpressions.Regex.Match(k?.MatchLine ?? "", @"(\d+)\s+match(?:es)?\s*\((\d+)\s*/\s*(\d+)\s*minutes\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : (-1, -1, -1);
        }

        internal static Page Build(string today, string signer, int startMonth)
        {
            var p = new Page();
            int now = Day(today);
            var lvl = Dept.Now(signer);
            p.Reads = lvl.Sports < 0 || lvl.Sports >= 4;
            if (!p.Reads) p.ReadsNot = "Nobody here reads these numbers closely: a Good or better head of sports science would say what they show.";
            var author = Brief.ReportAuthor(signer);
            p.Signer = author != null ? author.Name + (Dept.IsBand(author.Sports) ? " · Sports Science: " + author.Sports : "") : signer;

            // ---------- the latest assessment ----------
            int last = Files.LatestRiskDay();
            var latest = last < 0 ? new List<Ledger.Rk>() : Ledger.Risk.Values.Where(k => Day(k.Date) == last).OrderBy(k => Rank(k.Risk)).ThenBy(k => k.Person).ToList();
            if (latest.Count > 0)
            {
                p.Assessed = latest[0].Date; p.Age = now >= 0 ? now - last : -1;
                p.AgeText = p.Age < 0 ? "" : p.Age == 0 ? "today" : p.Age == 1 ? "1 day old" : p.Age + " days old";
                p.Players = latest.Count;
                p.VeryHigh = latest.Count(k => Rank(k.Risk) == 0); p.High = latest.Count(k => Rank(k.Risk) == 1); p.Increased = latest.Count(k => Rank(k.Risk) == 2);
                p.Normal = latest.Count(k => Rank(k.Risk) == 3); p.Low = latest.Count(k => Rank(k.Risk) >= 4);
                var hot = latest.Where(k => Rank(k.Risk) <= 1).ToList();
                var common = hot.Where(k => k.Factor.Length > 0).GroupBy(k => Lower(k.Factor)).OrderByDescending(g => g.Count()).FirstOrDefault();
                var bits = new List<string>();
                if (common != null && common.Count() * 2 > hot.Count) bits.Add("The common thread: " + common.Key + ", behind " + common.Count() + " of the " + hot.Count + " at high or very high risk.");
                var ex = latest.Where(k => Lower(k.Fatigue).Contains("exhausted")).Select(k => Surname(k.Person)).ToList();
                var rec = latest.Count(k => k.Recurring.Length > 0);
                var more = new List<string>();
                if (ex.Count > 0) more.Add((ex.Count == 1 ? "One player" : Cap(Num(ex.Count)) + " players") + " exhausted (" + Join(ex) + ")");
                if (rec > 0) more.Add(Num(rec) + " recurring " + (rec == 1 ? "problem" : "problems"));
                if (more.Count > 0) bits.Add(Cap(string.Join("; ", more)) + ".");
                p.Thread = string.Join(" ", bits);

                // ---------- workload ----------
                foreach (var load in new[] { "Heavy", "Medium", "Light" })
                {
                    var g = latest.Where(k => k.MatchLoad.Equals(load, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (g.Count == 0) continue;
                    int hi = g.Count(k => Rank(k.Risk) <= 1);
                    p.MatchLoad.Add(new Bar { Label = load, Count = g.Count, Total = latest.Count, Level = hi > 0 && hi == g.Count ? 0 : hi > 0 ? 1 : 2,
                        Note = hi == g.Count && hi > 0 ? (g.Count == 1 ? "at high risk" : "all at high risk") : hi > 0 ? hi + " at high risk" : "none high" });
                }
                foreach (var load in latest.Select(k => k.TrainLoad).Where(t => t.Length > 0).Distinct().OrderBy(t => t == "Heavy" ? 0 : t == "Medium" ? 1 : 2))
                {
                    var g = latest.Where(k => k.TrainLoad == load).ToList();
                    p.TrainLoad.Add(new Bar { Label = load, Count = g.Count, Total = latest.Count, Level = 2, Note = g.Count <= 2 ? "(" + Join(g.Select(k => Surname(k.Person)).ToList()) + ")" : "" });
                }
                int ind = latest.Count(k => Lower(k.TrainLine).Contains("individual"));
                if (ind > 0) p.Individual = ind + (ind == 1 ? " player is" : " players are") + " also on individual training.";
                if (p.Reads)
                {
                    var heavy = latest.Where(k => k.MatchLoad.Equals("Heavy", StringComparison.OrdinalIgnoreCase)).ToList();
                    var light = latest.Where(k => k.MatchLoad.Equals("Light", StringComparison.OrdinalIgnoreCase)).ToList();
                    int heavyHot = heavy.Count(k => Rank(k.Risk) <= 1);
                    if (heavy.Count >= 3 && heavyHot == heavy.Count) p.LoadShows.Add("Every player on a heavy match load is at high or very high risk (" + heavyHot + " of " + heavy.Count + ")." + (light.Count > 0 && light.All(k => Rank(k.Risk) >= 2) ? " Nobody on a light load is above increased." : ""));
                    else if (heavy.Count >= 3 && heavyHot * 2 > heavy.Count) p.LoadShows.Add(heavyHot + " of the " + heavy.Count + " players on a heavy match load are at high or very high risk.");
                    // the same minutes, different risk: susceptibility makes the difference
                    var full = latest.Where(k => { var m = Minutes(k); return m.Of > 0 && m.Got == m.Of && m.Matches >= 2; }).ToList();
                    var safe = full.Where(k => Rank(k.Risk) >= 4).OrderByDescending(k => SusRank(k.Suscept)).FirstOrDefault();
                    var risky = full.Where(k => Rank(k.Risk) <= 1 && safe != null && Minutes(k).Got == Minutes(safe).Got).OrderBy(k => k.Fatigue.Length > 0 ? 1 : 0).ThenBy(k => SusRank(k.Suscept)).FirstOrDefault();   // no fatigue: susceptibility alone
                    if (safe != null && risky != null && SusRank(safe.Suscept) > SusRank(risky.Suscept))
                        p.LoadShows.Add("Susceptibility makes the difference at the same minutes: " + safe.Person + " has played all " + Minutes(safe).Got + " and is at low risk (susceptibility " + safe.Suscept + "); "
                            + risky.Person + " has played the same and is at " + Lower(risky.Risk) + " risk (" + risky.Suscept + ").");
                    if (light.Count >= 5 && heavyHot >= 3) p.LoadShows.Add(light.Count + " players are on a light load, so the minutes are there to share. Guidance has who to rest and who to rotate.");
                }
                // the full table
                foreach (var k in latest)
                {
                    var m = Minutes(k);
                    var factor = k.Factor.Length > 0 ? k.Factor + (Lower(k.Fatigue).Contains("exhausted") ? " · exhausted" : "") : (k.Fatigue.Length > 0 ? k.Fatigue : "-");
                    var train = k.TrainLoad + (Lower(k.TrainLine).Contains("individual") ? " + individual" : "");
                    p.Table.Add(new[] { k.Person, RiskWord(k.Risk), factor, k.MatchLoad.Length > 0 ? k.MatchLoad : "-", m.Of >= 0 ? m.Got + "/" + m.Of : "-", train.Length > 0 ? train : "-", k.Suscept.Length > 0 ? k.Suscept : "-" });
                    p.TableLevel.Add(Math.Min(3, Rank(k.Risk)));
                }
            }

            // ---------- this season's injuries ----------
            int from = Summary.SeasonStart(today, startMonth);
            var rows = Summary.Injuries().Where(i => ("," + i.Sources + ",").Contains(",season,") && (from < 0 || Day(i.Date) >= from)).ToList();
            p.Injuries = rows.Count;
            var training = rows.Where(i => i.Received == "In training").ToList(); var match = rows.Where(i => i.Received == "In match").ToList();
            p.Training = training.Count; p.Match = match.Count; p.Other = rows.Count - training.Count - match.Count;
            p.Days = rows.Sum(i => Summary.Days(i.OutFor)); p.TrainingDays = training.Sum(i => Summary.Days(i.OutFor));
            var parts = rows.Select(i => Summary.Part(i.Injury)).Where(x => x.Length > 0 && Summary.Muscle.Contains(x)).GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
            p.MuscleN = parts.Sum(g => g.Count()); p.MuscleParts = string.Join(" · ", parts.Take(3).Select(g => g.Key + " " + g.Count()));
            p.Tight = rows.Count(i => i.Injury.StartsWith("A tight", StringComparison.OrdinalIgnoreCase));
            if (p.Injuries > 0 && p.Tight > 0) p.TightNote = p.Tight * 3 >= p.Injuries ? "about a third of the season's injuries" : p.Tight * 4 >= p.Injuries ? "about a quarter of them" : "";
            foreach (var m in Files.Months(today, startMonth)) p.Months.Add(new Month { Label = m.Label, Training = m.Training, Match = m.Match, Other = m.Other, Days = m.Days });
            if (p.Reads && p.Injuries >= 5)
            {
                if (p.Training * 2 > p.Injuries && p.TrainingDays * 2 > p.Days)
                {
                    p.SeasonShows.Add("Training is where the injuries come from: " + p.Training + " of " + p.Injuries + ", and " + p.TrainingDays + " of the " + p.Days + " days lost." + (p.MuscleN * 2 > p.Injuries ? " Most are muscle problems" + (p.Tight >= 2 ? ", " + p.Tight + " of them 'tight' niggles." : ".") : ""));
                    p.SeasonShows.Add("Training workload is one of the factors the risk assessment names. Worth a look at training intensity: lighter individual training for the players on the Low condition card first.");
                }
                else if (p.Match * 2 > p.Injuries) p.SeasonShows.Add("Matches are where the injuries come from: " + p.Match + " of " + p.Injuries + ". Minutes and tactical intensity matter more than training here.");
            }

            // ---------- how good are the flags? ----------
            var days = Ledger.Risk.Values.Select(k => Day(k.Date)).Distinct().Where(x => x >= 0 && (now < 0 || x + 14 <= now)).ToList();
            p.FlagDays = days.Count;
            foreach (var dd in days)
                foreach (var k in Ledger.Risk.Values.Where(x => Day(x.Date) == dd))
                {
                    bool hurt = Summary.Injuries().Any(i => i.Who == k.Who && (("," + i.Sources + ",").Contains(",season,") || ("," + i.Sources + ",").Contains(",current,")) && Day(i.Date) > dd && Day(i.Date) <= dd + 14);
                    if (Rank(k.Risk) <= 1) { p.Flagged++; if (hurt) p.FlaggedHurt++; } else { p.Others++; if (hurt) p.OthersHurt++; }
                }
            if (Ledger.Risk.Count == 0) p.FlagNote = "No risk assessment on file yet: open Risk Assessment once.";
            else if (p.FlagDays == 0) p.FlagNote = "The first assessment can be checked 14 days after it was made.";
            else if (p.FlagDays < 3) p.FlagNote = "Too early to judge: " + (p.FlagDays == 1 ? "one assessment is one week's snapshot" : "two assessments are two snapshots") + ". Each new assessment adds to this record, and after a few the flags can be checked against what happened.";
            else
            {
                double a = p.Flagged == 0 ? 0 : (double)p.FlaggedHurt / p.Flagged, b = p.Others == 0 ? 0 : (double)p.OthersHurt / p.Others;
                p.FlagNote = a > b * 1.5 && p.FlaggedHurt >= 2 ? "The flags are working: flagged players have been injured more often than the rest."
                           : a <= b ? "So far the flags haven't picked out who gets injured: flagged players have been hurt no more often than the rest."
                           : "The flags lean the right way, but not by much yet.";
            }
            p.FlagWhy = "Sports Science sets how accurate these reports are." + (author != null && Dept.IsBand(author.Sports) ? " " + author.Name + " is " + author.Sports + ", so this is the record that should show it." : "");
            return p;
        }
        static string RiskWord(string r) { var t = (r ?? "").Trim(); return t.Length == 0 ? "-" : char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant(); }
    }
}
