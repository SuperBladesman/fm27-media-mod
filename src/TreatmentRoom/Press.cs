// FM27 Media Mod - Treatment Room 0.3.0: stories for the press (pure C#, tested offline).
// Design 4.3, "the press remembers medical calls". The Treatment Room writes "treatment press.tsv" whenever its ledger is
// saved: only fresh items (21 days at most), only what is public or plain from the record, with the facts already in words.
// Press Room (0.13.5+) reads the file and may open one question per conference with it. Neither plugin needs the other.
//   recurrence  a second (third...) injury in the same area within 12 months, the latest in the last 21 days
//   overrun     still out past the physio's longest forecast
//   comeback    back after a major injury, or after four weeks or more
//   training    away from training for a specialist assessment (a plan carried out)
//   pushed      the medical team suggested a rest, the player wasn't rested, and was injured within 14 days
//   crisis      (0.5.2) four or more of the squad out at once; "stretched" when a physio post is empty or two or more are
//               out per physio in post. Player = the latest one injured
//   staffgone   (0.5.2) the head physio, head of sports science or chief doctor gone from the staff list in the last 21 days
// The 0.5.2 kinds come last in the file: Press Room 0.13.5 stops at the first item it can use and does not know them.
// Personal absences are never included (Summary.Injuries()).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FM27MediaMod
{
    internal static class Press
    {
        internal const string FileName = "treatment press.tsv";
        internal sealed class Item { public string Id = "", Kind = "", Who = "", Player = "", Date = "", Area = "", Count = "", Month = "", Injury = "", Extra = ""; public int Priority; }

        static int Day(string d) => GameDate.Day(d);
        static string Bare(string injury)
        {
            var t = (injury ?? "").Trim();
            if (t.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2); else if (t.StartsWith("An ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(3);
            return t.Length > 0 ? char.ToLowerInvariant(t[0]) + t.Substring(1) : t;
        }
        static string MonthOf(string date)
        {
            int d = Day(date); if (d < 0) return "";
            return new DateTime(2000, 1, 1).AddDays(d).ToString("MMMM", CultureInfo.InvariantCulture);
        }

        internal static List<Item> Build(string today, int startMonth)
        {
            var items = new List<Item>();
            int now = Day(today);
            if (now < 0) return items;
            var squad = new HashSet<string>(Files.Players(today, startMonth).Where(p => p.InSquad).Select(p => p.Who));
            var inj = Summary.Injuries().Where(i => squad.Contains(i.Who)).ToList();

            // recurrence: the same area twice or more in a year, the latest in the last 21 days (at the club)
            foreach (var g in inj.Where(i => Day(i.Date) > now - 365 && Day(i.Date) <= now && Summary.Part(i.Injury).Length > 0).GroupBy(i => i.Who + "|" + Summary.Part(i.Injury)))
            {
                var l = g.OrderBy(i => Day(i.Date)).ToList();
                var last = l[l.Count - 1];
                bool atClub = ("," + last.Sources + ",").Contains(",season,") || ("," + last.Sources + ",").Contains(",current,");
                if (l.Count < 2 || !atClub || now - Day(last.Date) > 21) continue;
                items.Add(new Item { Id = "recurrence|" + last.Who + "|" + Summary.Part(last.Injury) + "|" + last.Date, Kind = "recurrence", Who = last.Who, Player = Files.Name(last.Who),
                    Date = last.Date, Area = Summary.Part(last.Injury), Count = l.Count.ToString(), Month = MonthOf(l[0].Date), Injury = Bare(last.Injury), Priority = 3 });
            }
            // overrun: still out past the longest forecast
            foreach (var f in Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0 && squad.Contains(f.Who)))
            {
                int.TryParse(f.Max, out var max); int out_ = now - Day(f.Date);
                if (max <= 0 || out_ <= max + 6) continue;
                items.Add(new Item { Id = "overrun|" + f.Who + "|" + f.Date + "|" + (out_ / 7), Kind = "overrun", Who = f.Who, Player = Files.Name(f.Who), Date = f.Date,
                    Injury = Bare(f.Injury), Extra = f.Expected.Replace(" - ", " to "), Count = (out_ / 7).ToString(), Priority = 2 });   // said aloud: "2 to 3 weeks"
            }
            // comeback: back after a major injury, or four weeks or more
            foreach (var r in Notes.All.Where(n => n.Kind == "returned" && squad.Contains(n.Who) && now - Day(n.Since) <= 14))
            {
                var i = inj.Where(x => x.Who == r.Who && Bare(x.Injury) == Bare(r.What)).OrderByDescending(x => Day(x.Date)).FirstOrDefault();
                if (i == null || Summary.Personal(i.Injury)) continue;
                bool big = i.Severity.Equals("Major", StringComparison.OrdinalIgnoreCase) || Summary.Days(i.OutFor) >= 28;
                if (!big) continue;
                items.Add(new Item { Id = "comeback|" + r.Who + "|" + i.Date, Kind = "comeback", Who = r.Who, Player = Files.Name(r.Who), Date = r.Since, Injury = Bare(i.Injury), Extra = i.OutFor, Priority = 4 });
            }
            // training: away for a specialist assessment
            foreach (var p in Notes.All.Where(n => n.Kind == "plan" && n.What == "specialist" && (n.Status == "carried out" || n.Status == "reviewed") && n.Done.Length > 0 && now - Day(n.Done) <= 10))
                items.Add(new Item { Id = "training|" + p.Who + "|" + p.Done, Kind = "training", Who = p.Who, Player = Files.Name(p.Who), Date = p.Done, Priority = 5 });
            // pushed: rest advice not taken, then injured
            foreach (var v in Notes.All.Where(n => n.Kind == "advice" && n.Status == "not taken" && n.Detail.Length > 0))
            {
                var i = inj.Where(x => x.Who == v.Who && Day(x.Date) > Day(v.Since) && Day(x.Date) <= Day(v.Since) + 14).OrderBy(x => Day(x.Date)).FirstOrDefault();
                if (i == null || now - Day(i.Date) > 21) continue;
                items.Add(new Item { Id = "pushed|" + v.Who + "|" + i.Date, Kind = "pushed", Who = v.Who, Player = Files.Name(v.Who), Date = i.Date, Injury = Bare(i.Injury), Extra = v.Since, Priority = 1 });
            }
            // 0.5.2: the department behind the injuries
            var outNow = Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0 && squad.Contains(f.Who) && Day(f.Date) >= 0 && now - Day(f.Date) <= 120 && !Summary.Personal(f.Injury))
                .OrderByDescending(f => Day(f.Date)).ToList();
            if (outNow.Count >= 4)
            {
                var latest = outNow[0];
                int physios = Dept.Posts.Count > 0 ? Dept.Posts.Where(p => p.Key.ToLowerInvariant().Contains("physio")).Sum(p => p.Value.Filled)
                                                   : Dept.Medical().Count(m => m.Job.ToLowerInvariant().Contains("physio"));
                bool empty = Dept.Posts.Any(p => p.Key.ToLowerInvariant().Contains("physio") && p.Value.Filled < p.Value.Allowed);
                bool stretched = empty || (physios > 0 && outNow.Count >= physios * 2);
                var kind = stretched ? "stretched" : "crisis";
                items.Add(new Item { Id = kind + "|" + outNow.Count + "|" + latest.Date, Kind = kind, Who = latest.Who, Player = Files.Name(latest.Who), Date = latest.Date,
                    Count = outNow.Count.ToString(), Injury = Bare(latest.Injury), Priority = 8 });
            }
            foreach (var m in Dept.Staff.Values.Where(m => m.Left.Length > 0 && Day(m.Left) >= 0 && now - Day(m.Left) <= 21
                         && (m.Job == "Head Physio" || m.Job == "Head of Sports Science" || m.Job == "Chief Doctor")))
                items.Add(new Item { Id = "staffgone|" + m.Key + "|" + m.Left, Kind = "staffgone", Who = m.Key, Player = m.Name, Date = m.Left, Area = m.Job.ToLowerInvariant(), Priority = 9 });
            return items.OrderBy(x => x.Priority).ThenByDescending(x => Day(x.Date)).ToList();
        }

        internal static string Header => "id\tkind\twho\tplayer\tdate\tarea\tcount\tmonth\tinjury\textra";
        internal static void Write(string dir, string today, int startMonth, Func<string, string> c)
        {
            var sb = new StringBuilder(Header + "\n");
            foreach (var x in Build(today, startMonth))
                sb.Append($"{c(x.Id)}\t{x.Kind}\t{x.Who}\t{c(x.Player)}\t{x.Date}\t{c(x.Area)}\t{x.Count}\t{x.Month}\t{c(x.Injury)}\t{c(x.Extra)}\n");
            File.WriteAllText(Path.Combine(dir, FileName), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
