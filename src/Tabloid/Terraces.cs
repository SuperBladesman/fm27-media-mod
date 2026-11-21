// FM27 Media Mod - The Terraces (Tabloid 0.6). Personal single-player use only.
// SI's Top Reactions are generic one-liners from any club anywhere. The game itself knows much more about a club's fans:
// who they are (Core, Family, Fair Weather, Corporate, Casual, Hardcore), how much say they have, what they expect
// (culture & expectations), how they feel about each part of the club, what they remember (highlights and criticisms),
// and what they make of each signing. FanBook reads that off SI's own screens as you look at them and keeps it per save
// (fans.tsv). Terraces then writes the fan posts under a story from that: who is posting follows the club's supporter
// mix (tilted by mood), what they say follows the story and the club's own record. SI's post slots, layout and hashtags
// are kept; only the words and the invented names are ours. Club accounts are left as SI wrote them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class FanBook
    {
        internal static string Tag = "", Code = "";   // Code (0.6.5): our three-letter code in match hashtags (SHUvsPOR -> SHU), learned from our own match stories
        static readonly Dictionary<string, int> CodeVotes = new Dictionary<string, int>();
        internal static string Club = "", Group = "", Spokes = "", Description = "", Influence = "", Overall = "", Expect = "";
        internal static int Followers, Attendance, Tickets;
        internal static readonly Dictionary<string, int> Segments = new Dictionary<string, int>();
        internal static readonly Dictionary<string, string> Areas = new Dictionary<string, string>();
        internal static List<string> Highs = new List<string>(), Crits = new List<string>(), Voice = new List<string>();
        internal class Cult { public string Name = "", Importance = "", Type = "", Reaction = ""; }
        internal static List<Cult> Culture = new List<Cult>();
        internal static readonly Dictionary<string, string> Signings = new Dictionary<string, string>();
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "fans.tsv");
        static DateTime Stamp = DateTime.MinValue;
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        internal static string Vis(string t) => Regex.Replace(Tabloid.Plain(t ?? ""), "<[^>]*>", "").Trim();
        static void W(string a, string b) { try { Tabloid.Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\tfanbook\t{a}\t{b}"); } catch { } }

        static bool Under(VisualElement e, string part, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent) if ((e.name ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        // which list a highlight or criticism belongs to: the supporters' or the board's
        static bool FansSide(VisualElement e, string v)
        {
            if (v.StartsWith("The board", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < 40 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                if (n.IndexOf("board", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("BindingsRemapper", StringComparison.Ordinal) < 0) return false;
                if (n.IndexOf("supporter", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return v.StartsWith("The supporters", StringComparison.OrdinalIgnoreCase) || v.StartsWith("The fans", StringComparison.OrdinalIgnoreCase);
        }

        // a burst of list items (one screen) replaces what was there
        static DateTime ListAt = DateTime.MinValue; static List<string> NewHighs = new List<string>(), NewCrits = new List<string>();
        static DateTime VoiceAt = DateTime.MinValue; static List<string> NewVoice = new List<string>();
        static bool Dirty;

        internal static string Seg(string k)
        {
            var l = (k ?? "").ToLowerInvariant();
            if (l.StartsWith("fair")) return "Fair Weather"; if (l.Contains("hardcore")) return "Hardcore"; if (l.Contains("core")) return "Core";
            if (l.Contains("family")) return "Family"; if (l.Contains("corporate")) return "Corporate"; if (l.Contains("casual")) return "Casual";
            return k;
        }
        static readonly Regex Verdict = new Regex(@"^(The supporters|The fans|(Very |Extremely |Slightly )?(Disappointed|Pleased|Delighted|Happy|Unhappy|Thrilled|Concerned|Angry|Furious|Satisfied|Upset|Frustrated|Annoyed|Relieved|Excited))\b", RegexOptions.Compiled);
        static readonly Regex SignVerdict = new Regex(@"^(Very |Extremely )?(Happy|Pleased|Delighted|Keen|Not sure|Unsure|Disappointed|Unhappy|Thrilled|Excited)\b", RegexOptions.Compiled);
        static readonly Regex Count = new Regex(@"^[\d,]+$", RegexOptions.Compiled);
        static readonly HashSet<string> Levels = new HashSet<string> { "Very High", "High", "Medium", "Low", "Very Low", "None" };
        // the supporter profile (Club > Supporters > Profile), by SI's element names
        static bool Consider(VisualElement el, string n, string txt)
        {
            if (n.StartsWith("FanPercentage", StringComparison.Ordinal))
            {
                var v = Vis(txt); if (!v.EndsWith("%")) return true;
                var k = Seg(n.Substring(13));
                int pc = Num(v); if (!Segments.TryGetValue(k, out var o) || o != pc) { Segments[k] = pc; Dirty = true; Save(); }
                return true;
            }
            if (n == "SocialMediaFollowers") { var c = Num(Vis(txt)); if (c > 0 && c != Followers) { Followers = c; Dirty = true; Save(); } return true; }
            var t = Vis(txt);
            if (Count.IsMatch(t))
            {
                if (Under(el, "average-attendance", 5)) { var c = Num(t); if (c != Attendance) { Attendance = c; Dirty = true; Save(); } return true; }
                if (Under(el, "season-tickets-holders", 5)) { var c = Num(t); if (c != Tickets) { Tickets = c; Dirty = true; Save(); } return true; }
            }
            if (Levels.Contains(t) && Under(el, "supporters-influence", 5)) { Set(ref Influence, t); return true; }
            return false;
        }

        internal static void OnText(VisualElement el, string txt)
        {
            try
            {
                if (string.IsNullOrEmpty(txt)) return;
                var n = el.name ?? "";
                if (Consider(el, n, txt)) return;
                if (n == "SupProfile" || txt == "Supporter Profile") { ScheduleScan(el, "profile"); return; }
                if (txt == "Supporter Culture & Expectations") { CultRows.Clear(); CultOrder.Clear(); CultOpen = DateTime.Now.AddSeconds(15); ScheduleScan(el, "culture"); return; }
                if (n == "ImportanceText") CultOpen = DateTime.Now.AddSeconds(15);
                // 0.6.5: the culture table's cells, read as SI sets them (the screen scan never found them: 0 rows in every
                // log). SI fills the table column by column; each row is a "View" under "HasElements" holding name,
                // ImportanceText, date (the type) and text (the reaction).
                if ((n == "name" || n == "ImportanceText" || n == "date" || n == "text") && DateTime.Now < CultOpen && CultCell(el, n, txt)) return;   // only while that screen is open: "text" is everywhere
                if (SignVerdict.IsMatch(txt) && Under(el, "SupportersTableTransfersIn", 14)) { ScheduleScan(el, "transfers"); return; }
                switch (n)
                {
                    case "ClubSupportersGroup":
                    {
                        var v = Vis(txt); if (v.EndsWith(" Supporters Group")) { Set(ref Group, v); Set(ref Club, v.Substring(0, v.Length - " Supporters Group".Length)); }
                        ScheduleScan(el, "profile"); return;
                    }
                    case "SupporterReportCompliedBy":
                    {
                        var m = Regex.Match(Vis(txt), @"compiled by (.+?) \("); if (m.Success) Set(ref Spokes, m.Groups[1].Value.Trim()); return;
                    }
                    case "ExpectationsTitle": ScheduleScan(el, "profile"); return;
                    case "Description":
                        if (Under(el, "ClubSupportersDescription", 4)) Set(ref Description, Vis(txt));
                        return;
                    case "ColoredText":
                    case "container":
                    {
                        var v = Vis(txt);
                        if (v.StartsWith("Overall, the supporters")) Set(ref Overall, v);
                        else if (v.StartsWith("The fans") && v.Contains("vision")) SetArea("ClubVision", v);
                        return;
                    }
                    case "ClubVision": case "Match": case "Recruitment": case "Tactics": case "Squad":
                    {
                        var v = Vis(txt); if (v.StartsWith("The fans")) SetArea(n, v); return;
                    }
                    default:
                        if (txt.StartsWith("The fans ", StringComparison.Ordinal))
                            for (var e = el.parent; e != null && e.name != "Breakdown"; e = e.parent)
                            {
                                var a = e.name ?? "";
                                if (a == "ClubVision" || a == "Match" || a == "Recruitment" || a == "Tactics" || a == "Squad") { SetArea(a, Vis(txt)); return; }
                            }
                        break;
                    case "description":
                        if (Under(el, "future-match-expectations", 6) && Under(el, "supporters-visible", 10)) Set(ref Expect, Vis(txt));
                        return;
                    case "root":
                        if (Under(el, "expectations-list", 4) && Under(el, "supporters-visible", 12))
                        {
                            var v = Vis(txt); if (v.Length < 8) return;
                            if ((DateTime.Now - VoiceAt).TotalSeconds > 3) NewVoice = new List<string>();
                            VoiceAt = DateTime.Now; if (!NewVoice.Contains(v)) NewVoice.Add(v);
                            if (string.Join("|", NewVoice) != string.Join("|", Voice)) { Voice = new List<string>(NewVoice); Dirty = true; Save(); }
                        }
                        return;
                }
                if (txt == "Supporter Feedback") { if (Under(el, "SupportersTableTransfersIn", 10)) ScheduleScan(el, "transfers"); return; }
                if (!Verdict.IsMatch(txt)) return;
                bool hi = Under(el, "HighlightsList", 12), cr = !hi && Under(el, "CriticismList", 12);
                if (hi || cr)
                {
                    var v = Vis(txt); if (v.Length < 10 || !FansSide(el, v)) return;
                    if ((DateTime.Now - ListAt).TotalSeconds > 3) { NewHighs = new List<string>(); NewCrits = new List<string>(); }
                    ListAt = DateTime.Now;
                    var into = hi ? NewHighs : NewCrits; if (!into.Contains(v)) into.Add(v);
                    if (NewHighs.Count > 0 && string.Join("|", NewHighs) != string.Join("|", Highs)) { Highs = new List<string>(NewHighs); Dirty = true; }
                    if (NewCrits.Count > 0 && string.Join("|", NewCrits) != string.Join("|", Crits)) { Crits = new List<string>(NewCrits); Dirty = true; }
                    if (Dirty) Save();
                }
            }
            catch (Exception e) { Tabloid.Fail("fanbook", e); }
        }
        internal static void VoteCode(string matchTag)
        {
            if (Code.Length > 0 || string.IsNullOrEmpty(matchTag)) return;
            var sides = Regex.Split(matchTag, "vs");
            foreach (var c in sides) { CodeVotes.TryGetValue(c, out var n); CodeVotes[c] = n + 1; }
            string best = null; int b1 = 0, b2 = 0;
            foreach (var kv in CodeVotes) { if (kv.Value > b1) { b2 = b1; b1 = kv.Value; best = kv.Key; } else if (kv.Value > b2) b2 = kv.Value; }
            if (best != null && b1 >= 2 && b1 > b2) { Code = best; Dirty = true; Save(); W("club code", best); }
        }
        internal static bool MatchIsOurs(string matchTag)
        {
            if (Code.Length == 0 || string.IsNullOrEmpty(matchTag)) return false;
            foreach (var c in Regex.Split(matchTag, "vs")) if (c.Equals(Code, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static readonly Dictionary<IntPtr, Cult> CultRows = new Dictionary<IntPtr, Cult>();
        static readonly List<IntPtr> CultOrder = new List<IntPtr>();
        static bool CultPending; static DateTime CultOpen = DateTime.MinValue;
        static bool CultCell(VisualElement el, string n, string txt)
        {
            VisualElement row = null;
            for (var e = el.parent; e != null && row == null; e = e.parent)
            {
                if (e.name == "View" && e.parent != null && Under(e.parent, "HasElements", 3)) row = e;
                if (e.name == "TIFO-TableHeaderTemplate" || e.name == "ClubSupporterCultureAndExpectation") break;
            }
            if (row == null || !Under(row, "ClubSupporterCultureAndExpectation", 40)) return false;
            var v = Vis(txt);
            if (!CultRows.TryGetValue(row.Pointer, out var c)) { c = new Cult(); CultRows[row.Pointer] = c; CultOrder.Add(row.Pointer); }
            if (n == "name") { if (c.Name.Length > 0 && c.Name != v) { c = new Cult(); CultRows[row.Pointer] = c; } c.Name = v; }   // a pooled row given a new item
            else if (n == "ImportanceText") c.Importance = v;
            else if (n == "date") c.Type = v;
            else c.Reaction = v;
            if (!CultPending)
            {
                CultPending = true;
                System.Action run = () => { CultPending = false; try { CultCommit(); } catch (Exception ex) { Tabloid.Fail("culture", ex); } };
                row.schedule.Execute(run).ExecuteLater(700);
            }
            return true;
        }
        static void CultCommit()
        {
            var cult = new List<Cult>(); var seen = new HashSet<string>();
            foreach (var p in CultOrder) if (CultRows.TryGetValue(p, out var c) && c.Name.Length > 3 && c.Importance.Length > 0 && seen.Add(c.Name))
                    cult.Add(new Cult { Name = c.Name, Importance = c.Importance, Type = c.Type, Reaction = c.Reaction });
            W("culture cells", $"{CultRows.Count} rows seen, {cult.Count} complete");
            if (cult.Count == 0) return;
            var sig = string.Join("|", cult.ConvertAll(c => c.Name + c.Importance + c.Type + c.Reaction));
            if (sig == string.Join("|", Culture.ConvertAll(c => c.Name + c.Importance + c.Type + c.Reaction))) return;
            Culture = cult; Dirty = true; Save();
            foreach (var c in cult) W("culture", c.Name + " | " + c.Importance + " | " + c.Type + " | " + c.Reaction);
        }
        internal static void LearnTag(string t) { if (string.IsNullOrEmpty(t) || t == Tag) return; Tag = t; Dirty = true; Save(); W("club tag", t); }
        static void Set(ref string field, string v) { if (string.IsNullOrEmpty(v) || field == v) return; field = v; Dirty = true; Save(); }
        static void SetArea(string area, string v) { if (Areas.TryGetValue(area, out var o) && o == v) return; Areas[area] = v; Dirty = true; Save(); }

        // ---------- reading a whole screen (some of it is drawn without SI's text component, so no hook sees it) ----------
        static readonly Dictionary<string, DateTime> LastScan = new Dictionary<string, DateTime>();
        static void ScheduleScan(VisualElement el, string what)
        {
            if (LastScan.TryGetValue(what, out var t) && (DateTime.Now - t).TotalSeconds < 3) return;
            LastScan[what] = DateTime.Now;
            VisualElement root = el;
            if (what == "transfers") { for (var e = el; e != null; e = e.parent) if (e.name == "SupportersTableTransfersIn") { root = e; break; } }
            else { for (var e = el; e != null; e = e.parent) { root = e; if (e.name == "MainUIDocument-container") break; } }
            var keep = root;
            System.Action run = () => { try { if (what == "transfers") ScanTransfers(keep); else ScanProfile(keep); } catch (Exception ex) { Tabloid.Fail("fan scan", ex); } };
            root.schedule.Execute(run).ExecuteLater(what == "transfers" ? 400 : 900);
        }
        struct Tx { public string Name, Text; public int Row; public bool Exp; public VisualElement El; }
        static int Visited;
        static void Collect(VisualElement e, List<Tx> acc, int depth, bool exp, ref int row)
        {
            if (e == null || depth > 120 || acc.Count > 12000 || ++Visited > 40000) return;
            var nm = e.name ?? "";
            if (!exp && nm == "ClubSupporterCultureAndExpectation") exp = true;
            var te = e.TryCast<TextElement>();
            if (te != null) { var t = te.text; if (!string.IsNullOrEmpty(t)) acc.Add(new Tx { Name = nm, Text = t, Row = row, Exp = exp, El = e }); }
            for (int i = 0; i < e.childCount; i++) Collect(e.ElementAt(i), acc, depth + 1, exp, ref row);
        }
        static VisualElement RowOf(VisualElement e) { for (int i = 0; i < 6 && e != null; i++, e = e.parent) if (e.name == "root") return e; return null; }
        static int Num(string s) { int.TryParse(Regex.Replace(s ?? "", @"[^\d]", ""), out var n); return n; }
        static void ScanProfile(VisualElement root)
        {
            var acc = new List<Tx>(); int row = 0; Visited = 0; Collect(root, acc, 0, false, ref row);
            int before = Segments.Count;
            foreach (var t in acc) Consider(t.El, t.Name, t.Text);
            // culture & expectations: one "root" per row, with name, ImportanceText, date (the type) and text (the reaction)
            var rows = new Dictionary<IntPtr, Cult>(); var order = new List<IntPtr>();
            foreach (var t in acc)
            {
                if (!t.Exp) continue;
                if (t.Name != "name" && t.Name != "ImportanceText" && t.Name != "date" && t.Name != "text") continue;
                var r = RowOf(t.El); if (r == null) continue;
                if (!rows.TryGetValue(r.Pointer, out var c)) { c = new Cult(); rows[r.Pointer] = c; order.Add(r.Pointer); }
                var v = Vis(t.Text);
                if (t.Name == "name" && c.Name.Length == 0) c.Name = v;
                else if (t.Name == "ImportanceText" && c.Importance.Length == 0) c.Importance = v;
                else if (t.Name == "date" && c.Type.Length == 0) c.Type = v;
                else if (t.Name == "text" && c.Reaction.Length == 0) c.Reaction = v;
            }
            var cult = new List<Cult>(); foreach (var p in order) { var c = rows[p]; if (c.Name.Length > 3 && c.Importance.Length > 0) cult.Add(c); }
            // 0.6.4: SI draws the table column by column (all names, then importance, type, reaction), and the type cell
            // is not inside the row's "root". When the four columns have the same length, pair them by position.
            var cn = new List<string>(); var ci = new List<string>(); var ct = new List<string>(); var cr = new List<string>();
            foreach (var t in acc)
            {
                if (!t.Exp) continue;
                var v = Vis(t.Text);
                if (t.Name == "name" && Under(t.El, "container", 2)) cn.Add(v);
                else if (t.Name == "ImportanceText") ci.Add(v);
                else if (t.Name == "date" && Under(t.El, "View", 2)) ct.Add(v);
                else if (t.Name == "text" && Under(t.El, "container", 2) && RowOf(t.El) != null) cr.Add(v);
            }
            if (cn.Count > 0 && cn.Count == ci.Count && cn.Count == ct.Count && cn.Count == cr.Count && cn.Count >= cult.Count)
            {
                cult = new List<Cult>();
                for (int i = 0; i < cn.Count; i++) if (cn[i].Length > 3) cult.Add(new Cult { Name = cn[i], Importance = ci[i], Type = ct[i], Reaction = cr[i] });
            }
            else if (cult.Count > 0 && ct.Count == cult.Count) for (int i = 0; i < cult.Count; i++) if (cult[i].Type.Length == 0) cult[i].Type = ct[i];
            if (cult.Count > 0 && cult.Count >= Culture.Count)
            {
                var sig = string.Join("|", cult.ConvertAll(c => c.Name + c.Importance + c.Type + c.Reaction));
                if (sig != string.Join("|", Culture.ConvertAll(c => c.Name + c.Importance + c.Type + c.Reaction))) { Culture = cult; Dirty = true; }
            }
            W("screen scan", $"{Visited} elements, {acc.Count} texts; segments {Segments.Count}, followers {Followers}, attendance {Attendance}, tickets {Tickets}, influence {Influence}, culture rows {cult.Count}");
            foreach (var c in cult) W("culture", c.Name + " | " + c.Importance + " | " + c.Type + " | " + c.Reaction);
            Save();
        }
        static readonly Regex PersonLink = new Regex("\u0001([A-Za-z0-9+/=]+)\u0002([^\u0001\u0002<]+)", RegexOptions.Compiled);
        static void ScanTransfers(VisualElement table)
        {
            // each row: the player (a link to a profile), then nation, fee, wage and the supporters' verdict (the longest text)
            VisualElement view = null;
            for (int i = 0; i < table.childCount && view == null; i++) view = FindNamed(table.ElementAt(i), "View", 0);
            if (view == null) return;
            int got = 0;
            for (int r = 0; r < view.childCount; r++)
            {
                var acc = new List<Tx>(); int row = 0; Visited = 0; Collect(view.ElementAt(r), acc, 0, false, ref row);
                string who = null, best = "";
                foreach (var t in acc)
                {
                    if (who == null) { var m = PersonLink.Match(t.Text); if (m.Success) who = Vis(m.Groups[2].Value); }
                    var v = Vis(t.Text); if (v.Length > best.Length) best = v;
                }
                if (who == null && acc.Count > 0) who = Vis(acc[0].Text);
                if (string.IsNullOrEmpty(who) || best.Length < 15 || best == who) continue;
                if (!Signings.TryGetValue(who, out var o) || o != best) { Signings[who] = best; Dirty = true; }
                got++;
            }
            W("transfers scan", got + " signings with a supporters' verdict");
            foreach (var kv in Signings) W("signing", kv.Key + " | " + kv.Value);
            Save();
        }
        static VisualElement FindNamed(VisualElement e, string name, int depth)
        {
            if (e == null || depth > 6) return null;
            if (e.name == name) return e;
            for (int i = 0; i < e.childCount; i++) { var r = FindNamed(e.ElementAt(i), name, depth + 1); if (r != null) return r; }
            return null;
        }

        // ---------- the file (kept per save by Press Room's memory) ----------
        internal static void Save()
        {
            if (!Dirty) return;
            try
            {
                var sb = new StringBuilder();
                void L(string k, string v) { if (!string.IsNullOrEmpty(v)) sb.Append(k).Append('\t').Append(Clean(v)).Append('\n'); }
                L("club", Club); L("tag", Tag); L("code", Code); L("group", Group); L("spokes", Spokes); L("desc", Description); L("influence", Influence); L("overall", Overall); L("expect", Expect);
                if (Followers > 0) L("followers", Followers.ToString()); if (Attendance > 0) L("attendance", Attendance.ToString()); if (Tickets > 0) L("tickets", Tickets.ToString());
                foreach (var kv in Segments) sb.Append("seg\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                foreach (var kv in Areas) sb.Append("area\t").Append(kv.Key).Append('\t').Append(Clean(kv.Value)).Append('\n');
                foreach (var h in Highs) L("high", h); foreach (var c in Crits) L("crit", c); foreach (var v in Voice) L("voice", v);
                foreach (var c in Culture) sb.Append("cult\t").Append(Clean(c.Name)).Append('\t').Append(Clean(c.Importance)).Append('\t').Append(Clean(c.Type)).Append('\t').Append(Clean(c.Reaction)).Append('\n');
                foreach (var kv in Signings) sb.Append("sign\t").Append(Clean(kv.Key)).Append('\t').Append(Clean(kv.Value)).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
                Stamp = File.GetLastWriteTimeUtc(FilePath); Dirty = false;
            }
            catch (Exception e) { Tabloid.Fail("fanbook save", e); }
        }
        // the file changes under us when a different save is loaded (Press Room swaps the memory)
        internal static void Fresh()
        {
            try
            {
                if (!File.Exists(FilePath)) { if (Stamp != DateTime.MinValue) { Clear(); Stamp = DateTime.MinValue; } return; }
                var t = File.GetLastWriteTimeUtc(FilePath); if (t == Stamp) return;
                Clear(); Stamp = t;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t'); if (p.Length < 2) continue;
                    switch (p[0])
                    {
                        case "club": Club = p[1]; break; case "tag": if (!Story.IsMatchTag(p[1])) Tag = p[1]; break; case "code": Code = p[1]; break; case "group": Group = p[1]; break; case "spokes": Spokes = p[1]; break;
                        case "desc": Description = p[1]; break; case "influence": Influence = p[1]; break; case "overall": Overall = p[1]; break; case "expect": Expect = p[1]; break;
                        case "followers": Followers = Num(p[1]); break; case "attendance": Attendance = Num(p[1]); break; case "tickets": Tickets = Num(p[1]); break;
                        case "seg": if (p.Length >= 3) Segments[Seg(p[1])] = Num(p[2]); break;
                        case "area": if (p.Length >= 3) Areas[p[1]] = p[2]; break;
                        case "high": Highs.Add(p[1]); break; case "crit": Crits.Add(p[1]); break; case "voice": Voice.Add(p[1]); break;
                        case "cult": if (p.Length >= 5) Culture.Add(new Cult { Name = p[1], Importance = p[2], Type = p[3], Reaction = p[4] }); break;
                        case "sign": if (p.Length >= 3) Signings[p[1]] = p[2]; break;
                    }
                }
            }
            catch (Exception e) { Tabloid.Fail("fanbook load", e); }
        }
        static void Clear()
        {
            CodeVotes.Clear(); Club = Tag = Code = Group = Spokes = Description = Influence = Overall = Expect = ""; Followers = Attendance = Tickets = 0;
            Segments.Clear(); Areas.Clear(); Highs = new List<string>(); Crits = new List<string>(); Voice = new List<string>(); Culture = new List<Cult>(); Signings.Clear();
        }

        // -3 (furious) .. +3 (could not be happier), from SI's own wording
        internal static int Mood(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Length == 0) return 0;
            if (s.Contains("could not be happier") || s.Contains("couldn't be happier") || s.Contains("ecstatic") || s.Contains("jubilant")) return 3;
            if (s.Contains("delighted") || s.Contains("very pleased") || s.Contains("very happy")) return 2;
            if (s.Contains("furious") || s.Contains("could not be more") || s.Contains("livid")) return -3;
            if (s.Contains("unhappy") || s.Contains("very disappointed") || s.Contains("angry")) return -2;
            if (s.Contains("disappointed") || s.Contains("concerned") || s.Contains("frustrated")) return -1;
            if (s.Contains("pleased") || s.Contains("happy")) return 1;
            return 0;
        }
    }

    internal static class Terraces
    {
        internal static bool On = true, Emoji = true;
        internal static bool Applying;
        internal static string StoryKey = "";
        internal static readonly Dictionary<string, string> Replaced = new Dictionary<string, string>();
        static readonly HashSet<string> Mine = new HashSet<string>();
        static readonly Dictionary<string, HashSet<string>> Used = new Dictionary<string, HashSet<string>>();
        static readonly HashSet<IntPtr> Pending = new HashSet<IntPtr>();
        static void W(string a, string b) { try { Tabloid.Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\tterraces\t{a}\t{b}"); } catch { } }
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        // SI's text shown again in the same story keeps our words (prefix)
        internal static string Map(string txt)
        {
            if (Replaced.Count == 0 || string.IsNullOrEmpty(txt)) return null;
            return Replaced.TryGetValue(StoryKey + "\u0007" + txt, out var m) ? m : null;
        }

        internal static void Schedule(VisualElement list)
        {
            var p = list.Pointer; if (!Pending.Add(p)) return;
            var keep = list;
            System.Action run = () => { Pending.Remove(p); try { Rewrite(keep); } catch (Exception e) { Tabloid.Fail("terraces", e); } };
            list.schedule.Execute(run).ExecuteLater(60);
        }

        static void FindAll(VisualElement e, string name, List<VisualElement> into, int depth)
        {
            if (e == null || depth > 12) return;
            if (e.name == name) into.Add(e);
            for (int i = 0; i < e.childCount; i++) FindAll(e.ElementAt(i), name, into, depth + 1);
        }
        static TextElement Text(VisualElement e, string name)
        {
            var l = new List<VisualElement>(); FindAll(e, name, l, 0);
            foreach (var x in l) { var t = x.TryCast<TextElement>(); if (t != null) return t; }
            return null;
        }
        static void SetText(TextElement t, string s)
        {
            try { Applying = true; var si = t.TryCast<SI.Bindable.SIText>(); if (si != null) si.SetText(s); else t.text = s; } finally { Applying = false; }
        }

        static readonly Dictionary<IntPtr, int> Tries = new Dictionary<IntPtr, int>();
        static void Again(VisualElement list)
        {
            var p = list.Pointer; Tries.TryGetValue(p, out var n); if (n >= 4) return; Tries[p] = n + 1;
            var keep = list; System.Action run = () => { try { Rewrite(keep); } catch (Exception e) { Tabloid.Fail("terraces", e); } };
            list.schedule.Execute(run).ExecuteLater(150);
        }
        static TextElement NameText(VisualElement fake)
        {
            var l = new List<VisualElement>(); FindAll(fake, "text", l, 0);
            foreach (var x in l)
            {
                bool ini = false; for (var e = x.parent; e != null && e.Pointer != fake.Pointer; e = e.parent) if (e.name == "Initials") { ini = true; break; }
                if (ini) continue;
                var t = x.TryCast<TextElement>(); if (t != null) return t;
            }
            return null;
        }
        static void Rewrite(VisualElement list)
        {
            EmojiFont.Ensure();
            FanBook.Fresh();
            var head = FanDesk.LastHead ?? ""; var body = FanDesk.LastBody ?? "";
            StoryKey = Hash(head + "|" + body).ToString();
            var descs = new List<VisualElement>(); FindAll(list, "Description", descs, 0);
            var story = Story.Read(head, body, descs);
            int slot = 0, done = 0; bool waiting = false;
            if (!Used.TryGetValue(StoryKey, out var used)) { used = new HashSet<string>(); Used[StoryKey] = used; if (Used.Count > 200) Used.Clear(); }
            foreach (var d in descs)
            {
                var dt = d.TryCast<TextElement>(); if (dt == null) continue;
                // the post is the nearest ancestor holding an author block
                VisualElement post = d.parent, fakeEl = null;
                for (int i = 0; i < 7 && post != null; i++, post = post.parent) { fakeEl = FindFirst(post, "FakeAuthor"); if (fakeEl != null || FindFirst(post, "RealAuthor") != null) break; }
                if (post == null) { waiting = true; slot++; continue; }
                // a club's own account (RealAuthor) keeps SI's words
                var realEl = FindFirst(post, "RealAuthor"); var real = realEl == null ? null : Text(realEl, "text");
                var fake = fakeEl == null ? null : NameText(fakeEl);
                if (real != null && !string.IsNullOrEmpty(real.text) && IsShown(realEl) && (fakeEl == null || !IsShown(fakeEl))) { slot++; continue; }
                if (fake == null || string.IsNullOrEmpty(fake.text)) { waiting = true; if (Seen++ < 20) W("wait", "no author yet: " + FanBook.Vis(dt.text ?? "")); slot++; continue; }
                var orig = dt.text ?? "";
                if (Mine.Contains(orig)) { slot++; continue; }     // ours already (the story's used lines are kept)
                var mine = Voice.Compose(story, slot, used);
                if (mine.text == null) { slot++; continue; }
                if (!Emoji) mine.text = Voice.NoEmoji(mine.text);
                int tagAt = TagStart(orig);
                var tags = tagAt >= 0 ? " " + orig.Substring(tagAt) : "";
                var newText = mine.text + tags;
                Mine.Add(newText); if (Mine.Count > 400) Mine.Clear();
                Replaced[StoryKey + "\u0007" + orig] = newText;
                SetText(dt, newText);
                var origName = fake.text ?? "";
                Replaced[StoryKey + "\u0007" + origName] = mine.name;
                SetText(fake, mine.name);
                var ini = Text(FindFirst(fakeEl, "Initials"), "text");
                if (ini != null) { Replaced[StoryKey + "\u0007" + (ini.text ?? "")] = Initials(mine.name); SetText(ini, Initials(mine.name)); }
                W(mine.seg + " " + story.Kind + (story.Ours ? " (ours: " + story.OursWhy + ")" : " (" + (story.ClubTag.Length > 0 ? story.ClubTag : story.MatchTag) + "; " + story.OursWhy + ")"), mine.name + ": " + mine.text);
                slot++; done++;
            }
            if (waiting) Again(list);
            while (Replaced.Count > 600) { var e = Replaced.Keys.GetEnumerator(); e.MoveNext(); Replaced.Remove(e.Current); }
            if (done > 0) W("story", story.Kind + " | " + head + (story.ResultWhy.Length > 0 ? " | " + story.ResultWhy : ""));
        }
        static int Seen;
        static bool IsShown(VisualElement e) { try { return e != null && e.resolvedStyle.display != DisplayStyle.None; } catch { return false; } }
        static VisualElement FindFirst(VisualElement e, string name) { if (e == null) return null; var l = new List<VisualElement>(); FindAll(e, name, l, 0); return l.Count > 0 ? l[0] : null; }
        static int TagStart(string s)
        {
            int b = s.IndexOf("|#", StringComparison.Ordinal); if (b < 0) return -1;
            int a = s.LastIndexOf("<color", b, StringComparison.Ordinal);
            if (a >= 0 && s.IndexOf('>', a) < b && s.Substring(s.IndexOf('>', a) + 1, b - s.IndexOf('>', a) - 1).Trim().Length == 0) return a;
            return b;
        }
        static string Initials(string name)
        {
            var p = name.Split(' '); var sb = new StringBuilder();
            foreach (var x in p) if (x.Length > 0 && char.IsLetter(x[0])) { sb.Append(char.ToUpperInvariant(x[0])); if (sb.Length == 2) break; }
            return sb.ToString();
        }
    }

    // what a story is about, read from its headline, text and SI's hashtags
    internal class Story
    {
        internal string Head = "", Body = "", All = "", Kind = "other", Subject = "", Manager = "", ClubTag = "", MatchTag = "", Rival = "", Score = "";
        internal static bool IsMatchTag(string t) => Regex.IsMatch(t ?? "", @"^[A-Z0-9]{2,4}vs[A-Z0-9]{2,4}$");
        internal bool Ours, Negative, Positive, AboutRival, Loan;
        internal string OursWhy = "", ResultWhy = "";
        static readonly Regex Tag = new Regex("\\|#(?:<[^>]*>|\u0001[A-Za-z0-9+/=]*\u0002)*([^\u0001<|]+)", RegexOptions.Compiled);   // 0.6.5: also the read-back form, |#<color..><link..><style..>Name
        static readonly (string kind, Regex rx)[] Kinds =
        {
            ("storm", new Regex(@"storm(ed|ing)? out|blows top|loses cool|walked out of|refus(ed|es|ing) to (answer|comment)|no comment", RegexOptions.IgnoreCase)),
            ("unrest", new Regex(@"irreconcilable|wants out|transfer request|unhappy|unhappiness|efforts to leave|complain|moaning|unsettled|uncertain future|way out of the club", RegexOptions.IgnoreCase)),
            ("crisis", new Regex(@"under pressure|pressure (is )?(building|mounts)|under fire|sacked|sack race|crisis|serious questions|all is not well|below expectations", RegexOptions.IgnoreCase)),
            ("injury", new Regex(@"injur|ruled out|sidelined|out for|knock|scan|surgery", RegexOptions.IgnoreCase)),
            ("award", new Regex(@"of the month|of the week|award|scoops|accolade|team of the", RegexOptions.IgnoreCase)),
            ("sale", new Regex(@"\b(sold|sells|sale of|selling|departs|departure|leaves|exit|offloaded|moves on|moved on)\b", RegexOptions.IgnoreCase)),
            ("signing", new Regex(@"signing|signed|signs|joins|joined|completed the|unveil|on loan|loan (move|deal|capture)|capture|new arrival|debut", RegexOptions.IgnoreCase)),
            ("draw", new Regex(@"\bdraw\b|\bdrew\b|stalemate|share the spoils|held to|goalless|all square", RegexOptions.IgnoreCase)),
            ("defeat", new Regex(@"defeat|beaten|\blost\b|losing|suffered|thrashed|humbled", RegexOptions.IgnoreCase)),
            ("win", new Regex(@"\bwin\b|victory|\bwon\b|comeback|\bbeat\b|three points|thrash|stars in|drives .* forward", RegexOptions.IgnoreCase)),
            ("preview", new Regex(@"ahead of|preview|all eyes on|clash|travel to|welcome .* to|face .* (on|this)", RegexOptions.IgnoreCase)),
        };
        static readonly HashSet<string> Bad = new HashSet<string> { "storm", "unrest", "crisis", "injury", "defeat" };
        static readonly HashSet<string> Good = new HashSet<string> { "award", "signing", "win" };

        internal static Story Read(string head, string body, List<VisualElement> descs)
        {
            var s = new Story { Head = FanBook.Vis(head), Body = FanBook.Vis(body) };
            s.All = s.Head + " " + s.Body;
            var tags = new List<string>(); var allTags = new List<string>();
            foreach (var d in descs)
            {
                var t = d.TryCast<TextElement>(); if (t == null) continue;
                var mine = new List<string>();
                foreach (Match m in Tag.Matches(t.text ?? "")) { var x = m.Groups[1].Value.Trim(); if (x.Length > 0 && !mine.Contains(x)) mine.Add(x); }
                foreach (var x in mine) if (!allTags.Contains(x)) allTags.Add(x);
                mine.RemoveAll(IsMatchTag);
                // the subject and club come from the first post tagged "#Player #Club" (0.6.5: a lone "#CH" competition tag is not a club)
                if (tags.Count < 2 && mine.Count > tags.Count) tags = mine;
            }
            foreach (var t in allTags) if (IsMatchTag(t)) s.MatchTag = t;
            tags.RemoveAll(IsMatchTag);
            s.ClubTag = tags.Count > 0 ? tags[tags.Count - 1] : "";
            s.Subject = tags.Count > 1 ? tags[0] : "";
            if (tags.Count > 2) s.Manager = tags[1];
            if (s.Subject.Length > 0 && s.Subject.Equals(s.ClubTag, StringComparison.OrdinalIgnoreCase)) s.Subject = "";
            if (s.Subject.Length > 0 && Regex.IsMatch(s.Subject, @"\d")) s.Subject = "";
            // whose fans post under it (0.6.5): any post's club hashtag that is ours decides; posts tagged only with other
            // clubs are theirs. With no club hashtag at all, the headline or the story's first sentence must name us:
            // a passing mention further down ("... impressing at Sheffield United") no longer makes a story ours.
            if (FanBook.Club.Length > 0)
            {
                var ourPack = FanWords.For(FanBook.Club);
                string ourTag = null;
                foreach (var t in allTags)
                {
                    if (IsMatchTag(t)) continue;
                    bool isOurs = (FanBook.Tag.Length > 0 && t.Equals(FanBook.Tag, StringComparison.OrdinalIgnoreCase)) || (ourPack != null && FanWords.For(t) == ourPack);
                    if (isOurs) { ourTag = t; break; }
                }
                if (ourTag != null) { s.Ours = true; if (FanBook.Tag.Length == 0) FanBook.LearnTag(ourTag); if (s.ClubTag.Length == 0) s.ClubTag = ourTag; }
                else
                {
                    var first = FirstSentence(s.Body);
                    bool named = Mentions(s.Head, FanBook.Club) || Mentions(first, FanBook.Club);
                    if (!named && ourPack != null) foreach (var a in ourPack.Aka) if (a.Length > 3 && (Mentions(s.Head, a) || Mentions(first, a))) { named = true; break; }
                    // a match story tagged only with the match (#SHUvsPOR): ours if the match is ours. Until our code is learned,
                    // the old test (named anywhere in the story) stands for match stories only.
                    if (!named && s.MatchTag.Length > 0 && tags.Count == 0)
                        named = FanBook.Code.Length > 0 ? FanBook.MatchIsOurs(s.MatchTag) : (s.Head + " " + s.Body).IndexOf(FanBook.Club, StringComparison.OrdinalIgnoreCase) >= 0;
                    s.Ours = named;
                }
                // learn our match code from match stories the headline or first sentence ties to us
                if (s.MatchTag.Length > 0 && FanBook.Code.Length == 0 && (ourTag != null || Mentions(s.Head, FanBook.Club) || Mentions(FirstSentence(s.Body), FanBook.Club))) FanBook.VoteCode(s.MatchTag);
                s.OursWhy = ourTag != null ? "tag " + ourTag : s.Ours ? "named" : allTags.Count > 0 ? "tags " + string.Join(",", allTags) : "not named";
            }
            // what kind of story: the headline first, then the first sentence, then the whole text (0.6.5)
            s.Kind = KindOf(s.Head) ?? KindOf(FirstSentence(s.Body)) ?? KindOf(s.All) ?? "other";
            // whose result is it (0.6.6): "Middlesbrough ease to victory" is a defeat for us, "QPR promotion bid takes hit"
            // (after a 3-1 defeat to Sheffield United) a win. SI's own result sentence decides; a result word that can't be
            // tied to us only counts when a headline names us before it, otherwise the fans post about nothing in particular.
            if (s.Ours && FanBook.Club.Length > 0)
            {
                var names = new List<string> { FanBook.Club };
                var op = FanWords.For(FanBook.Club); if (op != null) names.AddRange(op.Aka);
                var r = ResultRules.Ours(s.Head, s.Body, names, out var rwhy);
                if (r != null && (s.Kind == "win" || s.Kind == "defeat" || s.Kind == "draw" || s.Kind == "other" || s.Kind == "preview"))
                {
                    s.ResultWhy = (r != s.Kind ? s.Kind + "->" + r + " " : "") + "(" + rwhy + ")";
                    s.Kind = r;
                }
                else if (r == null && (s.Kind == "win" || s.Kind == "defeat"))
                {
                    bool fromHead = KindOf(s.Head) == s.Kind;
                    if (!(fromHead && ResultRules.HeadIsOurs(s.Head, names, KindRx(s.Kind))))
                    {
                        s.ResultWhy = s.Kind + "->other (result not tied to us)";
                        s.Kind = "other";
                    }
                }
            }
            var sc = Regex.Match(s.All, @"\b(\d{1,2})\s*-\s*(\d{1,2})\b"); if (sc.Success) s.Score = sc.Value.Replace(" ", "");
            foreach (var c in FanBook.Culture)
                if (c.Type == "Rivalry")
                {
                    var name = RivalName(c.Name); if (name.Length > 0 && (Mentions(s.All, name) || Mentions(s.ClubTag, name))) { s.Rival = name; break; }
                }
            s.Negative = Bad.Contains(s.Kind); s.Positive = Good.Contains(s.Kind);
            s.Loan = Regex.IsMatch(s.All, @"\bloan", RegexOptions.IgnoreCase);
            // a story about one of our rivals (their fans post under it; one of ours may turn up)
            if (!s.Ours && s.Rival.Length > 0 && s.ClubTag.Length > 0)
            {
                var rp = FanWords.For(s.Rival);
                if (Mentions(s.ClubTag, s.Rival) || (rp != null && FanWords.For(s.ClubTag) == rp)) s.AboutRival = true;
            }
            return s;
        }
        static Regex KindRx(string kind) { foreach (var (k, rx) in Kinds) if (k == kind) return rx; return new Regex("(?!)"); }
        static string KindOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var (k, rx) in Kinds) if (rx.IsMatch(text)) return k;
            return null;
        }
        static string FirstSentence(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            var m = Regex.Match(body, @"^.*?[.!?](?=\s|$)", RegexOptions.Singleline);
            return m.Success ? m.Value : body;
        }
        // "Get the better of our rivals Sheff Wed if we happen to play against them" -> "Sheff Wed"
        internal static string RivalName(string c)
        {
            var m = Regex.Match(c ?? "", @"(?:rivals|against)\s+(.+?)\s+if we", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }
        // "Sheff Wed" matches "Sheffield Wednesday"; "Nott'm Forest" matches "Nottingham Forest"
        internal static bool Mentions(string text, string name)
        {
            var words = Regex.Split(text, @"[^\p{L}']+");
            var parts = name.Replace("'", "").Split(' ');
            for (int i = 0; i + parts.Length <= words.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < parts.Length && ok; j++)
                {
                    var w = words[i + j].Replace("'", ""); var p = parts[j];
                    ok = w.Length >= p.Length - 1 && (w.StartsWith(p.Length > 4 ? p.Substring(0, 4) : p, StringComparison.OrdinalIgnoreCase));
                }
                if (ok) return true;
            }
            return false;
        }
    }

}
