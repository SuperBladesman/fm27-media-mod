// FM27 Media Mod - Treatment Room: reading one table row (pure C#, no game types, so it can be tested offline).
// A row is the texts of one list item, in layout order. Each text is classified by what it looks like rather than by
// column position, because the medical tables differ (Season Summary, a player's history page, the league injury
// table, Recently Returned) and some cells hold one text or two (cause: "In training" + "Wear and tear").
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal sealed class Cell
    {
        public string Name = "", Text = "", PersonId = "", PersonName = "";
        public bool PersonCell;   // inside a player-name button (ShowPerson / player-name-button-template / CellButton)
    }

    internal sealed class Row
    {
        public string Kind = "", Club = "", PersonId = "", Person = "", Date = "", Injury = "", Severity = "", Received = "",
            Cause = "", Treatment = "", OutFor = "", Expected = "", FitnessTest = "", Status = "", Recommendation = "";
        public int MinDays = -1, MaxDays = -1;
        public string Tag = "";                          // Player Summary: the status tag before the name (Wnt, Lst, Lng, A21...)
        public int Count = -1, LastCount = -1, Pct = -1; // Player Summary: injuries this season, last season, % of the season missed
        // Risk Assessment (0.2.0): match load + its line, training load + its line, susceptibility, fatigue, SI's recurring injury, risk, main factor
        public string MatchLoad = "", MatchLine = "", TrainLoad = "", TrainLine = "", Suscept = "", Fatigue = "", Recurring = "", Risk = "", Factor = "";
        public readonly List<string> Extra = new List<string>();
        public string Who => PersonId.Length > 0 ? PersonId : "name:" + Person;
        public string Describe() =>
            $"{Kind} | {Person} [{PersonId}] | club={Club} | date={Date} | injury={Injury} | sev={Severity} | recv={Received} | cause={Cause} | " +
            $"treat={Treatment} | out={OutFor} | exp={Expected} ({MinDays}-{MaxDays}d) | fit={FitnessTest} | status={Status} | rec={Recommendation} | " +
            (Count >= 0 || Tag.Length > 0 ? $"tag={Tag} | count={Count} | last={LastCount} | pct={Pct} | " : "") + $"extra={string.Join(" / ", Extra)}";
    }

    internal static class Parse
    {
        static readonly RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        static readonly Regex DateRx = new Regex(@"^\d{1,2}/\d{1,2}/\d{4}$", I);
        static readonly Regex ShortDate = new Regex(@"^\d{1,2}/\d{2}$", I);
        static readonly Regex Sev = new Regex(@"^(Slight|Minor|Moderate|Major)$", I);
        static readonly Regex Recv = new Regex(@"^(In match|In training|Unknown)$", I);
        static readonly Regex Treat = new Regex(@"^(Physio|Specialist|Doctor|Surgery|Injection|Protective equipment|By (Physio|Specialist|Doctor|Injection|Surgery)|-)$", I);
        static readonly Regex Dur = new Regex(@"^(\d+)\s*(day|days|week|weeks|month|months|year|years)$", I);
        static readonly Regex Range = new Regex(@"^(\d+)\s*(day|days|week|weeks|month|months)?\s*-\s*(\d+)\s*(day|days|week|weeks|month|months)$", I);
        static readonly Regex Fit = new Regex(@"^(Not required|Not applicable|Required|Passed|Failed|Pending|Awaiting result)$", I);
        static readonly Regex StatusRx = new Regex(@"^(Important Player|Regular Starter|Squad Player|Fringe Player|Key Player|Star Player|First Team Player|Rotation Player|Backup|Emergency Backup|Hot Prospect|Decent Young Player|Youngster|Not Needed|Surplus to Requirements|Breakthrough Prospect|Senior Squad Player)$", I);
        static readonly Regex Count = new Regex(@"^\d+\s+Injur(y|ies)$", I);
        static readonly HashSet<string> Headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Date", "Player", "Injury", "Severity", "Cause", "Treatment", "Out For", "Received", "Time Out", "Date Injured", "Expected Return",
          "Recent Injury", "Fitness Test", "Recommendation", "Jump to Top", "Shp", "CON", "SHP", "Inf" };

        // one spelling for every date: Season Summary shows "5/11/2026", a history page "05/11/2026"
        internal static string NormDate(string t)
        {
            var p = (t ?? "").Trim().Split('/');
            if (p.Length != 3) return t ?? "";
            if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ||
                !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ||
                !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) return t;
            return d.ToString(CultureInfo.InvariantCulture) + "/" + m.ToString(CultureInfo.InvariantCulture) + "/" + y.ToString(CultureInfo.InvariantCulture);
        }

        static readonly Regex IntRx = new Regex(@"^(\d+)%?$", I);
        static readonly Regex BracketRx = new Regex(@"^\((\d+)\)$", I);
        static readonly Regex TagRx = new Regex(@"^[A-Za-z][A-Za-z0-9]{0,4}$", I);

        // Player Summary row (22 Nov): [status tag] | player | injuries this season | (last season) | time missed | % missed
        static Row Squad(List<Cell> cells)
        {
            var r = new Row { Kind = "squad" };
            bool seenPerson = false;
            foreach (var c in cells)
            {
                var t = (c.Text ?? "").Trim();
                if (t.Length == 0 || Headers.Contains(t)) continue;
                if ((c.PersonCell || c.PersonId.Length > 0) && r.Person.Length == 0)
                { r.PersonId = c.PersonId; r.Person = c.PersonName.Length > 0 ? c.PersonName : t; seenPerson = true; continue; }
                if (!seenPerson) { if (TagRx.IsMatch(t) && r.Tag.Length == 0) r.Tag = t; else r.Extra.Add(t); continue; }
                var m = BracketRx.Match(t);
                if (m.Success) { if (r.LastCount < 0) r.LastCount = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else r.Extra.Add(t); continue; }
                m = IntRx.Match(t);
                if (m.Success)
                {
                    var v = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (r.Count < 0 && r.OutFor.Length == 0) r.Count = v; else if (r.Pct < 0) r.Pct = v; else r.Extra.Add(t);
                    continue;
                }
                if (Dur.IsMatch(t) && r.OutFor.Length == 0) { r.OutFor = t; continue; }
                r.Extra.Add(t);
            }
            return r;
        }

        // Risk Assessment row (22 Nov): player | status | match load | "3 matches (270/270 minutes) in last 14 days" | training load |
        // "Team training only" | susceptibility | fatigue + recurring ("Exhausted\nNo recurring injuries", "Recurring tight hamstring") |
        // risk | main risk factor (none on the Normal and Low tabs)
        static readonly Regex RecurRx = new Regex(@"^(.*?)\s*(No recurring injuries|Recurring\s+(.+))$", I | RegexOptions.Singleline);
        static readonly Regex RiskRx = new Regex(@"^(Very High|High|Increased|Normal|Low|Very Low)$", I);
        internal static Row RiskRow(List<Cell> cells)
        {
            var r = new Row { Kind = "risk" };
            var t = new List<string>();
            foreach (var c in cells)
            {
                var x = (c.Text ?? "").Trim();
                if (x.Length == 0 || Headers.Contains(x)) continue;
                if ((c.PersonCell || c.PersonId.Length > 0) && r.Person.Length == 0) { r.PersonId = c.PersonId; r.Person = c.PersonName.Length > 0 ? c.PersonName : x; continue; }
                t.Add(x);
            }
            return RiskFrom(r, t);
        }
        // also used to convert 0.1.1's stored rows (status, first text, other texts)
        internal static Row RiskFrom(Row r, List<string> t)
        {
            r.Kind = "risk";
            if (t.Count < 8) { r.Extra.AddRange(t); return r; }
            r.Status = t[0]; r.MatchLoad = t[1]; r.MatchLine = t[2]; r.TrainLoad = t[3]; r.TrainLine = t[4]; r.Suscept = t[5];
            // the fatigue / recurring cell always says "recurring"; 0.1.1 filed a long one ("Unfit due to recent injury ...") elsewhere,
            // so a stored row can lack it: then the risk level comes straight after susceptibility
            if (!RecurRx.IsMatch(t[6]) && RiskRx.IsMatch(t[6])) t.Insert(6, "");
            if (t.Count < 8) { r.Extra.AddRange(t); return r; }
            var m = RecurRx.Match(t[6]);
            if (m.Success) { r.Fatigue = m.Groups[1].Value.Trim(); r.Recurring = m.Groups[3].Success ? m.Groups[3].Value.Trim() : ""; }
            else r.Fatigue = t[6];
            if (RiskRx.IsMatch(t[7])) r.Risk = t[7]; else r.Extra.Add(t[7]);
            if (t.Count > 8) r.Factor = t[8];
            for (int i = 9; i < t.Count; i++) r.Extra.Add(t[i]);
            return r;
        }

        internal static int Days(string n, string unit)
        {
            if (!int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return -1;
            var u = (unit ?? "").ToLowerInvariant();
            if (u.StartsWith("day")) return v;
            if (u.StartsWith("week")) return v * 7;
            if (u.StartsWith("month")) return v * 30;
            if (u.StartsWith("year")) return v * 365;
            return -1;
        }

        internal static Row Row(List<Cell> cells, string kind = "")
        {
            if (kind == "squad") return Squad(cells);
            if (kind == "risk") return RiskRow(cells);
            var r = new Row { Kind = kind };
            bool afterReceived = false;
            foreach (var c in cells)
            {
                var t = (c.Text ?? "").Trim();
                if (t.Length == 0 || Headers.Contains(t)) continue;
                if ((c.PersonCell || c.PersonId.Length > 0) && r.Person.Length == 0)
                {
                    r.PersonId = c.PersonId; r.Person = c.PersonName.Length > 0 ? c.PersonName : t; continue;
                }
                if (DateRx.IsMatch(t)) { if (r.Date.Length == 0) r.Date = NormDate(t); else r.Extra.Add(t); continue; }
                if (ShortDate.IsMatch(t)) { r.Extra.Add(t); continue; }
                if (Sev.IsMatch(t) && r.Severity.Length == 0) { r.Severity = t; continue; }
                if (Recv.IsMatch(t) && r.Received.Length == 0) { r.Received = t; afterReceived = true; continue; }
                if (Treat.IsMatch(t) && (t != "-" || r.Severity.Length > 0 || r.Received.Length > 0)) { if (r.Treatment.Length == 0) r.Treatment = t; afterReceived = false; continue; }
                var m = Range.Match(t);
                if (m.Success)
                {
                    var u1 = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[4].Value;
                    r.Expected = t; r.MinDays = Days(m.Groups[1].Value, u1); r.MaxDays = Days(m.Groups[3].Value, m.Groups[4].Value);
                    afterReceived = false; continue;
                }
                m = Dur.Match(t);
                if (m.Success) { if (r.OutFor.Length == 0) r.OutFor = t; else r.Extra.Add(t); afterReceived = false; continue; }
                if (Fit.IsMatch(t)) { r.FitnessTest = t; continue; }
                if (StatusRx.IsMatch(t)) { r.Status = t; continue; }
                if (Count.IsMatch(t)) { r.Extra.Add(t); continue; }
                if (afterReceived && r.Cause.Length == 0) { r.Cause = t; afterReceived = false; continue; }
                if (t.Length > 45) { if (r.Recommendation.Length == 0) r.Recommendation = t; else r.Extra.Add(t); continue; }
                if (r.Injury.Length == 0) { r.Injury = t; continue; }
                r.Extra.Add(t);
            }
            return r;
        }
    }
}
