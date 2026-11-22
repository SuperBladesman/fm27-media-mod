// FM27 Media Mod - the press office's week (Press Room 0.13). MIT licence (see LICENSE).
// The Media Office's clock and budget: about 3 hours of press-office time a week, spent on the office's own actions
// (bans, favours, planting, keeping things quiet, briefing back, closing ranks, taking on a mission). Looking is free;
// SI's own chats and commands are never charged. Every action has a cost, a cap and a cool-down, and every chance
// (a whisper that won't stay quiet, a plant traced back to you) is fixed by the save, the week and the target, so
// reloading changes nothing. Risk is shown as a band, never a number.
// The week comes from the date in the top bar when the office can read it, otherwise from the game's day changes.
// Also groundwork for tapping up (0.14): the player and club in a profile's title bar, and the interest events.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Desk
    {
        // ---------- settings (config) ----------
        internal static int Hours = 3;
        internal static bool Badges = true, RiskBands = true;

        // ---------- state (kept in media office.tsv, per save) ----------
        internal static DateTime Today = DateTime.MinValue;    // the game's date, when read
        internal static int Days;                               // day changes counted since the office started (fallback clock)
        internal static uint Seed;                              // fixes every chance for this career
        internal static int LastStatusWeek = int.MinValue, HeadLockUntil = int.MinValue;
        internal static readonly Dictionary<string, int> Acted = new Dictionary<string, int>();     // target -> week of the last office action on it
        internal static readonly Dictionary<string, int> BanSince = new Dictionary<string, int>();  // outlet -> conference the ban was set
        internal class Spend { public int Week, Hours, Conf; public string When = "", Action = "", Target = "", Outcome = ""; }
        internal static readonly List<Spend> Spent = new List<Spend>();

        internal static void Reset()
        {
            Today = DateTime.MinValue; Days = 0; Seed = 0; LastStatusWeek = int.MinValue; HeadLockUntil = int.MinValue;
            Acted.Clear(); BanSince.Clear(); Spent.Clear();
            NoShow.Reset();
        }

        internal static void LoadLine(string[] p)
        {
            switch (p[0])
            {
                case "clock":
                    if (p.Length >= 3)
                    {
                        if (DateTime.TryParseExact(p[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) Today = d;
                        int.TryParse(p[2], out Days);
                    }
                    break;
                case "seed": if (p.Length >= 2) uint.TryParse(p[1], out Seed); break;
                case "statusweek": if (p.Length >= 2) int.TryParse(p[1], out LastStatusWeek); break;
                case "headlock": if (p.Length >= 2) int.TryParse(p[1], out HeadLockUntil); break;
                case "acted": if (p.Length >= 3 && int.TryParse(p[2], out var w)) Acted[p[1]] = w; break;
                case "bansince": if (p.Length >= 3 && int.TryParse(p[2], out var c)) BanSince[p[1]] = c; break;
                case "spend":
                    if (p.Length >= 7) Spent.Add(new Spend { Week = int.TryParse(p[1], out var sw) ? sw : 0, Hours = int.TryParse(p[2], out var h) ? h : 0,
                        Conf = int.TryParse(p[3], out var sc) ? sc : 0, Action = p[4], Target = p[5], Outcome = p[6], When = p.Length >= 8 ? p[7] : "" });
                    break;
                default: NoShow.LoadLine(p); break;   // 0.13.3: press conferences you sent the assistant to
            }
        }

        internal static void SaveLines(StringBuilder sb, Func<string, string> clean)
        {
            sb.Append("clock\t").Append(Today == DateTime.MinValue ? "" : Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\t').Append(Days).Append('\n');
            if (Seed != 0) sb.Append("seed\t").Append(Seed).Append('\n');
            if (LastStatusWeek != int.MinValue) sb.Append("statusweek\t").Append(LastStatusWeek).Append('\n');
            if (HeadLockUntil != int.MinValue) sb.Append("headlock\t").Append(HeadLockUntil).Append('\n');
            foreach (var kv in Acted) sb.Append("acted\t").Append(clean(kv.Key)).Append('\t').Append(kv.Value).Append('\n');
            foreach (var kv in BanSince) sb.Append("bansince\t").Append(clean(kv.Key)).Append('\t').Append(kv.Value).Append('\n');
            foreach (var s in Spent) sb.Append("spend\t").Append(s.Week).Append('\t').Append(s.Hours).Append('\t').Append(s.Conf).Append('\t')
                .Append(clean(s.Action)).Append('\t').Append(clean(s.Target)).Append('\t').Append(clean(s.Outcome)).Append('\t').Append(clean(s.When)).Append('\n');
            NoShow.SaveLines(sb, clean);
        }

        // 0.13.3: weeks kept before the date was read are counted from the office's own days (100000 + days / 7). Once the
        // date is known, move them onto the calendar, or a cool-down set on day 3 would read "again in 98,000 weeks".
        const int FallbackBase = 100000;
        internal static void Migrate()
        {
            if (!DateKnown) return;
            int offset = Week - (FallbackBase + Days / 7);
            int Move(int v) => v >= FallbackBase ? v + offset : v;
            bool any = false;
            foreach (var k in new List<string>(Acted.Keys)) if (Acted[k] >= FallbackBase) { Acted[k] = Move(Acted[k]); any = true; }
            if (LastStatusWeek != int.MinValue && LastStatusWeek >= FallbackBase) { LastStatusWeek = Move(LastStatusWeek); any = true; }
            if (HeadLockUntil != int.MinValue && HeadLockUntil >= FallbackBase) { HeadLockUntil = Move(HeadLockUntil); any = true; }
            foreach (var x in Spent) if (x.Week >= FallbackBase) { x.Week = Move(x.Week); any = true; }
            if (any) PressRoom.W("clock", "weeks moved onto the calendar", "offset " + offset);
        }

        // ---------- the clock ----------
        static readonly DateTime Monday0 = new DateTime(2000, 1, 3);
        internal static bool DateKnown => Today != DateTime.MinValue;
        internal static int Week => DateKnown ? (int)Math.Floor((Today - Monday0).TotalDays / 7) : FallbackBase + Days / 7;
        internal static string WeekLabel()
        {
            if (!DateKnown) return "Week " + (Days / 7 + 1) + " of the office";
            var mon = Monday0.AddDays(7.0 * Week);
            return "Week of " + mon.ToString("ddd d MMM", CultureInfo.GetCultureInfo("en-GB"));
        }

        // "THU 18/3/2027" in the top bar (day/month as FM shows it in a UK setup). 0.13.2: on Nik's screen the day ("FRI") and
        // the date ("26/3/2027") are separate labels, so a lone date counts too when it sits in the title bar (MenuTitle),
        // and the day of the week, if a label beside it holds one, settles which way round it is written.
        static readonly Regex TopDate = new Regex(@"^(MON|TUE|WED|THU|FRI|SAT|SUN)[A-Z]*\.? +(\d{1,2})/(\d{1,2})/(\d{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex LoneDate = new Regex(@"^(\d{1,2})/(\d{1,2})/(\d{4})$", RegexOptions.Compiled);
        static readonly Regex LoneDay = new Regex(@"^(MON|TUE|WED|THU|FRI|SAT|SUN)[A-Z]*\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static string LastDateText = "";
        internal static void SeeDate(string txt) => SeeDate(null, txt);
        internal static void SeeDate(VisualElement el, string txt)
        {
            if (txt.Length < 8 || txt.Length > 20 || txt == LastDateText) return;
            var t = txt.Trim();
            var m = TopDate.Match(t);
            if (m.Success) { Settle(txt, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value); return; }
            var l = LoneDate.Match(t); if (!l.Success || el == null) return;
            // a lone date only from the title bar: fixture lists and match reports carry dates too
            VisualElement title = null; int depth = 0;
            for (var e = el; e != null && depth < 40; e = e.parent, depth++) { var n = e.name ?? ""; if (n == "MenuTitle") { title = e; break; } if (n == "MainUIDocument-container") break; }
            if (title == null) return;
            Settle(txt, DayNear(el), l.Groups[1].Value, l.Groups[2].Value, l.Groups[3].Value);
        }
        // a day-of-week label next to the date (same parent or the one above)
        static string DayNear(VisualElement el)
        {
            var e = el.parent;
            for (int up = 0; up < 3 && e != null; up++, e = e.parent)
            {
                var found = new List<string>(); Texts(e, found, 0, 4);
                foreach (var s in found) { var v = PressRoom.Visible(s).Trim(); if (LoneDay.IsMatch(v)) return v; }
            }
            return "";
        }
        static void Texts(VisualElement e, List<string> into, int depth, int max)
        {
            if (e == null || depth > max || into.Count > 30) return;
            var t = e.TryCast<TextElement>(); if (t != null && !string.IsNullOrEmpty(t.text)) into.Add(t.text);
            for (int i = 0; i < e.childCount; i++) Texts(e.ElementAt(i), into, depth + 1, max);
        }
        static void Settle(string seen, string dow, string a, string b, string year)
        {
            int d = int.Parse(a), mo = int.Parse(b), y = int.Parse(year);
            DateTime when, alt = DateTime.MinValue; bool okDm = false, okMd = false;
            try { when = new DateTime(y, mo, d); okDm = true; } catch { when = DateTime.MinValue; }
            try { alt = new DateTime(y, d, mo); okMd = true; } catch { }
            dow = (dow ?? "").Trim().ToUpperInvariant(); if (dow.Length > 3) dow = dow.Substring(0, 3);
            if (dow.Length == 3)
            {
                if (okDm && when.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant() == dow) { }
                else if (okMd && alt.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant() == dow) when = alt;
            }
            else if (!okDm && okMd) when = alt;                 // only a US-style reading is a real date
            if (when == DateTime.MinValue) return;
            LastDateText = seen;
            if (when == Today) return;
            var was = Today; Today = when;
            PressRoom.W("clock", "date", seen + (dow.Length == 3 ? " (" + dow + ")" : "") + " -> " + when.ToString("yyyy-MM-dd") + (was == DateTime.MinValue ? " (first read)" : ""));
            if (was == DateTime.MinValue) Migrate();
            MediaOffice.Save();
        }

        // the game's own day change (a fallback when the date can't be read, and to move a known date on)
        internal static bool RescanDate;
        internal static void OnDayChange()
        {
            Days++;
            if (DateKnown) Today = Today.AddDays(1);
            RescanDate = true;                                  // read the top bar again at the next chance (main thread)
            MediaOffice.Save();
        }

        // the top bar may be plain labels that never pass the text hook: look for the date when the office opens
        static int ScanLogged;
        internal static void ScanForDate(VisualElement root)
        {
            try
            {
                RescanDate = false;
                VisualElement title = null;
                for (int i = 0; i < root.childCount && title == null; i++) { var k = root.ElementAt(i); if (k.name == "MenuTitle") title = k; }
                var all = new List<(VisualElement el, string text)>();
                Gather(title ?? root, all, 0);
                string day = "";
                foreach (var x in all) if (LoneDay.IsMatch(x.text)) { day = x.text; break; }
                bool got = false;
                foreach (var x in all)
                {
                    var m = TopDate.Match(x.text);
                    if (m.Success) { Settle(x.text, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value); got = true; break; }
                    var l = LoneDate.Match(x.text);
                    if (l.Success) { var dn = DayNear(x.el); Settle(x.text, dn.Length > 0 ? dn : day, l.Groups[1].Value, l.Groups[2].Value, l.Groups[3].Value); got = true; break; }
                }
                if (!got && ScanLogged < 3)
                {
                    ScanLogged++;
                    var sample = new List<string>(); foreach (var x in all) if (x.text.Length <= 20 && sample.Count < 25) sample.Add(x.text);
                    PressRoom.W("clock", "no date in the title bar", (title == null ? "(no MenuTitle) " : "") + all.Count + " texts: " + string.Join(" | ", sample));
                }
            }
            catch (Exception e) { PressRoom.Fail("clock scan", e); }
        }
        static void Gather(VisualElement e, List<(VisualElement, string)> into, int depth)
        {
            if (e == null || depth > 30 || into.Count > 400) return;
            var t = e.TryCast<TextElement>();
            if (t != null && !string.IsNullOrEmpty(t.text)) { var v = PressRoom.Visible(t.text).Trim(); if (v.Length > 0) into.Add((e, v)); }
            for (int i = 0; i < e.childCount; i++) Gather(e.ElementAt(i), into, depth + 1);
        }

        // ---------- the budget ----------
        internal static int SpentThisWeek() { int w = Week, n = 0; foreach (var s in Spent) if (s.Week == w) n += s.Hours; return n; }
        internal static int Left => Math.Max(0, Hours - SpentThisWeek());

        internal static void Charge(int hours, string action, string target, string outcome)
        {
            if (Seed == 0) Seed = (uint)Environment.TickCount | 1u;
            Spent.Add(new Spend { Week = Week, Hours = hours, Conf = PressRoom.Record.Conf, Action = action, Target = target ?? "", Outcome = outcome ?? "",
                When = DateKnown ? Today.ToString("yyyy-MM-dd") : "day " + Days });
            if (!string.IsNullOrEmpty(target)) Acted[target] = Week;
            if (Spent.Count > 600) Spent.RemoveRange(0, Spent.Count - 600);
            PressRoom.W("desk", action + " (" + hours + "h)", (target ?? "") + (string.IsNullOrEmpty(outcome) ? "" : " | " + outcome));
            MediaOffice.Save();
        }

        // what each action costs, by who runs the office
        internal static int Cost(string action)
        {
            var m = MediaOffice.Mode;
            switch (action)
            {
                case "favour": return 1;
                case "ban": return m == MediaOffice.Approach.OpenDoor ? 2 : 1;
                case "lift": return 0;
                case "mission": return 1;
                case "plant": return m == MediaOffice.Approach.Gatekeeper ? 2 : 1;
                case "hush": return m == MediaOffice.Approach.Gatekeeper ? 0 : 1;
                case "brief": return 1;
                case "quiet": return 0;     // a Gatekeeper saying nothing
                case "closeranks": return 1;
                case "letask": return 0;
            }
            return 1;
        }

        // ---------- caps and cool-downs ----------
        internal const int CoolWeeks = 3;
        internal static int MaxFavoured => 2;
        internal static int MaxBanned => MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? 2 : 1;
        internal static int Count(string status) { int n = 0; foreach (var kv in MediaOffice.Status) if (kv.Value == status) n++; return n; }

        // why an action can't be taken now, or null when it can
        internal static string Blocked(string action, string target)
        {
            int cost = Cost(action);
            if (cost > Left) return cost == 1 ? "No time left this week" : "Needs " + cost + " hours; " + Left + " left this week";
            if ((action == "favour" || action == "ban") && LastStatusWeek == Week) return "One status change a week";
            if (action == "favour" && Count("favoured") >= MaxFavoured) return "Already " + MaxFavoured + " favoured outlets";
            if (action == "ban" && Count("banned") >= MaxBanned) return MaxBanned == 1 ? "Already one outlet banned" : "Already " + MaxBanned + " outlets banned";
            if (action == "lift" && BanSince.TryGetValue(target ?? "", out var c) && PressRoom.Record.Conf - c < 2) return "A ban lasts at least 2 press conferences";
            if (cost > 0 && !string.IsNullOrEmpty(target) && Acted.TryGetValue(target, out var w) && Week - w < CoolWeeks)
                return "Again in " + (CoolWeeks - (Week - w)) + (CoolWeeks - (Week - w) == 1 ? " week" : " weeks");
            return null;
        }

        internal static bool HeadLocked => Week < HeadLockUntil;
        internal static string HeadLockLine() => HeadLocked ? "A new head of media can be appointed in " + (HeadLockUntil - Week) + (HeadLockUntil - Week == 1 ? " week." : " weeks.") : "";
        internal static void OnHeadChanged(bool firstAppointment)
        {
            if (firstAppointment) return;
            int left = Left;
            if (left > 0) Charge(left, "new head of media", "", "the rest of the week goes on the handover");
            HeadLockUntil = Week + 4;
            MediaOffice.Save();
        }

        // ---------- risk ----------
        internal enum Risk { None, Low, Medium, High }
        internal static string RiskName(Risk r) => r == Risk.Low ? "Low risk" : r == Risk.Medium ? "Medium risk" : r == Risk.High ? "High risk" : "";
        static int Threshold(Risk r) => r == Risk.Low ? 15 : r == Risk.Medium ? 35 : r == Risk.High ? 60 : 0;
        static Risk Shift(Risk r, int by) { int v = Math.Max(1, Math.Min(3, (int)r + by)); return (Risk)v; }

        internal static Risk RiskOf(string action, string extra)
        {
            var m = MediaOffice.Mode;
            int shift = m == MediaOffice.Approach.Gatekeeper ? -1 : m == MediaOffice.Approach.Fixer ? 1 : 0;
            switch (action)
            {
                case "plant": return m == MediaOffice.Approach.Fixer ? Risk.Low : Risk.Medium;   // the room works out where it came from
                case "hush":                                                                  // depends on who heard it
                    {
                        var who = (extra ?? "").ToLowerInvariant();
                        var b = who.Contains("director") || who.Contains("assistant") || who.Contains("staff") ? Risk.Low : Risk.Medium;
                        return Shift(b, shift);
                    }
                case "closeranks": return m == MediaOffice.Approach.OpenDoor ? Risk.High : m == MediaOffice.Approach.Gatekeeper ? Risk.Low : Risk.Medium;
                case "brief": return Risk.Medium;                                             // the agent answers back
            }
            return Risk.None;
        }

        // true when the risk comes off; fixed by the career, the week, the action and the target
        internal static bool Misfires(string action, string target, Risk r)
        {
            if (r == Risk.None) return false;
            if (Seed == 0) { Seed = (uint)Environment.TickCount | 1u; MediaOffice.Save(); }
            uint h = 2166136261;
            foreach (var ch in Seed + "|" + Week + "|" + action + "|" + (target ?? ""))
            { h ^= ch; h *= 16777619; }
            h ^= h >> 15; h *= 0x2c1b3c6d; h ^= h >> 12;
            return h % 100 < (uint)Threshold(r);
        }

        // ---------- groundwork for tapping up (0.14): the profile in the title bar, interest marking ----------
        static int HeaderLogged;
        static readonly HashSet<string> HeaderSeen = new HashSet<string>();
        internal static string ProfileName = "", ProfileClub = ""; internal static DateTime ProfileAt = DateTime.MinValue;
        // called for each PlayerName text; a profile's title bar is read 0.4 s later, once the rest of it is drawn
        internal static void OnPlayerName(VisualElement el, string txt)
        {
            try
            {
                VisualElement bar = null; var e = el;
                for (int i = 0; i < 6 && e != null; i++, e = e.parent) if (e.name == "ViewedEntityTypeSwitch") { bar = e; break; }
                if (bar == null) return;
                var vis = PressRoom.Visible(txt).Trim(); if (vis.Length == 0) return;
                ProfileName = vis; ProfileClub = ""; ProfileAt = DateTime.Now;
                var keep = bar; var who = vis;
                System.Action scan = () => ScanBar(keep, who);
                bar.schedule.Execute(scan).ExecuteLater(400);
            }
            catch (Exception ex) { PressRoom.Fail("profile bar", ex); }
        }
        static void ScanBar(VisualElement bar, string who)
        {
            try
            {
                var found = new List<(string name, string text, bool link)>();
                Collect(bar, found, 0);
                foreach (var f in found)
                {
                    if (f.name == "PlayerName") continue;
                    if (f.link && ProfileClub.Length == 0 && f.text != who) ProfileClub = f.text;
                    var key = f.name + "|" + f.text;
                    if (HeaderLogged < 120 && HeaderSeen.Add(key)) { HeaderLogged++; PressRoom.W("profilebar", f.name + (f.link ? " (link)" : ""), f.text.Length > 80 ? f.text.Substring(0, 80) : f.text); }
                }
                if (ProfileClub.Length > 0) { PressRoom.W("profile", who, "club " + ProfileClub); return; }
                // 0.13.3: a player's bar holds only the name (the club label is for club pages), so read the page itself
                if (who == PageScanned && (DateTime.Now - PageScannedAt).TotalSeconds < 5) return;
                PageScanned = who; PageScannedAt = DateTime.Now;
                var root = bar; while (root.parent != null) root = root.parent;
                ScanPage(root, who);
            }
            catch (Exception ex) { PressRoom.Fail("profile scan", ex); }
        }

        // club and team links on the page (type byte 0x18 club, 0x03 team), scored by the named elements around them
        static string PageScanned = ""; static DateTime PageScannedAt = DateTime.MinValue; static int PagesLogged;
        static readonly string[] Good = { "contract", "clubname", "club", "teamname", "team", "employer" };
        static readonly string[] Fine = { "header", "overview", "info", "bio", "details", "card" };
        static readonly string[] Bad = { "loan", "ban", "history", "transfer", "fixture", "match", "scout", "interest", "compar", "training", "news", "inbox", "portal", "message", "calendar", "league", "table", "result", "fm27mm" };
        static void ScanPage(VisualElement root, string who)
        {
            var found = new List<(string club, string chain, int score)>();
            int seen = 0;
            Walk(root, found, 0, ref seen);
            string pick = ""; int best = 0;
            foreach (var f in found) if (f.score > best) { best = f.score; pick = f.club; }
            string how = "scored";
            if (pick.Length == 0)
            {
                var distinct = new HashSet<string>(); foreach (var f in found) if (f.score >= 0) distinct.Add(f.club);
                if (distinct.Count == 1) { foreach (var c in distinct) pick = c; how = "only club on the page"; }
            }
            if (PagesLogged < 6)
            {
                PagesLogged++;
                int shown = 0;
                foreach (var f in found) { if (shown++ >= 14) break; PressRoom.W("profilepage", who + " | " + f.club + " | " + f.score, f.chain); }
            }
            ProfileClub = pick;
            PressRoom.W("profile", who, pick.Length > 0 ? "club " + pick + " (" + how + ", " + found.Count + " club links, " + seen + " elements)" : "no club found on the page (" + found.Count + " club links, " + seen + " elements)");
        }
        static void Walk(VisualElement e, List<(string, string, int)> into, int depth, ref int seen)
        {
            if (e == null || depth > 80 || seen > 20000 || into.Count > 40) return;
            seen++;
            var n = e.name ?? "";
            if (n == "MenuTitle" || n.StartsWith("FM27MM")) return;          // the title bar and our own panels
            try { if (e.resolvedStyle.display == DisplayStyle.None) return; } catch { }
            var t = e.TryCast<TextElement>();
            if (t != null && !string.IsNullOrEmpty(t.text) && t.text.IndexOf('\u0001') >= 0)
            {
                foreach (var li in PersonLinks.All(t.text))
                {
                    uint type = li.Id >> 24; if (type != 0x18 && type != 0x03) continue;
                    var chain = new StringBuilder(); int score = 0, named = 0;
                    for (var x = e; x != null && named < 7; x = x.parent)
                    {
                        var xn = x.name ?? ""; if (xn.Length == 0) continue;
                        named++; chain.Append(xn).Append('<'); var low = xn.ToLowerInvariant();
                        foreach (var g in Good) if (low.Contains(g)) { score += 3; break; }
                        foreach (var g in Fine) if (low.Contains(g)) { score += 1; break; }
                        foreach (var g in Bad) if (low.Contains(g)) { score -= 5; break; }
                    }
                    into.Add((li.Full.Length > 0 ? li.Full : li.Visible, chain.ToString(), score));
                }
            }
            for (int i = 0; i < e.childCount; i++) Walk(e.ElementAt(i), into, depth + 1, ref seen);
        }
        static void Collect(VisualElement e, List<(string, string, bool)> into, int depth)
        {
            if (e == null || depth > 14 || into.Count > 40) return;
            var t = e.TryCast<TextElement>();
            if (t != null && !string.IsNullOrEmpty(t.text))
            {
                var raw = t.text; var vis = PressRoom.Visible(raw).Trim();
                if (vis.Length > 0) into.Add((e.name ?? "", vis, raw.IndexOf('\u0001') >= 0));
            }
            for (int i = 0; i < e.childCount; i++) Collect(e.ElementAt(i), into, depth + 1);
        }

        static int EventsLogged;
        internal static void OnUiEvent(int ev, string identity)
        {
            if (EventsLogged >= 60) return;
            string name;
            try { name = ((FM.GamePlugin.InteropEventIDs)ev).ToString(); } catch { return; }
            if (!(name.StartsWith("EventFmxPlayerInterest") || name == "EventFmxDoAction" || name == "EventFmxAuditActionsMenuObjectSelected" || name == "EventFmxNewsResponseAction" || name == "EventFmxSpeakToDropdownActionComplete" || name == "EventFmxSpeakToQuickActionComplete" || name == "EventFmxPressConferenceSendAssistant" || name == "EventFmxPressConferenceEarlyExit")) return;
            EventsLogged++;
            PressRoom.W("uievent", name, identity + (ProfileName.Length > 0 && (DateTime.Now - ProfileAt).TotalMinutes < 5 ? " | profile " + ProfileName + (ProfileClub.Length > 0 ? " (" + ProfileClub + ")" : "") : ""));
        }

        // ---------- the cuttings ----------
        internal class Cutting { public int Conf; public string When = "", Kind = "", Who = "", Text = ""; }
        // kind: all | said | refused | office | leaks; who: a journalist, outlet or player picked from the chips (0.13.2:
        // no typing, since FM's own hotkeys take every key pressed in a text box)
        internal static List<Cutting> Cuttings(string who, string kind, int max)
        {
            var list = new List<Cutting>();
            var f = (who ?? "").Trim(); kind = string.IsNullOrEmpty(kind) ? "all" : kind;
            bool Match(params string[] s) { if (f.Length == 0) return true; foreach (var x in s) if (!string.IsNullOrEmpty(x) && x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
            if (kind == "all" || kind == "said" || kind == "refused")
                foreach (var e in PressRoom.Record.All)
                {
                    bool said = e.Kind == "answer", refused = e.Kind == "nocomment" || e.Kind == "storm" || e.Kind == "ban" || e.Kind == "wrapped";
                    if (kind == "said" && !said) continue;
                    if (kind == "refused" && !refused) continue;
                    string k = e.Kind == "answer" ? "Said" : e.Kind == "nocomment" ? "No comment" : e.Kind == "storm" ? "Walked out" : e.Kind == "ban" ? "Shut out" : e.Kind == "favour" ? "Favoured" : e.Kind == "wrapped" ? "Wrapped it up" : e.Kind;
                    var w = (e.Journalist ?? "") + (string.IsNullOrEmpty(e.Outlet) ? "" : " (" + e.Outlet + ")");
                    var text = said ? "\u201C" + Short(e.Answer, 150) + "\u201D, asked " + Short(e.Question, 110) : Short(e.Question, 150);
                    if (!Match(w, e.Answer, e.Question, e.Subjects)) continue;
                    list.Add(new Cutting { Conf = e.Conf, When = e.When ?? "", Kind = k, Who = w, Text = text });
                }
            if (kind == "all" || kind == "refused") NoShow.AddCuttings(list, a => Match(a));
            if (kind == "all" || kind == "office")
                foreach (var s in Spent)
                {
                    if (!Match(s.Action, s.Target, s.Outcome)) continue;
                    list.Add(new Cutting { Conf = s.Conf, When = s.When, Kind = "Office", Who = s.Target, Text = Cap(s.Action) + (s.Hours > 0 ? " (" + s.Hours + "h)" : "") + (s.Outcome.Length > 0 ? ": " + s.Outcome : "") });
                }
            if (kind == "all" || kind == "leaks")
                foreach (var w in Word.All)
                {
                    if (w.State != "out" && w.State != "planted") continue;
                    var d = Word.Describe(w);
                    if (!Match(d, w.Outlet, w.Player)) continue;
                    list.Add(new Cutting { Conf = w.Conf, When = w.When ?? "", Kind = w.State == "out" ? "Leaked" : "Planted", Who = w.Outlet ?? "", Text = d });
                }
            list.Sort((a, b) => b.Conf != a.Conf ? b.Conf.CompareTo(a.Conf) : string.CompareOrdinal(b.When, a.When));
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        // the names worth a chip: the outlets and players that come up most on the record
        internal static List<string> TopNames(bool players, int n)
        {
            var count = new Dictionary<string, int>();
            foreach (var e in PressRoom.Record.All)
            {
                if (players)
                {
                    foreach (var s in (e.Subjects ?? "").Split(new[] { '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    { var v = PressRoom.Visible(s).Trim(); if (v.Length > 2) count[v] = (count.TryGetValue(v, out var c) ? c : 0) + 1; }
                }
                else if (!string.IsNullOrEmpty(e.Outlet)) count[e.Outlet] = (count.TryGetValue(e.Outlet, out var c2) ? c2 : 0) + 1;
            }
            var list = new List<KeyValuePair<string, int>>(count);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var outp = new List<string>(); foreach (var kv in list) { if (outp.Count >= n) break; outp.Add(kv.Key); }
            return outp;
        }

        internal static string Totals()
        {
            int answers = 0, nc = 0, storms = 0, bans = 0, wraps = 0, confs = 0, last = -1;
            foreach (var e in PressRoom.Record.All)
            {
                if (e.Conf != last) { last = e.Conf; confs++; }
                if (e.Kind == "answer") answers++; else if (e.Kind == "nocomment") nc++; else if (e.Kind == "storm") storms++; else if (e.Kind == "ban") bans++; else if (e.Kind == "wrapped") wraps++;
            }
            int planted = 0, leaked = 0; foreach (var w in Word.All) { if (w.State == "planted") planted++; else if (w.State == "out") leaked++; }
            int hours = 0; foreach (var s in Spent) hours += s.Hours;
            return confs + " press conferences, " + answers + " answers, " + nc + " no comment" + (nc == 1 ? "" : "s") + ", " + storms + " walk-out" + (storms == 1 ? "" : "s") + (wraps > 0 ? ", " + wraps + " wrapped up early" : "")
                + ", " + bans + " question" + (bans == 1 ? "" : "s") + " cut by a ban, " + planted + " stor" + (planted == 1 ? "y" : "ies") + " planted, " + leaked + " leak" + (leaked == 1 ? "" : "s")
                + ", " + hours + " office hour" + (hours == 1 ? "" : "s") + " spent.";
        }

        static string Short(string s, int n) { s = PressRoom.Visible(s ?? "").Trim(); return s.Length > n ? s.Substring(0, n - 1).TrimEnd() + "…" : s; }
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
