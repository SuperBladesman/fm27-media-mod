// FM27 Media Mod - Treatment Room 0.3.0: the Treatment Room page.
// 0.6.0: GUIDANCE FROM THE TEAM is the first page (Guidance.cs, GuidanceView.cs), and the whole page has the new look
// (the Design canvas sketch, 23 Nov): a header with the game's display font, pill tabs, cards with a heading band, roomier tables.
// A MEDICAL tab at the right edge (below Press Room's MEDIA tab) opens an overlay page of our own, built like the
// Media Office (PROVEN pattern: an absolutely positioned element in MainUIDocument-container, Buttons, a ScrollView):
//   Overview      the season so far, month by month, who to watch, the physio's record in a line
//   Who to watch  the table, with a file and a watch button for each player
//   Player files  every squad player; a file holds the screening note, the injuries on record, the watch item and the plans
//   Physio's record  forecasts against returns
//   Diary         the medical diary this season
//   Sports science   the latest assessment in numbers, the flag record, injuries by month
// Plans are v1 "guide and record" (design part 2): the page says what to do in the game's own menus and the Treatment
// Room marks the plan carried out when it sees the action. Nothing here sends a command to the game.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class Room
    {
        static VisualElement Host, Tab, Page, Body, TabsRow;
        static Label Sub;
        static DateTime NextCheck = DateTime.MinValue; static bool Ticking;
        static string View = "guidance", FileWho = "", Flash = "";

        // 0.5.7 (Nik, 23 Nov): one look for every part (the Media Office's near-black), with the Treatment Room's accent:
        // red and white, not glaring ("Gold" keeps its name: it is the highlight colour everywhere on the page)
        // 0.6.0: the sketch's palette (GuidanceView.cs); the old names stay for the older pages
        static readonly Color Bg = new Color(0.082f, 0.082f, 0.09f, 0.985f), Card = new Color(0.11f, 0.11f, 0.12f, 1f), Ink = new Color(0.957f, 0.949f, 0.937f, 1f),
            Quiet = new Color(0.741f, 0.722f, 0.694f, 1f), Gold = new Color(1f, 0.541f, 0.576f, 1f), TabC = new Color(0.81f, 0.18f, 0.23f, 1f),
            Sel = new Color(0.81f, 0.18f, 0.23f, 1f), Grey = new Color(0.169f, 0.169f, 0.188f, 1f), Dim = new Color(0.137f, 0.137f, 0.153f, 1f),
            Rule = new Color(0.173f, 0.173f, 0.192f, 1f), Hot = new Color(0.361f, 0.122f, 0.145f, 1f);

        static int StartMonth => TreatmentRoom.SeasonStartMonth.Value;
        static string Today => GameDate.Text;

        // ---------- the tab (checked every couple of seconds from any text the game sets) ----------
        internal static void EnsureTab(VisualElement any)
        {
            if (!TreatmentRoom.RoomOn.Value || DateTime.Now < NextCheck) return;
            NextCheck = DateTime.Now.AddSeconds(2);
            try
            {
                if (Tab != null && Tab.panel != null) { Place(false); Popout.Update(); return; }   // 0.5.6: the popout's status, pill and notes
                VisualElement host = null;
                for (var e = any; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { host = e; break; }
                if (host == null) return;
                Host = host;
                Tab = Btn("M\nE\nD\nI\nC\nA\nL", Toggle, TabC);
                Tab.name = "FM27MM-TreatmentRoomTab"; Tab.tooltip = "Treatment Room";
                Tab.style.color = new Color(0.98f, 0.96f, 0.92f, 1f);
                Tab.style.position = Position.Absolute; Tab.style.right = 0; Tab.style.top = TreatmentRoom.TabTop.Value;
                Tab.style.width = 22; Tab.style.fontSize = 11; Tab.style.whiteSpace = WhiteSpace.Normal;
                Tab.style.paddingLeft = 2; Tab.style.paddingRight = 2; Tab.style.paddingTop = 6; Tab.style.paddingBottom = 6; Tab.style.marginRight = 0;
                Tab.style.borderTopLeftRadius = 6; Tab.style.borderBottomLeftRadius = 6; Tab.style.borderTopRightRadius = 0; Tab.style.borderBottomRightRadius = 0;
                host.Add(Tab);
                Popout.Attach(Tab, Views);
                // 0.6.0: the popout's notes every second, not only when the game sets a text
                if (!Ticking) { Ticking = true; host.schedule.Execute((System.Action)(() => { try { Popout.Update(); } catch { } })).Every(1000); }   // 0.5.6: Core's popout reads these (hidden) children
                TreatmentRoom.W("room", "tab added", "top " + TreatmentRoom.TabTop.Value);
            }
            catch (Exception e) { TreatmentRoom.Fail("room tab", e); }
        }

        // 0.3.3: other tabs share the right edge (Press Room's MEDIA at 120, and test tabs such as READ at 222 and SI TEST at 330
        // on 23 Nov; Heffem's around 560). Start at TabTop and move below any narrow element already on the right edge that
        // would overlap. Checked every couple of seconds, so a tab that appears later still pushes MEDICAL clear of it.
        static float LastTop = -1f;
        static void Place(bool log)
        {
            try
            {
                if (Tab == null || Host == null) return;
                var hb = Host.worldBound; var tb = Tab.worldBound;
                if (float.IsNaN(hb.width) || hb.width < 100) return;
                float h = !float.IsNaN(tb.height) && tb.height > 20 ? tb.height : 120f;
                var taken = new List<(float a, float b, string n)>();
                int n = Host.childCount;
                for (int i = 0; i < n; i++)
                {
                    VisualElement c = null; try { c = Host.ElementAt(i); } catch { }
                    if (c == null || c.Pointer == Tab.Pointer || (Page != null && c.Pointer == Page.Pointer)) continue;
                    var r = c.worldBound;
                    if (float.IsNaN(r.width) || r.width < 4 || r.width > 48 || r.height < 20 || r.height > 400) continue;
                    if (r.xMax < hb.xMax - 4) continue;
                    taken.Add((r.yMin - hb.yMin, r.yMax - hb.yMin, c.name ?? ""));
                }
                float y = TreatmentRoom.TabTop.Value;
                for (int round = 0; round < 12; round++)
                {
                    bool moved = false;
                    foreach (var t in taken) if (y < t.b + 8 && y + h + 8 > t.a) { y = t.b + 10; moved = true; }
                    if (!moved) break;
                }
                if (Math.Abs(y - LastTop) > 2)
                {
                    Tab.style.top = y;
                    TreatmentRoom.W("room", "tab placed", $"top {y:0} (asked for {TreatmentRoom.TabTop.Value}); other tabs on the right edge: " + string.Join(", ", taken.Select(t => $"{(t.n.Length > 0 ? t.n : "?")} {t.a:0}-{t.b:0}")));
                    LastTop = y;
                }
            }
            catch (Exception e) { TreatmentRoom.Fail("room tab place", e); }
        }

        // from the Season Summary panel's button, or the tab: on a player's profile, open that player's file
        internal static void Open(string view, VisualElement from = null)
        {
            try
            {
                if (Host == null && from != null) for (var e = from; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { Host = e; break; }
                if (Host == null) { TreatmentRoom.W("room", "not opened", "the screen's root is not known yet"); return; }
                if (view != null) View = view;
                if (view == null && Screens.OwnerId.Length > 0 && (DateTime.Now - Screens.OwnerAt).TotalSeconds < 25) { View = "files"; FileWho = Screens.OwnerId; }
                if (Page == null || Page.panel == null) { try { Build(); } catch (Exception e) { TreatmentRoom.Fail("room build", e); } }
                Refresh();
                Popout.Seen();
            }
            catch (Exception e) { TreatmentRoom.Fail("room open", e); }
        }

        static void Toggle()
        {
            if (Page != null && Page.panel != null) { Page.RemoveFromHierarchy(); Page = null; return; }
            Open(null);
        }

        static Label TitleL;
        static void Build()
        {
            float hw = 1600f, hh = 900f;
            try { var b = Host.worldBound; if (!float.IsNaN(b.width) && b.width > 400) { hw = b.width; hh = b.height; } } catch { }
            FindFont();
            Page = new VisualElement(); Page.name = "FM27MM-TreatmentRoom";
            Page.style.position = Position.Absolute; Page.style.right = 40; Page.style.top = 50;
            Page.style.width = Math.Min(1180f, hw - 120f);
            Page.style.maxHeight = hh - 90f;
            Page.style.backgroundColor = PageBg; Border(Page, 1, Edge); Radius(Page, 14); Page.style.overflow = Overflow.Hidden;
            // the header: the room's mark, the page's name, the date and the assessment
            var head = Flex(true, true); head.style.justifyContent = Justify.SpaceBetween; Pad(head, 16, 22); head.style.borderBottomWidth = 1; head.style.borderBottomColor = Edge;
            var left = Flex(true);
            var mark = L("+", 24, White, true); mark.style.width = 40; mark.style.height = 40; mark.style.backgroundColor = Red; Radius(mark, 10);
            mark.style.unityTextAlign = TextAnchor.MiddleCenter; mark.style.marginRight = 12; left.Add(mark);
            var titles = Flex(false);
            var room = L("TREATMENT ROOM", 12, RedLight, true, 1); room.style.letterSpacing = 2f; titles.Add(room);
            TitleL = Disp(L("Guidance from the team", 20, Text1, true)); titles.Add(TitleL);
            left.Add(titles); head.Add(left);
            var right = Flex(true);
            Sub = L("", 13, Text2); Sub.style.unityTextAlign = TextAnchor.MiddleRight; Sub.style.marginRight = 14; right.Add(Sub);
            right.Add(Pill("Close", Toggle, false));
            head.Add(right);
            Page.Add(head);
            TabsRow = Flex(true, true); Pad(TabsRow, 10, 22); TabsRow.style.borderBottomWidth = 1; TabsRow.style.borderBottomColor = Edge; Page.Add(TabsRow);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; scroll.style.maxHeight = Math.Max(240f, hh - 250f);   // as the Media Office does
            try { scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; } catch { }
            Body = new VisualElement(); Pad(Body, 18, 22); scroll.Add(Body);
            Page.Add(scroll);
            Host.Add(Page);
            TreatmentRoom.W("room", "opened", $"width {Math.Min(1180f, hw - 120f):0} of {hw:0}" + ((object)Display != null ? ", game display font" : ""));
        }
        static readonly Dictionary<string, string> Titles = new Dictionary<string, string> { { "guidance", "Guidance from the team" }, { "overview", "The medical briefing" }, { "dept", "The medical department" },
            { "watch", "Who to watch" }, { "files", "Player files" }, { "physio", "The physio's record" }, { "diary", "The medical diary" }, { "sports", "Sports science" } };

        static readonly (string Key, string Label)[] Views = { ("guidance", "Guidance"), ("sports", "Sports science"), ("overview", "Briefing"), ("files", "Player files"), ("dept", "Department"), ("physio", "Physio's record"), ("diary", "Diary") };   // 0.6.2: Who to watch folded into Guidance and Sports science

        internal static void Refresh()
        {
            if (Page == null || Page.panel == null || Body == null) return;
            try
            {
                try { if (Notes.Update(Today, Notes.Parse(Ledger.ActionRows()))) Ledger.Save(); } catch (Exception e) { TreatmentRoom.Fail("notes update", e); }
                var lead = Notes.StaffName("risk"); var fit = Notes.StaffName("fitness");
                int ld = Files.LatestRiskDay(), td = GameDate.Day(Today);
                string dow = ""; try { if (td >= 0) dow = new DateTime(2000, 1, 1).AddDays(td).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture) + " "; } catch { }
                Sub.text = (Today.Length > 0 ? dow + Today : "No game loaded") + (ld >= 0 && td >= 0 ? "\nLatest risk assessment: " + (td - ld == 0 ? "today" : td - ld == 1 ? "1 day old" : (td - ld) + " days old") : "")
                    + (lead.Length > 0 ? " · signed " + lead : "");
                if (TitleL != null) TitleL.text = Titles.TryGetValue(View, out var tt) ? tt : "Treatment Room";
                TabsRow.Clear();
                foreach (var v in Views) { var k = v.Key; TabsRow.Add(Pill(v.Label, () => { View = k; if (k != "files") FileWho = ""; Refresh(); }, View == k)); }
                Body.Clear();
                if (Flash.Length > 0) { var f = Panel(Hex("2A1518"), AlertEdge, 10); Pad(f, 12, 16); f.style.marginBottom = 14; f.Add(L(Flash, 14, Pink)); Body.Add(f); Flash = ""; }
                if (Today.Length == 0) { Body.Add(Text("Load a game and open the Medical Centre once: the Treatment Room reads its screens.", 13, Quiet, 0)); return; }
                Section(() =>
                {
                    switch (View)
                    {
                        case "dept": DeptView(); break;
                        case "watch": WatchView(); break;
                        case "files": if (FileWho.Length > 0) FileView(FileWho); else PlayersView(); break;
                        case "physio": PhysioView(); break;
                        case "diary": DiaryView(); break;
                        case "sports": ScienceView(); break;   // 0.6.2: the evidence page (SportsView is kept for reference)
                        case "overview": OverviewView(); break;
                        default: GuidanceView(); break;
                    }
                }, View);
            }
            catch (Exception e) { TreatmentRoom.Fail("room refresh", e); }
        }
        static void Section(System.Action build, string what)
        {
            try { build(); }
            catch (Exception e) { TreatmentRoom.Fail("room " + what, e); Body.Add(Text("This section could not be drawn (" + e.GetType().Name + "). The Treatment Room log has the details.", 12, Quiet, 0)); }
        }

        // ---------- views ----------
        static void OverviewView()
        {
            // 0.6.2: the head physio's briefing and the season in words. The sports science report, who to watch and the month table
            // now live on Guidance and Sports science; the department on its own page.
            Section(() =>
            {
                var mb = Brief.Medical(Today, StartMonth);
                var bx = Box(); bx.Add(Head(Brief.Title("MEDICAL BRIEFING", mb)));
                if (mb.Note.Length > 0) bx.Add(Text(mb.Note, 12, Quiet, 3));
                foreach (var l in mb.Lines) bx.Add(LineL(l));
                Body.Add(bx);
            }, "medical briefing");
            var b = Box(); b.Add(Head("THE SEASON SO FAR"));
            foreach (var l in Files.Overview(Today, StartMonth)) b.Add(LineL(l));
            Body.Add(b);
        }
        static readonly float[] Grow5 = { 2f, 1f, 1f, 1f, 1f };

        // 0.4.0: the medical department, as the staff screens describe it, and what it means for the Treatment Room
        // 0.6.3: DeptView is in DeptView.cs

        static void WatchView()
        {
            var b = Box(); b.Add(Head("WHO TO WATCH"));
            b.Add(Text("Players on the medical team's recurring list or injured three or more times at the club this season, and the players the latest risk assessment flags. " + Dept.ReachText(Dept.Now(Notes.StaffName("risk"))) + " Most at risk first.", 11.5f, Quiet, 6));
            var rows = Summary.Watch(Today, StartMonth, 40);
            if (rows.Count == 0) b.Add(Text("Nobody right now. Open Risk Assessment and Season Summary to bring this up to date.", 12.5f, Quiet, 0));
            else b.Add(WatchTable(rows, true));
            Body.Add(b);
            var mine = Notes.All.Where(n => n.Kind == "watch" && n.Status == "open").ToList();
            var y = Box(); y.Add(Head("YOUR WATCH LIST"));
            if (mine.Count == 0) y.Add(Text("Empty. Add a player from their file (Player files), where the screening note suggests what to watch.", 12, Quiet, 0));
            foreach (var n in mine.OrderBy(n => n.Name))
            {
                var r = Row(); r.style.marginBottom = 3;
                r.Add(Fixed(Text(n.Name, 12.5f, Ink, 0), 220));
                r.Add(Fixed(Text((n.What.Length > 0 ? n.What : "general") + (n.Detail.Length > 0 ? " (" + n.Detail + ")" : "") + ", since " + n.Since, 12, Quiet, 0), 520));
                var who = n.Who; r.Add(Btn("File", () => { View = "files"; FileWho = who; Refresh(); }, Grey));
                var p = Notes.OpenPlan(n.Who); if (p != null) r.Add(Text("  " + Notes.PlanLabel(p.What) + ": " + p.Status, 12, Gold, 0));
                y.Add(r);
            }
            Body.Add(y);
        }

        static VisualElement WatchTable(List<Summary.WatchRow> rows, bool buttons)
        {
            var t = new VisualElement();
            var grow = new[] { 2.2f, 0.7f, 0.7f, 1.8f, 2.4f, 1.3f, buttons ? 1.6f : 0.9f };
            t.Add(TRow(new[] { "Player", "Injuries", "Days out", "Recurring", "Risk today", "Flagged", "" }, grow, true, null));
            foreach (var r in rows)
            {
                var risk = r.Risk.Length == 0 ? "-" : r.Risk + (r.Factor.Length > 0 ? " (" + r.Factor.ToLowerInvariant() + ")" : "");
                var flag = r.FlagDays == 0 ? "-" : r.FlagDays + " of " + r.AssessDays + (r.AssessDays == 1 ? " day" : " days") + (r.FirstFlag.Length > 0 ? ", since " + r.FirstFlag : "");
                var row = TRow(new[] { r.Name, r.Injuries.ToString(), r.Days > 0 ? r.Days.ToString() : "-", r.Recurring.Length > 0 ? r.Recurring : "-", risk, flag }, grow, false,
                    r.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase) ? Gold : (Color?)null);
                var cell = new VisualElement(); cell.style.flexDirection = FlexDirection.Row; cell.style.flexGrow = grow[6]; cell.style.flexBasis = 0; cell.style.flexShrink = 1;
                var who = r.Who; var name = r.Name;
                cell.Add(Small("File", () => { View = "files"; FileWho = who; Refresh(); }, Grey));
                if (buttons)
                {
                    var open = Notes.OpenWatch(who);
                    if (open == null) cell.Add(Small("Watch", () => { var s = Files.Screen(who, Today, StartMonth); Notes.Watch(who, name, s.Area, s.Reason, Today); Ledger.Save(); Flash = name + " added to your watch list" + (s.Area.Length > 0 ? " (" + s.Area + ")." : "."); Refresh(); }, Grey));
                    else cell.Add(Text(" watching", 11.5f, Gold, 0));
                }
                row.Add(cell);
                t.Add(row);
            }
            return t;
        }

        static void PlayersView()
        {
            var b = Box(); b.Add(Head("PLAYER FILES"));
            b.Add(Text("Every player on Player Summary, and anyone injured at the club this season. Open a file for the screening note.", 11.5f, Quiet, 6));
            var grow = new[] { 2.4f, 0.6f, 0.9f, 0.9f, 0.9f, 1.6f, 1.8f, 1.4f, 0.8f };
            b.Add(TRow(new[] { "Player", "Tag", "This season", "At the club", "Days out", "Risk today", "Recurring", "Watch / plan", "" }, grow, true, null));
            foreach (var p in Files.Players(Today, StartMonth))
            {
                var row = TRow(new[] { p.Name + (p.InSquad ? "" : " (not on Player Summary)"), p.Tag.Length > 0 ? p.Tag : "-", p.Season >= 0 ? p.Season.ToString() : "-", p.AtClub.ToString(), p.Days > 0 ? p.Days.ToString() : "-",
                    p.Risk.Length > 0 ? p.Risk : "-", p.Recurring.Length > 0 ? p.Recurring : "-", p.Plan.Length > 0 ? p.Plan : p.Watch.Length > 0 ? "watch: " + p.Watch : "-" }, grow, false,
                    p.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase) || p.Recurring.Length > 0 ? Gold : p.InSquad ? (Color?)null : Quiet);
                var who = p.Who; var cell = new VisualElement(); cell.style.flexGrow = grow[8]; cell.style.flexBasis = 0; cell.style.flexDirection = FlexDirection.Row;
                cell.Add(Small("File", () => { FileWho = who; Refresh(); }, Grey));
                row.Add(cell); b.Add(row);
            }
            Body.Add(b);
        }

        static void FileView(string who)
        {
            var name = Files.Name(who);
            var top = Row(); top.style.marginBottom = 6;
            top.Add(Btn("< All players", () => { FileWho = ""; Refresh(); }, Grey));
            Body.Add(top);
            var s = Files.Screen(who, Today, StartMonth);
            var b = Box();
            var by = Notes.StaffName("risk");
            b.Add(Head(name.ToUpperInvariant() + ": SCREENING NOTE"));
            b.Add(Text("From the injuries on file, the latest risk assessment and your own notes" + (by.Length > 0 ? " (risk assessments by " + by + ")" : "") + ". Patterns are watch items, not diagnoses.", 11, Quiet, 6));
            foreach (var l in s.Lines) b.Add(LineL(l));
            Body.Add(b);

            // 0.5.3: the physio's view, and the advice as one of the Treatment Room's own plans
            Section(() =>
            {
                var pv = Brief.Player(who, Today, StartMonth);
                var pb = Box(); pb.Add(Head(Brief.Title(name.ToUpperInvariant() + ": THE PHYSIO'S VIEW", pv.R)));
                foreach (var l in pv.R.Lines) pb.Add(LineL(l));
                if (pv.Plan.Length > 0)
                {
                    var pr = Row(); pr.style.marginTop = 4;
                    string pk = pv.Plan, rs = pv.Reason, adviser = pv.R.By.Length > 0 ? pv.R.By : "the medical team";
                    pr.Add(Btn("Follow the advice: " + Notes.PlanLabel(pk), () => { var p = Notes.Plan(who, name, pk, rs + " (advice from " + adviser + ")", Today); Ledger.Save(); Flash = Notes.PlanLabel(pk) + " for " + name + ": " + Notes.Guide(p); Refresh(); }, Sel));
                    pb.Add(pr);
                }
                Body.Add(pb);
            }, "physio's view");

            // watch item and plans (v1: guide and record)
            var a = Box(); a.Add(Head("WATCH AND MANAGE"));
            var w = Notes.OpenWatch(who);
            var r1 = Row(); r1.style.marginBottom = 6;
            if (w == null) r1.Add(Btn(s.Area.Length > 0 ? "Watch: " + s.Area : "Add to watch list", () => { Notes.Watch(who, name, s.Area, s.Reason, Today); Ledger.Save(); Flash = name + " is on your watch list."; Refresh(); }, Sel));
            else r1.Add(Btn("Close watch item (" + (w.What.Length > 0 ? w.What : "general") + ")", () => { Notes.Unwatch(who, Today); Ledger.Save(); Flash = "Watch item closed."; Refresh(); }, Grey));
            a.Add(r1);
            var r2 = Row(); r2.style.marginBottom = 4;
            var reason = w != null ? (w.What.Length > 0 ? w.What + " watch" : "watch list") : s.Reason;
            foreach (var kind in new[] { "rest", "lighter", "specialist", "normal" })
            {
                var k = kind;
                r2.Add(Btn(Notes.PlanLabel(k), () => { var p = Notes.Plan(who, name, k, reason, Today); Ledger.Save(); Flash = Notes.PlanLabel(k) + " for " + name + ": " + Notes.Guide(p); Refresh(); }, Grey));
            }
            var open = Notes.OpenPlan(who);
            if (open != null && open.Status == "requested") r2.Add(Btn("Cancel plan", () => { Notes.CancelPlan(who, Today); Ledger.Save(); Flash = "Plan cancelled."; Refresh(); }, Dim));
            a.Add(r2);
            if (open != null)
            {
                a.Add(Text(Files.PlanLine(open), 12.5f, Gold, 2));
                if (open.Status == "requested") a.Add(Text("To carry it out: " + Notes.Guide(open) + " The Treatment Room marks it done when it sees the action.", 12, Ink, 2));
            }
            else a.Add(Text("Rest and specialist assessments are real rests from training, set with the Rest Player option; lighter training is the player's own training intensity. The Treatment Room records the plan, marks it carried out when it sees the action, and reviews it when the days are up.", 11.5f, Quiet, 0));
            Body.Add(a);

            // the injuries on record
            var list = Summary.Injuries().Where(i => i.Who == who).OrderByDescending(i => GameDate.Day(i.Date)).ToList();
            var t = Box(); t.Add(Head("INJURIES ON FILE (" + list.Count + ")"));
            if (list.Count == 0) t.Add(Text("None.", 12, Quiet, 0));
            else
            {
                var grow = new[] { 1.1f, 2.6f, 1f, 1.1f, 1.6f, 1f, 1f };
                t.Add(TRow(new[] { "Date", "Injury", "Severity", "Received", "Cause", "Treatment", "Out for" }, grow, true, null));
                foreach (var i in list) t.Add(TRow(new[] { i.Date, i.Injury, i.Severity, i.Received.Length > 0 ? i.Received : "-", i.Cause.Length > 0 ? i.Cause : "-", i.Treatment.Length > 0 ? i.Treatment : "-", i.OutFor.Length > 0 ? i.OutFor : "-" },
                    grow, false, i.Severity.Equals("Major", StringComparison.OrdinalIgnoreCase) ? Gold : (Color?)null));
            }
            Body.Add(t);
        }

        static void PhysioView()
        {
            var b = Box(); b.Add(Head("THE PHYSIO'S RECORD"));
            foreach (var l in Files.PhysioSummary(Today)) b.Add(LineL(l));
            Body.Add(b);
            var all = Ledger.Forecasts.Values.Where(f => f.Ours != "n").OrderByDescending(f => GameDate.Day(f.Date)).ToList();
            if (all.Count > 0)
            {
                var t = Box(); t.Add(Head("FORECASTS"));
                var grow = new[] { 2f, 2.4f, 1f, 1.6f, 1f, 1.4f };
                t.Add(TRow(new[] { "Player", "Injury", "Injured", "Forecast", "Back by", "Verdict" }, grow, true, null));
                foreach (var f in all)
                {
                    string verdict = "still out", back = "-";
                    if (f.Returned.Length > 0) { var v = Files.Verdict(f); verdict = v.Kind + (v.Kind == "late" ? " (" + v.Over + " days)" : "") + ", " + v.Actual + " days"; back = f.Returned; }
                    t.Add(TRow(new[] { f.Person, f.Injury, f.Date, f.Expected, back, verdict }, grow, false, verdict.StartsWith("late") ? Gold : (Color?)null));
                }
                Body.Add(t);
            }
        }

        static void DiaryView()
        {
            var b = Box(); b.Add(Head("MEDICAL DIARY, THIS SEASON"));
            var d = Files.Diary(Today, StartMonth, 120);
            if (d.Count == 0) b.Add(Text("Nothing yet.", 12, Quiet, 0));
            foreach (var e in d)
            {
                var r = Row(); r.style.flexWrap = Wrap.NoWrap; r.style.marginBottom = 2; r.style.alignItems = Align.FlexStart;
                r.Add(Fixed(Text(e.Date, 12, Quiet, 0), 90));
                var l = Text(e.Text, 12.5f, e.Flag ? Gold : e.Quiet ? Quiet : Ink, 0); l.style.flexGrow = 1; l.style.flexShrink = 1; r.Add(l);
                b.Add(r);
            }
            Body.Add(b);
        }

        static void SportsView()
        {
            var b = Box(); b.Add(Head("SPORTS SCIENCE"));
            b.Add(Text("What the sports science and medical staff put on the screens: the latest risk assessment in numbers, how their flags have turned out, and their rest advice. These are observations, not proof of cause.", 11, Quiet, 6));
            // 0.4.0: whose assessments these are, and how far to trust them
            var signer = Notes.StaffName("risk");
            if (signer.Length > 0)
            {
                var lv = Dept.Now(signer);
                b.Add(Text("Risk assessments signed by " + signer + (lv.SportsFromAssessor ? " (Sports Science " + Dept.Scale[lv.Sports] + "). " + Dept.ReachText(lv) : ". The skills have not been read yet: open the profile once."), 12.5f, Ink, 4));
            }
            foreach (var l in Files.Sports(Today, StartMonth)) b.Add(LineL(l));
            Body.Add(b);
            int last = Files.LatestRiskDay();
            var risk = Ledger.Risk.Values.Where(k => GameDate.Day(k.Date) == last).OrderBy(k => Rank(k.Risk)).ThenBy(k => k.Person).ToList();
            if (risk.Count > 0)
            {
                var t = Box(); t.Add(Head("LATEST ASSESSMENT, PLAYER BY PLAYER (" + Files.DateOf(last) + ")"));
                var grow = new[] { 2.2f, 1f, 2.4f, 0.9f, 1.4f, 1.6f, 1.2f };
                t.Add(TRow(new[] { "Player", "Risk", "Main factor", "Match load", "Minutes (14 days)", "Training", "Susceptibility" }, grow, true, null));
                foreach (var k in risk) t.Add(TRow(new[] { k.Person, k.Risk, k.Factor.Length > 0 ? k.Factor : "-", k.MatchLoad, Minutes(k.MatchLine), k.TrainLoad + (k.TrainLine.Contains("individual") ? " + individual" : ""), k.Suscept },
                    grow, false, k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase) ? Gold : (Color?)null));
                Body.Add(t);
            }
        }
        static int Rank(string r) { switch ((r ?? "").ToLowerInvariant()) { case "very high": return 0; case "high": return 1; case "increased": return 2; case "normal": return 3; case "low": return 4; default: return 5; } }
        static string Minutes(string line)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line ?? "", @"\((\d+/\d+) minutes\)");
            return m.Success ? m.Groups[1].Value : (line ?? "");
        }

        // ---------- pieces ----------
        static Label Head(string t) { var l = L(t, 12, Gold, true, 8); l.style.letterSpacing = 1.3f; return l; }
        static Label LineL(Summary.Line l) => Text(l.Text, l.Quiet ? 12f : 13f, l.Quiet ? Quiet : l.Flag ? Gold : Ink, 3);
        static Label Text(string t, float size, Color c, float gap)
        {
            // 0.6.0: the older pages read a size up, with a little more room between lines
            var l = new Label(t); l.style.fontSize = size + 1f; l.style.color = c; l.style.whiteSpace = WhiteSpace.Normal;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.marginTop = 0; l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginBottom = gap + 1f;
            return l;
        }
        static Button Btn(string t, System.Action act, Color bg)
        {
            var b = new Button(); b.text = t;
            b.style.backgroundColor = bg; b.style.color = Ink; b.style.fontSize = 13; b.style.unityFontStyleAndWeight = FontStyle.Bold;
            Border(b, 1, bg == Sel ? Sel : BtnEdge); Radius(b, 8);
            b.style.paddingLeft = 12; b.style.paddingRight = 12; b.style.paddingTop = 6; b.style.paddingBottom = 6; b.style.marginRight = 6; b.style.marginBottom = 4; b.style.marginLeft = 0;
            System.Action safe = () => { try { act(); } catch (Exception e) { TreatmentRoom.Fail("room click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        static Button Small(string t, System.Action act, Color bg)
        {
            var b = Btn(t, act, bg); b.style.fontSize = 11; b.style.paddingTop = 1; b.style.paddingBottom = 1; b.style.paddingLeft = 6; b.style.paddingRight = 6; return b;
        }
        static VisualElement Box()
        {
            var v = new VisualElement(); v.style.backgroundColor = Card; Border(v, 1, Edge); Radius(v, 12);
            v.style.paddingLeft = 18; v.style.paddingRight = 18; v.style.paddingTop = 14; v.style.paddingBottom = 14; v.style.marginBottom = 14;
            return v;
        }
        static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; r.style.alignItems = Align.Center; return r; }
        static VisualElement Fixed(VisualElement e, float w) { e.style.width = w; e.style.flexShrink = 0; return e; }
        static VisualElement TRow(string[] cells, float[] grow, bool head, Color? colour)
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.FlexStart;
            row.style.paddingTop = 7; row.style.paddingBottom = 7; row.style.paddingLeft = 8; row.style.borderBottomWidth = 1; row.style.borderBottomColor = Rule;
            if (head) { row.style.backgroundColor = Dim; Radius(row, 6); }
            for (int i = 0; i < cells.Length; i++)
            {
                var l = Text(cells[i], head ? 11f : 12.5f, head ? Quiet : colour ?? Ink, 0);
                l.style.flexGrow = i < grow.Length ? grow[i] : 1f; l.style.flexShrink = 1; l.style.flexBasis = 0; l.style.paddingRight = 8;
                row.Add(l);
            }
            return row;
        }
    }
}
