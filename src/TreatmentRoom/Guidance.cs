// FM27 Media Mod - Treatment Room 0.6.0: GUIDANCE FROM THE TEAM, the page's front (pure C#, tested offline).
// Nik (23 Nov): "frame it round guidance from the team: what low condition is, who we need to watch out for, with guidance
// on what to do... Data is good. Actionable insight is even better. And it needs to link back to staff numbers/quality."
// One card per problem the latest risk assessment names (its "main factor" column, in the game's words), each with
//   what it is   from the game's own descriptions (the fitness hints, the injury risk key factors, tactical intensity);
//   who to watch the players, as far as the department can see (the signer's Sports Science, Dept.Reach);
//   what we'd do rest / minutes / lighter training / a specialist, as far as the head physio's Physiotherapy allows
//                (the same rules as the physio's view, Brief.Player);
//   why          which member of staff, how good, and what a stronger or weaker department would show.
// Nothing here changes the game's numbers. Players are named, never he or she.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class Guidance
    {
        internal sealed class Where { public string Text = "", Path = ""; }
        internal sealed class Act { public string Text = "", Plan = "", Label = "", Reason = ""; public bool Strong; public List<(string Who, string Name)> Players = new List<(string, string)>(); }
        internal sealed class Card
        {
            public string Key = "", Title = "", Count = "", Advisor = "", What = "", Extra = "", Footer = "";
            public int Level = 2;                       // the count pill: 0 very high, 1 high, 2 neutral
            public string[] Cols = { "Player", "Risk", "Last 14 days", "Susceptibility", "Note" };
            public List<string[]> Rows = new List<string[]>();
            public List<int> RowLevel = new List<int>();   // per row: 0 very high, 1 high, 2 increased, 3 other
            public List<Act> Acts = new List<Act>();
        }
        internal sealed class DeptTile { public string Role = "", Name = "", Line = "", Note = ""; }
        internal sealed class Page
        {
            public string Today = "", Assessed = "", AgeText = "";
            public int Age = -1, Out, VeryHigh, High, Hidden;
            public string OutSub = "", RiskSub = "";
            public List<Where> Waiting = new List<Where>();
            public string Intro = "";
            public List<Card> Cards = new List<Card>();
            public string Beyond = "";
            public List<DeptTile> Dept = new List<DeptTile>();
            public List<string> DeptLines = new List<string>();
            public string DeptRead = "";
            public bool DeptKnown;
        }

        static int Day(string d) => GameDate.Day(d);
        static string Bare(string s) => PersonLinks.Plain(s ?? "").Trim();
        static string Surname(string n) { var p = (n ?? "").Trim().Split(' '); return p.Length == 0 ? n : p[p.Length - 1]; }
        static string Join(List<string> l) => l.Count == 0 ? "" : l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
        static string Lower(string s) => (s ?? "").ToLowerInvariant();
        static readonly string[] Words = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        internal static string Num(int n) => n >= 0 && n < Words.Length ? Words[n] : n.ToString();
        internal static int Rank(string r) { switch (Lower(r)) { case "very high": return 0; case "high": return 1; case "increased": return 2; case "normal": return 3; case "low": return 4; default: return 5; } }
        static string RiskWord(string r) { var t = (r ?? "").Trim(); return t.Length == 0 ? "-" : char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant(); }
        static readonly Regex MinRx = new Regex(@"(\d+)\s+match(?:es)?\s*\((\d+)\s*/\s*(\d+)\s*minutes\)", RegexOptions.IgnoreCase);
        static (int Matches, int Got, int Of) Minutes(Ledger.Rk k)
        {
            var m = MinRx.Match(k?.MatchLine ?? "");
            return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : (-1, -1, -1);
        }
        static string Last14(Ledger.Rk k)
        {
            var (n, got, of) = Minutes(k);
            var fat = k.Fatigue.Length > 0 ? k.Fatigue + " · " : "";
            if (n < 0) return fat.Length > 0 ? k.Fatigue : "-";
            if (n == 0) return fat + "No minutes";
            return fat + got + " of " + of + " min";
        }
        static bool Condition(Ledger.Rk k) => Lower(k.Factor).Contains("condition");
        static bool Sharpness(Ledger.Rk k) => Lower(k.Factor).Contains("sharpness");
        static bool Intensity(Ledger.Rk k) { var f = Lower(k.Factor); return f.Contains("intensity") || f.Contains("match fatigue") || f.Contains("workload") || f.Contains("schedule"); }
        static readonly Regex Muscle = new Regex(@"hamstring|thigh|calf|groin|quad|adductor|strain|tight|tear|pull", RegexOptions.IgnoreCase);

        // what each problem is, in the game's own descriptions (its fitness hints and injury-risk key factors), without he or she
        const string WhatCondition = "How fit and fresh a player is right now. Tiredness, a recent injury or a lack of fitness lowers it. Only rest from matches or training brings it back, and playing on low condition risks injury and lowers it further.";
        const string WhatConditionShort = "How fit and fresh a player is right now. Only rest brings it back.";
        const string WhatSharpness = "Readiness for competitive football. It drops without recent game time, and only minutes on the pitch build it back. A player short of sharpness struggles with the pace of a match and is more likely to get injured.";
        const string WhatSharpnessShort = "Readiness for competitive football. Only minutes on the pitch build it back.";
        const string WhatIntensity = "A heavy run of matches or training on top of a demanding tactic. How hard the tactic works the players is set by its pressing, tempo, tackling and mentality, and it wears down condition.";
        const string WhatRecurring = "An injury that has come back. The risk assessment lists it as recurring, and a specialist can look for the cause.";
        const string WhatOther = "The assessment raises the risk without naming a single main cause.";

        internal static Page Build(string today, string signer, string waiting)
        {
            var p = new Page { Today = today };
            int now = Day(today);
            var lvl = FM27MediaMod.Dept.Now(signer);
            int reach = FM27MediaMod.Dept.Reach(lvl);
            int sci = lvl.Sports;                      // -1 = not known yet
            bool hedge = sci >= 0 && sci <= 3;         // Average and below hedge (Competent, Average; below that there are no risk cards)
            var head = Brief.BriefingAuthor(); int physio = head == null ? -1 : FM27MediaMod.Dept.Band(head.Physio);
            int adv = physio < 0 ? 4 : physio;         // as the physio's view: not known yet = middle-of-the-road advice
            var author = Brief.ReportAuthor(signer);
            string SciBy() => author != null ? author.Name + ", " + (author.Job.Length > 0 ? author.Job : "sports science") + " · Sports Science: " + (FM27MediaMod.Dept.IsBand(author.Sports) ? author.Sports : "not read yet")
                                             : "the medical team";
            string PhysioBy() => head != null ? head.Name + ", " + (head.Job.Length > 0 ? head.Job : "physio") + " · Physiotherapy: " + (FM27MediaMod.Dept.IsBand(head.Physio) ? head.Physio : "not read yet") : "the medical team";
            string SciName() => author != null ? author.Name : "The medical team";
            string Band(int b) => b >= 0 && b < FM27MediaMod.Dept.Scale.Length ? FM27MediaMod.Dept.Scale[b] : "";

            // ---------- the assessment ----------
            int last = Files.LatestRiskDay();
            var latest = last < 0 ? new List<Ledger.Rk>() : Ledger.Risk.Values.Where(k => Day(k.Date) == last).ToList();
            var outNow = Ledger.Forecasts.Values.Where(f => f.Returned.Length == 0 && f.Ours != "n" && !Summary.Personal(f.Injury) && (now < 0 || now - Day(f.Date) <= 400))
                .OrderByDescending(f => Day(f.Date)).ToList();
            var outIds = new HashSet<string>(outNow.Select(f => f.Who));
            if (latest.Count > 0) { p.Assessed = latest[0].Date; p.Age = now >= 0 ? now - last : -1; p.AgeText = p.Age < 0 ? "" : p.Age == 0 ? "today" : p.Age == 1 ? "1 day old" : p.Age + " days old"; }
            int Max(int r) => reach >= 2 ? 2 : reach == 1 ? 1 : reach == 0 ? 0 : -1;
            bool Seen(Ledger.Rk k) => !outIds.Contains(k.Who) && Rank(k.Risk) <= Max(reach);
            var seen = latest.Where(Seen).OrderBy(k => Rank(k.Risk)).ThenBy(k => k.Person).ToList();
            var hidden = latest.Where(k => !outIds.Contains(k.Who) && Rank(k.Risk) <= 2 && !Seen(k)).ToList();

            // ---------- right now ----------
            p.Out = outNow.Count;
            if (outNow.Count > 0) p.OutSub = string.Join(". ", outNow.Take(2).Select(f => Files.Name(f.Who) + ": " + Lower(Bare(f.Injury)) + (Bare(f.Expected).Length > 0 ? ", back in " + Bare(f.Expected).Replace(" - ", " to ") : ""))) + ".";
            else
            {
                var rec = Summary.Injuries().Where(i => i.Ours != "n" && Day(i.Date) >= 0).OrderByDescending(i => Day(i.Date)).FirstOrDefault();
                p.OutSub = rec != null ? "Last on record: " + Files.Name(rec.Who) + ", " + Lower(Bare(rec.Injury)) + " (" + rec.Date + ")." : "Nothing on record yet.";
            }
            p.VeryHigh = seen.Count(k => Rank(k.Risk) == 0); p.High = seen.Count(k => Rank(k.Risk) == 1); p.Hidden = hidden.Count;
            if (latest.Count == 0) p.RiskSub = "No risk assessment read yet.";
            else if (reach < 0) p.RiskSub = "Nobody here tracks risk closely enough to say. The Medical Centre's Risk Assessment lists it all.";
            else p.RiskSub = "From the " + p.Assessed + " assessment." + (hidden.Count > 0 ? " The Medical Centre lists " + Num(hidden.Count) + " more." : "");

            // ---------- waiting on you ----------
            if (!string.IsNullOrEmpty(waiting)) p.Waiting.Add(new Where { Text = "Decision waiting: " + waiting + ".", Path = "Inbox" });
            if (latest.Count == 0) p.Waiting.Add(new Where { Text = "No risk assessment read yet.", Path = "Training › Medical Centre › Risk Assessment" });
            else if (p.Age > 7) p.Waiting.Add(new Where { Text = "The risk assessment is " + p.AgeText + ".", Path = "Ask for an update: Medical Centre › Risk Assessment" });
            bool Recent(string d) => now < 0 || (Day(d) >= 0 && now - Day(d) <= 28);
            if (FM27MediaMod.Dept.Advice.TryGetValue("vacancies", out var va) && Recent(va.Date)) p.Waiting.Add(new Where { Text = "The board's staffing advice (" + va.Date + "): " + va.Text + ".", Path = "Club › Staff › Medical Team" });
            else
            {
                var empty = FM27MediaMod.Dept.Posts.Where(x => x.Value.Filled < x.Value.Allowed).OrderBy(x => FM27MediaMod.Dept.JobRank(x.Key)).ToList();
                if (empty.Count > 0)
                {
                    var e = empty[0];
                    p.Waiting.Add(new Where { Text = (e.Value.Allowed - e.Value.Filled) + " of " + e.Value.Allowed + " " + Lower(e.Key) + " posts " + (e.Value.Allowed - e.Value.Filled == 1 ? "is" : "are") + " empty.", Path = "Club › Staff › Medical Team › Place Advert" });
                }
            }
            if (FM27MediaMod.Dept.Advice.TryGetValue("improve", out var im) && Recent(im.Date)) p.Waiting.Add(new Where { Text = "The board's staffing advice (" + im.Date + "): " + im.Text + ".", Path = "Club › Staff › Medical Team" });

            p.Intro = reach >= 1 || sci < 0
                ? "What the department sees in the latest risk assessment: what each problem is, who it affects and what they would do. Each card says who is advising and how far their skills let them see."
                : "A small or less skilled department sees less, explains less and suggests less. The numbers are the same; what changes is how much of them this department can turn into advice.";

            // ---------- the cards ----------
            string Footer(List<Ledger.Rk> shown, Func<Ledger.Rk, bool> fits)
            {
                if (sci < 0) return "Who signs the risk assessments, and how good they are, isn't known yet: open Club › Staff › Medical Team once.";
                var more = hidden.Where(fits).ToList();
                var shortTxt = lvl.SportsShort ? " An empty sports science post costs a step." : "";
                if (reach >= 2)
                {
                    var vh = shown.Where(k => Rank(k.Risk) == 0).Select(k => Surname(k.Person)).ToList();
                    return "Why this many names: " + SciName() + "'s Sports Science (" + Band(sci) + ") reads every very high, high and increased risk. With an Average head of sports science, "
                        + (vh.Count == 0 ? "this card wouldn't be here at all." : "this card would list " + Join(vh) + " only.") + shortTxt;
                }
                if (reach == 1)
                    return SciName() + "'s Sports Science (" + Band(sci) + ") reads high and very high risk." + (more.Count > 0 ? " An Outstanding head of sports science would also bring " + Join(more.Select(k => Surname(k.Person)).ToList()) + " here." : "") + shortTxt;
                return (more.Count > 0 ? "An Outstanding head of sports science would add " + Num(more.Count) + " more at raised risk, and say why each one is on the list." : "A stronger head of sports science would explain more of the assessment.") + shortTxt;
            }
            string Note(Ledger.Rk k, string card)
            {
                if (k.Recurring.Length > 0 && card != "recurring") return "Recurring: " + Lower(k.Recurring);
                if (card == "sharpness") return k.Status;
                if (card == "intensity" || card == "other") return k.Factor;
                var (n, got, of) = Minutes(k);
                if (n >= 2 && got == of && of > 0) return "Every minute of the last " + Num(n) + " matches";
                return "";
            }
            void AddRow(Card c, Ledger.Rk k, string card)
            {
                c.Rows.Add(new[] { k.Person, RiskWord(k.Risk), Last14(k), k.Suscept.Length > 0 ? k.Suscept : "-", Note(k, card) });
                c.RowLevel.Add(Math.Min(3, Rank(k.Risk)));
            }
            void Count(Card c, List<Ledger.Rk> l)
            {
                c.Count = l.Count == 1 ? "1 player" : l.Count + " players";
                c.Level = l.Any(k => Rank(k.Risk) == 0) ? 0 : l.Any(k => Rank(k.Risk) == 1) ? 1 : 2;
            }
            Act Plan(string text, string plan, IEnumerable<Ledger.Rk> who, string label, string reason, bool strong)
            {
                var a = new Act { Text = text, Strong = strong };
                bool allowed = plan == "rest" ? adv >= 2 : plan == "lighter" ? adv >= 4 : plan == "specialist" ? adv >= 4 : false;
                if (allowed && who != null)
                {
                    a.Plan = plan; a.Label = label; a.Reason = reason;
                    foreach (var k in who) a.Players.Add((k.Who, k.Person));
                    if (a.Players.Count == 0) a.Plan = "";
                }
                return a;
            }

            if (reach >= 0)
            {
                // low condition
                var cond = seen.Where(Condition).ToList();
                if (cond.Count > 0)
                {
                    var c = new Card { Key = "condition", Title = "Low condition", Advisor = SciBy(), What = hedge ? WhatConditionShort : WhatCondition };
                    Count(c, cond); foreach (var k in cond) AddRow(c, k, "condition");
                    var vh = cond.Where(k => Rank(k.Risk) == 0).ToList(); var rest = cond.Where(k => Rank(k.Risk) > 0).ToList();
                    if (hedge)
                    {
                        var who = cond.Count == 1 ? Surname(cond[0].Person) + " looks" : cond.Count == 2 ? "Both look" : "All " + Num(cond.Count) + " look";
                        c.Acts.Add(Plan(who + " tired, so rest would probably help. The assessment is " + (p.AgeText.Length > 0 ? p.AgeText : "the latest we have") + " and we can't say much more than that.",
                            "rest", vh.Count > 0 ? vh : cond, cond.Count == 1 ? "Plan: rest " + Surname(cond[0].Person) : "Plan: rest " + (cond.Count == 2 ? "both" : "all " + Num(cond.Count)), "low condition", false));
                    }
                    else
                    {
                        foreach (var k in vh.Take(3))
                            c.Acts.Add(Plan(k.Fatigue.Length > 0
                                    ? Surname(k.Person) + ": rest from training and start on the bench. " + k.Fatigue + " on top of " + Lower(k.Suscept) + " susceptibility."
                                    : Surname(k.Person) + ": a rest day this week, then judge the next match.",
                                "rest", new[] { k }, "Plan: rest " + Surname(k.Person), "very high risk, low condition", c.Acts.Count == 0));
                        if (rest.Count > 0)
                            c.Acts.Add(Plan((vh.Count > 0 ? "The other " + Num(rest.Count) : rest.Count == 1 ? Surname(rest[0].Person) : "All " + Num(rest.Count))
                                + ": rotate " + (rest.Count == 1 ? "in and out" : "one or two a match") + " until the next assessment.",
                                "lighter", rest, rest.Count == 1 ? "Plan: lighter training for " + Surname(rest[0].Person) : "Plan: lighter training for " + Num(rest.Count), "low condition", vh.Count == 0));
                    }
                    c.Footer = Footer(cond, Condition);
                    p.Cards.Add(c);
                }
                // low match sharpness
                var sharp = seen.Where(Sharpness).ToList();
                var noMins = sci >= 4 || sci < 0 ? latest.Where(k => !outIds.Contains(k.Who) && !Sharpness(k) && Minutes(k).Matches == 0 && Rank(k.Risk) >= 2).Select(k => k.Person).OrderBy(n => n).ToList() : new List<string>();
                if (sharp.Count > 0)
                {
                    var c = new Card { Key = "sharpness", Title = "Low match sharpness", Advisor = SciBy(), What = hedge ? WhatSharpnessShort : WhatSharpness };
                    Count(c, sharp); if (noMins.Count > 0) { c.Count = sharp.Count + " flagged · " + noMins.Count + " more without minutes"; }
                    foreach (var k in sharp) AddRow(c, k, "sharpness");
                    if (noMins.Count > 0) c.Extra = "Also without minutes, not flagged yet: " + Join(noMins) + ".";
                    if (hedge) c.Acts.Add(new Act { Text = "Minutes would help." });
                    else
                    {
                        c.Acts.Add(new Act { Text = "Minutes, in steps: a reserve or under-21s match, or a planned spell off the bench. The medical team's fitness tests work in 30, 45, 60 and 75-minute steps." });
                        var first = sharp.Where(k => Minutes(k).Matches == 0 && !Lower(k.Status).Contains("goalkeeper")).OrderBy(k => SusRank(k.Suscept)).FirstOrDefault();
                        if (first != null && sharp.Count > 1) c.Acts.Add(new Act { Text = Surname(first.Person) + " first: " + (first.Suscept.Length > 0 ? first.Suscept + " susceptibility and " : "") + "no minutes at all." });
                        foreach (var k in sharp.Where(k => Lower(k.Status).Contains("goalkeeper")))
                            c.Acts.Add(new Act { Text = Surname(k.Person) + ": a reserve match keeps the " + Lower(k.Status) + " ready." });
                    }
                    c.Footer = "Sharpness and condition pull against each other: rest brings condition back, only minutes build sharpness." + (cond.Count > 0 ? " Check the Low condition card before handing out minutes." : "");
                    p.Cards.Add(c);
                }
                // match fatigue, workloads and tactical intensity
                var inten = seen.Where(k => Intensity(k) && !Condition(k) && !Sharpness(k)).ToList();
                if (inten.Count > 0)
                {
                    bool same = inten.All(k => Lower(k.Factor) == "match fatigue and tactical intensity");
                    var c = new Card { Key = "intensity", Title = same ? "Match fatigue and tactical intensity" : "Workload and tactical intensity", Advisor = SciBy(), What = WhatIntensity };
                    Count(c, inten); foreach (var k in inten) AddRow(c, k, same ? "intensity-same" : "intensity");
                    c.Acts.Add(new Act { Text = hedge ? "Fewer minutes would probably help." : "Shorter minutes in the next match, or ease the pressing and tempo while " + (inten.Count == 1 ? Surname(inten[0].Person) + " is" : "they're") + " on." });
                    foreach (var k in inten.Where(k => k.Recurring.Length > 0))
                        c.Acts.Add(Plan(Surname(k.Person) + ": lighter training as well. The " + Lower(k.Recurring) + " has gone before.", "lighter", new[] { k }, "Plan: lighter training for " + Surname(k.Person), "tactical intensity and a recurring " + Lower(k.Recurring), false));
                    c.Footer = Footer(inten, k => Intensity(k) && !Condition(k) && !Sharpness(k));
                    p.Cards.Add(c);
                }
                // raised, no single cause
                var other = seen.Where(k => !Condition(k) && !Sharpness(k) && !Intensity(k)).ToList();
                if (other.Count > 0)
                {
                    var c = new Card { Key = "other", Title = "Raised risk, no single cause", Advisor = SciBy(), What = WhatOther };
                    Count(c, other); foreach (var k in other) AddRow(c, k, "other");
                    var vh = other.Where(k => Rank(k.Risk) == 0).ToList(); var hi = other.Where(k => Rank(k.Risk) == 1).ToList();
                    if (vh.Count > 0) c.Acts.Add(Plan(Join(vh.Select(k => Surname(k.Person)).ToList()) + ": rest from training for a few days.", "rest", vh, "Plan: rest " + (vh.Count == 1 ? Surname(vh[0].Person) : Num(vh.Count)), "very high risk", true));
                    if (hi.Count > 0) c.Acts.Add(Plan(Join(hi.Select(k => Surname(k.Person)).ToList()) + ": lighter training for two weeks.", "lighter", hi, "Plan: lighter training for " + (hi.Count == 1 ? Surname(hi[0].Person) : Num(hi.Count)), "high risk", vh.Count == 0));
                    if (vh.Count == 0 && hi.Count == 0) c.Acts.Add(new Act { Text = "Nothing to change yet: keep an eye on the minutes." });
                    c.Footer = Footer(other, k => !Condition(k) && !Sharpness(k) && !Intensity(k));
                    p.Cards.Add(c);
                }
            }
            // recurring problems: every department keeps the recurring list
            var recur = latest.Where(k => k.Recurring.Length > 0 && !outIds.Contains(k.Who)).OrderBy(k => Rank(k.Risk)).ThenBy(k => k.Person).ToList();
            if (recur.Count > 0)
            {
                var c = new Card { Key = "recurring", Title = "Recurring problems", Advisor = PhysioBy(), What = WhatRecurring, Cols = new[] { "Player", "Recurring", "Risk now", "Note" } };
                c.Count = recur.Count == 1 ? "1 player" : recur.Count + " players"; c.Level = 2;
                foreach (var k in recur)
                {
                    var also = p.Cards.Where(x => x.Rows.Any(r => r[0] == k.Person)).Select(x => x.Title.ToLowerInvariant()).FirstOrDefault();
                    var mins = Minutes(k);
                    c.Rows.Add(new[] { k.Person, Bare(k.Recurring), RiskWord(k.Risk), also != null ? "Also on the " + also + " card" : mins.Matches == 0 ? "No minutes in 14 days" : "" });
                    c.RowLevel.Add(Math.Min(3, Rank(k.Risk)));
                }
                if (adv >= 4)
                {
                    var spec = recur.Where(k => Muscle.IsMatch(k.Recurring)).ToList(); var rest = recur.Where(k => !Muscle.IsMatch(k.Recurring)).ToList();
                    if (spec.Count > 0)
                        c.Acts.Add(Plan("Specialist assessments for " + Join(spec.Select(k => Surname(k.Person)).ToList()) + ": " + (spec.Count == 1 ? "a " + Lower(spec[0].Recurring) + " that keeps coming back." : spec.Count == 2 ? "two problems that keep coming back." : Num(spec.Count) + " problems that keep coming back."),
                            "specialist", spec, spec.Count == 1 ? "Plan: specialist for " + Surname(spec[0].Person) : spec.Count == 2 ? "Plan: specialists for both" : "Plan: specialists for " + Num(spec.Count), "recurring", false));
                    foreach (var k in rest) c.Acts.Add(new Act { Text = Surname(k.Person) + ": the " + Lower(k.Recurring) + " is on file. Nothing to do while fit." });
                    c.Footer = physio < 0 ? "The head physio's skills aren't known yet: open Club › Staff › Medical Team once."
                        : "Why specialists: " + head.Name + "'s Physiotherapy (" + Band(physio) + ") suggests them for recurring problems. A head physio rated Average or below would only suggest rest, and only for very high risk.";
                }
                else
                {
                    c.Acts.Add(new Act { Text = "The physios will keep an eye on " + (recur.Count == 1 ? Surname(recur[0].Person) : recur.Count == 2 ? "both" : "all " + Num(recur.Count)) + "." });
                    var spec = recur.Where(k => Muscle.IsMatch(k.Recurring)).Select(k => Surname(k.Person)).ToList();
                    c.Footer = "A head physio rated Good or better would suggest specialist assessments" + (spec.Count > 0 ? " for " + Join(spec) : "") + ".";
                }
                p.Cards.Add(c);
            }
            // order: the most urgent first, recurring last
            p.Cards = p.Cards.OrderBy(c => c.Key == "recurring" ? 9 : c.Key == "other" ? 5 : c.Level).ThenByDescending(c => c.Rows.Count).ToList();
            // what the department can't turn into advice
            if (hidden.Count > 0 && reach < 2)
            {
                var kinds = new List<string>();
                if (hidden.Any(Condition)) kinds.Add("low condition");
                if (hidden.Any(Sharpness)) kinds.Add("low match sharpness");
                if (hidden.Any(Intensity)) kinds.Add("match fatigue and tactical intensity");
                if (hidden.Any(k => !Condition(k) && !Sharpness(k) && !Intensity(k))) kinds.Add("raised risk");
                p.Beyond = "The Medical Centre's Risk Assessment also shows " + Join(kinds) + " for " + (hidden.Count == 1 ? "one other player" : Num(hidden.Count) + " other players")
                    + (sci < 0 ? ". Open Club › Staff › Medical Team once, so the Treatment Room knows who signs the assessments and how far they can see."
                       : ". Nobody here can turn " + (hidden.Count == 1 ? "that" : "those") + " into advice: " + (reach >= 1 ? "an Outstanding" : "a Good or better") + " head of sports science would bring them to this page.");
            }

            // ---------- the department ----------
            var med = FM27MediaMod.Dept.Medical().Where(m => m.Left.Length == 0 && FM27MediaMod.Dept.FirstTeam(m)).OrderBy(m => FM27MediaMod.Dept.JobRank(m.Job)).ThenBy(m => m.Name).ToList();
            p.DeptKnown = med.Count > 0;
            string Short(FM27MediaMod.Dept.Member m, bool sports) { var b = sports ? m.Sports : m.Physio; return Surname(m.Name) + (FM27MediaMod.Dept.IsBand(b) ? " (" + b + ")" : ""); }
            if (head != null) p.Dept.Add(new DeptTile { Role = head.Job.Length > 0 ? head.Job : "Head Physio", Name = head.Name, Line = "Physiotherapy: " + (FM27MediaMod.Dept.IsBand(head.Physio) ? head.Physio : "not read yet") });
            if (author != null) p.Dept.Add(new DeptTile { Role = author.Job.Length > 0 ? author.Job : "Sports science", Name = author.Name, Line = "Sports Science: " + (FM27MediaMod.Dept.IsBand(author.Sports) ? author.Sports : "not read yet") });
            var physios = med.Where(m => Lower(m.Job).Contains("physio") && (head == null || m.Key != head.Key)).OrderByDescending(m => FM27MediaMod.Dept.Band(m.Physio)).ThenBy(m => m.Name).ToList();
            var posts = FM27MediaMod.Dept.Posts.Where(x => Lower(x.Key).Contains("physio") && !Lower(x.Key).StartsWith("head")).OrderBy(x => x.Key.Length).FirstOrDefault();
            if (physios.Count > 0 || posts.Key != null)
            {
                var t = new DeptTile { Role = "Physios" + (posts.Key != null ? " · " + posts.Value.Filled + " of " + posts.Value.Allowed + " posts" : " · " + physios.Count), Line = physios.Count > 0 ? Join(physios.Select(m => Short(m, false)).ToList()) : "Not read yet" };
                if (posts.Key != null && posts.Value.Filled < posts.Value.Allowed) t.Note = posts.Value.Allowed - posts.Value.Filled == 1 ? "One post empty" : char.ToUpperInvariant(Num(posts.Value.Allowed - posts.Value.Filled)[0]) + Num(posts.Value.Allowed - posts.Value.Filled).Substring(1) + " posts empty";
                p.Dept.Add(t);
            }
            var scis = med.Where(m => Lower(m.Job).Contains("sports scien") && (author == null || m.Key != author.Key)).OrderByDescending(m => FM27MediaMod.Dept.Band(m.Sports)).ThenBy(m => m.Name).ToList();
            var docs = med.Where(m => Lower(m.Job).Contains("doctor")).ToList();
            if (scis.Count > 0 || docs.Count > 0)
                p.Dept.Add(new DeptTile { Role = scis.Count > 0 ? "Sports scientists · " + scis.Count : "Doctors", Line = scis.Count > 0 ? Join(scis.Select(m => Short(m, true)).ToList()) : Join(docs.Select(m => m.Name).ToList()),
                    Note = scis.Count > 0 && docs.Count > 0 ? (docs[0].Job.Length > 0 ? docs[0].Job : "Doctor") + ": " + docs[0].Name : "" });
            foreach (var kv in FM27MediaMod.Dept.Compare.OrderBy(c => c.Key == "Physiotherapy" ? 0 : c.Key == "Sports Science" ? 1 : 2).Take(2))
            {
                p.DeptLines.Add("How you compare: " + kv.Key + " " + kv.Value.Rank + (kv.Value.Verdict.Length > 0 ? " (" + kv.Value.Verdict + ")" : "") + ".");
            }
            if (p.DeptKnown)
            {
                var gets = new List<string>();
                gets.Add(reach >= 2 ? "the full watch list" : reach == 1 ? "high and very high risk" : reach == 0 ? "very high risk only" : "no risk tracking");
                if (reach >= 1) gets.Add("the reasons behind each risk");
                if (physio >= 6) gets.Add("confident return estimates");
                if (adv >= 4) gets.Add("specialist plans for recurring problems");
                p.DeptLines.Add("What this department gets you: " + Join(gets) + ".");
                var read = med.Select(m => m.Seen).Where(d => Day(d) >= 0).OrderByDescending(Day).FirstOrDefault() ?? "";
                p.DeptRead = (read.Length > 0 ? "As read on " + read + " · " : "") + "open Club › Staff › Medical Team to refresh";
            }
            else p.DeptRead = "The Treatment Room hasn't met the medical staff yet. Open Club › Staff › Medical Team once and this fills in.";
            return p;
        }
        static int SusRank(string s) { switch (Lower(s)) { case "very high": return 0; case "high": return 1; case "above average": return 2; case "average": return 3; case "below average": return 4; case "low": return 5; case "very low": return 6; default: return 7; } }
    }
}
