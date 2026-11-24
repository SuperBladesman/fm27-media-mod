// FM27 Media Mod - Treatment Room: the manager's own notes (pure C#, tested offline). Kept per save in "treatment notes.tsv".
//   watch    a watch item: a player and an area the manager wants watched (from a screening note, or by hand)
//   plan     managed load or a specialist assessment (design part 2, sections 3-4), v1 "guide and record": the manager carries
//            it out through the game's own Rest Player menu; the plan is marked carried out when the rest (or intensity)
//            action for that player is seen, and reviewed when its days are up
//   advice   the medical team's own advice seen in the inbox ("Suggest Resting X"): taken if that player is rested within
//            3 days, otherwise not taken (and, if the player is then injured within 14 days, that is noted)
//   staff    names the game shows for medical roles ("Will Tullett will provide you with a copy of his latest assessment")
//   returned a player on Recently Returned: the injury, the fitness test and the recommendation, first day seen
// Wording matches wiring: a plan says "rested", "lighter training" or "away from training for a specialist assessment",
// never that anything was treated.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal sealed class Note
    {
        public string Kind = "", Who = "", Name = "", What = "", Since = "", Status = "", Done = "", Detail = "";
        public int Days;
        public string Key => Kind + "|" + Who + "|" + What + "|" + (Kind == "watch" || Kind == "staff" || Kind == "returned" ? "" : Since);
    }

    internal static class Notes
    {
        internal static readonly List<Note> All = new List<Note>();

        internal static readonly Dictionary<string, (string Label, int Days, string Guide)> Plans = new Dictionary<string, (string, int, string)>
        {
            { "rest",       ("Rest", 3, "Rest {0} from training for a few days: Rest Player menu on the profile (Performance).") },
            { "lighter",    ("Lighter training", 14, "Lower {0}'s individual training intensity for two weeks, then put it back.") },
            { "specialist", ("Specialist assessment", 3, "{0} is away from training for a specialist assessment: rest {0} for a few days from the Rest Player menu on the profile.") },
            { "normal",     ("Back to normal", 0, "Put {0} back to normal training: cancel any rest and restore the training intensity.") },
        };

        // ---------- the manager's choices ----------
        internal static Note Watch(string who, string name, string area, string reason, string date)
        {
            var w = All.FirstOrDefault(n => n.Kind == "watch" && n.Who == who && n.Status == "open");
            if (w != null) { if (area.Length > 0) w.What = area; if (reason.Length > 0) w.Detail = reason; return w; }
            w = new Note { Kind = "watch", Who = who, Name = name, What = area, Since = date, Status = "open", Detail = reason };
            All.Add(w); return w;
        }
        internal static void Unwatch(string who, string date)
        {
            foreach (var w in All.Where(n => n.Kind == "watch" && n.Who == who && n.Status == "open")) { w.Status = "closed"; w.Done = date; }
        }
        internal static Note OpenWatch(string who) => All.FirstOrDefault(n => n.Kind == "watch" && n.Who == who && n.Status == "open");

        internal static Note Plan(string who, string name, string kind, string reason, string date)
        {
            if (!Plans.ContainsKey(kind)) return null;
            // a new plan replaces an open one for the same player
            foreach (var o in All.Where(n => n.Kind == "plan" && n.Who == who && (n.Status == "requested" || n.Status == "carried out")))
            { o.Status = "replaced"; if (o.Done.Length == 0) o.Done = date; }
            var p = new Note { Kind = "plan", Who = who, Name = name, What = kind, Since = date, Days = Plans[kind].Days, Status = "requested", Detail = reason };
            All.Add(p); return p;
        }
        internal static void CancelPlan(string who, string date)
        {
            foreach (var o in All.Where(n => n.Kind == "plan" && n.Who == who && n.Status == "requested")) { o.Status = "cancelled"; o.Done = date; }
        }
        internal static Note OpenPlan(string who) => All.Where(n => n.Kind == "plan" && n.Who == who && (n.Status == "requested" || n.Status == "carried out")).LastOrDefault();
        internal static string Guide(Note p) => p == null || !Plans.ContainsKey(p.What) ? "" : string.Format(Plans[p.What].Guide, p.Name);
        internal static string PlanLabel(string kind) => Plans.TryGetValue(kind, out var v) ? v.Label : kind;

        // ---------- what the game showed ----------
        internal static bool Advice(string who, string name, string what, string date)
        {
            // the same message can be opened again later: one record per player and advice within three weeks
            if (All.Any(n => n.Kind == "advice" && n.Who == who && n.What == what && Math.Abs(Day(date) - Day(n.Since)) <= 21)) return false;
            All.Add(new Note { Kind = "advice", Who = who, Name = name, What = what, Since = date, Status = "seen" });
            return true;
        }
        internal static bool Staff(string role, string name, string date)
        {
            var s = All.FirstOrDefault(n => n.Kind == "staff" && n.What == role);
            if (s != null) { if (s.Name == name) return false; s.Name = name; s.Since = date; return true; }
            All.Add(new Note { Kind = "staff", What = role, Name = name, Since = date, Status = "seen" });
            return true;
        }
        internal static string StaffName(string role) => All.FirstOrDefault(n => n.Kind == "staff" && n.What == role)?.Name ?? "";
        internal static bool Returned(string who, string name, string injury, string fitness, string rec, string date)
        {
            if (who.Length == 0 || injury.Length == 0) return false;
            var r = All.FirstOrDefault(n => n.Kind == "returned" && n.Who == who && n.What == injury);
            if (r != null) { bool ch = false; if (fitness.Length > 0 && r.Status != fitness) { r.Status = fitness; ch = true; } if (rec.Length > 0 && r.Detail != rec) { r.Detail = rec; ch = true; } return ch; }
            All.Add(new Note { Kind = "returned", Who = who, Name = name, What = injury, Since = date, Status = fitness, Detail = rec });
            return true;
        }

        // ---------- keeping plans and advice up to date (from the actions the manager took and the injuries the game showed) ----------
        static readonly Regex IdRx = new Regex(@"\[([0-9a-fA-F]{6,10})\]", RegexOptions.Compiled);
        internal sealed class Act { public string Date = "", Action = "", Who = ""; }
        internal static List<Act> Parse(IEnumerable<string[]> rows)
        {
            var l = new List<Act>();
            foreach (var a in rows)
            {
                if (a.Length < 6) continue;
                var m = IdRx.Match(a[5]);
                l.Add(new Act { Date = a[0], Action = a[3], Who = m.Success ? m.Groups[1].Value : "" });
            }
            return l;
        }
        static int Day(string d) => GameDate.Day(d);
        static bool IsRest(string a) => a == "PersonRestTrainingSchedule";
        static bool IsIntensity(string a) => a.IndexOf("Intensity", StringComparison.Ordinal) >= 0;
        static bool IsBackToNormal(string a) => a == "PersonCancelRestTrainingSchedule" || IsIntensity(a);

        // returns true if anything changed
        internal static bool Update(string today, List<Act> acts)
        {
            bool changed = false; int now = Day(today);
            if (now < 0) return false;
            foreach (var p in All.Where(n => n.Kind == "plan"))
            {
                if (p.Status == "requested")
                {
                    Func<string, bool> fits = p.What == "lighter" ? IsIntensity : p.What == "normal" ? (Func<string, bool>)IsBackToNormal : IsRest;
                    var a = acts.FirstOrDefault(x => x.Who == p.Who && fits(x.Action) && Day(x.Date) >= Day(p.Since));
                    if (a != null) { p.Status = "carried out"; p.Done = a.Date; changed = true; }
                }
                if (p.Status == "carried out" && p.What != "normal" && now >= Day(p.Done) + Math.Max(1, p.Days))
                {
                    p.Status = "reviewed"; p.Detail = Review(p, today); changed = true;
                }
            }
            foreach (var v in All.Where(n => n.Kind == "advice" && n.Status == "seen"))
            {
                var a = acts.FirstOrDefault(x => x.Who == v.Who && IsRest(x.Action) && Day(x.Date) >= Day(v.Since) && Day(x.Date) <= Day(v.Since) + 3);
                if (a != null) { v.Status = "taken"; v.Done = a.Date; changed = true; }
                else if (now > Day(v.Since) + 3) { v.Status = "not taken"; v.Done = today; changed = true; }
            }
            // advice not taken, then an injury within 14 days: say so (once)
            foreach (var v in All.Where(n => n.Kind == "advice" && n.Status == "not taken" && n.Detail.Length == 0))
            {
                var i = Summary.Injuries().Where(x => x.Who == v.Who && Day(x.Date) > Day(v.Since) && Day(x.Date) <= Day(v.Since) + 14).OrderBy(x => Day(x.Date)).FirstOrDefault();
                if (i != null) { v.Detail = "injured on " + i.Date + ": " + Lower(i.Injury) + (i.OutFor.Length > 0 ? " (" + i.OutFor + ")" : ""); changed = true; }
            }
            return changed;
        }
        static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        // the end of a plan: injuries since it started (and in the watched area), and the risk then and now
        internal static string Review(Note p, string today)
        {
            var parts = new List<string>();
            int from = Day(p.Since);
            var since = Summary.Injuries().Where(x => x.Who == p.Who && Day(x.Date) >= from).OrderBy(x => Day(x.Date)).ToList();
            var w = All.FirstOrDefault(n => n.Kind == "watch" && n.Who == p.Who && (n.Status == "open" || Day(n.Done) >= from));
            string area = w?.What ?? "";
            if (since.Count == 0) parts.Add("no injury since the plan started");
            else
            {
                var inArea = area.Length > 0 ? since.Where(x => Summary.Part(x.Injury) == area).ToList() : new List<Ledger.Inj>();
                parts.Add(since.Count + (since.Count == 1 ? " injury" : " injuries") + " since the plan started (last: " + Lower(since[since.Count - 1].Injury) + ", " + since[since.Count - 1].Date + ")");
                if (area.Length > 0) parts.Add(inArea.Count == 0 ? "none in the " + area : inArea.Count + " in the " + area);
            }
            var before = Ledger.Risk.Values.Where(k => k.Who == p.Who && Day(k.Date) <= from).OrderByDescending(k => Day(k.Date)).FirstOrDefault();
            var after = Ledger.Risk.Values.Where(k => k.Who == p.Who && Day(k.Date) > from).OrderByDescending(k => Day(k.Date)).FirstOrDefault();
            if (before != null && after != null) parts.Add("risk " + before.Risk.ToLowerInvariant() + " before, " + after.Risk.ToLowerInvariant() + " at the last assessment (" + after.Date + ")");
            else if (before != null) parts.Add("risk " + before.Risk.ToLowerInvariant() + " before; no assessment read since");
            return string.Join("; ", parts);
        }

        // ---------- file ----------
        internal static string Header => "kind\twho\tname\twhat\tsince\tdays\tstatus\tdone\tdetail";
        internal static string Line(Note n, Func<string, string> c) => $"{n.Kind}\t{n.Who}\t{c(n.Name)}\t{c(n.What)}\t{n.Since}\t{n.Days}\t{c(n.Status)}\t{n.Done}\t{c(n.Detail)}";
        internal static Note From(string[] a, Func<string[], int, string> at)
        {
            int.TryParse(at(a, 5), out var d);
            return new Note { Kind = at(a, 0), Who = at(a, 1), Name = at(a, 2), What = at(a, 3), Since = at(a, 4), Days = d, Status = at(a, 6), Done = at(a, 7), Detail = at(a, 8) };
        }
    }
}
