// FM27 Media Mod - Treatment Room 0.5.0-0.5.3: the department at work (pure C#, tested offline).
// Two daily reports on the Treatment Room's front page, each signed by the member of staff who would write it, and each as
// good as the department behind it (Nik, 23 Nov: realism, tied to the number and quality of staff, so clubs differ):
//   the medical briefing  from the head physio (or the best physio, or the doctor): who is out and the physio's view of the
//                         estimate, who is back and how to bring them back, the workload against the physios in post, new staff
//   the physio's view (0.5.3)  on a player's file: the same author's estimate or return-to-play note, and advice that is one
//                         of the Treatment Room's own plans (rest, lighter training, a specialist assessment), as far as the
//                         physio's skill allows (Reasonable/Unsuited advise nothing; Average/Competent only rest for very high
//                         risk; Good and better also lighter training and specialist assessments; Outstanding/Elite also act
//                         on risk that has stayed raised)
//   the sports science report  from whoever signs the risk assessments (or the head of sports science): how far it looks
//                         (Dept.Reach) and, for a strong department, which loads and susceptibilities to watch
// Every fact comes from the game's own screens (the ledger). Only the voice and the depth change with the staff:
//   Physiotherapy of the briefing's author: Elite / Outstanding speak with confidence and add return-to-play notes from the
//   record; Very Good / Good give the facts and the return-to-play notes; Average / Competent give the facts plainly;
//   Reasonable / Unsuited hedge and add nothing. The game's estimate is never changed, only how it is put.
// Staff speak in the first person; players are named (no he/she); squad words stay neutral.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FM27MediaMod
{
    internal static class Brief
    {
        internal sealed class Report
        {
            public string By = "", Job = "", Note = "";
            public int Band = -1;            // the author's Physiotherapy (briefing) or Sports Science (report), -1 unknown
            public List<Summary.Line> Lines = new List<Summary.Line>();
        }

        static int Day(string d) => GameDate.Day(d);
        static string Last(string n) { var p = (n ?? "").Trim().Split(' '); return p[p.Length - 1]; }
        static string Lower1(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
        static string Bare(string injury)
        {
            var t = (injury ?? "").Trim();
            if (t.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2); else if (t.StartsWith("An ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(3);
            return Lower1(t);
        }
        static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);
        static readonly string[] Ords = { "", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth" };
        static string Ordinal(int n) => n > 0 && n < Ords.Length ? Ords[n] : n + (n % 100 >= 11 && n % 100 <= 13 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th");
        static string Join(IEnumerable<string> items)
        {
            var l = items.Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (l.Count == 0) return ""; if (l.Count == 1) return l[0];
            return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        }
        static void Add(Report r, string t, bool flag = false, bool quiet = false) => r.Lines.Add(new Summary.Line { Text = t, Flag = flag, Quiet = quiet });

        // ---------- who writes ----------
        static Dept.Member Head(Func<Dept.Member, bool> job) => Dept.Medical().Where(m => m.Left.Length == 0 && job(m)).OrderByDescending(m => Dept.Band(m.Physio)).FirstOrDefault();
        internal static Dept.Member BriefingAuthor()
        {
            var j = (Func<Dept.Member, string>)(m => m.Job.ToLowerInvariant());
            return Head(m => j(m) == "head physio")
                ?? Dept.Medical().Where(m => m.Left.Length == 0 && j(m).Contains("physio")).OrderByDescending(m => Dept.Band(m.Physio)).ThenBy(m => m.Name).FirstOrDefault()
                ?? Head(m => j(m).Contains("doctor"));
        }
        internal static Dept.Member ReportAuthor(string signer)
        {
            var cur = Dept.Medical().Where(m => m.Left.Length == 0).ToList();
            if (!string.IsNullOrEmpty(signer)) { var s = cur.FirstOrDefault(m => Dept.SameName(m.Name, signer)); if (s != null) return s; }
            return cur.FirstOrDefault(m => m.Job.Equals("Head of Sports Science", StringComparison.OrdinalIgnoreCase))
                ?? cur.Where(m => m.Job.ToLowerInvariant().Contains("sports scien")).OrderByDescending(m => Dept.Band(m.Sports)).FirstOrDefault();
        }

        // ---------- the medical briefing ----------
        internal static Report Medical(string today, int startMonth)
        {
            var r = new Report();
            var a = BriefingAuthor();
            if (a != null) { r.By = a.Name; r.Job = a.Job; r.Band = Dept.Band(a.Physio); }
            else if (Dept.Medical().Count == 0) r.Note = "The Treatment Room has not met the medical staff yet: open Club > Staff > Medical Team once.";
            else r.Note = "Nobody in the department writes a medical briefing: there is no physio or doctor in post.";
            int band = r.Band;   // -1 = not known yet: plain facts, no extra notes
            int now = Day(today), from = Summary.SeasonStart(today, startMonth);

            // who is out: open forecasts for our players (Current Injuries and the league table), the game's estimate as it stands
            var open = Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0 && !Summary.Personal(f.Injury) && Day(f.Date) >= 0 && (now < 0 || now - Day(f.Date) <= 400))
                .OrderBy(f => Day(f.Date)).ToList();
            foreach (var f in open) { var t = Estimate(f, band, now, out var late); Add(r, t, flag: late); }

            // who is back: Recently Returned in the last 7 days, with the game's fitness test and recommendation
            var back = Notes.All.Where(n => n.Kind == "returned" && now >= 0 && Day(n.Since) >= 0 && now - Day(n.Since) <= 7).OrderBy(n => Day(n.Since)).ToList();
            foreach (var n in back) Add(r, Return(n, band, from));
            if (open.Count == 0 && back.Count == 0) Add(r, band >= 0 && band <= 1 ? "Nobody injured that I know of." : "Nobody is injured right now.");

            // the workload: players out against the physios in post, and posts the board allows but nobody fills
            int physios = Dept.Posts.Count > 0
                ? Dept.Posts.Where(p => p.Key.ToLowerInvariant().Contains("physio")).Sum(p => p.Value.Filled)
                : Dept.Medical().Count(m => m.Left.Length == 0 && m.Job.ToLowerInvariant().Contains("physio"));
            if (open.Count > 0 && physios > 0)
            {
                if (open.Count >= physios * 2) Add(r, Plural(open.Count, "player", "players") + " out and " + Plural(physios, "physio", "physios") + " in post: we're stretched.", flag: true);
                else if (band >= 4) Add(r, Plural(open.Count, "player", "players") + " out, " + Plural(physios, "physio", "physios") + " in post: the workload is manageable.", quiet: true);
            }
            else if (open.Count > 0 && physios == 0 && Dept.Medical().Count > 0) Add(r, Plural(open.Count, "player", "players") + " out and no physio in post.", flag: true);
            var empty = Dept.Posts.Where(p => p.Key.ToLowerInvariant().Contains("physio") && p.Value.Filled < p.Value.Allowed).Sum(p => p.Value.Allowed - p.Value.Filled);
            bool busy = open.Count >= physios && physios > 0;
            if (empty > 0) Add(r, (empty == 1 ? "One physio post the board allows is empty." : empty + " physio posts the board allows are empty.")
                + (busy ? " Another pair of hands would help: Place Advert on Club > Staff > Medical Team." : ""), flag: busy);
            else if (physios > 0 && open.Count >= physios * 2 && Dept.Posts.Count > 0)
                Add(r, "Every physio post the board allows is filled, so more hands would mean a board request (Club > Staff > Medical Team).", quiet: true);

            // new and departed staff (Club > Staff > Medical Team, 0.5.0)
            foreach (var m in Dept.Staff.Values.Where(m => m.Joined.Length > 0 && now >= 0 && now - Day(m.Joined) <= 14).OrderBy(m => Day(m.Joined)))
                Add(r, "New in the department: " + m.Name + " (" + (m.Job.Length > 0 ? m.Job : "medical staff") + (Dept.IsBand(m.Physio) ? ", Physiotherapy " + m.Physio : "") + "), since " + m.Joined + ".");
            foreach (var m in Dept.Staff.Values.Where(m => m.Left.Length > 0 && now >= 0 && now - Day(m.Left) <= 14).OrderBy(m => Day(m.Left)))
                Add(r, m.Name + " (" + (m.Job.Length > 0 ? m.Job : "medical staff") + ") is no longer on the staff list, since " + m.Left + ".", flag: true);
            return r;
        }
        // the game's estimate for an injury, in the physio's voice
        static string Estimate(Ledger.Fc f, int band, int now, out bool late)
        {
            int.TryParse(f.Min, out var min); int.TryParse(f.Max, out var max);
            int so = now >= 0 ? now - Day(f.Date) : -1;
            var est = Latest(f.Expected);
            late = max > 0 && so > max;
            if (late) return f.Person + ": " + Bare(f.Injury) + ". Past the longest estimate (" + est + ") after " + Plural(so, "day", "days") + (band >= 4 ? "; that's longer than I'd hoped." : ".");
            if (band >= 6) return f.Person + ": " + Bare(f.Injury) + ". " + (max - min <= 7 ? "I'd expect " + Last(f.Person) + " back in " + est + "." : "The estimate is " + est + ", a wide range at this stage.");
            if (band >= 2 || band < 0) return f.Person + ": " + Bare(f.Injury) + ", expected back in " + est + ".";
            return f.Person + ": " + Bare(f.Injury) + ". Hard to call: the estimate is " + est + ".";
        }
        // back from injury, with the game's fitness test and recommendation, and (Good or better) a return-to-play note from the record
        static string Return(Note n, int band, int from)
        {
            var bits = new List<string>();
            if (n.Status.Length > 0) bits.Add("fitness test: " + Lower1(n.Status));
            if (n.Detail.Length > 0) bits.Add(Lower1(n.Detail));
            var t = n.Name + " is back from the " + Bare(n.What) + (bits.Count > 0 ? " (" + string.Join("; ", bits) + ")" : "") + ".";
            if (band >= 4)
            {
                var part = Summary.Part(n.What);
                int same = part.Length == 0 ? 0 : Summary.Injuries().Count(i => i.Who == n.Who && Summary.Part(i.Injury) == part && from >= 0 && Day(i.Date) >= from);
                var k = Files.LatestRisk(n.Who);
                if (same >= 2) t += " That's the " + Ordinal(same) + " " + part + " problem this season, so I'd bring " + Last(n.Name) + " back gradually.";
                else if (band >= 6 && k != null && (k.Suscept.Equals("Very High", StringComparison.OrdinalIgnoreCase) || k.Suscept.Equals("High", StringComparison.OrdinalIgnoreCase)))
                    t += " Injury susceptibility was " + k.Suscept.ToLowerInvariant() + " on the last assessment, so I'd manage the minutes at first.";
            }
            return t;
        }

        // ---------- the physio's view of one player (0.5.3) ----------
        internal sealed class View { public Report R = new Report(); public string Plan = "", Reason = ""; }
        internal static View Player(string who, string today, int startMonth)
        {
            var v = new View(); var r = v.R;
            var a = BriefingAuthor();
            if (a != null) { r.By = a.Name; r.Job = a.Job; r.Band = Dept.Band(a.Physio); }
            int band = r.Band, adv = band < 0 ? 4 : band;   // not met yet: plain, middle-of-the-road advice
            int now = Day(today), from = Summary.SeasonStart(today, startMonth);
            var name = Files.Name(who); var last = Last(name);
            var f = Ledger.Forecasts.Values.Where(x => x.Who == who && x.Returned.Length == 0 && x.Ours != "n" && !Summary.Personal(x.Injury) && (now < 0 || now - Day(x.Date) <= 400))
                .OrderByDescending(x => Day(x.Date)).FirstOrDefault();
            if (f != null) { Add(r, Estimate(f, band, now, out var late), flag: late); return v; }   // injured: no training advice
            var back = Notes.All.Where(n => n.Kind == "returned" && n.Who == who && now >= 0 && now - Day(n.Since) <= 14).OrderByDescending(n => Day(n.Since)).FirstOrDefault();
            if (back != null) Add(r, Return(back, band, from));
            var open = Notes.OpenPlan(who);
            if (open != null) { Add(r, "The current plan stands: " + Notes.PlanLabel(open.What).ToLowerInvariant() + " (" + open.Status + ")."); return v; }
            if (adv <= 1) { Add(r, "I'd want a closer look at the record before advising anything for " + last + "."); return v; }
            var k = Files.LatestRisk(who);
            bool Is(Ledger.Rk x, string lvl) => x != null && x.Risk.Equals(lvl, StringComparison.OrdinalIgnoreCase);
            string why = k != null && k.Factor.Length > 0 ? " (" + k.Factor.ToLowerInvariant() + ")" : "";
            // the same area again and again, or the medical team's recurring list: a specialist
            var parts = Summary.Injuries().Where(i => i.Who == who && now >= 0 && Day(i.Date) > now - 365 && Summary.Part(i.Injury).Length > 0)
                .GroupBy(i => Summary.Part(i.Injury)).OrderByDescending(g => g.Count()).FirstOrDefault();
            int sameYear = parts?.Count() ?? 0;
            bool recurring = k != null && k.Recurring.Length > 0;
            bool stayed = false;
            if (k != null && Is(k, "Increased"))
            {
                var prev = Ledger.Risk.Values.Where(x => x.Who == who && Day(x.Date) < Day(k.Date)).OrderByDescending(x => Day(x.Date)).FirstOrDefault();
                stayed = prev != null && (Is(prev, "Increased") || Is(prev, "High") || Is(prev, "Very High"));
            }
            if (adv >= 4 && (recurring || sameYear >= 3))
            {
                v.Plan = "specialist";
                v.Reason = recurring ? "the recurring " + k.Recurring.ToLowerInvariant() : "the " + Ordinal(sameYear) + " " + parts.Key + " problem in a year";
                Add(r, "My advice: send " + last + " for a specialist assessment (a few days away from training), given " + v.Reason + ".", flag: true);
            }
            else if (Is(k, "Very High"))
            {
                v.Plan = "rest"; v.Reason = "very high injury risk" + why;
                Add(r, "My advice: rest " + last + " from training for a few days. The risk is very high" + why + ".", flag: true);
            }
            else if (adv >= 4 && Is(k, "High"))
            {
                v.Plan = "lighter"; v.Reason = "high injury risk" + why;
                Add(r, "My advice: lighter training for " + last + " for two weeks. The risk is high" + why + ".");
            }
            else if (adv >= 6 && stayed)
            {
                v.Plan = "lighter"; v.Reason = "risk raised on the last two assessments";
                Add(r, "My advice: lighter training for " + last + " for two weeks, before the risk climbs. It has stayed raised on the last two assessments.");
            }
            else if (Is(k, "High")) Add(r, "The risk is high" + why + ", but I wouldn't change the training yet.");
            else if (recurring) Add(r, "On the recurring list: " + k.Recurring.ToLowerInvariant() + ".");
            else Add(r, k == null ? "No risk assessment on file for " + last + " yet." : "No concerns from me about " + last + " right now.");
            if (adv >= 6 && v.Plan.Length == 0 && k != null && k.Suscept.Equals("Very High", StringComparison.OrdinalIgnoreCase))
                Add(r, "Injury susceptibility is very high, so I keep a close eye on the minutes.");
            return v;
        }

        // "2 - 3 weeks -> 3 - 4 weeks" (revised): the latest estimate
        static string Latest(string expected)
        {
            var e = expected ?? "";
            var i = e.LastIndexOf(" -> ", StringComparison.Ordinal);
            return (i >= 0 ? e.Substring(i + 4) : e).Replace(" - ", " to ");
        }

        // ---------- the sports science report ----------
        internal static Report Science(string today, int startMonth, string signer)
        {
            var r = new Report();
            var a = ReportAuthor(signer);
            var lv = Dept.Now(signer);
            if (a != null) { r.By = a.Name; r.Job = a.Job; r.Band = Dept.Band(a.Sports); }
            int reach = Dept.Reach(lv);
            bool known = Dept.Medical().Count > 0;
            if (known && a == null)
            {
                r.Note = "There is no sports science in the department: the Risk Assessment screen is all there is.";
                return r;
            }
            int last = Files.LatestRiskDay();
            if (last < 0) { Add(r, "No risk assessment read yet: open Training > Medical Centre > Risk Assessment.", quiet: true); return r; }
            int now = Day(today);
            var latest = Ledger.Risk.Values.Where(k => Day(k.Date) == last).ToList();
            int prevDay = Ledger.Risk.Values.Select(k => Day(k.Date)).Where(d => d < last).DefaultIfEmpty(-1).Max();
            var prev = Ledger.Risk.Values.Where(k => Day(k.Date) == prevDay).ToDictionary(k => k.Who, k => k);
            bool Is(Ledger.Rk k, string lvl) => k.Risk.Equals(lvl, StringComparison.OrdinalIgnoreCase);
            string Names(IEnumerable<Ledger.Rk> ks) => Join(ks.OrderBy(k => k.Person).Select(k => k.Person + (k.Factor.Length > 0 ? " (" + k.Factor.ToLowerInvariant() + ")" : "")));

            var vh = latest.Where(k => Is(k, "Very High")).ToList();
            var hi = latest.Where(k => Is(k, "High")).ToList();
            var stayed = latest.Where(k => Is(k, "Increased") && prev.TryGetValue(k.Who, out var p) && (Is(p, "Increased") || Is(p, "High") || Is(p, "Very High"))).ToList();
            if (reach >= 0)
            {
                if (vh.Count > 0) Add(r, "Very high risk: " + Names(vh) + ".", flag: true);
                if (reach >= 1 && hi.Count > 0) Add(r, "High risk: " + Names(hi) + ".");
                if (reach >= 2 && stayed.Count > 0) Add(r, "Raised on the last two assessments: " + Join(stayed.OrderBy(k => k.Person).Select(k => k.Person)) + ". Worth easing the load before it climbs.");
                if (vh.Count == 0 && (reach < 1 || hi.Count == 0)) Add(r, "Nobody at the top of the risk scale on the latest assessment.");
            }
            else Add(r, "I can't track the loads closely enough to flag anyone with confidence. The Risk Assessment screen has the full picture.");

            // a strong department also reads the columns behind the rating (Outstanding or Elite)
            if (r.Band >= 6 || (a == null && lv.Sports >= 6))
            {
                var heavy = latest.Where(k => k.MatchLoad.Equals("Heavy", StringComparison.OrdinalIgnoreCase) && (k.Suscept.Equals("Very High", StringComparison.OrdinalIgnoreCase) || k.Suscept.Equals("High", StringComparison.OrdinalIgnoreCase))).ToList();
                if (heavy.Count > 0) Add(r, "Heavy match load and high susceptibility together: " + Join(heavy.OrderBy(k => k.Person).Select(k => k.Person + " (" + k.Suscept.ToLowerInvariant() + ")")) + ".");
                var factors = latest.Where(k => k.Factor.Length > 0 && (Is(k, "Very High") || Is(k, "High"))).GroupBy(k => k.Factor).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (factors != null && factors.Count() >= 3) Add(r, "The common thread is " + factors.Key.ToLowerInvariant() + " (" + factors.Count() + " players).");
            }
            else if (r.Band >= 4)
            {
                var factors = latest.Where(k => k.Factor.Length > 0 && (Is(k, "Very High") || Is(k, "High"))).GroupBy(k => k.Factor).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (factors != null && factors.Count() >= 3) Add(r, "Most of the flags come from " + factors.Key.ToLowerInvariant() + ".");
            }
            if (lv.SportsShort) Add(r, "With a sports science post empty, we're covering less of the squad than we should.", flag: true);
            if (now >= 0 && now - last > 7) Add(r, "The latest assessment is " + Plural(now - last, "day", "days") + " old: Risk Assessment has a fresh one.", quiet: true);
            return r;
        }

        // the title of each box on the front page
        internal static string Title(string what, Report r) => r.By.Length > 0 ? what + " (" + r.By.ToUpperInvariant() + ", " + r.Job.ToUpperInvariant() + ")" : what;
    }
}
