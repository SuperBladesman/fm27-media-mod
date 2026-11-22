// FM27 Media Mod - the press pack (Press Room 0.10). MIT licence (see LICENSE).
// Until 0.9 every answer you gave earned +1 with whoever asked it, so everyone ended up loving you. Now each journalist
// has a character and wants something, and how you get on is worked out again from everything on the record:
//   - what you say matters: red tops and phone-ins want a story, broadsheets candour, local press loyalty, the
//     opponents' local paper respect for their club; stock positive answers bore the people who want more;
//   - each journalist has a fixed character (straight reporter, old school, attack dog, contrarian, insider, cheerleader)
//     that sets where they start, how fast they warm up and how hard they take a slight;
//   - relationships drift back to each journalist's normal every conference: favour has to be kept up, grudges fade slowly;
//   - the results turn the press: our results (SI's own "Key Findings" match headlines), pressure stories about the
//     manager, the supporters' mood and open dressing-room stories raise the pressure, and the hard cases turn first;
//   - favouring one outlet annoys the others; a mission can fail;
//   - U-turns: an answer on the record about a player, contradicted by a later sale or signing, is thrown back once;
//   - push someone far enough and they have it in for you: blunt openers, and they hear your quiet whispers first.
// Nothing is invented about real people: every line restates what the record, the news or the supporters' screens say.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Pack
    {
        internal enum Trait { Straight, OldSchool, AttackDog, Contrarian, Insider, Cheerleader }
        internal class J
        {
            public string Name = "", Outlet = ""; public Trait T; public int Base, Rel, LastConf = -1, Seen;
            public string Grievance = ""; public int GrievanceConf = -1; public string Town = ""; public bool OurTown;
        }
        internal class Ev { public int Conf; public string Kind = "", Who = "", Why = ""; public int Delta; }
        internal class Result { public int Conf; public char R; public string Headline = "", Opponent = ""; public int For, Against; }
        internal class UTurn { public string Player = "", Kind = "", Quote = "", State = "pending"; public int SaidConf, CaughtConf = -1; }

        internal static bool On = true;
        internal static int Harsh = 1;                    // 0 gentle, 1 realistic, 2 brutal
        internal static readonly List<Ev> Events = new List<Ev>();
        internal static readonly List<Result> Results = new List<Result>();
        internal static readonly List<UTurn> UTurns = new List<UTurn>();
        internal static readonly List<int> PressureStories = new List<int>();   // conferences at which a "pressure on the manager" story was seen
        static readonly Dictionary<string, J> Js = new Dictionary<string, J>();
        static int BuiltFor = -1, BuiltConf = -1, BuiltEvents = -1;
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "press pack.tsv");
        internal const string FileName = "press pack.tsv";
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }
        static uint Mix(uint x) { x ^= x >> 16; x *= 0x85ebca6b; x ^= x >> 13; x *= 0xc2b2ae35; x ^= x >> 16; return x; }
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // ---------- who they are ----------
        internal static string TraitName(Trait t)
        {
            switch (t)
            {
                case Trait.OldSchool: return "Old school";
                case Trait.AttackDog: return "Attack dog";
                case Trait.Contrarian: return "Contrarian";
                case Trait.Insider: return "Insider";
                case Trait.Cheerleader: return "Cheerleader";
                default: return "Straight reporter";
            }
        }
        internal static string Wants(J j)
        {
            switch (j.T)
            {
                case Trait.OldSchool: return "straight answers, not stock lines";
                case Trait.AttackDog: return "a scalp";
                case Trait.Contrarian: return "an argument";
                case Trait.Insider: return "exclusives";
                case Trait.Cheerleader: return "good news for the club";
            }
            var k = PressRoom.Classify(j.Outlet);
            if (!j.OurTown && j.Town.Length > 0) return "respect for their club";
            switch (k)
            {
                case PressRoom.Kind.RedTop: return "a headline";
                case PressRoom.Kind.PhoneIn: return "something to argue about";
                case PressRoom.Kind.Broadsheet: return "candour";
                case PressRoom.Kind.Local: return "loyalty to the club";
                default: return "a fair answer";
            }
        }

        static readonly HashSet<string> GenericWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "the", "bbc", "radio", "football", "messenger", "chronicle", "echo", "post", "gazette", "examiner", "herald", "evening", "news", "argus", "mercury",
          "journal", "advertiser", "star", "telegraph", "live", "press", "courier", "sentinel", "times", "sport", "sports", "daily", "weekly", "north", "south",
          "east", "west", "league", "paper", "life", "sporting", "talksport", "sky", "itv", "onefootball", "fourfourtwo", "sgorio", "matchday", "world" };
        // the town a local outlet serves ("The Wolverhampton Football Messenger" -> Wolverhampton), if any
        static string TownOf(string outlet)
        {
            var k = PressRoom.Classify(outlet);
            if (k == PressRoom.Kind.Broadcast && outlet.IndexOf("Radio", StringComparison.OrdinalIgnoreCase) < 0) return "";
            if (k == PressRoom.Kind.PhoneIn || k == PressRoom.Kind.RedTop || k == PressRoom.Kind.Broadsheet) return "";
            foreach (var w in Regex.Split(outlet ?? "", @"[^\p{L}']+"))
                if (w.Length >= 4 && !GenericWords.Contains(w) && char.IsUpper(w[0]) && !Regex.IsMatch(w, @"\d")) return w;
            return "";
        }
        static string OurTown() { var c = Incidents.OwnClubName(); var p = c.Split(' '); return p.Length > 0 && p[0].Length >= 4 ? p[0] : c; }
        static bool SameStem(string a, string b) => a.Length >= 4 && b.Length >= 4 && string.Compare(a, 0, b, 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
        // does this text name the club that this outlet's town follows? ("Wolves" for Wolverhampton)
        static bool NamesTheirClub(J j, string text)
        {
            if (j.Town.Length == 0 || j.OurTown || string.IsNullOrEmpty(text)) return false;
            foreach (var w in Regex.Split(text, @"[^\p{L}']+")) if (char.IsUpper(w.Length > 0 ? w[0] : 'a') && SameStem(w, j.Town)) return true;
            return false;
        }

        static J Make(string name, string outlet)
        {
            var j = new J { Name = name, Outlet = outlet };
            j.Town = TownOf(outlet);
            var ours = OurTown();
            j.OurTown = j.Town.Length > 0 && ours.Length > 0 && SameStem(j.Town, ours);
            var k = PressRoom.Classify(outlet);
            uint r = Mix(Hash(name + "|" + outlet)) % 100;
            if (j.OurTown) j.T = r < 45 ? Trait.Cheerleader : r < 70 ? Trait.Straight : r < 85 ? Trait.OldSchool : Trait.Insider;
            else if (j.Town.Length > 0 && k != PressRoom.Kind.Broadcast) j.T = r < 20 ? Trait.AttackDog : r < 40 ? Trait.Contrarian : Trait.Straight;
            else if (k == PressRoom.Kind.RedTop || k == PressRoom.Kind.PhoneIn) j.T = r < 30 ? Trait.AttackDog : r < 55 ? Trait.Contrarian : r < 75 ? Trait.Insider : Trait.Straight;
            else if (k == PressRoom.Kind.Broadsheet) j.T = r < 40 ? Trait.OldSchool : r < 60 ? Trait.Contrarian : Trait.Straight;
            else if (k == PressRoom.Kind.Broadcast) j.T = r < 6 ? Trait.AttackDog : r < 16 ? Trait.Contrarian : r < 26 ? Trait.Insider : r < 46 ? Trait.OldSchool : r < 54 ? Trait.Cheerleader : Trait.Straight;
            else j.T = r < 10 ? Trait.AttackDog : r < 24 ? Trait.Contrarian : r < 38 ? Trait.Insider : r < 54 ? Trait.OldSchool : r < 64 ? Trait.Cheerleader : Trait.Straight;
            int b = 0;
            switch (j.T) { case Trait.AttackDog: b = -2; break; case Trait.Contrarian: b = -1; break; case Trait.Cheerleader: b = 2; break; }
            if (k == PressRoom.Kind.RedTop || k == PressRoom.Kind.PhoneIn) b -= 1;
            if (j.OurTown) b += 1; else if (j.Town.Length > 0) b -= 1;
            j.Base = b; j.Rel = b;
            return j;
        }

        // ---------- what an answer is ----------
        // +1 positive, 0 neutral, -1 negative: the group heading the answer sat under, else SI's slot (5 = top)
        internal static int Tone(PressRoom.Record.E e)
        {
            var g = e.Group ?? "";
            if (g.StartsWith("Pos", StringComparison.OrdinalIgnoreCase) || g.StartsWith("Agree", StringComparison.OrdinalIgnoreCase)) return 1;
            if (g.StartsWith("Neg", StringComparison.OrdinalIgnoreCase) || g.StartsWith("Disagree", StringComparison.OrdinalIgnoreCase)) return -1;
            if (g.StartsWith("Neu", StringComparison.OrdinalIgnoreCase)) return 0;
            return e.Value >= 4 ? 1 : e.Value == 3 || e.Value == 0 ? 0 : -1;
        }
        static readonly Regex Disparaging = new Regex(@"\b(lucky|fortunate|poor|boring|negative|parked the bus|anti-?football|dirty|cynical|time-?wast\w*|dive|dived|diving|cheat\w*|didn't deserve|rubbish|dull|long[- ]ball|kicked|bully\w*|nothing about them|got away with)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // how one entry on the record moves the journalist who asked it (realistic scale; Harsh scales the bad news)
        static int Effect(J j, PressRoom.Record.E e, int posRun, out string grievance)
        {
            grievance = null;
            var k = PressRoom.Classify(j.Outlet);
            int d = 0;
            switch (e.Kind)
            {
                case "answer":
                {
                    int t = Tone(e);
                    bool rivalTown = !j.OurTown && j.Town.Length > 0;
                    // what the outlet wants
                    if (rivalTown && NamesTheirClub(j, e.Question))
                    {
                        if (t < 0 && NamesTheirClub(j, e.Answer) && Disparaging.IsMatch(e.Answer ?? "")) { d = -3; grievance = "slight"; }
                        else d = t > 0 ? 1 : 0;
                    }
                    else switch (k)
                    {
                        case PressRoom.Kind.RedTop: case PressRoom.Kind.PhoneIn: d = t > 0 ? 0 : t == 0 ? -1 : 2; break;
                        case PressRoom.Kind.Broadsheet: d = t > 0 ? 0 : 1; break;
                        case PressRoom.Kind.Local: d = j.OurTown ? (t > 0 ? 1 : 0) : (t >= 0 ? 1 : 0); break;
                        case PressRoom.Kind.Digital: d = t != 0 ? 1 : 0; break;
                        default: d = t > 0 ? 1 : 0; break;
                    }
                    // who they are
                    switch (j.T)
                    {
                        case Trait.OldSchool: if (t == 0) { d -= 1; grievance = grievance ?? "evasive"; } else if (t < 0) d += 1; break;
                        case Trait.AttackDog: if (d > 0) d = d / 2; break;
                        case Trait.Contrarian: if (t > 0) d -= 1; else if (t < 0) d += 1; break;
                        case Trait.Insider: d = Math.Min(d, 0); break;
                        case Trait.Cheerleader: if (t > 0) d += 1; else if (t < 0) d -= 1; break;
                    }
                    // the third stock positive answer in a row: the people who wanted more switch off
                    if (t > 0 && posRun >= 4 && (k == PressRoom.Kind.RedTop || k == PressRoom.Kind.PhoneIn || j.T == Trait.Contrarian || j.T == Trait.OldSchool || j.T == Trait.AttackDog))
                    { d -= 1; grievance = grievance ?? "bored"; }
                    break;
                }
                case "nocomment": d = j.T == Trait.AttackDog ? -5 : j.T == Trait.OldSchool ? -4 : j.T == Trait.Cheerleader ? -2 : -3; grievance = "nocomment"; break;
                case "storm": d = j.T == Trait.AttackDog ? -8 : j.T == Trait.Cheerleader ? -4 : -6; grievance = "storm"; break;
                // 0.13.4: wrapping up; after three answers nobody minds (attack dogs wanted their follow-up)
                case "wrapped":
                    if (e.Value >= PressRoom.WrapUp.Fair) d = j.T == Trait.AttackDog ? -1 : 0;
                    else if (e.Value >= 1) d = j.T == Trait.AttackDog ? -3 : j.T == Trait.Cheerleader ? -1 : -2;
                    else d = j.T == Trait.AttackDog ? -4 : j.T == Trait.Cheerleader ? -2 : -3;
                    if (d < 0) grievance = "cutshort";
                    break;
                case "ban": d = j.T == Trait.Insider ? -5 : -4; grievance = "ban"; break;
                case "favour": d = 1; break;
            }
            return Scale(d);
        }
        static int Scale(int d) => d >= 0 ? d : Harsh == 0 ? Math.Min(-1, d / 2) : Harsh == 2 ? (d * 3) / 2 : d;
        static int Clamp(int r) => r < -10 ? -10 : r > 10 ? 10 : r;

        // ---------- pressure on you (0-10) ----------
        internal static int PressureAt(int conf, bool now, out string why)
        {
            var bits = new List<string>();
            int p = 0;
            // our last five results seen by then
            var last = new List<Result>();
            for (int i = Results.Count - 1; i >= 0 && last.Count < 5; i--) if (Results[i].Conf <= conf) last.Add(Results[i]);
            int w = 0, d = 0, l = 0;
            foreach (var r in last) { if (r.R == 'W') w++; else if (r.R == 'D') d++; else l++; }
            int form = 2 * l + d - w; if (form > 0) p += form;
            if (last.Count > 0)
            {
                if (l >= 2) bits.Add(l + " defeats in the last " + last.Count);
                else if (l == 0 && w >= 3) bits.Add("unbeaten in " + last.Count);
                else bits.Add(w + "W " + d + "D " + l + "L in the last " + last.Count);
            }
            foreach (var c in PressureStories) if (c <= conf && c >= conf - 2) { p += 3; bits.Add("pressure stories about you"); break; }
            if (now)
            {
                try
                {
                    FanPress.Fresh();
                    int m = FanMood(FanPress.Overall);
                    if (m <= -2) { p += 2; bits.Add("supporters unhappy"); } else if (m == -1) { p += 1; bits.Add("supporters disappointed"); }
                    else if (m >= 2) { p -= 1; bits.Add("supporters delighted"); }
                }
                catch { }
                int open = 0; foreach (var x in Incidents.All) if (x.State == "open" || x.State == "inhouse") open++;
                if (open > 0) { p += Math.Min(2, open); bits.Add(open + " open dressing-room " + (open == 1 ? "story" : "stories")); }
                int ns = NoShow.Recent();   // 0.13.3
                if (ns >= 2) { p += ns >= 3 ? 2 : 1; bits.Add(ns + " press conferences missed in six weeks"); }
            }
            why = string.Join(", ", bits);
            return p < 0 ? 0 : p > 10 ? 10 : p;
        }
        static int FanMood(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("could not be happier") || s.Contains("ecstatic")) return 3;
            if (s.Contains("delighted") || s.Contains("very pleased")) return 2;
            if (s.Contains("furious") || s.Contains("angry") || s.Contains("unhappy")) return -2;
            if (s.Contains("disappointed") || s.Contains("concerned")) return -1;
            return 0;
        }
        internal static string PressureBand(int p) => p >= 7 ? "Intense" : p >= 4 ? "High" : p >= 2 ? "Rising" : "Low";

        // ---------- working it all out again from the record ----------
        static void Build()
        {
            int now = PressRoom.Record.Conf;
            if (BuiltFor == PressRoom.Record.All.Count && BuiltConf == now && BuiltEvents == Events.Count + Results.Count * 1000 + PressureStories.Count * 100000) return;
            BuiltFor = PressRoom.Record.All.Count; BuiltConf = now; BuiltEvents = Events.Count + Results.Count * 1000 + PressureStories.Count * 100000;
            Js.Clear();
            var recs = PressRoom.Record.All;
            int first = int.MaxValue;
            foreach (var e in recs) if (e.Conf < first) first = e.Conf;
            foreach (var e in Events) if (e.Conf < first) first = e.Conf;
            if (first == int.MaxValue) return;
            int ri = 0;
            for (int c = first; c <= now; c++)
            {
                // a new conference: everyone drifts back towards their normal, and pressure turns the hard cases
                int p = PressureAt(c, c == now, out _);
                foreach (var j in Js.Values)
                {
                    if (j.Rel > j.Base + 4) j.Rel -= 2; else if (j.Rel > j.Base + 1) j.Rel -= 1;
                    else if (j.Rel < j.Base - 1 && (c % (Harsh == 0 ? 1 : Harsh == 2 ? 3 : 2)) == 0) j.Rel += 1;
                    bool hard = j.T == Trait.AttackDog || j.T == Trait.Contrarian || PressRoom.Classify(j.Outlet) == PressRoom.Kind.RedTop || PressRoom.Classify(j.Outlet) == PressRoom.Kind.PhoneIn;
                    if (p >= 4 && hard) j.Rel -= 1;
                    if (p >= 7 && j.T != Trait.Cheerleader) j.Rel -= 1;
                    j.Rel = Clamp(j.Rel);
                }
                foreach (var ev in Events)
                {
                    if (ev.Conf != c) continue;
                    if (!Js.TryGetValue(ev.Who, out var j)) continue;    // events about people met later wait for them
                    j.Rel = Clamp(j.Rel + ev.Delta);
                    if (ev.Delta < 0 && ev.Why.Length > 0) { j.Grievance = ev.Why; j.GrievanceConf = c; }
                }
                int posRun = 0;
                for (; ri < recs.Count && recs[ri].Conf <= c; ri++)
                {
                    var e = recs[ri];
                    if (e.Conf < c || string.IsNullOrEmpty(e.Journalist)) { if (e.Kind == "answer") posRun = Tone(e) > 0 ? posRun + 1 : 0; continue; }
                    if (!Js.TryGetValue(e.Journalist, out var j))
                    {
                        j = Make(e.Journalist, e.Outlet); Js[e.Journalist] = j;
                        foreach (var ev in Events) if (ev.Who == e.Journalist && ev.Conf <= c) j.Rel = Clamp(j.Rel + ev.Delta);
                    }
                    if (e.Kind == "answer") posRun = Tone(e) > 0 ? posRun + 1 : 0;
                    int d = Effect(j, e, posRun, out var g);
                    if (d < 0 && (g == "bored" || g == "evasive") && j.Rel + d < j.Base - 3) d = Math.Min(0, j.Base - 3 - j.Rel);
                    j.Rel = Clamp(j.Rel + d); j.LastConf = c; j.Seen++;
                    if (g != null && d < 0) { j.Grievance = g; j.GrievanceConf = c; }
                }
            }
        }

        internal static J Get(string journalist) { if (string.IsNullOrEmpty(journalist)) return null; Build(); return Js.TryGetValue(journalist, out var j) ? j : null; }
        internal static int Rel(string journalist) { var j = Get(journalist); return j == null ? 0 : j.Rel; }
        internal static bool IsFeud(string journalist) { var j = Get(journalist); return j != null && j.Rel <= -6; }
        internal static IEnumerable<J> All() { Build(); return Js.Values; }
        internal static string Band(int r) => r >= 6 ? "Friendly" : r >= 3 ? "Warm" : r >= -2 ? "Neutral" : r >= -5 ? "Cool" : "Hostile";

        // a change that isn't an answer (a mission, a story you planted, a U-turn, jealousy), from now on
        internal static void Add(string who, int delta, string why)
        {
            if (string.IsNullOrEmpty(who) || delta == 0) return;
            Events.Add(new Ev { Conf = PressRoom.Record.Conf, Who = who, Delta = delta, Why = why ?? "" });
            Save();
            PressRoom.W("pack", (delta > 0 ? "+" : "") + delta + " " + who, why ?? "");
        }
        // an outlet got something from you: its people are pleased, the insiders and story-chasers elsewhere notice
        internal static void Favoured(string outlet, int gain, string why)
        {
            var mine = new HashSet<string>();
            foreach (var j in All()) if (j.Outlet == outlet) mine.Add(j.Name);
            foreach (var n in mine) Add(n, gain, why);
            foreach (var j in new List<J>(All()))
            {
                if (j.Outlet == outlet) continue;
                var k = PressRoom.Classify(j.Outlet);
                if (j.T == Trait.Insider) Add(j.Name, -2, "jealous:" + outlet);
                else if (k == PressRoom.Kind.RedTop || k == PressRoom.Kind.PhoneIn) Add(j.Name, -1, "jealous:" + outlet);
            }
        }
        // a mended relationship doesn't always take: the hard cases may not have it
        internal static bool BridgeTakes(string journalist)
        {
            var j = Get(journalist); if (j == null) return true;
            if (j.T != Trait.AttackDog && j.T != Trait.Contrarian) return true;
            return Hash(journalist + PressRoom.Record.Conf) % 2 == 0;
        }

        // ---------- 0.13.4: today's result, from SI's match report tile ----------
        // The tile sits on the post-match invite, the conference screen and the inbox (BindingExpect_MatchReportTile_4x2:
        // Info/HomeTeam, Info/HomeTeam/Score, Info/AwayTeam, Info/AwayTeam/Score, Info/Date "3rd April 2027"). SI's
        // Key Findings, which the pack used alone, arrive later, so a post-match conference still had the old form.
        // Only a tile dated today (or yesterday) counts, so old reports in the inbox can't add old results.
        class Tile { public string Home = "", Away = "", Date = ""; public int Hs = -1, As = -1; public bool Pending; }
        static readonly Dictionary<IntPtr, Tile> Tiles = new Dictionary<IntPtr, Tile>();
        internal static char TodayR = ' '; internal static DateTime TodayDate = DateTime.MinValue;
        static readonly Regex Ordinal = new Regex(@"^(\d{1,2})(st|nd|rd|th) ", RegexOptions.Compiled);
        static string Named(VisualElement e, int levels)
        {
            for (int i = 0; i < levels && e != null; i++, e = e.parent) { var n = e.name ?? ""; if (n.Length > 0) return n; }
            return "";
        }
        internal static void OnTile(VisualElement el, string txt)
        {
            try
            {
                if (!On) return;
                var n = el.name ?? "";
                if (n != "Score" && n != "HomeTeam" && n != "AwayTeam" && n != "Date") return;
                VisualElement tile = null; var e = el.parent;
                for (int i = 0; i < 8 && e != null; i++, e = e.parent) if ((e.name ?? "").StartsWith("BindingExpect_MatchReportTile")) { tile = e; break; }
                if (tile == null) return;
                if (!Tiles.TryGetValue(tile.Pointer, out var t)) { if (Tiles.Count > 40) Tiles.Clear(); t = new Tile(); Tiles[tile.Pointer] = t; }
                var v = PressRoom.Visible(txt).Trim();
                if (n == "Score")
                {
                    if (!int.TryParse(v, out var s)) return;
                    var side = Named(el.parent, 3);
                    if (side == "HomeTeam") t.Hs = s; else if (side == "AwayTeam") t.As = s; else return;
                }
                else if (n == "HomeTeam") t.Home = v; else if (n == "AwayTeam") t.Away = v; else t.Date = v;
                if (t.Pending) return;
                t.Pending = true; var keep = t;
                System.Action run = () => { keep.Pending = false; Commit(keep); };
                tile.schedule.Execute(run).ExecuteLater(300);
            }
            catch (Exception ex) { PressRoom.Fail("pack tile", ex); }
        }
        static bool SameAsRecent(string opponent, int gf, int ga)
        {
            int seen = 0;
            for (int i = Results.Count - 1; i >= 0 && seen < 3; i--, seen++)
            {
                var r = Results[i];
                if (r.For != gf || r.Against != ga) continue;
                var a = (r.Opponent ?? "").ToLowerInvariant(); var b = (opponent ?? "").ToLowerInvariant();
                if (a.Length > 0 && b.Length > 0 && (a == b || a.Contains(b) || b.Contains(a))) return true;
            }
            return false;
        }
        static void Commit(Tile t)
        {
            try
            {
                if (t.Home.Length == 0 || t.Away.Length == 0 || t.Hs < 0 || t.As < 0 || t.Date.Length == 0 || !Desk.DateKnown) return;
                var ds = Ordinal.Replace(t.Date, "$1 ");
                if (!DateTime.TryParseExact(ds, "d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"), System.Globalization.DateTimeStyles.None, out var date)) return;
                double age = (Desk.Today - date).TotalDays;
                if (age < 0 || age > 1) return;
                var club = Incidents.OwnClubName(); if (club.Length == 0) return;
                bool h = t.Home.Equals(club, StringComparison.OrdinalIgnoreCase), a = t.Away.Equals(club, StringComparison.OrdinalIgnoreCase);
                if (!h && !a) return;
                int gf = h ? t.Hs : t.As, ga = h ? t.As : t.Hs; var opp = h ? t.Away : t.Home;
                char r = gf > ga ? 'W' : gf < ga ? 'L' : 'D';
                TodayR = r; TodayDate = date;
                var key = "Match report: " + t.Home + " " + t.Hs + "-" + t.As + " " + t.Away + " (" + date.ToString("yyyy-MM-dd") + ")";
                foreach (var r0 in Results) if (r0.Headline == key) return;
                if (SameAsRecent(opp, gf, ga)) return;
                Results.Add(new Result { Conf = PressRoom.Record.Conf, R = r, Headline = key, Opponent = opp, For = gf, Against = ga });
                while (Results.Count > 60) Results.RemoveAt(0);
                Save(); BuiltFor = -1;
                PressRoom.W("pack", "result " + r + " (match report)", key);
            }
            catch (Exception ex) { PressRoom.Fail("pack tile commit", ex); }
        }
        internal static bool WonToday => TodayR == 'W' && Desk.DateKnown && TodayDate == Desk.Today;

        // the questions a form or pressure opener fits: about the run, the results, the pressure or the job
        static readonly Regex FormQ = new Regex(@"\b(form|run|results?|defeats?|loss(es)?|losing|lost|pressure|job|future|board|sack\w*|confidence|bounce back|respond|response|turn(ing)? (it|things) around|struggl\w*|poor|slump|crisis|winless|without a win|table|position|standings|season|critic\w*|worried|concern\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ---------- our results and pressure stories, from the news ----------
        static readonly Regex KeyFindings = new Regex(@"^(?:[A-Z]{1,4}: )?(.+?) (\d{1,2})-(\d{1,2}) (.+?): Key Findings$", RegexOptions.Compiled);
        static readonly Regex PressureWords = new Regex(@"under pressure|pressure (is )?(building|mounts|mounting)|under fire|on the brink|job (is )?on the line|future (in doubt|uncertain)|sack|axe|questions over", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly HashSet<string> SeenHeads = new HashSet<string>();
        internal static void OnText(VisualElement el, string txt)
        {
            try
            {
                if (!On || string.IsNullOrEmpty(txt) || txt.Length < 12) return;
                var n = el.name ?? "";
                if (n != "MessageHeadline" && n != "newsHeadlineText" && n != "Headline" && n != "NewsStoryHeadline") return;
                var v = PressRoom.Visible(txt).Trim();
                if (!SeenHeads.Add(v)) return;
                if (SeenHeads.Count > 3000) SeenHeads.Clear();
                var club = Incidents.OwnClubName();
                if (club.Length == 0) return;
                var m = KeyFindings.Match(v);
                if (m.Success)
                {
                    string home = m.Groups[1].Value.Trim(), away = m.Groups[4].Value.Trim();
                    bool h = home.Equals(club, StringComparison.OrdinalIgnoreCase), a = away.Equals(club, StringComparison.OrdinalIgnoreCase);
                    if (!h && !a) return;
                    foreach (var r0 in Results) if (r0.Headline == v) return;
                    int gf = int.Parse(h ? m.Groups[2].Value : m.Groups[3].Value), ga = int.Parse(h ? m.Groups[3].Value : m.Groups[2].Value);
                    if (SameAsRecent(h ? away : home, gf, ga)) { PressRoom.W("pack", "result already read from the match report", v); return; }
                    // an inbox full of old results shows newest first; at most two new results count per conference
                    int conf = PressRoom.Record.Conf, here = 0;
                    foreach (var r0 in Results) if (r0.Conf == conf) here++;
                    if (here >= 2) return;
                    var res = new Result { Conf = conf, R = gf > ga ? 'W' : gf < ga ? 'L' : 'D', Headline = v, Opponent = h ? away : home, For = gf, Against = ga };
                    Results.Add(res); while (Results.Count > 60) Results.RemoveAt(0);
                    Save(); BuiltFor = -1;
                    PressRoom.W("pack", "result " + res.R, v);
                    return;
                }
                var mgr = Incidents.OwnManagerName();
                if (mgr.Length > 0 && PressureWords.IsMatch(v))
                {
                    var last = mgr.Split(' '); var surname = last[last.Length - 1];
                    if (v.IndexOf(surname, StringComparison.OrdinalIgnoreCase) >= 0 && !PressureStories.Contains(PressRoom.Record.Conf))
                    { PressureStories.Add(PressRoom.Record.Conf); Save(); BuiltFor = -1; PressRoom.W("pack", "pressure story", v); }
                }
            }
            catch (Exception e) { PressRoom.Fail("pack news", e); }
        }

        // ---------- U-turns ----------
        static readonly Regex Stay = new Regex(@"not for sale|going nowhere|not going anywhere|isn't going anywhere|staying|stays here|part of (my|our) plans|key (player|part|man|figure)|important (player|part|member)|won't be leaving|not leaving|isn't leaving|wants to stay|happy here|future is here|long[- ]term|legend|big part|build(ing)? (the team )?around|no intention of selling|not selling|here for the long haul", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex NotInterested = new Regex(@"not interested|no interest|not looking at|isn't a target|not a target|no plans to sign|happy with what we('ve| have) got|nothing in (it|the rumours)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static string Surname(string full) { var p = (full ?? "").Trim().Split(' '); return p[p.Length - 1]; }
        // look for answers on the record that a later sale or signing contradicts
        internal static void FindUTurns()
        {
            try
            {
                var gone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);    // surname -> conference it was known
                var came = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in Deals.All.Values)
                {
                    if (!d.Signals.Contains("done") && !d.Signals.Contains("closed")) continue;
                    if (d.Side == "selling" && !(d.Stance == "Convince" || d.Stance == "Threaten" || d.Stance == "Back Down")) gone[Surname(d.Player)] = d.LastConf;
                    else if (d.Side == "buying" && d.Signals.Contains("done")) came[Surname(d.Player)] = d.LastConf;
                }
                try
                {
                    FanPress.Fresh();
                    foreach (var c in FanPress.Crits) { var m = Regex.Match(c, @"sale of (.+?)\.?$"); if (m.Success && !gone.ContainsKey(Surname(m.Groups[1].Value))) gone[Surname(m.Groups[1].Value)] = PressRoom.Record.Conf; }
                }
                catch { }
                bool changed = false;
                foreach (var e in PressRoom.Record.All)
                {
                    if (e.Kind != "answer" || string.IsNullOrEmpty(e.Subjects) || string.IsNullOrEmpty(e.Answer)) continue;
                    foreach (var who in e.Subjects.Split('|'))
                    {
                        if (who.Length < 3) continue;
                        var s = Surname(who);
                        string kind = null; int when = -1;
                        if (gone.TryGetValue(s, out when) && Stay.IsMatch(e.Answer) && e.Conf <= when) kind = "sold";
                        else if (came.TryGetValue(s, out when) && NotInterested.IsMatch(e.Answer) && e.Conf <= when) kind = "signed";
                        if (kind == null) continue;
                        bool have = false; foreach (var u in UTurns) if (u.Player == who && u.Kind == kind) { have = true; break; }
                        if (have) continue;
                        UTurns.Add(new UTurn { Player = who, Kind = kind, Quote = e.Answer, SaidConf = e.Conf });
                        changed = true;
                        PressRoom.W("pack", "u-turn found " + kind, who + " | " + e.Answer);
                    }
                }
                if (changed) Save();
            }
            catch (Exception e) { PressRoom.Fail("u-turns", e); }
        }

        // ---------- in the room ----------
        static int UTurnConf = -1, FeudConf = -1, PressureConf = -1, TalkConf = -1, TalkThis;
        internal static int PerConference = 2;
        static readonly Regex TransferQ = new Regex(@"\b(transfer|window|sign|signed|signing|sell|sold|sale|depart\w*|deal|future|squad|replace\w*|recruit\w*|market|business)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static string WhenSaid(int gap) => gap <= 1 ? "at your last press conference" : gap <= 3 ? "a couple of press conferences ago" : gap <= 10 ? "a few weeks back" : "a while back";
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static readonly string[] FeudLines = { "Let's not pretend we get on.", "I'll be blunt, as you'd expect from me by now.", "You won't like this one either.", "I'll ask what everyone's thinking, since nobody else will." };
        static readonly string[] CoolLines = { "You won't like this one.", "I'll not dress this one up.", "Here's one you can't answer with a stock line." };
        static readonly string[] BoredLines = { "I'll try to get more than a press release out of you this time.", "Let's see if we can get past the stock answers today.", "Something other than the usual line would be nice." };
        static readonly string[] EvasiveLines = { "Let's see if we can get a straight answer this time.", "You sat on the fence last time. Try again." };
        static readonly string[] JealousLines = { "Funny how {0} always seem to hear things first.", "I'll ask, since some outlets get their answers in advance. Ask {0}." };

        // the opener this journalist has for you, or null
        internal static string Talk(string q, string outlet, string journalist, PressRoom.Kind k)
        {
            if (!On) return null;
            try
            {
                int conf = PressRoom.Record.Conf;
                if (TalkConf != conf) { TalkConf = conf; TalkThis = 0; }
                if (TalkThis >= PerConference) return null;
                var line0 = Talk0(q, outlet, journalist, k, conf);
                if (line0 != null) TalkThis++;
                return line0;
            }
            catch (Exception e) { PressRoom.Fail("pack talk", e); }
            return null;
        }
        static string Talk0(string q, string outlet, string journalist, PressRoom.Kind k, int conf)
        {
            {
                var j = Get(journalist);
                uint h = Hash(q + journalist + conf);
                var v = PressRoom.Visible(q ?? "");
                // 1. a U-turn, thrown back once, by whoever asks a question it fits
                if (UTurnConf != conf)
                {
                    FindUTurns();
                    foreach (var u in UTurns)
                    {
                        if (u.State != "pending" || u.SaidConf >= conf) continue;
                        var s = Surname(u.Player);
                        if (!TransferQ.IsMatch(v) && v.IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var line = Cap(WhenSaid(conf - u.SaidConf)) + ", you said this about " + s + ": " + PressRoom.Record.Quote(u.Quote) + " " +
                                   (u.Kind == "sold" ? s + " has gone since." : s + " has signed since.");
                        u.State = "used"; u.CaughtConf = conf; UTurnConf = conf;
                        Save();
                        if (j != null) Add(journalist, 2, "story");
                        foreach (var o in new List<J>(All())) if (o.Name != journalist) Add(o.Name, -1, "uturn");
                        PressRoom.W("pack", "u-turn " + outlet + " / " + journalist, line);
                        return line;
                    }
                }
                if (j == null) return null;
                // 2. someone who has it in for you
                if (j.Rel <= -6 && FeudConf != conf && h % 100 < 70)
                {
                    FeudConf = conf;
                    string line = null;
                    if (j.Grievance.StartsWith("jealous:")) line = string.Format(JealousLines[(h / 7) % (uint)JealousLines.Length], j.Grievance.Substring(8));
                    else if (j.Grievance == "bored") line = BoredLines[(h / 7) % (uint)BoredLines.Length];
                    else if (j.Grievance == "evasive") line = EvasiveLines[(h / 7) % (uint)EvasiveLines.Length];
                    if (line == null) line = FeudLines[(h / 7) % (uint)FeudLines.Length];
                    PressRoom.W("pack", "feud " + outlet + " / " + journalist + " (" + j.Rel + ", " + j.Grievance + ")", line);
                    return line;
                }
                // 3. the pressure, put by the hard cases
                bool hard = j.T == Trait.AttackDog || j.T == Trait.Contrarian || k == PressRoom.Kind.RedTop || k == PressRoom.Kind.PhoneIn;
                int p = PressureAt(conf, true, out _);
                if (p >= 4 && hard && PressureConf != conf && h % 100 < 55 && !WonToday && FormQ.IsMatch(v))   // 0.13.4: not after a win today, and only on a question it fits
                {
                    var line = PressureLine(p, h);
                    if (line != null) { PressureConf = conf; PressRoom.W("pack", "pressure " + p + " " + outlet + " / " + journalist, line); return line; }
                }
                // 4. a cool hard case, now and then
                if (j.Rel <= -3 && (j.T == Trait.AttackDog || j.T == Trait.Contrarian) && FeudConf != conf && h % 100 < 30)
                {
                    FeudConf = conf;
                    string line = j.Grievance == "bored" && j.GrievanceConf >= conf - 3 ? BoredLines[(h / 7) % (uint)BoredLines.Length]
                                : j.Grievance == "evasive" && j.GrievanceConf >= conf - 3 ? EvasiveLines[(h / 7) % (uint)EvasiveLines.Length]
                                : CoolLines[(h / 7) % (uint)CoolLines.Length];
                    PressRoom.W("pack", "cool " + outlet + " / " + journalist + " (" + j.Rel + ")", line);
                    return line;
                }
            }
            return null;
        }
        // only what the record shows: the defeats, the pressure stories
        static string PressureLine(int p, uint h)
        {
            var last = new List<Result>(); for (int i = Results.Count - 1; i >= 0 && last.Count < 5; i--) last.Add(Results[i]);
            int l = 0, w = 0; foreach (var r in last) { if (r.R == 'L') l++; else if (r.R == 'W') w++; }
            bool story = false; foreach (var c in PressureStories) if (c >= PressRoom.Record.Conf - 2) story = true;
            if (l >= 2 && last.Count >= 3)
            {
                var opts = new[] { "That's " + l + " defeats in your last " + last.Count + ".", l + " defeats in " + last.Count + ". People are starting to ask questions.", "One win in " + last.Count + " doesn't make for much of a defence." };
                if (w != 1) opts[2] = l + " defeats in " + last.Count + ". That's not the form of a side going places.";
                return opts[(h / 13) % (uint)opts.Length];
            }
            if (story) return h % 2 == 0 ? "You'll have seen the talk of pressure building on you." : "The questions about your future aren't going away.";
            if (last.Count > 0 && last[0].R == 'L') return "After " + last[0].For + "-" + last[0].Against + " against " + last[0].Opponent + ", I have to ask.";
            return null;
        }

        // ---------- state ----------
        internal static void Reset() { Events.Clear(); Results.Clear(); UTurns.Clear(); PressureStories.Clear(); Js.Clear(); BuiltFor = -1; UTurnConf = FeudConf = PressureConf = -1; }
        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t'); if (p.Length < 2) continue;
                    switch (p[0])
                    {
                        case "ev": if (p.Length >= 5) Events.Add(new Ev { Conf = int.TryParse(p[1], out var c) ? c : 0, Who = p[2], Delta = int.TryParse(p[3], out var d) ? d : 0, Why = p[4] }); break;
                        case "result": if (p.Length >= 7) Results.Add(new Result { Conf = int.TryParse(p[1], out var c2) ? c2 : 0, R = p[2].Length > 0 ? p[2][0] : 'D', For = int.TryParse(p[3], out var f) ? f : 0, Against = int.TryParse(p[4], out var a) ? a : 0, Opponent = p[5], Headline = p[6] }); break;
                        case "pressure": if (int.TryParse(p[1], out var pc)) PressureStories.Add(pc); break;
                        case "uturn": if (p.Length >= 6) UTurns.Add(new UTurn { Player = p[1], Kind = p[2], SaidConf = int.TryParse(p[3], out var sc) ? sc : 0, State = p[4], Quote = p[5] }); break;
                    }
                }
            }
            catch (Exception e) { PressRoom.Fail("pack load", e); }
            BuiltFor = -1;
        }
        internal static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var e in Events) sb.Append("ev\t").Append(e.Conf).Append('\t').Append(Clean(e.Who)).Append('\t').Append(e.Delta).Append('\t').Append(Clean(e.Why)).Append('\n');
                foreach (var r in Results) sb.Append("result\t").Append(r.Conf).Append('\t').Append(r.R).Append('\t').Append(r.For).Append('\t').Append(r.Against).Append('\t').Append(Clean(r.Opponent)).Append('\t').Append(Clean(r.Headline)).Append('\n');
                foreach (var c in PressureStories) sb.Append("pressure\t").Append(c).Append('\n');
                foreach (var u in UTurns) sb.Append("uturn\t").Append(Clean(u.Player)).Append('\t').Append(u.Kind).Append('\t').Append(u.SaidConf).Append('\t').Append(u.State).Append('\t').Append(Clean(u.Quote)).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("pack save", e); }
        }
    }
}
