// FM27 Media Mod - Team Talk Filter. Personal single-player use only.
// Team-talk options carry a tag at the start of their text (from the v0.15+ language file, e.g. "CALM – ...").
// This plugin (1) marks each option with a coloured bar for its group, (2) adds a row of filter buttons above them
// (All, ROAST, CRITICISE, ...) that only show or hide options and never pick one, and (3) removes the tag from the
// line echoed in the talk history. It does not touch what any option does.
// 0.3.0: never moves the game's own option buttons (0.2.x re-ordered them, which confused the game's list after a
// talk was given). Everything it hid is shown again the moment a talk is given or handed over.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Team Talk Filter", "0.3.1")]
    public class TeamTalkFilter : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.teamtalkfilter";
        internal static string[] Order;
        internal static bool Bars, Filter, Strip;
        internal static readonly Dictionary<IntPtr, string> TextTag = new Dictionary<IntPtr, string>();   // SIText -> tag of its current text
        internal static BepInEx.Logging.ManualLogSource L;
        internal static string Current;            // selected tag, null = all
        internal static readonly HashSet<IntPtr> Pending = new HashSet<IntPtr>();
        internal static int Errors;
        internal static string LastSig;
        internal static string AdviceLabel;
        internal static VisualElement LastBox;
        internal const string Sep = " – ";
        internal const string RowName = "FM27MM-TagFilter";
        internal static Regex TagAtStart, TagAfterSpeaker;

        public override void Load()
        {
            L = Log;
            var order = Config.Bind("Team talks", "Order", "ROAST,CRITICISE,DEMAND,MOTIVATE,INSPIRE,FOCUS,ENCOURAGE,PRAISE,CALM,TACTICS,EXPLAIN",
                "Order of the groups on screen, left to right in the filter row and top to bottom in the list.").Value;
            Order = order.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Colours = new Color[Order.Length];
            for (int i = 0; i < Order.Length; i++) Colours[i] = ColourFor(Order[i], i);
            Bars = Config.Bind("Team talks", "ColourBars", true, "Mark each option with a coloured bar for its group (the filter buttons use the same colours). Options stay in the game's own order.").Value;
            Filter = Config.Bind("Team talks", "ShowFilterButtons", true, "Show the row of filter buttons above the options.").Value;
            Strip = !Config.Bind("Team talks", "ShowTags", false, "Show the tag (\"CALM – \") at the start of each line. The sorting and filter work either way.").Value;
            AdviceLabel = Config.Bind("Team talks", "AdviceLabel", "Advice", "The text of the assistant's badge on the option it recommends; its group's button gets a dot.").Value;
            var alt = string.Join("|", Order);
            TagAtStart = new Regex("^(" + alt + ")" + Sep);
            TagAfterSpeaker = new Regex("^([^\\n]{0,60}?: )(" + alt + ")" + Sep);
            var h = new Harmony(GUID);
            try { h.PatchAll(typeof(TextPatch)); } catch (Exception e) { Log.LogError("Team Talk Filter not active: " + e.Message); }
            try { h.PatchAll(typeof(TextPatch_Apply)); } catch (Exception e) { Log.LogWarning("Team Talk Filter: ApplyText hook not active (fine before the 17 Nov patch): " + e.Message); }
            try { h.PatchAll(typeof(AdvicePatch)); } catch (Exception e) { Log.LogWarning("Advice refresh not active: " + e.Message); }
            Log.LogInfo($"FM27 Media Mod Team Talk Filter 0.3.1 loaded (bars {Bars}, filter {Filter}, hide tags {Strip}; options are never re-ordered).");
        }

        internal static void Fail(string where, Exception e)
        {
            if (Errors++ < 20) L.LogWarning($"[{where}] {e.GetType().Name}: {e.Message}");
        }

        internal static int Rank(string tag) { int i = Array.IndexOf(Order, tag); return i < 0 ? -1 : i; }

        internal static Color[] Colours;
        // hot to cool: harsh talks red, warm talks green, calm and technical talks blue and violet
        static Color ColourFor(string tag, int i)
        {
            switch (tag)
            {
                case "ROAST": return new Color(0.80f, 0.06f, 0.10f, 1f);
                case "CRITICISE": return new Color(0.90f, 0.33f, 0.10f, 1f);
                case "DEMAND": return new Color(0.95f, 0.55f, 0.08f, 1f);
                case "MOTIVATE": return new Color(0.96f, 0.76f, 0.10f, 1f);
                case "INSPIRE": return new Color(0.80f, 0.85f, 0.15f, 1f);
                case "FOCUS": return new Color(0.10f, 0.70f, 0.66f, 1f);
                case "ENCOURAGE": return new Color(0.22f, 0.75f, 0.32f, 1f);
                case "PRAISE": return new Color(0.50f, 0.86f, 0.42f, 1f);
                case "CALM": return new Color(0.25f, 0.55f, 0.95f, 1f);
                case "TACTICS": return new Color(0.58f, 0.40f, 0.92f, 1f);
                case "EXPLAIN": return new Color(0.66f, 0.66f, 0.76f, 1f);
            }
            return Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 0.9f);
        }
        internal static Color ColourOf(string tag) { int i = Rank(tag); return i < 0 ? new Color(0.6f, 0.6f, 0.6f, 1f) : Colours[i]; }

        // the coloured bar sits on the option's own left border; nothing is added to or moved in the game's list
        internal static void Mark(VisualElement item, string tag)
        {
            if (!Bars) return;
            if (tag == null)
            {
                if (!item.ClassListContains("fm27mm-bar")) return;
                item.RemoveFromClassList("fm27mm-bar");
                item.style.borderLeftWidth = new StyleFloat(StyleKeyword.Null);
                item.style.borderLeftColor = new StyleColor(StyleKeyword.Null);
                return;
            }
            item.AddToClassList("fm27mm-bar");
            item.style.borderLeftWidth = 5;
            item.style.borderLeftColor = ColourOf(tag);
        }

        // show everything this plugin hid, and go back to ALL (called whenever a talk is given or handed over)
        internal static void Release(VisualElement box)
        {
            try
            {
                Current = null;
                if (box == null) return;
                for (int i = 0; i < box.childCount; i++)
                {
                    var k = box.ElementAt(i);
                    if (k.ClassListContains(Hidden)) { k.RemoveFromClassList(Hidden); k.style.display = new StyleEnum<DisplayStyle>(StyleKeyword.Null); }
                }
                var host = box.parent; if (host == null) return;
                int at = host.IndexOf(box);
                if (at > 0) { var prev = host.ElementAt(at - 1); if (prev.name == RowName) { var present = new HashSet<string>(); for (int i = 0; i < box.childCount; i++) { var t = TagOf(box.ElementAt(i)); if (t != null) present.Add(t); } BuildRow(box, present); } }
            }
            catch (Exception e) { Fail("release", e); }
        }

        internal static string TagOf(VisualElement el)
        {
            foreach (var t in Order) if (el.ClassListContains("fm27mm-tag-" + t)) return t;
            var found = TagInText(el, 0);
            if (found != null) { el.AddToClassList("fm27mm-tag-" + found); Mark(el, found); }
            return found;
        }

        // fallback: read the option's own text (for items whose text was set before they joined the list)
        static string TagInText(VisualElement e, int depth)
        {
            if (e == null || depth > 9) return null;
            if (TextTag.TryGetValue(e.Pointer, out var known)) return known;
            var te = e.TryCast<TextElement>();
            if (te != null)
            {
                var m = TagAtStart.Match(te.text ?? "");
                if (m.Success) return m.Groups[1].Value;
            }
            for (int i = 0; i < e.childCount; i++) { var r = TagInText(e.ElementAt(i), depth + 1); if (r != null) return r; }
            return null;
        }

        // true if the assistant's "Advice" badge is showing on this option
        internal static bool HasAdvice(VisualElement item)
        {
            var te = FindText(item, AdviceLabel, 0);
            if (te == null) return false;
            for (VisualElement e = te; e != null && e.Pointer != item.Pointer; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        static VisualElement FindText(VisualElement e, string text, int depth)
        {
            if (e == null || depth > 14) return null;
            var te = e.TryCast<TextElement>();
            if (te != null && (te.text ?? "").Trim() == text) return e;
            for (int i = 0; i < e.childCount; i++) { var r = FindText(e.ElementAt(i), text, depth + 1); if (r != null) return r; }
            return null;
        }

        internal static VisualElement FindAncestor(VisualElement e, string name, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent)
                if (e.name == name) return e;
            return null;
        }

        // Called a frame after the options' texts are set.
        internal static void Arrange(VisualElement box)
        {
            try
            {
                var kids = new List<VisualElement>();
                for (int i = 0; i < box.childCount; i++) kids.Add(box.ElementAt(i));
                var tags = new List<string>(); var present = new HashSet<string>();
                foreach (var k in kids) { var t = Live(k) ? TagOf(k) : null; tags.Add(t); if (t != null) present.Add(t); }
                if (present.Count == 0) return;
                var sig = new List<string>(); foreach (var t in tags) if (t != null) sig.Add(t); sig.Sort();
                var sigs = string.Join(",", sig);
                if (sigs != LastSig) { LastSig = sigs; Current = null; }
                if (Current != null && !present.Contains(Current)) Current = null;
                ApplyFilter(box);
                if (Filter && kids.Count - tags.FindAll(x => x == null).Count >= 2) BuildRow(box, present);
            }
            catch (Exception e) { Fail("arrange", e); }
        }

        internal const string Hidden = "fm27mm-hidden";

        // Only ever hides tagged options, and only ever un-hides what it hid itself.
        internal static void ApplyFilter(VisualElement box)
        {
            for (int i = 0; i < box.childCount; i++)
            {
                var k = box.ElementAt(i); var t = TagOf(k);
                if (t == null) continue;
                bool show = Current == null || t == Current;
                bool ours = k.ClassListContains(Hidden);
                if (!show && !ours) { k.AddToClassList(Hidden); k.style.display = DisplayStyle.None; }
                else if (show && ours) { k.RemoveFromClassList(Hidden); k.style.display = new StyleEnum<DisplayStyle>(StyleKeyword.Null); }
            }
        }

        // an option SI itself has hidden (a spare pooled button) does not count
        internal static bool Live(VisualElement k) => k.ClassListContains(Hidden) || k.resolvedStyle.display != DisplayStyle.None;

        static readonly Color Off = new Color(0.20f, 0.22f, 0.26f, 0.95f), On = new Color(0.85f, 0.62f, 0.15f, 1f);
        static readonly Color TxtOff = new Color(0.92f, 0.93f, 0.95f, 1f), TxtOn = new Color(0.08f, 0.08f, 0.10f, 1f);

        internal static void BuildRow(VisualElement box, HashSet<string> present)
        {
            var host = box.parent; if (host == null) return;
            VisualElement row = null;
            int at = host.IndexOf(box);
            if (at < 0) return;
            if (at > 0) { var prev = host.ElementAt(at - 1); if (prev.name == RowName) row = prev; }
            if (row == null)
            {
                row = new VisualElement(); row.name = RowName;
                row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap;
                row.style.marginBottom = 6; row.style.marginTop = 2;
                host.Insert(at, row);
            }
            row.Clear();
            var counts = new Dictionary<string, int>(); string advised = null;
            for (int i = 0; i < box.childCount; i++)
            {
                var k = box.ElementAt(i); if (!Live(k)) continue; var t = TagOf(k); if (t == null) continue;
                counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
                if (advised == null) { try { if (HasAdvice(k)) advised = t; } catch { } }
            }
            AddChip(row, box, null, "ALL", false);
            foreach (var t in Order) if (present.Contains(t) && counts.ContainsKey(t)) AddChip(row, box, t, t + " " + counts[t] + (t == advised ? " \u2022" : ""), t == advised);
        }

        static readonly Color AdviceRing = new Color(0.90f, 0.22f, 0.38f, 1f);

        static void AddChip(VisualElement row, VisualElement box, string tag, string label, bool advised)
        {
            var b = new Button();
            b.text = label; b.name = "FM27MM-Chip";
            bool on = Current == tag;
            var gc = tag == null ? On : ColourOf(tag);
            b.style.backgroundColor = on ? gc : Off; b.style.color = on ? TxtOn : TxtOff;
            if (tag != null && !on && Bars) { b.style.borderLeftColor = gc; b.style.borderLeftWidth = 5; }
            b.style.marginRight = 4; b.style.marginBottom = 4;
            b.style.paddingLeft = 8; b.style.paddingRight = 8; b.style.paddingTop = 3; b.style.paddingBottom = 3;
            b.style.borderTopLeftRadius = 10; b.style.borderTopRightRadius = 10; b.style.borderBottomLeftRadius = 10; b.style.borderBottomRightRadius = 10;
            b.style.fontSize = 12;
            if (advised)
            {
                b.style.borderTopColor = AdviceRing; b.style.borderBottomColor = AdviceRing; b.style.borderRightColor = AdviceRing;
                b.style.borderTopWidth = 2; b.style.borderBottomWidth = 2; b.style.borderRightWidth = 2;
                if (!(tag != null && !on && Bars)) { b.style.borderLeftColor = AdviceRing; b.style.borderLeftWidth = 2; }
            }
            System.Action act = null;
            act = () =>
            {
                try
                {
                    Current = tag;
                    var r = b.parent; var h = r?.parent; if (h == null) return;
                    int ri = h.IndexOf(r); if (ri < 0 || ri + 1 >= h.childCount) return;
                    var bx = h.ElementAt(ri + 1);
                    ApplyFilter(bx);
                    var present = new HashSet<string>();
                    for (int i = 0; i < bx.childCount; i++) { var k = bx.ElementAt(i); if (!Live(k)) continue; var t = TagOf(k); if (t != null) present.Add(t); }
                    BuildRow(bx, present);
                }
                catch (Exception e) { Fail("chip", e); }
            };
            b.clickable.add_clicked(act);
            row.Add(b);
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class TextPatch
    {
        // SI's 17 Nov patch: bound text now goes through ApplyText, so the same code runs there too (see TextPatch_Apply);
        // a call that comes from inside SetText on the same element is left to the SetText hook.
        [System.ThreadStatic] internal static System.IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { InSet = __instance.Pointer; Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance) { InSet = System.IntPtr.Zero; Post(__instance); }

        // Read the tag, remember it for this text element, and (by default) take it off the text before it is shown.
        internal static void Pre(SI.Bindable.SIText __instance, ref string txt)
        {
            try
            {
                var tt = TeamTalkFilter.TextTag;
                if (string.IsNullOrEmpty(txt) || txt.IndexOf(TeamTalkFilter.Sep, StringComparison.Ordinal) < 0)
                {
                    if (tt.Count > 0) tt.Remove(__instance.Pointer);
                    return;
                }
                var m = TeamTalkFilter.TagAtStart.Match(txt);
                if (m.Success)
                {
                    tt[__instance.Pointer] = m.Groups[1].Value;
                    if (TeamTalkFilter.Strip) txt = txt.Substring(m.Length);
                    return;
                }
                tt.Remove(__instance.Pointer);
                if (!TeamTalkFilter.Strip) return;
                m = TeamTalkFilter.TagAfterSpeaker.Match(txt);      // "All players: CALM – ..." in the history
                if (m.Success) txt = m.Groups[1].Value + txt.Substring(m.Length);
            }
            catch (Exception e) { TeamTalkFilter.Fail("text", e); }
        }

        internal static void Post(SI.Bindable.SIText __instance)
        {
            try
            {
                var btn = TeamTalkFilter.FindAncestor(__instance, "SpeakToMeetingAnswerActionButton", 18);
                if (btn == null) return;
                TeamTalkFilter.TextTag.TryGetValue(__instance.Pointer, out var tag);
                // the list item is the button or the nearest wrapper that is one of several siblings
                var item = btn;
                for (int i = 0; i < 4 && item.parent != null; i++)
                {
                    if (item.parent.name == "View" || item.parent.childCount > 1) break;
                    item = item.parent;
                }
                // the assistant's "Advice" badge is a text inside the same button: it must not wipe the option's tag
                if (tag == null && TeamTalkFilter.FindAncestor(__instance, "TagVisible", 5) != null) return;
                foreach (var t in TeamTalkFilter.Order) item.RemoveFromClassList("fm27mm-tag-" + t);
                if (tag != null) item.AddToClassList("fm27mm-tag-" + tag);
                TeamTalkFilter.Mark(item, tag);
                var box = item.parent; if (box == null) return;
                var p = box.Pointer;
                if (!TeamTalkFilter.Pending.Add(p)) return;
                var keep = box;   // managed wrapper keeps the element alive until the callback runs
                TeamTalkFilter.LastBox = box;
                System.Action run = () => { TeamTalkFilter.Pending.Remove(p); TeamTalkFilter.Arrange(keep); };
                box.schedule.Execute(run);
            }
            catch (Exception e) { TeamTalkFilter.Fail("tag", e); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class TextPatch_Apply
    {
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { if (__instance.Pointer == TextPatch.InSet) return; TextPatch.Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance) { if (__instance.Pointer == TextPatch.InSet) return; TextPatch.Post(__instance); }
    }

    // When a talk is given or handed over, put the list back exactly as the game made it before the game acts on it.
    // When the assistant's advice is asked for, rebuild the filter row a moment later so the advised group gets its dot.
    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class AdvicePatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxTeamTalkProcessTalk || ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxTeamTalkHandOver)
                {
                    TeamTalkFilter.Release(TeamTalkFilter.LastBox);
                    return;
                }
                if (ev != (int)FM.GamePlugin.InteropEventIDs.EventFmxTeamTalkSuggestedComment) return;
                var box = TeamTalkFilter.LastBox; if (box == null) return;
                System.Action run = () => TeamTalkFilter.Arrange(box);
                box.schedule.Execute(run).ExecuteLater(300);
            }
            catch (Exception e) { TeamTalkFilter.Fail("advice", e); }
        }
    }
}
