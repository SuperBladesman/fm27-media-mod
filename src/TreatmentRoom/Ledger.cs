// FM27 Media Mod - Treatment Room: the ledger (read-only record of what the game showed), kept per save by TrMemory.
//   treatment injuries.tsv   every injury seen, one row per player + date + injury (Season Summary, history pages,
//                            Current Injuries, the league table for our players)
//   treatment forecasts.tsv  the physio's expected return when an injury was current (league table, Current Injuries),
//                            and the game date the player was first seen back (Recently Returned)
//   treatment actions.tsv    medical actions taken (rest, reports, risk updates...) and news responses after a treatment offer
//   treatment risk.tsv       Risk Assessment rows, one per player per game day, by column (0.2.0; 0.1.1's rows are converted on load)
//   treatment squad.tsv      who is ours: Player Summary only (tag, injuries this season and last, time and % missed, last seen).
//                            Season Summary also lists players who have since left, so it no longer adds to the squad (0.1.1)
//   treatment offers.tsv     SI's own treatment offers seen in the inbox (specialist, protective equipment)
//   treatment notes.tsv      the manager's watch list and plans, the medical team's rest advice, staff names, recent returns (Notes.cs, 0.3.0)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FM27MediaMod
{
    internal static class Ledger
    {
        internal const string FInj = "treatment injuries.tsv", FFc = "treatment forecasts.tsv", FAct = "treatment actions.tsv",
            FRisk = "treatment risk.tsv", FSquad = "treatment squad.tsv", FOffer = "treatment offers.tsv", FNotes = "treatment notes.tsv";
        internal static readonly string[] Files = { FInj, FFc, FAct, FRisk, FSquad, FOffer, FNotes, Press.FileName, Dept.FileName };
        internal static int StartMonth = 7;   // set from the config at load (TreatmentRoom.SeasonStartMonth)
        internal static IEnumerable<string[]> ActionRows() => ActionLines.Select(l => l.Split('\t')).ToList();
        internal static IEnumerable<string[]> OfferRows() => Offers.Values.Select(l => l.Split('\t')).ToList();
        static string P(string f) => Path.Combine(TreatmentRoom.Dir, f);
        static string C(string s) => TreatmentRoom.C(s);

        internal sealed class Inj
        {
            public string Who = "", PersonId = "", Person = "", Date = "", Injury = "", Severity = "", Received = "", Cause = "", Treatment = "",
                OutFor = "", Sources = "", Ours = "", FirstSeen = "", LastSeen = "";
        }
        internal sealed class Fc
        {
            public string Who = "", PersonId = "", Person = "", Club = "", Injury = "", Date = "", Expected = "", Min = "", Max = "",
                FirstSeen = "", LastSeen = "", Returned = "", Ours = "", By = "";   // 0.5.1: By = the head physio when the forecast was first seen
        }

        internal static readonly Dictionary<string, Inj> Injuries = new Dictionary<string, Inj>();
        internal static readonly Dictionary<string, Fc> Forecasts = new Dictionary<string, Fc>();
        internal sealed class Sq { public string Name = "", Tag = "", Count = "", Last = "", Missed = "", Pct = "", Seen = ""; }
        internal static readonly Dictionary<string, Sq> Squad = new Dictionary<string, Sq>();               // who -> Player Summary line
        internal sealed class Rk
        {
            public string Date = "", Who = "", Person = "", Status = "", MatchLoad = "", MatchLine = "", TrainLoad = "", TrainLine = "", Suscept = "",
                Fatigue = "", Recurring = "", Risk = "", Factor = "";
        }
        internal static readonly Dictionary<string, Rk> Risk = new Dictionary<string, Rk>();              // who|game date -> row
        static Rk RkOf(Row r, string who) => new Rk { Date = GameDate.Text, Who = who, Person = r.Person, Status = r.Status, MatchLoad = r.MatchLoad,
            MatchLine = r.MatchLine, TrainLoad = r.TrainLoad, TrainLine = r.TrainLine, Suscept = r.Suscept, Fatigue = r.Fatigue, Recurring = r.Recurring, Risk = r.Risk, Factor = r.Factor };
        static string Line(Rk k) => $"{k.Date}\t{k.Who}\t{C(k.Person)}\t{C(k.Status)}\t{C(k.MatchLoad)}\t{C(k.MatchLine)}\t{C(k.TrainLoad)}\t{C(k.TrainLine)}\t{C(k.Suscept)}\t{C(k.Fatigue)}\t{C(k.Recurring)}\t{C(k.Risk)}\t{C(k.Factor)}";
        // each player's latest Risk Assessment row
        internal static List<Rk> LatestRisk()
        {
            var best = new Dictionary<string, Rk>();
            foreach (var k in Risk.Values) if (!best.TryGetValue(k.Who, out var b) || GameDate.Day(k.Date) > GameDate.Day(b.Date)) best[k.Who] = k;
            return best.Values.ToList();
        }
        static readonly List<string> ActionLines = new List<string>();
        static readonly Dictionary<string, string> Offers = new Dictionary<string, string>();              // text -> line
        static string LastOfferText = ""; static DateTime LastOfferAt = DateTime.MinValue;

        static string Key(string who, string date, string injury) => who + "|" + date + "|" + (injury ?? "").ToLowerInvariant();

        // the same person seen without a link record: use the id we already know for that name, if exactly one
        static string Canon(Row r)
        {
            if (r.PersonId.Length > 0) return r.PersonId;
            if (r.Person.Length == 0) return "";
            var ids = Squad.Where(kv => kv.Value.Name == r.Person && !kv.Key.StartsWith("name:")).Select(kv => kv.Key).ToList();
            return ids.Count == 1 ? ids[0] : "name:" + r.Person;
        }
        static string OursOf(Row r, string who)
        {
            if (r.Kind == "season" || r.Kind == "current" || r.Kind == "returned") return "y";
            if (Squad.ContainsKey(who)) return "y";
            if (Squad.Count == 0) return "?";
            return r.Kind == "league" ? "n" : "?";
        }
        static void AddSource(Inj i, string s) { if (("," + i.Sources + ",").IndexOf("," + s + ",", StringComparison.Ordinal) < 0) i.Sources = i.Sources.Length == 0 ? s : i.Sources + "," + s; }
        static bool Fill(ref string field, string v) { if (string.IsNullOrEmpty(v) || field == v) return false; if (field.Length > 0 && v == "-") return false; field = v; return true; }

        // one parsed row; true if anything new or changed
        internal static bool Take(Row r)
        {
            var who = Canon(r);
            if (who.Length == 0) return false;
            bool changed = false;

            if (r.Kind == "squad")
            {
                if (!Squad.TryGetValue(who, out var q)) { q = new Sq(); Squad[who] = q; changed = true; }
                string n(int v) => v >= 0 ? v.ToString() : "";
                var nq = new Sq { Name = r.Person.Length > 0 ? r.Person : q.Name, Tag = r.Tag, Count = n(r.Count), Last = n(r.LastCount), Missed = r.OutFor, Pct = n(r.Pct), Seen = GameDate.Text };
                if (nq.Name != q.Name || nq.Tag != q.Tag || nq.Count != q.Count || nq.Last != q.Last || nq.Missed != q.Missed || nq.Pct != q.Pct || nq.Seen != q.Seen)
                { Squad[who] = nq; changed = true; }
                return changed;
            }
            if (r.Kind == "risk")
            {
                if (r.Risk.Length == 0) return false;   // not a full row (a header, or SI still filling it)
                var k = who + "|" + GameDate.Text; var nk = RkOf(r, who);
                if (!Risk.TryGetValue(k, out var old) || Line(old) != Line(nk)) { Risk[k] = nk; changed = true; }
                return changed;
            }
            var ours = OursOf(r, who);

            // an injury with a date: the ledger (other clubs' injuries from the league table are left out)
            if (r.Date.Length > 0 && r.Injury.Length > 0 && r.Kind != "returned" && ours != "n")
            {
                var k = Key(who, r.Date, r.Injury);
                if (!Injuries.TryGetValue(k, out var i))
                {
                    i = new Inj { Who = who, PersonId = r.PersonId, Person = r.Person, Date = r.Date, Injury = r.Injury, FirstSeen = GameDate.Text };
                    Injuries[k] = i; changed = true;
                }
                if (r.Person.Length > i.Person.Length) { i.Person = r.Person; changed = true; }   // keep the fuller name ("Pablo Felipe", not "P. Felipe")
                changed |= Fill(ref i.PersonId, r.PersonId);
                changed |= Fill(ref i.Severity, r.Severity); changed |= Fill(ref i.Received, r.Received); changed |= Fill(ref i.Cause, r.Cause);
                changed |= Fill(ref i.Treatment, r.Treatment); changed |= Fill(ref i.OutFor, r.OutFor); changed |= Fill(ref i.Ours, ours);
                var before = i.Sources; AddSource(i, r.Kind); changed |= before != i.Sources;
                i.LastSeen = GameDate.Text;
            }

            // a forecast: the expected return while the injury is current
            if (r.Expected.Length > 0 && r.Injury.Length > 0 && (r.Kind == "league" || r.Kind == "current") && ours != "n")
            {
                var k = Key(who, r.Date, r.Injury);
                if (!Forecasts.TryGetValue(k, out var f))
                {
                    f = new Fc { Who = who, PersonId = r.PersonId, Person = r.Person, Club = r.Club, Injury = r.Injury, Date = r.Date,
                        Expected = r.Expected, Min = r.MinDays.ToString(), Max = r.MaxDays.ToString(), FirstSeen = GameDate.Text, Ours = ours,
                        By = ours == "y" ? (Brief.BriefingAuthor()?.Name ?? "") : "" };
                    Forecasts[k] = f; changed = true;
                    TreatmentRoom.W("forecast", r.Person + ": " + r.Injury, r.Expected + " (injured " + r.Date + ")");
                }
                else if (f.Expected != r.Expected)
                {   // the physio revised it: keep the first forecast, note the change
                    TreatmentRoom.W("forecast revised", r.Person + ": " + r.Injury, f.Expected + " -> " + r.Expected);
                    f.Expected = f.Expected + " -> " + r.Expected; changed = true;
                }
                f.LastSeen = GameDate.Text;
            }

            // back from injury: the first game date seen on Recently Returned closes the open forecast for that player
            if (r.Kind == "returned")
            {
                if (Notes.Returned(who, r.Person, r.Injury, r.FitnessTest, r.Recommendation, GameDate.Text)) changed = true;
                var open = Forecasts.Values.Where(x => x.Who == who && x.Returned.Length == 0).OrderByDescending(x => GameDate.Day(x.Date)).FirstOrDefault();
                if (open != null && GameDate.Text.Length > 0) { open.Returned = GameDate.Text; changed = true; TreatmentRoom.W("returned", r.Person + ": " + open.Injury, "expected " + open.Expected + ", back by " + GameDate.Text); }
            }
            return changed;
        }

        internal static void Offer(string plain, string raw)
        {
            try
            {
                LastOfferText = plain; LastOfferAt = DateTime.Now;
                if (Offers.ContainsKey(plain)) return;
                string who = "";
                if (raw.Length > 0) foreach (var l in PersonLinks.All(raw)) if (l.IsPerson) { who = l.Visible + " [" + l.Key + "]"; break; }
                Offers[plain] = $"{GameDate.Text}\t{C(who)}\t{C(plain)}";
                NewOffers.Enqueue((who, plain));   // 0.5.8: the popout's note on SI's treatment choice
                TreatmentRoom.W("offer", who, plain);
                Save();
            }
            catch (Exception e) { TreatmentRoom.Fail("offer", e); }
        }
        internal static DateTime AnsweredAt = DateTime.MinValue;   // 0.5.8: a news response seen soon after a treatment offer
        internal static readonly Queue<(string Who, string Plain)> NewOffers = new Queue<(string, string)>();
        internal static string RecentOffer(int minutes) => (DateTime.Now - LastOfferAt).TotalMinutes <= minutes ? LastOfferText : "";

        static string LastActionKey = ""; static DateTime LastActionAt = DateTime.MinValue;
        internal static void Action(string channel, string name, string id, string who, string detail)
        {
            var k = name + "|" + who;
            if (k == LastActionKey && (DateTime.Now - LastActionAt).TotalSeconds < 3) return;   // the same click seen on two channels
            LastActionKey = k; LastActionAt = DateTime.Now;
            ActionLines.Add($"{GameDate.Text}\t{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{channel}\t{name}\t{id}\t{C(who)}\t{C(detail)}");
            TreatmentRoom.W("medical action", name + " (" + channel + ")", who + " | " + detail);
            Save();
        }

        // ---------- files ----------
        internal static void Save()
        {
            try
            {
                // one name per player: the fullest seen anywhere ("Japhet Tanganga", not the profile's "J. Tanganga")
                var best = new Dictionary<string, string>();
                void Seen(string w, string n) { if (w.Length > 0 && n.Length > 0 && (!best.TryGetValue(w, out var b) || n.Length > b.Length)) best[w] = n; }
                foreach (var kv in Squad) Seen(kv.Key, kv.Value.Name);
                foreach (var x in Injuries.Values) Seen(x.Who, x.Person);
                foreach (var x in Injuries.Values) if (best.TryGetValue(x.Who, out var bn)) x.Person = bn;
                var sb = new StringBuilder("who\tperson id\tperson\tdate\tinjury\tseverity\treceived\tcause\ttreatment\tout for\tsources\tours\tfirst seen\tlast seen\n");
                foreach (var i in Injuries.Values.OrderBy(x => x.Person).ThenBy(x => GameDate.Day(x.Date)))
                    sb.Append($"{i.Who}\t{i.PersonId}\t{C(i.Person)}\t{i.Date}\t{C(i.Injury)}\t{i.Severity}\t{i.Received}\t{C(i.Cause)}\t{C(i.Treatment)}\t{C(i.OutFor)}\t{i.Sources}\t{i.Ours}\t{i.FirstSeen}\t{i.LastSeen}\n");
                Write(FInj, sb);
                sb = new StringBuilder("who\tperson id\tperson\tclub\tinjury\tdate injured\texpected\tmin days\tmax days\tfirst seen\tlast seen\treturned\tours\tphysio\n");
                foreach (var f in Forecasts.Values.OrderBy(x => GameDate.Day(x.Date)))
                    sb.Append($"{f.Who}\t{f.PersonId}\t{C(f.Person)}\t{C(f.Club)}\t{C(f.Injury)}\t{f.Date}\t{C(f.Expected)}\t{f.Min}\t{f.Max}\t{f.FirstSeen}\t{f.LastSeen}\t{f.Returned}\t{f.Ours}\t{C(f.By)}\n");
                Write(FFc, sb);
                sb = new StringBuilder("game date\treal time\tchannel\taction\tid\twho\tdetail\n"); foreach (var l in ActionLines) sb.Append(l).Append('\n'); Write(FAct, sb);
                sb = new StringBuilder("game date\twho\tperson\tstatus\tmatch load\tmatches\ttraining load\ttraining\tsusceptibility\tfatigue\trecurring\trisk\tmain factor\n");
                foreach (var k in Risk.Values.OrderBy(x => GameDate.Day(x.Date)).ThenBy(x => x.Person)) sb.Append(Line(k)).Append('\n');
                Write(FRisk, sb);
                sb = new StringBuilder("who\tname\ttag\tinjuries this season\tinjuries last season\ttime missed\t% missed\tlast seen\n");
                foreach (var kv in Squad.OrderBy(x => x.Value.Name))
                { var q = kv.Value; sb.Append($"{kv.Key}\t{C(q.Name)}\t{C(q.Tag)}\t{q.Count}\t{q.Last}\t{C(q.Missed)}\t{q.Pct}\t{q.Seen}\n"); }
                Write(FSquad, sb);
                sb = new StringBuilder("game date\twho\ttext\n"); foreach (var l in Offers.Values) sb.Append(l).Append('\n'); Write(FOffer, sb);
                sb = new StringBuilder(Notes.Header + "\n"); foreach (var n in Notes.All) sb.Append(Notes.Line(n, C)).Append('\n'); Write(FNotes, sb);
                sb = new StringBuilder(Dept.Header + "\n"); foreach (var r in Dept.Rows(C)) sb.Append(r).Append('\n'); Write(Dept.FileName, sb);   // 0.4.0
                // 0.3.1: fresh medical stories for Press Room (design 4.3); recomputed on every save
                try { Press.Write(TreatmentRoom.Dir, GameDate.Text, StartMonth, C); } catch (Exception pe) { TreatmentRoom.Fail("press file", pe); }
            }
            catch (Exception e) { TreatmentRoom.Fail("save", e); }
        }
        static void Write(string f, StringBuilder sb) => File.WriteAllText(P(f), sb.ToString(), new UTF8Encoding(false));

        static List<string[]> Read(string f)
        {
            var list = new List<string[]>();
            try { if (File.Exists(P(f))) foreach (var l in File.ReadAllLines(P(f)).Skip(1)) if (l.Length > 0) list.Add(l.Split('\t')); }
            catch (Exception e) { TreatmentRoom.Fail("read " + f, e); }
            return list;
        }
        static string At(string[] a, int i) => i < a.Length ? a[i] : "";

        internal static void Load()
        {
            Injuries.Clear(); Forecasts.Clear(); Squad.Clear(); Risk.Clear(); ActionLines.Clear(); Offers.Clear(); Notes.All.Clear();
            foreach (var a in Read(FNotes)) { var n = Notes.From(a, At); if (n.Kind.Length > 0) Notes.All.Add(n); }
            Dept.Clear(); foreach (var a in Read(Dept.FileName)) Dept.From(a, At);   // 0.4.0: the medical department
            int merged = 0;
            foreach (var a in Read(FInj))
            {
                var i = new Inj { Who = At(a, 0), PersonId = At(a, 1), Person = At(a, 2), Date = Parse.NormDate(At(a, 3)), Injury = At(a, 4), Severity = At(a, 5), Received = At(a, 6),
                    Cause = At(a, 7), Treatment = At(a, 8), OutFor = At(a, 9), Sources = At(a, 10), Ours = At(a, 11), FirstSeen = At(a, 12), LastSeen = At(a, 13) };
                var k = Key(i.Who, i.Date, i.Injury);
                if (Injuries.TryGetValue(k, out var had)) { Merge(had, i); merged++; } else Injuries[k] = i;
            }
            foreach (var a in Read(FFc))
            {
                var f = new Fc { Who = At(a, 0), PersonId = At(a, 1), Person = At(a, 2), Club = At(a, 3), Injury = At(a, 4), Date = At(a, 5), Expected = At(a, 6),
                    Min = At(a, 7), Max = At(a, 8), FirstSeen = At(a, 9), LastSeen = At(a, 10), Returned = At(a, 11), Ours = At(a, 12), By = At(a, 13) };
                f.Date = Parse.NormDate(f.Date);
                Forecasts[Key(f.Who, f.Date, f.Injury)] = f;
            }
            foreach (var a in Read(FSquad))   // 0.1.0 files have only who and name
                Squad[At(a, 0)] = new Sq { Name = At(a, 1), Tag = At(a, 2), Count = At(a, 3), Last = At(a, 4), Missed = At(a, 5), Pct = At(a, 6), Seen = At(a, 7) };
            int conv = 0;
            foreach (var a in Read(FRisk))
            {
                Rk k;
                if (a.Length <= 7)
                {   // 0.1.1: game date, who, person, level, status, first text, other texts (" / ")
                    var t = new List<string>();
                    if (At(a, 4).Length > 0) t.Add(At(a, 4));
                    if (At(a, 5).Length > 0) t.Add(At(a, 5));
                    foreach (var x in At(a, 6).Split(new[] { " / " }, StringSplitOptions.None)) if (x.Length > 0) t.Add(x);
                    var r = Parse.RiskFrom(new Row { Person = At(a, 2) }, t);
                    k = new Rk { Date = At(a, 0), Who = At(a, 1), Person = At(a, 2), Status = r.Status, MatchLoad = r.MatchLoad, MatchLine = r.MatchLine, TrainLoad = r.TrainLoad,
                        TrainLine = r.TrainLine, Suscept = r.Suscept, Fatigue = r.Fatigue, Recurring = r.Recurring, Risk = r.Risk, Factor = r.Factor };
                    conv++;
                }
                else k = new Rk { Date = At(a, 0), Who = At(a, 1), Person = At(a, 2), Status = At(a, 3), MatchLoad = At(a, 4), MatchLine = At(a, 5), TrainLoad = At(a, 6),
                    TrainLine = At(a, 7), Suscept = At(a, 8), Fatigue = At(a, 9), Recurring = At(a, 10), Risk = At(a, 11), Factor = At(a, 12) };
                Risk[k.Who + "|" + k.Date] = k;
            }
            foreach (var a in Read(FAct)) ActionLines.Add(string.Join("\t", a));
            foreach (var a in Read(FOffer)) Offers[At(a, 2)] = string.Join("\t", a);
            TreatmentRoom.W("ledger", "loaded", $"{Injuries.Count} injuries, {Forecasts.Count} forecasts, {Squad.Count} squad, {Risk.Count} risk rows, {ActionLines.Count} actions, {Notes.All.Count} notes, {Dept.Staff.Count} staff" +
                (merged > 0 ? $"; {merged} duplicate rows merged (dates written two ways)" : "") + (conv > 0 ? $"; {conv} risk rows converted from 0.1.1" : ""));
            if (merged > 0 || conv > 0) Save();
        }
        // two rows for one injury (0.1.0 wrote history dates as 05/11/2026, Season Summary's as 5/11/2026): keep the fuller of each field
        static void Merge(Inj into, Inj from)
        {
            if (from.Person.Length > into.Person.Length) into.Person = from.Person;
            Fill(ref into.PersonId, from.PersonId); Fill(ref into.Severity, from.Severity); Fill(ref into.Received, from.Received);
            Fill(ref into.Cause, from.Cause); Fill(ref into.Treatment, from.Treatment); Fill(ref into.OutFor, from.OutFor);
            if (into.Ours != "y" && from.Ours.Length > 0) into.Ours = from.Ours;
            foreach (var src in from.Sources.Split(',')) if (src.Length > 0) AddSource(into, src);
            if (into.FirstSeen.Length == 0 || (from.FirstSeen.Length > 0 && GameDate.Day(from.FirstSeen) < GameDate.Day(into.FirstSeen))) into.FirstSeen = from.FirstSeen;
            if (GameDate.Day(from.LastSeen) > GameDate.Day(into.LastSeen)) into.LastSeen = from.LastSeen;
        }

        internal static void Clear() { foreach (var f in Files) File.WriteAllText(P(f), ""); Load(); }
    }
}
