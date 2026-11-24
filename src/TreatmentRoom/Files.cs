// FM27 Media Mod - Treatment Room: readings of the ledger and the notes (pure C#, tested offline).
// Every sentence restates something the game showed (Season Summary, history pages, Risk Assessment, Recently Returned,
// the league table, the inbox, the manager's own actions). Patterns are named as watch items, never as injuries or
// diagnoses. No "SI" in any text shown in the game; players are named, never he or she.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FM27MediaMod
{
    internal static class Files
    {
        internal sealed class Note2 { public string Date = ""; public string Text = ""; public bool Flag, Quiet; public int Order; }

        static int Day(string d) => GameDate.Day(d);
        internal static string DateOf(int day) { if (day < 0) return ""; var d = new DateTime(2000, 1, 1).AddDays(day); return d.Day + "/" + d.Month + "/" + d.Year; }
        static string Lower1(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
        static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);
        static string Join(IEnumerable<string> items)
        {
            var l = items.Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (l.Count == 0) return ""; if (l.Count == 1) return l[0];
            return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        }
        static bool AtClub(Ledger.Inj i) => ("," + i.Sources + ",").Contains(",season,") || ("," + i.Sources + ",").Contains(",current,");

        // ---------- people ----------
        internal static string Name(string who)
        {
            string best = "";
            if (Ledger.Squad.TryGetValue(who, out var q)) best = q.Name;
            foreach (var i in Ledger.Injuries.Values) if (i.Who == who && i.Person.Length > best.Length) best = i.Person;
            foreach (var k in Ledger.Risk.Values) if (k.Who == who && k.Person.Length > best.Length) best = k.Person;
            return best.Length > 0 ? best : who;
        }
        internal static Ledger.Rk LatestRisk(string who) => Ledger.Risk.Values.Where(k => k.Who == who).OrderByDescending(k => Day(k.Date)).FirstOrDefault();
        internal static int LatestRiskDay() => Ledger.Risk.Count == 0 ? -1 : Ledger.Risk.Values.Max(k => Day(k.Date));

        // the squad on Player Summary, most recently seen; with everyone else the ledger knows at the club this season
        internal sealed class PlayerRow { public string Who = "", Name = "", Tag = "", Risk = "", Factor = "", Recurring = "", Watch = "", Plan = ""; public int Season = -1, AtClub, Days; public bool InSquad; }
        internal static List<PlayerRow> Players(string today, int startMonth)
        {
            int from = Summary.SeasonStart(today, startMonth);
            var latestSeen = Ledger.Squad.Values.Select(q => Day(q.Seen)).DefaultIfEmpty(-1).Max();
            var who = new HashSet<string>();
            foreach (var kv in Ledger.Squad) if (latestSeen < 0 || Day(kv.Value.Seen) >= latestSeen - 30) who.Add(kv.Key);
            foreach (var i in Summary.Injuries()) if (AtClub(i) && Day(i.Date) >= from && !i.Who.StartsWith("name:")) who.Add(i.Who);
            var rows = new List<PlayerRow>();
            foreach (var w in who)
            {
                var r = new PlayerRow { Who = w, Name = Name(w) };
                if (Ledger.Squad.TryGetValue(w, out var q))
                {
                    r.Tag = q.Tag; r.InSquad = q.Seen.Length > 0 && (latestSeen < 0 || Day(q.Seen) >= latestSeen - 30);
                    if (int.TryParse(q.Count, out var c)) r.Season = c;
                }
                var mine = Summary.Injuries().Where(i => i.Who == w && AtClub(i) && Day(i.Date) >= from).ToList();
                r.AtClub = mine.Count; r.Days = mine.Sum(i => Summary.Days(i.OutFor));
                var k = LatestRisk(w); if (k != null && Day(k.Date) == LatestRiskDay()) { r.Risk = k.Risk; r.Factor = k.Factor; r.Recurring = k.Recurring; }
                var wn = Notes.OpenWatch(w); if (wn != null) r.Watch = wn.What.Length > 0 ? wn.What : "watching";
                var pn = Notes.OpenPlan(w); if (pn != null) r.Plan = Notes.PlanLabel(pn.What) + " (" + pn.Status + ")";
                rows.Add(r);
            }
            return rows.OrderBy(r => r.InSquad ? 0 : 1).ThenBy(r => r.Name).ToList();
        }

        // ---------- the screening note ----------
        internal sealed class Screening { public List<Summary.Line> Lines = new List<Summary.Line>(); public string Area = "", Reason = ""; public bool HasHistory; }
        internal static Screening Screen(string who, string today, int startMonth)
        {
            var s = new Screening();
            int now = Day(today), from = Summary.SeasonStart(today, startMonth);
            var all = Summary.Injuries().Where(i => i.Who == who).OrderBy(i => Day(i.Date)).ToList();
            s.HasHistory = all.Any(i => ("," + i.Sources + ",").Contains(",history,"));
            string name = Name(who);
            void Add(string t, bool flag = false, bool quiet = false) => s.Lines.Add(new Summary.Line { Text = t, Flag = flag, Quiet = quiet });

            if (all.Count == 0) Add("No injuries on file for " + name + ".");
            else
            {
                int majors = all.Count(i => i.Severity.Equals("Major", StringComparison.OrdinalIgnoreCase));
                int days = all.Sum(i => Summary.Days(i.OutFor));
                var first = all[0].Date.Split('/').LastOrDefault() ?? "";
                Add($"On file: {Plural(all.Count, "injury", "injuries")} since {first}" + (majors > 0 ? $" ({majors} major)" : "") + (days > 0 ? $"; {days} days out where the length is known." : "."));
                var parts = all.Select(i => Summary.Part(i.Injury)).Where(p => p.Length > 0).GroupBy(p => p).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(5).ToList();
                if (parts.Count > 0) Add("By area: " + string.Join(", ", parts.Select(g => g.Key + " " + g.Count())) + ".");
                var club = all.Where(i => AtClub(i) && Day(i.Date) >= from).ToList();
                if (club.Count > 0)
                {
                    int tr = club.Count(i => i.Received == "In training"), ma = club.Count(i => i.Received == "In match");
                    var where = new List<string>(); if (tr > 0) where.Add(tr + " in training"); if (ma > 0) where.Add(ma + (ma == 1 ? " in a match" : " in matches")); if (club.Count - tr - ma > 0) where.Add((club.Count - tr - ma) + " non-football");
                    Add($"This season at the club: {Plural(club.Count, "injury", "injuries")}, {club.Sum(i => Summary.Days(i.OutFor))} days out ({Join(where)}).");
                }
                var causes = all.Select(i => CauseOf(i.Cause)).Where(c => c.Length > 0).GroupBy(c => c).OrderByDescending(g => g.Count()).Take(4).ToList();
                if (causes.Count > 0) Add("Causes on record: " + string.Join(", ", causes.Select(g => g.Key + " " + g.Count())) + ".");
                var last = all[all.Count - 1];
                int ago = now >= 0 && Day(last.Date) >= 0 ? now - Day(last.Date) : -1;
                Add("Last injury: " + Lower1(last.Injury) + " on " + last.Date + (last.OutFor.Length > 0 ? " (" + last.OutFor + ")" : "") + (ago >= 0 ? ", " + Plural(ago, "day", "days") + " ago." : "."));
            }
            if (Ledger.Squad.TryGetValue(who, out var q) && q.Count.Length > 0)
                Add($"This season, all clubs: {Plural(int.Parse(q.Count, CultureInfo.InvariantCulture), "injury", "injuries")} (last season {(q.Last.Length > 0 ? q.Last : "?")}), {q.Missed} missed" + (q.Pct.Length > 0 ? $", {q.Pct}% of the season." : "."));

            var k = LatestRisk(who);
            if (k != null)
            {
                if (k.Recurring.Length > 0) Add("The medical team lists a recurring " + k.Recurring + ".", flag: true);
                var bits = new List<string>();
                bits.Add(k.Risk.ToLowerInvariant() + " risk" + (k.Factor.Length > 0 ? ", main factor " + k.Factor.ToLowerInvariant() : ""));
                if (k.MatchLoad.Length > 0) bits.Add("match load " + k.MatchLoad.ToLowerInvariant() + (k.MatchLine.Length > 0 ? " (" + k.MatchLine + ")" : ""));
                if (k.TrainLoad.Length > 0) bits.Add("training load " + k.TrainLoad.ToLowerInvariant() + (k.TrainLine.Length > 0 ? " (" + Lower1(k.TrainLine) + ")" : ""));
                if (k.Suscept.Length > 0) bits.Add("injury susceptibility " + k.Suscept.ToLowerInvariant());
                if (k.Fatigue.Length > 0) bits.Add(Lower1(k.Fatigue));
                Add("Latest assessment (" + k.Date + "): " + string.Join("; ", bits) + ".", flag: k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase));
            }
            else Add("No risk assessment on file yet: open Risk Assessment once.", quiet: true);

            // the watch item: the medical team's recurring injury first, then an area hit twice or more in a year
            if (k != null && k.Recurring.Length > 0) { s.Area = Summary.Part(k.Recurring); if (s.Area.Length == 0) s.Area = k.Recurring; s.Reason = "recurring " + k.Recurring + " (medical team)"; }
            else
            {
                var year = all.Where(i => now >= 0 && Day(i.Date) > now - 365).Select(i => Summary.Part(i.Injury)).Where(p => p.Length > 0).GroupBy(p => p).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (year != null) { s.Area = year.Key; s.Reason = year.Count() + " " + year.Key + " injuries in the last 12 months"; }
            }
            // 0.5.0: screening needs physiotherapy: a department led by a Reasonable or Unsuited physio suggests nothing
            var au = Brief.BriefingAuthor();
            if (au != null && Dept.Band(au.Physio) >= 0 && Dept.Band(au.Physio) <= 1 && s.Area.Length > 0)
            { s.Area = ""; s.Reason = ""; Add("No watch item suggested: screening for one needs better physiotherapy than the department has.", quiet: true); }
            var w = Notes.OpenWatch(who);
            if (w != null) Add("Watch item since " + w.Since + ": " + (w.What.Length > 0 ? w.What : "general") + (w.Detail.Length > 0 ? " (" + w.Detail + ")" : "") + ".", flag: true);
            else if (s.Area.Length > 0) Add("Suggested watch item: " + s.Area + " (" + s.Reason + ").", flag: true);

            foreach (var p in Notes.All.Where(n => n.Kind == "plan" && n.Who == who).OrderByDescending(n => Day(n.Since)).Take(3))
                Add(PlanLine(p), quiet: p.Status == "cancelled" || p.Status == "replaced");
            foreach (var v in Notes.All.Where(n => n.Kind == "advice" && n.Who == who).OrderByDescending(n => Day(n.Since)).Take(2))
                Add(AdviceLine(v), flag: v.Status == "not taken" && v.Detail.Length > 0);
            var fc = Ledger.Forecasts.Values.Where(f => f.Who == who).ToList();
            var checkedF = fc.Where(f => f.Returned.Length > 0).Select(Verdict).ToList();
            if (checkedF.Count > 0) Add("Forecasts checked: " + checkedF.Count + " (" + string.Join(", ", checkedF.GroupBy(v => v.Kind).Select(g => g.Count() + " " + g.Key)) + ").");
            var openF = fc.Where(f => f.Returned.Length == 0).OrderByDescending(f => Day(f.Date)).FirstOrDefault();
            if (openF != null) Add("Out now: " + Lower1(openF.Injury) + " since " + openF.Date + ", expected back in " + openF.Expected + (now >= 0 ? " (" + Plural(now - Day(openF.Date), "day", "days") + " so far)." : "."), flag: true);
            if (!s.HasHistory) Add("Open " + name + "'s injury history (Profile > Performance) to add the full record from before this season.", quiet: true);
            return s;
        }
        static string CauseOf(string c)
        {
            if (string.IsNullOrEmpty(c)) return "";
            var i = c.IndexOf(" vs ", StringComparison.OrdinalIgnoreCase);
            var t = i == 0 || c.StartsWith("vs ", StringComparison.OrdinalIgnoreCase) ? "" : i > 0 ? c.Substring(0, i) : c;
            return Lower1(t.Trim());
        }
        internal static string PlanLine(Note p)
        {
            var label = Notes.PlanLabel(p.What);
            switch (p.Status)
            {
                case "requested": return label + " requested on " + p.Since + "; not carried out yet." + (p.Detail.Length > 0 ? " Reason: " + p.Detail + "." : "");
                case "carried out": return label + " requested on " + p.Since + ", carried out on " + p.Done + ".";
                case "reviewed": return label + " from " + p.Done + ": " + p.Detail + ".";
                case "cancelled": return label + " requested on " + p.Since + ", cancelled.";
                case "replaced": return label + " requested on " + p.Since + ", replaced by a later plan.";
            }
            return label + ": " + p.Status + ".";
        }
        internal static string AdviceLine(Note v)
        {
            var t = "The medical team suggested resting " + v.Name + " on " + v.Since;
            if (v.Status == "taken") return t + "; rested on " + v.Done + ".";
            if (v.Status == "not taken") return t + "; not rested" + (v.Detail.Length > 0 ? ", then " + v.Detail : "") + ".";
            return t + ".";
        }

        // ---------- the physio's record ----------
        internal sealed class FcVerdict { public Ledger.Fc F; public string Kind = ""; public int Actual, Over; }
        internal static FcVerdict Verdict(Ledger.Fc f)
        {
            var v = new FcVerdict { F = f };
            int actual = Day(f.Returned) - Day(f.Date);
            int.TryParse(f.Min, out var min); int.TryParse(f.Max, out var max);
            v.Actual = actual;
            if (actual < 0 || max <= 0) { v.Kind = "unclear"; return v; }
            if (actual < min) v.Kind = "early";
            else if (actual <= max) v.Kind = "within range";
            else { v.Kind = "late"; v.Over = actual - max; }
            return v;
        }
        internal static List<Summary.Line> PhysioSummary(string today)
        {
            var l = new List<Summary.Line>();
            var done = Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length > 0).Select(Verdict).Where(v => v.Kind != "unclear").ToList();
            var open = Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0).ToList();
            if (done.Count == 0 && open.Count == 0)
            {
                l.Add(new Summary.Line { Text = "No forecasts on file yet. They are read while a player is injured (Current Injuries and the league injury table) and checked when the player shows up on Recently Returned.", Quiet = true });
                return l;
            }
            if (done.Count > 0)
            {
                int w = done.Count(v => v.Kind == "within range"), e = done.Count(v => v.Kind == "early"), lt = done.Count(v => v.Kind == "late");
                l.Add(new Summary.Line { Text = $"Forecasts checked: {done.Count}. Within range {w}, early {e}, late {lt}." });
                if (lt > 0) l.Add(new Summary.Line { Text = "When late, " + Math.Round(done.Where(v => v.Kind == "late").Average(v => v.Over)) + " days over on average.", Flag = lt * 3 >= done.Count });
                l.Add(new Summary.Line { Text = "Back-by dates are the first day a player was seen on Recently Returned, so the real return may be a little earlier.", Quiet = true });
            }
            // 0.5.1: by the head physio of the day: how often each one's estimates held, and how wide they were
            var byPhysio = done.Where(v => v.F.By.Length > 0).GroupBy(v => v.F.By).ToList();
            foreach (var g in byPhysio.OrderByDescending(g => g.Count()))
            {
                int w = g.Count(v => v.Kind == "within range");
                var widths = g.Select(v => { int.TryParse(v.F.Min, out var a); int.TryParse(v.F.Max, out var b); return b - a; }).Where(x => x >= 0).ToList();
                var m = Dept.Staff.Values.FirstOrDefault(x => x.Name == g.Key);
                var who = g.Key + (m != null && Dept.IsBand(m.Physio) ? " (Physiotherapy " + m.Physio + ")" : "");
                l.Add(new Summary.Line { Text = "Under " + who + ": " + g.Count() + " checked, " + w + " within range" + (widths.Count > 0 ? "; ranges " + Math.Round(widths.Average(), MidpointRounding.AwayFromZero) + " days wide on average." : ".") });
            }
            if (done.Count > 0 && byPhysio.Count == 0) l.Add(new Summary.Line { Text = "Forecasts are credited to the head physio from the day the department is read (Club > Staff > Medical Team).", Quiet = true });
            if (open.Count > 0) l.Add(new Summary.Line { Text = Plural(open.Count, "forecast", "forecasts") + " still open: " + Join(open.OrderBy(f => Day(f.Date)).Select(f => f.Person + " (" + f.Expected + ")")) + "." });
            return l;
        }

        // ---------- the medical diary ----------
        static readonly Dictionary<string, string> ActionWords = new Dictionary<string, string>
        {
            { "PersonRestTrainingSchedule", "Rested from training" }, { "PersonCancelRestTrainingSchedule", "Rest cancelled" },
            { "PersonRequestInjuryRiskUpdate", "Asked for a risk update" }, { "PersonRequestTrainingReport", "Asked for a training report" },
            { "PersonRequestPhysioReport", "Asked for a physio report" }, { "PersonTrainingIntensityRest", "Training intensity set to rest" },
            { "TalkToPlayerAboutInjury", "Talked about the injury" }, { "RestPlayerConversation", "Talked about a rest" },
            { "TeamSetRecommendedTrainingIntensity", "Team set to the recommended training intensity" }, { "PersonTrainingFocus", "Training focus changed" },
            { "PlaceMedicalJobAdvert", "Advertised a medical job" },
        };
        static string NameIn(string who)
        {
            if (string.IsNullOrEmpty(who)) return "";
            var i = who.IndexOf(" [", StringComparison.Ordinal); return i > 0 ? who.Substring(0, i) : who;
        }
        internal static List<Note2> Diary(string today, int startMonth, int max)
        {
            int from = Summary.SeasonStart(today, startMonth);
            var d = new List<Note2>(); int o = 0;
            void Add(string date, string t, bool flag = false, bool quiet = false) { if (Day(date) >= from) d.Add(new Note2 { Date = date, Text = t, Flag = flag, Quiet = quiet, Order = o++ }); }
            foreach (var i in Summary.Injuries().Where(AtClub))
            {
                var bits = new List<string>(); if (i.Severity.Length > 0) bits.Add(i.Severity.ToLowerInvariant()); if (i.Received.Length > 0) bits.Add(i.Received.ToLowerInvariant()); if (i.OutFor.Length > 0) bits.Add(i.OutFor);
                Add(i.Date, "Injured: " + i.Person + ", " + Lower1(i.Injury) + (bits.Count > 0 ? " (" + string.Join(", ", bits) + ")" : "") + ".", flag: i.Severity.Equals("Major", StringComparison.OrdinalIgnoreCase));
            }
            foreach (var f in Ledger.Forecasts.Values.Where(f => f.Ours != "n"))
            {
                Add(f.FirstSeen, "Forecast: " + f.Person + ", " + Lower1(f.Injury) + ", expected back in " + f.Expected + ".");
                if (f.Returned.Length > 0) { var v = Verdict(f); Add(f.Returned, "Back: " + f.Person + " after " + v.Actual + " days (" + v.Kind + (v.Kind == "late" ? ", " + v.Over + " over" : "") + ")."); }
            }
            foreach (var r in Notes.All.Where(n => n.Kind == "returned"))
                Add(r.Since, "Recently returned: " + r.Name + " after " + Lower1(r.What) + (r.Status.Length > 0 ? "; fitness test " + r.Status.ToLowerInvariant() : "") + (r.Detail.Length > 0 ? ". " + r.Detail : "") + ".");
            foreach (var a in Ledger.ActionRows())
            {
                if (a.Length < 6) continue;
                var words = ActionWords.TryGetValue(a[3], out var wd) ? wd : a[3];
                var idm = System.Text.RegularExpressions.Regex.Match(a[5], @"\[([0-9a-fA-F]{6,10})\]");
                var nm = idm.Success ? Name(idm.Groups[1].Value) : NameIn(a[5]);
                Add(a[0], words + (nm.Length > 0 && !a[3].StartsWith("Team", StringComparison.Ordinal) && a[3] != "PersonRequestInjuryRiskUpdate" ? ": " + nm : "") + ".");
            }
            foreach (var f in Ledger.OfferRows())
            {
                if (f.Length < 3) continue;
                var t = f[2]; if (t.Length > 170) t = t.Substring(0, 167).TrimEnd() + "...";
                Add(f[0], "From the medical team: " + t, flag: true);
            }
            foreach (var n in Notes.All)
            {
                if (n.Kind == "watch") { Add(n.Since, "Watch item: " + n.Name + (n.What.Length > 0 ? " (" + n.What + ")" : "") + "."); if (n.Status == "closed") Add(n.Done, "Watch item closed: " + n.Name + ".", quiet: true); }
                else if (n.Kind == "plan")
                {
                    Add(n.Since, Notes.PlanLabel(n.What) + " requested for " + n.Name + ".");
                    if (n.Status == "carried out" || n.Status == "reviewed") Add(n.Done, Notes.PlanLabel(n.What) + " carried out for " + n.Name + ".");
                    if (n.Status == "reviewed") Add(DateOf(Day(n.Done) + Math.Max(1, n.Days)), "Review, " + n.Name + ": " + n.Detail + ".");
                }
                else if (n.Kind == "advice") Add(n.Since, "The medical team suggested resting " + n.Name + (n.Status == "taken" ? "; rested on " + n.Done : n.Status == "not taken" ? "; not rested" + (n.Detail.Length > 0 ? ", then " + n.Detail : "") : "") + ".", flag: n.Status == "not taken" && n.Detail.Length > 0);
            }
            return d.OrderByDescending(x => Day(x.Date)).ThenByDescending(x => x.Order).Take(max).ToList();
        }

        // ---------- sports science ----------
        static readonly string[] Levels = { "Very High", "High", "Increased", "Normal", "Low", "Very Low" };
        internal static List<Summary.Line> Sports(string today, int startMonth)
        {
            var l = new List<Summary.Line>();
            void Add(string t, bool flag = false, bool quiet = false) => l.Add(new Summary.Line { Text = t, Flag = flag, Quiet = quiet });
            int last = LatestRiskDay();
            var risk = Ledger.Risk.Values.Where(k => Day(k.Date) == last).ToList();
            var by = Notes.StaffName("risk");
            if (risk.Count == 0) { Add("No risk assessment on file yet: open Risk Assessment once.", quiet: true); }
            else
            {
                Add("Latest assessment (" + DateOf(last) + (by.Length > 0 ? ", " + by : "") + "): " + Plural(risk.Count, "player", "players") + ".");
                Add("Risk: " + string.Join(", ", Levels.Select(v => (v, n: risk.Count(k => k.Risk.Equals(v, StringComparison.OrdinalIgnoreCase)))).Where(x => x.n > 0).Select(x => x.v.ToLowerInvariant() + " " + x.n)) + ".");
                var f = risk.Where(k => k.Factor.Length > 0).GroupBy(k => k.Factor.ToLowerInvariant()).OrderByDescending(g => g.Count()).ToList();
                if (f.Count > 0) Add("Main risk factors: " + string.Join(", ", f.Select(g => g.Key + " " + g.Count())) + ".", flag: true);
                var ml = risk.Where(k => k.MatchLoad.Length > 0).GroupBy(k => k.MatchLoad.ToLowerInvariant()).OrderByDescending(g => g.Count()).ToList();
                var tl = risk.Where(k => k.TrainLoad.Length > 0).GroupBy(k => k.TrainLoad.ToLowerInvariant()).OrderByDescending(g => g.Count()).ToList();
                if (ml.Count > 0) Add("Match load: " + string.Join(", ", ml.Select(g => g.Key + " " + g.Count())) + ". Training load: " + string.Join(", ", tl.Select(g => g.Key + " " + g.Count())) + ".");
                var ex = risk.Where(k => k.Fatigue.IndexOf("Exhausted", StringComparison.OrdinalIgnoreCase) >= 0).Select(k => k.Person).ToList();
                if (ex.Count > 0) Add("Exhausted: " + Join(ex) + ".", flag: true);
                var rec = risk.Where(k => k.Recurring.Length > 0).Select(k => k.Person + " (" + k.Recurring + ")").ToList();
                Add(rec.Count > 0 ? "Recurring problems: " + Join(rec) + "." : "No recurring problems listed.", flag: rec.Count > 0);
            }
            // how good the flags are: players flagged high or very high, injured within 14 days, against the rest
            int now = Day(today);
            var days = Ledger.Risk.Values.Select(k => Day(k.Date)).Distinct().Where(x => x >= 0 && x + 14 <= now).ToList();
            if (Ledger.Risk.Count > 0)
            {
                if (days.Count == 0)
                {
                    int first = Ledger.Risk.Values.Min(k => Day(k.Date));
                    Add("Flag record: the first assessment on file is " + DateOf(first) + "; it can be checked from " + DateOf(first + 14) + " (were the flagged players injured within 14 days?).", quiet: true);
                }
                else
                {
                    int fl = 0, flHurt = 0, ot = 0, otHurt = 0;
                    foreach (var dd in days)
                        foreach (var k in Ledger.Risk.Values.Where(x => Day(x.Date) == dd))
                        {
                            bool hurt = Summary.Injuries().Any(i => i.Who == k.Who && AtClub(i) && Day(i.Date) > dd && Day(i.Date) <= dd + 14);
                            bool flagged = k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase) || k.Risk.Equals("High", StringComparison.OrdinalIgnoreCase);
                            if (flagged) { fl++; if (hurt) flHurt++; } else { ot++; if (hurt) otHurt++; }
                        }
                    Add($"Flag record ({Plural(days.Count, "assessment day", "assessment days")}): {flHurt} of {fl} players flagged high or very high were injured within 14 days; {otHurt} of {ot} others.");
                }
            }
            var adv = Notes.All.Where(n => n.Kind == "advice").ToList();
            if (adv.Count > 0) Add("Rests suggested by the medical team: " + adv.Count + "; taken " + adv.Count(n => n.Status == "taken") + ", not taken " + adv.Count(n => n.Status == "not taken") + (adv.Any(n => n.Status == "seen") ? ", still open " + adv.Count(n => n.Status == "seen") : "") + ".");
            return l;
        }

        // injuries at the club by month this season: training, match, other, days lost
        internal sealed class Month { public string Label = ""; public int Training, Match, Other, Days; }
        internal static List<Month> Months(string today, int startMonth)
        {
            int from = Summary.SeasonStart(today, startMonth), now = Day(today);
            var res = new List<Month>();
            if (from < 0 || now < 0) return res;
            var start = new DateTime(2000, 1, 1).AddDays(from); var end = new DateTime(2000, 1, 1).AddDays(now);
            var rows = Summary.Injuries().Where(i => AtClub(i) && Day(i.Date) >= from && Day(i.Date) <= now).ToList();
            for (var m = new DateTime(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1))
            {
                var inM = rows.Where(i => { var d = new DateTime(2000, 1, 1).AddDays(Day(i.Date)); return d.Year == m.Year && d.Month == m.Month; }).ToList();
                res.Add(new Month { Label = m.ToString("MMM yyyy", CultureInfo.InvariantCulture), Training = inM.Count(i => i.Received == "In training"), Match = inM.Count(i => i.Received == "In match"),
                    Other = inM.Count(i => i.Received != "In training" && i.Received != "In match"), Days = inM.Sum(i => Summary.Days(i.OutFor)) });
            }
            return res;
        }

        // ---------- the overview ----------
        internal static List<Summary.Line> Overview(string today, int startMonth)
        {
            var l = new List<Summary.Line>(Summary.Season(today, startMonth).Where(x => x.Col == 0));
            int now = Day(today);
            var back = Notes.All.Where(n => n.Kind == "returned" && now >= 0 && now - Day(n.Since) <= 14).Select(n => n.Name).Distinct().ToList();
            if (back.Count > 0) l.Add(new Summary.Line { Text = "Back from injury in the last two weeks: " + Join(back) + "." });
            var open = Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0).ToList();
            if (open.Count > 0) l.Add(new Summary.Line { Text = "Out injured: " + Join(open.Select(f => f.Person + " (" + Lower1(f.Injury) + ", expected back in " + f.Expected + ")")) + ".", Flag = true });
            var watch = Notes.All.Count(n => n.Kind == "watch" && n.Status == "open");
            var plans = Notes.All.Count(n => n.Kind == "plan" && (n.Status == "requested" || n.Status == "carried out"));
            if (watch + plans > 0) l.Add(new Summary.Line { Text = "Your watch list: " + (watch == 0 ? "nobody" : Plural(watch, "player", "players")) + "; " + (plans == 0 ? "no plans" : Plural(plans, "plan", "plans")) + " running." });
            var ignored = Notes.All.Where(n => n.Kind == "advice" && n.Status == "not taken" && n.Detail.Length > 0).ToList();
            foreach (var v in ignored.OrderByDescending(n => Day(n.Since)).Take(2)) l.Add(new Summary.Line { Text = AdviceLine(v), Flag = true });
            l.AddRange(PhysioSummary(today).Take(1));
            // when each medical screen was last read, so the manager knows what to open
            var seasonSeen = Summary.Injuries().Where(i => ("," + i.Sources + ",").Contains(",season,")).Select(i => Day(i.LastSeen)).DefaultIfEmpty(-1).Max();
            var squadSeen = Ledger.Squad.Values.Select(q => Day(q.Seen)).DefaultIfEmpty(-1).Max();
            var reads = new List<string>();
            reads.Add("Season Summary " + (seasonSeen >= 0 ? DateOf(seasonSeen) : "never"));
            reads.Add("Risk Assessment " + (LatestRiskDay() >= 0 ? DateOf(LatestRiskDay()) : "never"));
            reads.Add("Player Summary " + (squadSeen >= 0 ? DateOf(squadSeen) : "never"));
            l.Add(new Summary.Line { Text = "Last read: " + string.Join(", ", reads) + ". Open them now and then (Risk Assessment every few days builds the flag record).", Quiet = true });
            return l;
        }
    }
}
