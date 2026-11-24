// FM27 Media Mod - Partnerships 0.3.0: what the record says about each partnership (pure C#, tested offline on Nik's ledgers).
// Nik, 24 Nov, on the sketch "Partnerships: the new look": (1) results with and without, (4) who's next and who's coming through,
// (5) what the players say. Everything from the ledger the plugin already keeps; nothing new is read from the game here.
//   Results: the match screen's scoreboard, home team first ("SHU | 3 | - | 0 | MIL" at home, "BOL | 1 | - | 1 | SHU" away),
//   with the venue the plugin records. By who started: both, or one without the other.
//   Stand-ins: who has played in a partner's usual slot alongside the other, from the start or off the bench.
//   Coming through: neighbours in this XI the tactics pitch doesn't recognise yet, by starts together and rating when paired.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class PtInsight
    {
        // a club's short code on the scoreboard to its name (the plugin fills this from the fact sheet; the code itself otherwise)
        internal static Func<string, string> OppName = c => c;

        internal sealed class Result
        {
            public string Date = "", Venue = "", Opp = ""; public int For, Against; public char R;
            public string Score => For + "-" + Against;
            // "W 3-0 v Millwall", "L 1-5 at Southampton", "D 1-1 at home"
            public string Text => R + " " + Score + (Opp.Length > 0 ? (Venue == "home" ? " v " : " at ") + Opp : Venue == "home" ? " at home" : " away");
        }
        static readonly Regex Bug = new Regex(@"(?:^|\| )([A-Z][A-Z0-9]{1,4}) \| (\d{1,2}) \| - \| (\d{1,2}) \| ([A-Z][A-Z0-9]{1,4})(?= \||$)", RegexOptions.Compiled);

        internal static Result ResultOf(string date)
        {
            if (!PtLedger.Fixtures.TryGetValue(date, out var f)) return null;
            if (f.Venue != "home" && f.Venue != "away") return null;
            string home = f.Home, away = f.Away;
            bool ok = int.TryParse(f.HomeGoals, out int hg) & int.TryParse(f.AwayGoals, out int ag);
            if (!ok)
            {
                Match last = null;
                foreach (Match m in Bug.Matches(f.Raw ?? "")) last = m;
                if (last == null) return null;
                home = last.Groups[1].Value; away = last.Groups[4].Value;
                hg = int.Parse(last.Groups[2].Value, CultureInfo.InvariantCulture); ag = int.Parse(last.Groups[3].Value, CultureInfo.InvariantCulture);
                // keep what the scoreboard said in the fixture record (saved with the ledger)
                f.Home = home; f.Away = away; f.HomeGoals = hg.ToString(CultureInfo.InvariantCulture); f.AwayGoals = ag.ToString(CultureInfo.InvariantCulture); PtLedger.Dirty = true;
            }
            bool atHome = f.Venue == "home";
            var r = new Result { Date = date, Venue = f.Venue, For = atHome ? hg : ag, Against = atHome ? ag : hg };
            var opp = atHome ? away : home;
            var full = opp.Length > 0 ? (OppName(opp) ?? opp) : "";
            if (full.Length > 0 && full != opp) { if (atHome) f.Away = full; else f.Home = full; PtLedger.Dirty = true; }   // the club's name once known
            r.Opp = full;
            r.R = r.For > r.Against ? 'W' : r.For == r.Against ? 'D' : 'L';
            return r;
        }

        static List<string> Dates() => PtLedger.Matches.Select(m => m.Date).Distinct().OrderBy(d => PtDate.Day(d)).ToList();
        static bool Started(string d, string n) => PtLedger.Matches.Any(m => m.Date == d && m.Name == n && m.Started);
        internal static int TrackedMatches => Dates().Count;

        // ---------- (1) with and without ----------
        internal sealed class Side
        {
            public readonly List<(Result R, string Only)> Games = new List<(Result, string)>();
            public int N => Games.Count;
            public int Pts => Games.Sum(g => g.R.R == 'W' ? 3 : g.R.R == 'D' ? 1 : 0);
            public int W => Games.Count(g => g.R.R == 'W'); public int D => Games.Count(g => g.R.R == 'D'); public int L => Games.Count(g => g.R.R == 'L');
            public double PerGame(Func<Result, int> f) => N == 0 ? 0 : Games.Sum(g => f(g.R)) / (double)N;
            public string Ppg => N == 0 ? "-" : (Pts / (double)N).ToString("0.0", CultureInfo.InvariantCulture);
            public string Conceded => N == 0 ? "-" : PerGame(r => r.Against).ToString("0.0", CultureInfo.InvariantCulture);
            public string Scored => N == 0 ? "-" : PerGame(r => r.For).ToString("0.0", CultureInfo.InvariantCulture);
            public string Record => $"W {W}, D {D}, L {L}";
        }
        internal sealed class WithWithout { public Side Together = new Side(), Apart = new Side(); public int NoScore, Neither; }

        internal static WithWithout Compare(Pair p)
        {
            var w = new WithWithout();
            foreach (var d in Dates())
            {
                bool sa = Started(d, p.A), sb = Started(d, p.B);
                if (!sa && !sb) { w.Neither++; continue; }
                var r = ResultOf(d);
                if (r == null) { w.NoScore++; continue; }
                if (sa && sb) w.Together.Games.Add((r, "")); else w.Apart.Games.Add((r, sa ? p.A : p.B));
            }
            return w;
        }
        internal const int Verdict = 5;   // no verdict until each side has this many matches
        internal static bool TooEarly(WithWithout w) => w.Together.N < Verdict || w.Apart.N < Verdict;

        // ---------- 0.3.0 (Nik: "Are Mengi and Tang playing well when DCR and DCL vs. DCC and DCR?"): by position ----------
        // each arrangement the pair has been on the pitch together in (A's slot, B's slot): matches together, starts, the
        // pair's average rating in those matches (whole-match ratings) and the results when both started in it
        internal sealed class Arrangement
        {
            public string SlotA = "", SlotB = ""; public int Together, Starts; public double Rating; public int RatedN;
            public readonly List<Result> Results = new List<Result>();
            public string RatingText => RatedN > 0 ? Rating.ToString("0.00", CultureInfo.InvariantCulture) : "-";
        }
        internal static List<Arrangement> ByPosition(Pair p)
        {
            var by = new Dictionary<string, Arrangement>();
            foreach (var d in Dates())
            {
                var rows = PtLedger.Matches.Where(m => m.Date == d).ToList();
                foreach (var ra in rows.Where(m => m.Name == p.A))
                    foreach (var rb in rows.Where(m => m.Name == p.B))
                    {
                        if (ra.LastSnap < rb.FirstSnap || rb.LastSnap < ra.FirstSnap) continue;   // never on together in these slots
                        var key = ra.Slot + "|" + rb.Slot;
                        if (!by.TryGetValue(key, out var a)) { a = new Arrangement { SlotA = ra.Slot, SlotB = rb.Slot }; by[key] = a; }
                        a.Together++;
                        if (ra.Started && rb.Started) { a.Starts++; var r = ResultOf(d); if (r != null) a.Results.Add(r); }
                        if (double.TryParse(ra.Rating, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(rb.Rating, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && x > 0 && y > 0)
                        { a.Rating = (a.Rating * a.RatedN + (x + y) / 2) / (a.RatedN + 1); a.RatedN++; }
                    }
            }
            return by.Values.OrderByDescending(a => a.Together).ThenByDescending(a => a.Rating).ToList();
        }
        // "Mengi DCL, Tanganga DCR: 2 matches together (1 start), rated 6.83 · D 1-1 at home"
        internal static string ArrangementLine(Pair p, Arrangement a) =>
            p.A + " " + a.SlotA + ", " + p.B + " " + a.SlotB + ": " + (a.Together == 1 ? "1 match" : a.Together + " matches") + " together"
            + (a.Starts == 0 ? " (off the bench)" : a.Starts < a.Together ? " (" + (a.Starts == 1 ? "1 start" : a.Starts + " starts") + ")" : a.Starts == 1 ? " (started)" : " (all started)")
            + ", rated " + a.RatingText + (a.Results.Count > 0 ? " · " + string.Join(", ", a.Results.Select(r => r.Text)) : "");

        // ---------- (4) who's next: stand-ins for each partner ----------
        internal sealed class Stand { public string Name = ""; public readonly List<(string Date, bool Start, string Rating, Result R, string Early)> Times = new List<(string, bool, string, Result, string)>(); }

        // the slot a player usually starts in
        static string UsualSlot(string name) => PtLedger.Matches.Where(m => m.Name == name && m.Started).GroupBy(m => m.Slot).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

        // who has played in <miss>'s usual slot while <keep> was on the pitch
        internal static List<Stand> StandIns(string keep, string miss)
        {
            var by = new Dictionary<string, Stand>();
            var slot = UsualSlot(miss);
            if (slot == null) return new List<Stand>();
            foreach (var d in Dates())
            {
                var rows = PtLedger.Matches.Where(m => m.Date == d).ToList();
                var k = rows.FirstOrDefault(m => m.Name == keep); if (k == null) continue;
                int end = rows.Max(m => m.LastSnap);
                foreach (var m in rows.Where(m => m.Slot == slot && m.Name != miss && m.Name != keep))
                {
                    if (m.LastSnap < k.FirstSnap || k.LastSnap < m.FirstSnap) continue;   // never on together
                    if (!by.TryGetValue(m.Name, out var s)) { s = new Stand { Name = m.Name }; by[m.Name] = s; }
                    string early = "";
                    if (m.Started && end > 0 && m.LastSnap < end * 0.9) early = m.LastSnap >= end * 0.4 && m.LastSnap <= end * 0.6 ? "off at half time" : "off early";
                    s.Times.Add((d, m.Started, m.Rating, ResultOf(d), early));
                }
            }
            return by.Values.OrderByDescending(s => s.Times.Count(t => t.Start)).ThenByDescending(s => s.Times.Count).ToList();
        }

        static string Short(string date) { var p = (date ?? "").Split('/'); return p.Length == 3 ? p[0] + "/" + p[1] : date; }

        // "Kitching has started beside Tanganga (24/4, 7.4, W 3-0 v Millwall)", "Peck, on beside Phillips 3 times (6.6, 6.9, 6.5); never started there"
        internal static string Line(Stand s, string keep)
        {
            var starts = s.Times.Where(t => t.Start).ToList(); var subs = s.Times.Where(t => !t.Start).ToList();
            if (starts.Count > 0)
            {
                var parts = starts.Select(t => Short(t.Date) + ", " + t.Rating + (t.R != null ? ", " + t.R.Text : "") + (t.Early.Length > 0 ? ", " + t.Early : ""));
                return s.Name + " has started beside " + keep + " (" + string.Join("; ", parts) + ")" + (subs.Count > 0 ? ", and come on beside " + keep + " " + Times(subs.Count) : "") + ".";
            }
            return s.Name + ", on beside " + keep + " " + Times(subs.Count) + " (" + string.Join(", ", subs.Select(t => t.Rating)) + "); never started there.";
        }
        static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : n + " times";

        internal static List<string> WhoIsNext(string keep, string miss, int max = 2)
        {
            var l = StandIns(keep, miss).Take(max).Select(s => Line(s, keep)).ToList();
            return l;
        }

        // ---------- (4) coming through ----------
        internal sealed class Prospect { public PairView V; public string Zone = ""; public bool Fledgling => V.Step == -1; }
        internal static List<Prospect> ComingThrough(List<Slot> shape, string today)
        {
            var l = new List<Prospect>();
            if (shape == null) return l;
            foreach (var (a, b) in PtLedger.Neighbours(shape))
            {
                var key = string.CompareOrdinal(a.Name, b.Name) < 0 ? a.Name + "|" + b.Name : b.Name + "|" + a.Name;
                if (PtLedger.Pairs.TryGetValue(key, out var have) && (have.Si && have.Dropped.Length == 0 || have.Feel.Length > 0)) continue;   // recognised already
                var v = PtLedger.Transient(a.Name, b.Name, today);
                if (v.Starts == 0) continue;
                l.Add(new Prospect { V = v, Zone = ZoneOf(a, b) });
            }
            return l.OrderByDescending(p => p.Fledgling).ThenByDescending(p => p.V.Starts >= PtLedger.FledglingStarts ? p.V.RatingAvg : 0).ThenByDescending(p => p.V.Starts).ThenByDescending(p => p.V.RatingAvg).ToList();
        }
        // where on the pitch, in plain words, from the bands (0 GK, 1 defence, 2 wing-backs and holding midfield, 3 midfield, 4 attacking midfield, 5 attack)
        internal static string ZoneOf(Slot a, Slot b)
        {
            int lo = Math.Min(a.Band, b.Band), hi = Math.Max(a.Band, b.Band);
            string Part(int band) => band <= 1 ? "back" : band <= 3 ? "middle" : "attack";
            if (Part(lo) != Part(hi)) return Part(lo) + " to " + Part(hi);
            if (Math.Max(a.Lane, b.Lane) <= 1 && Math.Min(a.Lane, b.Lane) == 0) return "right side";   // lane 0 is the right touchline
            if (Math.Min(a.Lane, b.Lane) >= 3 && Math.Max(a.Lane, b.Lane) == 4) return "left side";
            return Part(lo) == "back" ? "at the back" : Part(lo) == "middle" ? "in the middle" : "in attack";
        }

        // ---------- (5) what the players say ----------
        internal sealed class Said { public Pair P; public string By = "", Other = "", Grade = "", Zone = "", Date = ""; public bool Warning, Hurting; }
        internal static List<Said> PlayersSay()
        {
            var l = new List<Said>();
            foreach (var p in PtLedger.Pairs.Values.Where(p => p.Feel.Length > 0))
            {
                var by = p.FeelBy; var other = by == p.A ? p.B : p.A;
                bool warn = p.Feel == "weak" || p.Feel == "very weak";
                bool hurt = warn && PtLedger.Sightings.Any(s => s.Contains("\t" + p.A + "\t" + p.B + "\t") && s.Contains("hurting the club"));
                l.Add(new Said { P = p, By = by, Other = other, Grade = p.Feel, Zone = p.FeelZone, Date = p.FeelDate, Warning = warn, Hurting = hurt });
            }
            return l.OrderByDescending(s => s.Warning).ThenByDescending(s => PtDate.Day(s.Date)).ToList();
        }
        internal static string SayLine(Said s) =>
            s.By + " feels " + (s.Grade == "weak" || s.Grade == "very weak" ? "a " : "there's a ") + s.Grade + " partnership " + s.Zone + " with " + s.Other
            + (s.Hurting ? ", and that it could be contributing to the club's underachievement" : "") + ".";

        // the recognised pairs (a line on the tactics pitch, or a player's own words)
        internal static bool Recognised(Pair p) => p.Si && p.Dropped.Length == 0 || p.Feel.Length > 0 && p.Feel != "weak" && p.Feel != "very weak";
    }
}
