// FM27 Media Mod - Core 0.2.1. MIT licence (see LICENSE).
// 0.2.1 (Nik, 23 Nov 10:50-10:53): one look for every part, with its own accent: Press Office Henderson's orange on
// black, Treatment Room red and white (not glaring), Pairs green, Data blue. The buttons sit on a black tab with more of
// the letter showing (Heffem's tab shows about 15 units; ours 24), bigger letters; text left-aligned.
// One popout per part (Press Office, Treatment Room, Pairs, and the test tools), each behaving like Heffem's hub as
// seen on Nik's screen on 23 Nov (measured by Reader 0.2.2's UI map; Heffem's code is not read):
//  - closed, a round button peeks in at the right edge; on hover it slides out with its panel (dark navy, a letter
//    tile, the heading bold 22, a status line, the part's pages as bold chips); it slides back when the mouse leaves;
//  - now and then, when there is something worth a look, a short note pops out beside the button (a "nudge");
//  - the buttons stack in one column with Heffem's: ours above and below it, never on top of it;
//  - only in a game (after a save loads or a new game starts), as Heffem.
// Works with every plugin as it is: Core finds each plugin's own right-edge tab (FM27MM-...Tab under
// MainUIDocument-container), hides it, and presses it (Clickable.SimulateSingleClick) to open the plugin's page.
// Conventions a plugin can adopt (children of its tab, kept display:none by the plugin):
//   Label "FM27MM-Status": the status line ("first|second" gives two lines);
//   Label "FM27MM-Badge": a count for the pill (empty or 0 = none);
//   Label "FM27MM-Nudge": "id|text" - a new id pops the note out once; an id ending "@<page>" (0.2.2) opens that FM27MM-Sub page from the note;
//   Buttons "FM27MM-Sub:<label>": the part's pages, shown as chips and pressed.
// If Core fails, the old tabs come back by themselves. Hooks: the twin text hooks and the two SendEvent hooks
// (all four proven), each patched on its own.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using EV = FM.GamePlugin.InteropEventIDs;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Core", Version)]
    public class Core : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.core";
        public const string Version = "0.2.4";
        internal static StreamWriter Out; internal static string Dir;
        internal static ConfigEntry<bool> Enabled, HideOldTabs, Nudges, OnlyInGame;
        internal static ConfigEntry<int> FallbackTop, NudgeSeconds, NudgeGapSeconds;

        public override void Load()
        {
            Enabled = Config.Bind("Popouts", "Enabled", true, "Give each part (Press Office, Treatment Room, Pairs...) a Heffem-style popout on the right edge, and hide the old separate tabs.");
            HideOldTabs = Config.Bind("Popouts", "HideOldTabs", true, "Hide our plugins' old right-edge tabs (MEDIA, MEDICAL, PAIRS, READ...) while the popouts are up.");
            OnlyInGame = Config.Bind("Popouts", "OnlyInGame", true, "Show the popouts only in a game (after a save loads or a new game starts), as Heffem does.");
            FallbackTop = Config.Bind("Popouts", "FallbackTop", 400, "Where the column of buttons centres when Heffem's hub is not on screen (pixels from the top).");
            Nudges = Config.Bind("Nudges", "Enabled", true, "Pop a short note out beside a part's button when it has something worth a look.");
            NudgeSeconds = Config.Bind("Nudges", "Seconds", 9, "How long a note stays out.");
            NudgeGapSeconds = Config.Bind("Nudges", "GapSeconds", 90, "At least this long between two notes.");
            Dir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod"); Directory.CreateDirectory(Dir);
            Out = new StreamWriter(Path.Combine(Dir, "core_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            W("start", "core " + Version, "");
            var h = new Harmony(GUID); int ok = 0, bad = 0;
            foreach (var t in new[] { typeof(SetTextPatch), typeof(ApplyTextPatch), typeof(ReceivePatch), typeof(SendPatch) })
            {
                try { h.CreateClassProcessor(t).Patch(); ok++; }
                catch (Exception e) { bad++; W("patch-error", t.Name, e.GetType().Name + ": " + e.Message); }
            }
            W("patches", ok + " ok", bad + " failed");
            Log.LogInfo("FM27 Media Mod Core " + Version + " loaded (popouts " + (Enabled.Value ? "on" : "off") + ", nudges " + (Nudges.Value ? "on" : "off") + "; patches " + ok + " ok, " + bad + " failed).");
        }

        internal static void W(string kind, string a, string b)
        {
            try { var o = Out; if (o == null) return; lock (o) o.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{kind}\t{C(a)}\t{C(b)}"); } catch { }
        }
        static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }

    // a part's colours
    sealed class Theme
    {
        public Color Panel, Edge, Text, Muted, CircleBg, CircleFg, TileBg, TileFg, ChipBg, ChipText, Pill, PillText;
        static Color H(string h, float a = 1f) { ColorUtility.TryParseHtmlString(h, out var c); c.a = a; return c; }
        public static Theme Of(string panel, string edge, string text, string muted, string circleBg, string circleFg, string tileBg, string tileFg, string chipBg, string chipText, string pill, string pillText)
            => new Theme { Panel = H(panel, 0.97f), Edge = H(edge), Text = H(text), Muted = H(muted), CircleBg = H(circleBg), CircleFg = H(circleFg), TileBg = H(tileBg), TileFg = H(tileFg), ChipBg = H(chipBg), ChipText = H(chipText), Pill = H(pill), PillText = H(pillText) };
        // one look for every part (Nik, 23 Nov 10:53): the same near-black panel, off-white text and pill; only the accent
        // changes (the letter, its circle, the chips, the note's edge)
        const string Base = "#151517", EdgeC = "#34343A", TextC = "#F4F2EF", MutedC = "#BDB8B1", TileC = "#232327", PillC = "#F4F2EF";
        static Theme Accent(string accent, string onAccent) => Of(Base, EdgeC, TextC, MutedC, accent, onAccent, TileC, accent, accent, onAccent, PillC, Base);
        internal static readonly Theme Press = Accent("#FF992E", Base);          // Henderson's orange on black (the Media Office page's orange)
        internal static readonly Theme Medical = Accent("#CF2E3B", "#FFFFFF");   // red and white, not glaring: a white cross on red (the Treatment Room page's red)
        internal static readonly Theme Pairs = Accent("#8CD14D", Base);          // green (the Partnerships page's title)
        internal static readonly Theme Data = Accent("#4FA3E0", Base);           // SI blue
        internal static readonly Theme Lab = Accent("#A77BE0", Base);
        internal static readonly Theme Other = Data;
    }

    sealed class Known
    {
        public string Tab, Letter, Name; public int Order; public Theme T;
        Known(string tab, string letter, string name, int order, Theme t) { Tab = tab; Letter = letter; Name = name; Order = order; T = t; }
        // Order: below 50 sits above Heffem's button (nearest first from the bottom), 50 and over below it
        internal static readonly Known[] All =
        {
            new Known("FM27MM-MediaOfficeTab", "P", "Press Office", 10, Theme.Press),
            new Known("FM27MM-TreatmentRoomTab", "+", "Treatment Room", 20, Theme.Medical),
            new Known("FM27MM-PairsTab", "&", "Partnerships", 60, Theme.Pairs),   // 0.2.3: "Partnerships" (Nik, 24 Nov: "Pairs: call it partnerships")
            new Known("FM27MM-ReaderTab", "D", "Data", 70, Theme.Data),
            new Known("FM27MM-SiActionsTab", "L", "Lab", 90, Theme.Lab),
        };
        internal static Known For(string name, string tooltip)
        {
            foreach (var k in All) if (k.Tab == name) return k;
            var t = string.IsNullOrEmpty(tooltip) ? name.Replace("FM27MM-", "").Replace("Tab", "") : tooltip;
            return new Known(name, t.Substring(0, 1).ToUpperInvariant(), t, 80, Theme.Other);
        }
    }

    // one part's popout
    sealed class Dock
    {
        public Known K; public Button Tab; public IntPtr TabPtr;
        public VisualElement List, Dot, Circle; public Button Handle;
        public bool Open, Shown = true; public DateTime LastHover = DateTime.MinValue;
        public float HX, THX, LX, TLX, Fade, TFade;    // the button's and the list's slide offsets (px to the right), the list's opacity
        public string LastNudge = "", Signature = "";
        public float Top;
    }

    static class Hub
    {
        internal static volatile bool InGame;
        static VisualElement Host, Clock, Toast;
        static readonly List<Dock> Docks = new List<Dock>();
        static DateTime Next = DateTime.MinValue, ToastUntil = DateTime.MinValue, ToastNotBefore = DateTime.MinValue;
        static Dock ToastFor;
        static bool Broken; static int FailCount;
        // Heffem's hub as measured (same units as FM's UI)
        const float BtnW = 46.8f, BtnH = 63.1f, ListW = 339.6f, Pad = 9.8f, Icon = 33.7f, Gap = 9.8f, ChipIndent = 52.3f, M = 4.4f, Peek = 24f, Circle = 34f, SlotGap = 6f;
        static readonly Color Clear = new Color(0, 0, 0, 0);

        // ---- the slow tick (from the text hooks, once a second): find our tabs, build and place the docks ----
        internal static void Tick(VisualElement any)
        {
            if (Broken || !Core.Enabled.Value || DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(1);
            try
            {
                if (Host == null || Host.panel == null)
                {
                    Host = null; Clock = null; Toast = null; Docks.Clear();
                    for (var e = any; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { Host = e; break; }
                    if (Host == null) return;
                    Core.W("host", "found", "");
                }
                if (Clock == null || Clock.panel == null)
                {
                    Clock = new VisualElement(); Clock.name = "FM27MM-CoreClock"; Clock.style.position = Position.Absolute; Clock.style.width = 0; Clock.style.height = 0; Clock.pickingMode = PickingMode.Ignore;
                    Host.Add(Clock);
                    System.Action f = Frame; Clock.schedule.Execute(f).Every(30);
                }
                Scan();
                bool show = InGame || !Core.OnlyInGame.Value;
                foreach (var d in Docks)
                {
                    if (!Alive(d)) { try { d.Handle?.RemoveFromHierarchy(); d.List?.RemoveFromHierarchy(); } catch { } d.Shown = true; Build(d); }
                    Show(d, show);
                    var sig = Signature(d);
                    if (sig != d.Signature) { d.Signature = sig; Fill(d); }
                    if (show) CheckNudge(d);
                }
                Place();
            }
            catch (Exception e) { Fail("tick", e); }
        }

        static void Fail(string where, Exception e)
        {
            Core.W("error", where, e.ToString()); FailCount++;
            if (FailCount > 20) { Broken = true; Core.W("error", "too many errors: the popouts stop and the old tabs come back", ""); Restore(); }
        }

        static void Scan()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < Host.childCount; i++)
            {
                VisualElement c = null; try { c = Host[i]; } catch { }
                if (c == null) continue;
                var name = c.name ?? "";
                if (!name.StartsWith("FM27MM-", StringComparison.Ordinal) || !name.EndsWith("Tab", StringComparison.Ordinal) || name.StartsWith("FM27MM-Core", StringComparison.Ordinal)) continue;
                var b = c.TryCast<Button>(); if (b == null) continue;
                seen.Add(name);
                var d = Docks.Find(x => x.K.Tab == name);
                if (d == null) { d = new Dock { K = Known.For(name, b.tooltip) }; Docks.Add(d); Core.W("dock", "added " + d.K.Name, name); }
                if (d.TabPtr != b.Pointer) { d.Tab = b; d.TabPtr = b.Pointer; d.Signature = ""; }
                if (Core.HideOldTabs.Value && b.style.display != DisplayStyle.None) b.style.display = DisplayStyle.None;
            }
            foreach (var d in Docks.FindAll(x => !seen.Contains(x.K.Tab))) { try { d.Handle?.RemoveFromHierarchy(); d.List?.RemoveFromHierarchy(); } catch { } Core.W("dock", "removed " + d.K.Name, ""); }
            Docks.RemoveAll(x => !seen.Contains(x.K.Tab));
            Docks.Sort((a, b) => a.K.Order != b.K.Order ? a.K.Order.CompareTo(b.K.Order) : string.CompareOrdinal(a.K.Tab, b.K.Tab));
        }

        static void Restore()
        {
            try { foreach (var d in Docks) { if (d.Tab != null) d.Tab.style.display = DisplayStyle.Flex; if (d.Handle != null) d.Handle.style.display = DisplayStyle.None; if (d.List != null) d.List.style.display = DisplayStyle.None; } if (Toast != null) Toast.style.display = DisplayStyle.None; } catch { }
        }

        // ---- building ----
        static Label L(string t, float size, Color c, bool bold)
        {
            var l = new Label(t); l.style.fontSize = size; l.style.color = c;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }
        static Button B(string t, System.Action act, Color bg, Color fg)
        {
            var b = new Button(); b.text = t;
            b.style.backgroundColor = bg; b.style.color = fg;
            b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0; b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            System.Action safe = () => { try { act(); } catch (Exception e) { Core.W("error", "click", e.ToString()); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        static void Round(VisualElement e, float r) { e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r; }
        static void Border(VisualElement e, float w, Color c) { e.style.borderLeftWidth = w; e.style.borderRightWidth = w; e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftColor = c; e.style.borderRightColor = c; e.style.borderTopColor = c; e.style.borderBottomColor = c; }
        static VisualElement Tile(string letter, Color fg, Color bg, float size, float font, bool round)
        {
            var t = new VisualElement(); t.style.width = size; t.style.height = size; t.style.flexShrink = 0;
            t.style.backgroundColor = bg; Round(t, round ? size / 2f : 8); t.style.justifyContent = Justify.Center; t.style.alignItems = Align.Center; t.pickingMode = PickingMode.Ignore;
            var l = L(letter, font, fg, true); l.style.unityTextAlign = TextAnchor.MiddleCenter; t.Add(l);
            return t;
        }

        static void Build(Dock d)
        {
            var T = d.K.T;
            // the button: a tab of the part's colour (rounded on the left, as Heffem's), the round letter near its left edge so
            // that over half of it shows at rest; open, it sits just left of the list
            d.Handle = B("", () => OpenPage(d, d.Tab, d.K.Name), T.Panel, T.Text); d.Handle.name = "FM27MM-CoreButton-" + d.K.Name.Replace(" ", "");
            d.Handle.tooltip = d.K.Name;
            d.Handle.style.position = Position.Absolute; d.Handle.style.right = 0;
            d.Handle.style.width = BtnW; d.Handle.style.height = BtnH;
            d.Handle.style.paddingLeft = 5; d.Handle.style.paddingRight = 0; d.Handle.style.paddingTop = 0; d.Handle.style.paddingBottom = 0;
            d.Handle.style.flexDirection = FlexDirection.Row; d.Handle.style.alignItems = Align.Center; d.Handle.style.justifyContent = Justify.FlexStart;
            d.Handle.style.borderTopLeftRadius = 12; d.Handle.style.borderBottomLeftRadius = 12; d.Handle.style.borderTopRightRadius = 0; d.Handle.style.borderBottomRightRadius = 0;
            Border(d.Handle, 1, T.Edge); d.Handle.style.borderRightWidth = 0;
            d.Circle = Tile(d.K.Letter, T.CircleFg, T.CircleBg, Circle, 18, true);
            d.Handle.Add(d.Circle);
            d.Dot = new VisualElement(); d.Dot.style.position = Position.Absolute; d.Dot.style.left = 3; d.Dot.style.top = 7; d.Dot.style.width = 10; d.Dot.style.height = 10;
            d.Dot.style.backgroundColor = T.Pill; Round(d.Dot, 5); Border(d.Dot, 1.5f, T.Panel); d.Dot.pickingMode = PickingMode.Ignore; d.Dot.style.display = DisplayStyle.None;
            d.Handle.Add(d.Dot);
            // the list: against the right edge, centred on the button, hidden off screen when closed
            d.List = new VisualElement(); d.List.name = "FM27MM-CoreList-" + d.K.Name.Replace(" ", "");
            d.List.style.position = Position.Absolute; d.List.style.right = 0;
            d.List.style.width = ListW; d.List.style.flexDirection = FlexDirection.Column;
            d.List.style.backgroundColor = T.Panel; d.List.style.paddingLeft = Pad; d.List.style.paddingRight = Pad; d.List.style.paddingTop = Pad; d.List.style.paddingBottom = Pad;
            d.List.style.borderTopLeftRadius = 12; d.List.style.borderBottomLeftRadius = 12; Border(d.List, 1, T.Edge); d.List.style.borderRightWidth = 0;
            d.List.style.display = DisplayStyle.None;
            d.Open = false; d.HX = d.THX = BtnW - Peek; d.LX = d.TLX = ListW + 4; d.Fade = d.TFade = 0;
            Slide(d.Handle, d.HX); Slide(d.List, d.LX);
            Host.Add(d.List); Host.Add(d.Handle);
            d.Signature = "";
            Core.W("dock", "built " + d.K.Name, "");
        }
        static void Slide(VisualElement e, float x) { e.style.translate = new Translate(new Length(x, LengthUnit.Pixel), new Length(0, LengthUnit.Pixel)); }
        static bool Alive(Dock d) => d.Handle != null && d.Handle.panel != null && d.List != null && d.List.panel != null;
        static void Show(Dock d, bool show)
        {
            if (d.Shown == show) return; d.Shown = show;
            d.Handle.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { d.Open = false; d.List.style.display = DisplayStyle.None; d.HX = d.THX = BtnW - Peek; d.LX = d.TLX = ListW + 4; d.Fade = d.TFade = 0; Slide(d.Handle, d.HX); Slide(d.List, d.LX); }
        }

        // what the panel shows; rebuilt only when it changes
        static string Signature(Dock d)
        {
            var sb = new StringBuilder(StatusOf(d)).Append('\u0001').Append(BadgeOf(d));
            foreach (var s in SubsOf(d)) sb.Append('\u0001').Append(s.name);
            return sb.ToString();
        }

        static void Fill(Dock d)
        {
            var T = d.K.T;
            d.List.Clear();
            var head = new VisualElement(); head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center;
            head.style.paddingBottom = 7.6f; head.style.marginBottom = 7.6f; head.style.marginLeft = M;
            head.style.borderBottomWidth = 1.1f; head.style.borderBottomColor = T.Edge; head.pickingMode = PickingMode.Ignore;
            var hi = Tile(d.K.Letter, T.TileFg, T.TileBg, Icon, 18, false); hi.style.marginRight = Gap; head.Add(hi);
            var title = L(d.K.Name.ToUpperInvariant(), 22, T.Text, true); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; head.Add(title);
            var badge = BadgeOf(d);
            if (badge.Length > 0)
            {
                var pill = new VisualElement(); pill.style.backgroundColor = T.Pill; Round(pill, 9); pill.style.paddingLeft = 7; pill.style.paddingRight = 7; pill.style.paddingTop = 1; pill.style.paddingBottom = 1;
                pill.style.flexShrink = 0; pill.pickingMode = PickingMode.Ignore; pill.Add(L(badge, 12, T.PillText, true)); head.Add(pill);
            }
            d.List.Add(head);
            // the status, clickable: opens the part's page (left-aligned, as Heffem's)
            var status = StatusOf(d).Split('|');
            var row = B("", () => OpenPage(d, d.Tab, d.K.Name), Clear, T.Text); row.name = "FM27MM-CoreStatus";
            row.style.flexDirection = FlexDirection.Column; row.style.alignItems = Align.FlexStart; row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.paddingLeft = ChipIndent; row.style.paddingRight = 7.6f; row.style.paddingTop = 2; row.style.paddingBottom = 4; Round(row, 8);
            var first = status.Length > 0 && status[0].Trim().Length > 0 ? status[0].Trim() : "Open";
            var l1 = L(first, 13, T.Text, true); l1.style.whiteSpace = WhiteSpace.Normal; l1.style.unityTextAlign = TextAnchor.MiddleLeft; row.Add(l1);
            if (status.Length > 1 && status[1].Trim().Length > 0) { var l2 = L(status[1].Trim(), 12, T.Muted, false); l2.style.whiteSpace = WhiteSpace.Normal; l2.style.unityTextAlign = TextAnchor.MiddleLeft; row.Add(l2); }
            d.List.Add(row);
            // the part's pages as chips (or one chip that opens it)
            var chips = new VisualElement(); chips.style.flexDirection = FlexDirection.Row; chips.style.flexWrap = Wrap.Wrap; chips.style.marginTop = 4.4f; chips.style.marginLeft = ChipIndent; chips.pickingMode = PickingMode.Ignore;
            var subs = SubsOf(d);
            if (subs.Count == 0) chips.Add(Chip(T, "Open", () => OpenPage(d, d.Tab, d.K.Name)));
            foreach (var s in subs)
            {
                var target = s; var label = (target.name ?? "").Substring("FM27MM-Sub:".Length);
                chips.Add(Chip(T, label, () => OpenPage(d, target, d.K.Name + " > " + label)));
            }
            d.List.Add(chips);
            RefreshDot(d);
        }

        static Button Chip(Theme T, string label, System.Action act)
        {
            var chip = B(label, act, T.ChipBg, T.ChipText);
            chip.style.fontSize = 12; chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.paddingLeft = 8.7f; chip.style.paddingRight = 8.7f; chip.style.paddingTop = 3.3f; chip.style.paddingBottom = 3.3f;
            chip.style.marginRight = M; chip.style.marginBottom = M; Round(chip, 7);
            return chip;
        }

        // ---- opening a page: press the plugin's own (hidden) button ----
        static void OpenPage(Dock d, Button target, string what)
        {
            if (target == null) { Core.W("press", what, "no button"); return; }
            bool done = false;
            try { target.clickable.SimulateSingleClick(null, 0); done = true; Core.W("press", what, d.K.Tab); }
            catch (Exception e) { Core.W("error", "press " + what, e.GetType().Name + ": " + e.Message); }
            if (!done)
            {
                EventBase ev = null;
                try { ev = ClickEvent.GetPooled(); target.clickable.SimulateSingleClick(ev, 0); Core.W("press", what + " (with an event)", d.K.Tab); }
                catch (Exception e) { Core.W("error", "press " + what + " (with an event)", e.GetType().Name + ": " + e.Message); }
                finally { try { ev?.Dispose(); } catch { } }
            }
            d.LastHover = DateTime.MinValue; Close(d);
            if (ToastFor == d) HideToast();
        }

        // ---- the conventions ----
        static Label Child(Button tab, string name)
        {
            if (tab == null) return null;
            for (int i = 0; i < tab.childCount; i++) { var c = tab[i]; if ((c.name ?? "") == name) return c.TryCast<Label>(); }
            return null;
        }
        static string StatusOf(Dock d)
        {
            try { var l = Child(d.Tab, "FM27MM-Status"); if (l != null && !string.IsNullOrEmpty(l.text)) return l.text; } catch { }
            if (d.K.Tab == "FM27MM-ReaderTab") return Facts.Line();
            return "";
        }
        static string BadgeOf(Dock d)
        {
            try { var l = Child(d.Tab, "FM27MM-Badge"); if (l != null) { var t = (l.text ?? "").Trim(); if (t.Length > 0 && t != "0") return t; } } catch { }
            return "";
        }
        static List<Button> SubsOf(Dock d)
        {
            var o = new List<Button>();
            try { if (d.Tab != null) for (int i = 0; i < d.Tab.childCount; i++) { var c = d.Tab[i]; if ((c.name ?? "").StartsWith("FM27MM-Sub:", StringComparison.Ordinal)) { var b = c.TryCast<Button>(); if (b != null) o.Add(b); } } } catch { }
            return o;
        }
        static void RefreshDot(Dock d)
        {
            if (d.Dot == null) return;
            d.Dot.style.display = BadgeOf(d).Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---- the column: ours above and below Heffem's button ----
        static readonly Dictionary<string, float> LastTops = new Dictionary<string, float>();
        static float HostH = 1000;
        // 0.2.4 (Nik, 24 Nov: "The icons opened in the wrong place too then shifted"): Heffem's button goes while a save loads; keep
        // its last place rather than jumping to the fallback and back
        static float LastHeffem = float.NaN;
        static void Place()
        {
            var hb = Host.worldBound; if (float.IsNaN(hb.height) || hb.height < 200) return;
            float anchor = Core.FallbackTop.Value; string why = "fallback";
            try
            {
                for (int i = 0; i < Host.childCount; i++)
                {
                    var c = Host[i]; var n = c.name ?? "";
                    if (!n.StartsWith("Heffem", StringComparison.Ordinal)) continue;
                    for (int j = 0; j < c.childCount; j++)
                    {
                        var k = c[j]; if ((k.name ?? "") != "HeffemHubButton") continue;
                        var r = k.worldBound;
                        if (!float.IsNaN(r.yMin) && r.height > 10) { anchor = r.yMin - hb.yMin; why = "Heffem's button"; LastHeffem = anchor; }
                    }
                }
            }
            catch { }
            if (why == "fallback" && !float.IsNaN(LastHeffem)) { anchor = LastHeffem; why = "Heffem's last place"; }
            int above = 0, below = 0;
            var aboveDocks = Docks.FindAll(x => x.K.Order < 50); aboveDocks.Reverse();   // nearest to Heffem's first
            foreach (var d in aboveDocks) { above++; d.Top = anchor - above * (BtnH + SlotGap); }
            foreach (var d in Docks.FindAll(x => x.K.Order >= 50)) { below++; d.Top = anchor + below * (BtnH + SlotGap); }
            HostH = hb.height;
            foreach (var d in Docks)
            {
                if (!Alive(d)) continue;
                float top = Math.Max(8, Math.Min(hb.height - BtnH - 8, d.Top)); d.Top = top;
                d.Handle.style.top = top;
                if (!LastTops.TryGetValue(d.K.Tab, out var last) || Math.Abs(last - top) > 1) { LastTops[d.K.Tab] = top; Core.W("dock", "placed " + d.K.Name, $"top {top:0} ({why} at {anchor:0}), window {hb.width:0}x{hb.height:0}"); }
            }
        }

        // ---- the fast frame (every 30 ms on our own clock): hover, slide, fade, nudges ----
        static bool Hovered(VisualElement e) { try { return e != null && e.panel != null && e.resolvedStyle.display != DisplayStyle.None && (e.pseudoStates & PseudoStates.Hover) != 0; } catch { return false; } }
        static void Frame()
        {
            if (Broken) return;
            try
            {
                var now = DateTime.Now;
                foreach (var d in Docks)
                {
                    if (!Alive(d) || !d.Shown) continue;
                    if (Hovered(d.Handle) || Hovered(d.List)) { d.LastHover = now; if (!d.Open) OpenDock(d); }
                    else if (d.Open && (now - d.LastHover).TotalMilliseconds > 380) Close(d);
                    if (Math.Abs(d.HX - d.THX) > 0.5f) { d.HX += (d.THX - d.HX) * 0.35f; if (Math.Abs(d.HX - d.THX) <= 0.5f) d.HX = d.THX; Slide(d.Handle, d.HX); }
                    if (Math.Abs(d.LX - d.TLX) > 0.5f) { d.LX += (d.TLX - d.LX) * 0.35f; if (Math.Abs(d.LX - d.TLX) <= 0.5f) d.LX = d.TLX; Slide(d.List, d.LX); }
                    if (Math.Abs(d.Fade - d.TFade) > 0.02f) { d.Fade += (d.TFade - d.Fade) * 0.35f; if (Math.Abs(d.Fade - d.TFade) <= 0.02f) d.Fade = d.TFade; d.List.style.opacity = d.Fade; }
                    if (d.List.style.display != DisplayStyle.None)
                    {
                        // centred on the button, kept on screen
                        float h = d.List.resolvedStyle.height; if (float.IsNaN(h) || h < 10) h = 160;
                        float top = Math.Max(8, Math.Min(HostH - h - 8, d.Top + BtnH / 2f - h / 2f));
                        if (Math.Abs(d.List.resolvedStyle.top - top) > 0.5f) d.List.style.top = top;
                        if (!d.Open && d.LX >= ListW + 3.5f) d.List.style.display = DisplayStyle.None;
                    }
                }
                if (Toast != null && Toast.style.display != DisplayStyle.None && now > ToastUntil && !Hovered(Toast)) HideToast();
            }
            catch (Exception e) { Fail("frame", e); }
        }

        static void OpenDock(Dock d)
        {
            foreach (var o in Docks) if (o != d && o.Open) Close(o);
            d.Open = true;
            if (d.List.style.display == DisplayStyle.None) { d.List.style.display = DisplayStyle.Flex; d.List.style.opacity = 0; d.Fade = 0; }
            d.THX = -ListW; d.TLX = 0; d.TFade = 1;
            d.List.BringToFront(); d.Handle.BringToFront();
            if (ToastFor == d) HideToast();
            Core.W("dock", "open " + d.K.Name, "");
        }
        static void Close(Dock d)
        {
            if (!d.Open) return;
            d.Open = false; d.THX = BtnW - Peek; d.TLX = ListW + 4; d.TFade = 0;
        }

        // ---- nudges: a short note beside the button, now and then ----
        static void CheckNudge(Dock d)
        {
            if (!Core.Nudges.Value) return;
            string raw = ""; try { var l = Child(d.Tab, "FM27MM-Nudge"); if (l != null) raw = l.text ?? ""; } catch { }
            if (raw.Length == 0 || raw == d.LastNudge) return;
            if (DateTime.Now < ToastNotBefore || (Toast != null && Toast.style.display != DisplayStyle.None) || d.Open || !d.Shown) return;
            d.LastNudge = raw;
            var i = raw.IndexOf('|'); var text = i >= 0 ? raw.Substring(i + 1) : raw;
            // 0.2.2: an id ending "@<page>" names the page the note belongs to (one of the part's FM27MM-Sub buttons)
            var id = i >= 0 ? raw.Substring(0, i) : ""; var at = id.LastIndexOf('@');
            ShowToast(d, text, at >= 0 ? id.Substring(at + 1) : "");
        }
        static void ShowToast(Dock d, string text, string pageName)
        {
            var T = d.K.T;
            if (Toast == null || Toast.panel == null)
            {
                Toast = new VisualElement(); Toast.name = "FM27MM-CoreNudge";
                Toast.style.position = Position.Absolute; Toast.style.width = 300;
                Toast.style.paddingLeft = 12; Toast.style.paddingRight = 12; Toast.style.paddingTop = 8; Toast.style.paddingBottom = 9;
                Round(Toast, 10);
                Host.Add(Toast);
            }
            Toast.Clear();
            Toast.style.backgroundColor = T.Panel; Border(Toast, 1, T.Edge); Toast.style.borderLeftWidth = 4; Toast.style.borderLeftColor = T.TileFg;
            var who = L(d.K.Name.ToUpperInvariant(), 11, T.TileFg, true); who.style.letterSpacing = 1f; who.style.marginBottom = 3; Toast.Add(who);
            var t = L(text, 13, T.Text, false); t.style.whiteSpace = WhiteSpace.Normal; Toast.Add(t);
            Button page = null;
            if (pageName.Length > 0) foreach (var s in SubsOf(d)) if ((s.name ?? "") == "FM27MM-Sub:" + pageName) { page = s; break; }
            var pg = page;
            var open = Chip(T, pg != null ? "Open " + pageName : "Open", () => { if (pg != null) OpenPage(d, pg, d.K.Name + " > " + pageName + " (from a note)"); else OpenPage(d, d.Tab, d.K.Name + " (from a note)"); HideToast(); }); open.style.alignSelf = Align.FlexStart; open.style.marginTop = 6; Toast.Add(open);
            Toast.style.right = Peek + 10; Toast.style.top = Math.Max(8, d.Top - 4);
            Toast.style.display = DisplayStyle.Flex; Toast.BringToFront();
            ToastFor = d; ToastUntil = DateTime.Now.AddSeconds(Math.Max(3, Core.NudgeSeconds.Value));
            ToastNotBefore = DateTime.Now.AddSeconds(Math.Max(10, Core.NudgeGapSeconds.Value));
            d.Dot.style.display = DisplayStyle.Flex;
            Core.W("nudge", d.K.Name, text);
        }
        static void HideToast() { if (Toast != null) Toast.style.display = DisplayStyle.None; ToastFor = null; }
    }

    // the Reader's fact sheet, for the Data popout's status line
    static class Facts
    {
        static DateTime Read = DateTime.MinValue, Stamp = DateTime.MinValue; static string Cached = "";
        internal static string Line()
        {
            try
            {
                if (DateTime.Now < Read) return Cached;
                Read = DateTime.Now.AddSeconds(10);
                var p = Path.Combine(Core.Dir, "game facts.tsv"); if (!File.Exists(p)) return Cached = "";
                var t = File.GetLastWriteTime(p); if (t == Stamp) return Cached; Stamp = t;
                var d = new Dictionary<string, string>();
                foreach (var l in File.ReadAllLines(p)) { var x = l.Split('\t'); if (x.Length >= 2) d[x[0]] = x[1]; }
                string first = "", second = "";
                if (d.TryGetValue("position", out var pos) && int.TryParse(pos, out var n) && n > 0) first = Ord(n) + " in the league";
                if (d.TryGetValue("next_opponent", out var opp) && opp.Length > 0)
                    second = "Next: " + opp + (d.TryGetValue("next_venue", out var ven) ? (ven == "1" ? " (home)" : ven == "2" ? " (away)" : "") : "");
                return Cached = first + "|" + second;
            }
            catch { return Cached; }
        }
        static string Ord(int n) { int m = n % 100; string s = (m >= 11 && m <= 13) ? "th" : (n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th"); return n + s; }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class SetTextPatch
    {
        [ThreadStatic] internal static IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance) { InSet = __instance.Pointer; }
        static void Postfix(SI.Bindable.SIText __instance) { InSet = IntPtr.Zero; Hub.Tick(__instance); }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class ApplyTextPatch
    {
        static void Postfix(SI.Bindable.SIText __instance) { if (__instance.Pointer == SetTextPatch.InSet) return; Hub.Tick(__instance); }
    }

    // in a game or not (as Heffem: nothing on the start screens)
    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class ReceivePatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)EV.EventFmxLoadedSaveGame || ev == (int)EV.EventFmxNewGameCreated) { if (!Hub.InGame) Core.W("game", "in a game", ((EV)ev).ToString()); Hub.InGame = true; }
                else if (ev == (int)EV.EventFmxQuitGame) { if (Hub.InGame) Core.W("game", "left the game", ((EV)ev).ToString()); Hub.InGame = false; }
            }
            catch { }
        }
    }
    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class SendPatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)EV.EventFmxConfirmQuitGame || ev == (int)EV.EventFmxQuitGame) { if (Hub.InGame) Core.W("game", "left the game", ((EV)ev).ToString()); Hub.InGame = false; }
            }
            catch { }
        }
    }
}
