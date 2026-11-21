// FM27 Media Mod - Media Lab 0.1. Personal single-player use only. EXPERIMENTAL: save before using.
// Adds a MEDIA LAB button to FM27's main screen. Each button sends one of SI's own interop commands to the game engine
// (media dumps, debug news, leak-to-press, pivotal moment, press conference) and logs what comes back:
// game events for 10 s, and any files the game writes. It also logs every news / media / social text shown on screen,
// to map the media screens for the tabloid layer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using EV = FM.GamePlugin.InteropEventIDs;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Media Lab", "0.6.1")]
    public class MediaLab : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.medialab";
        internal static BepInEx.Logging.ManualLogSource L;
        internal static StreamWriter Out, MediaOut;
        internal static FM.GamePlugin.GameInteropSubsystem Interop;
        internal static readonly Dictionary<string, SI.Interop.InteropReference> Refs = new Dictionary<string, SI.Interop.InteropReference>();
        internal static readonly List<string> RefOrder = new List<string>();
        internal static readonly Dictionary<string, string> RefSeenWith = new Dictionary<string, string>();
        internal static SI.Core.TypedValue Empty;
        internal static string Target;           // chosen reference key for "game" commands
        internal static DateTime WatchUntil = DateTime.MinValue;
        internal static string WatchLabel = "";
        internal static VisualElement Host, Panel, Body;
        internal static DateTime NextHostCheck = DateTime.MinValue;
        internal static int Errors;
        internal static bool Say, Tests, WrapUp;
        internal static bool ShowPanel = false;   // the corner panel's debug commands are dead in a release build
        internal static bool Firing;
        internal static string LastMedia = "";
        static readonly string[] MediaKeys = { "news", "inbox", "article", "headline", "media", "social", "rumour", "rumor", "feed", "story", "journalist", "quote", "tweet", "post", "fan" };

        public override void Load()
        {
            L = Log;
            var dir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod"); Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Out = new StreamWriter(Path.Combine(dir, "medialab_" + stamp + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            MediaOut = new StreamWriter(Path.Combine(dir, "mediatext_" + stamp + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            W("start", "media lab 0.6.1", "");
            Say = Config.Bind("Press conference", "HaveYourSay", false, "Show the HAVE YOUR SAY text box beside STORM OUT and NO COMMENT (off by default since 0.5.0).").Value;
            Tests = Config.Bind("Press conference", "EngineTests", true, "0.6.0: show TEST: SEND ASSISTANT, one of SI's own press-conference commands we have never fired. It needs a second click. Turn off once tested.").Value;
            WrapUp = Config.Bind("Press conference", "WrapItUp", true, "0.6.1: show WRAP IT UP, which ends a press conference or interview politely (SI's own early exit, proven 21 Nov 2026). It needs a second click. Press Room decides what the room makes of it.").Value;
            var h = new Harmony(GUID);
            foreach (var t in new[] { typeof(SendPatch), typeof(ReceivePatch), typeof(TextPatch), typeof(TextPatch_Apply), typeof(ExitPatch) })
                try { h.PatchAll(t); } catch (Exception e) { Log.LogError(t.Name + ": " + e.Message); }
            Log.LogInfo($"FM27 Media Mod Media Lab 0.6.1 loaded: one STORM OUT (confirm) / NO COMMENT{(WrapUp ? " / WRAP IT UP (confirm)" : "")} row per press conference{(Say ? " with HAVE YOUR SAY" : "")}{(Tests ? ", engine test SEND ASSISTANT" : "")}.");
        }

        internal static void W(string kind, string a, string b)
        {
            try { Out?.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{kind}\t{C(a)}\t{C(b)}"); } catch { }
        }
        internal static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        internal static void Fail(string where, Exception e) { if (Errors++ < 30) { L.LogWarning($"[{where}] {e.GetType().Name}: {e.Message}"); W("error", where, e.GetType().Name + ": " + e.Message); } }

        internal static string Key(SI.Interop.InteropReference r)
        {
            try { var id = r?.GetPersistentIdentity() ?? ""; int c = id.IndexOf(','); return c < 0 ? id : id.Substring(0, c); } catch { return ""; }
        }

        // ---------- commands ----------
        internal class Cmd { public string Label; public EV Ev; public string Want; public string Note; }
        internal static readonly Cmd[] Cmds =
        {
            new Cmd{ Label="Trigger pivotal moment (debug)", Ev=EV.EventFmxDebugTriggerPivotalMoment, Want="game" },
        };

        // live speak-to sessions (type 102 objects): subtype 2 news response, 3 chat, 6 press conference, 10 team talk
        internal static SI.Interop.InteropReference Session; internal static int SessionType = -1; internal static string SessionId = "";
        internal static int LeakVariant;
        internal static VisualElement ActionRow;

        internal static int SubtypeOf(string identity)
        {
            int k = identity.IndexOf("1398040932:", StringComparison.Ordinal);
            if (k < 0) return -1;
            k += 11; int e = k; while (e < identity.Length && char.IsDigit(identity[e])) e++;
            return int.TryParse(identity.Substring(k, e - k), out var v) ? v : -1;
        }

        internal static void ForgetSessions(string why)
        {
            Session = null; SessionType = -1; SessionId = "";
            var keep = new List<string>(); foreach (var k in RefOrder) if (k == "825307168:54" || k == "825307168:5") keep.Add(k);
            foreach (var k in new List<string>(Refs.Keys)) if (!keep.Contains(k)) Refs.Remove(k);
            RefOrder.Clear(); RefOrder.AddRange(keep);
            if (Target != null && !Refs.ContainsKey(Target)) Target = null;
            W("forget", why, "");
        }

        internal static SI.Core.TypedValue LeakData(int v, out string label)
        {
            switch (v % 4)
            {
                case 0: label = "null"; return SI.Core.TypedValue.Null;
                case 1: label = "int 0"; return SI.Core.TypedValue.Create(0);
                case 2: label = "int 1"; return SI.Core.TypedValue.Create(1);
                default: label = "true"; return SI.Core.TypedValue.Create(true);
            }
        }

        internal static void FireSession(EV ev, SI.Core.TypedValue data, string dataLabel, Button b, string done)
        {
            try
            {
                if (Interop == null || Session == null) { if (b != null) b.text = "Not available now"; return; }
                W("fire", ev.ToString(), $"session {SessionType} {SessionId} | data {dataLabel}");
                L.LogInfo($"[MediaLab] firing {ev} at live session {SessionType} ({dataLabel})");
                WatchUntil = DateTime.Now.AddSeconds(10); WatchLabel = ev.ToString();
                var fired = DateTime.Now;
                Firing = true;
                try { Interop.SendEvent((uint)(int)ev, Session, data); } finally { Firing = false; }
                if (b != null) b.text = done;
                System.Action scan = () => ScanFiles(fired, ev.ToString());
                (Panel ?? ActionRow)?.schedule.Execute(scan).ExecuteLater(5000);
            }
            catch (Exception e) { Fail("fire " + ev, e); if (b != null) b.text = "Error: " + e.Message; }
        }

        // One row of our buttons per press conference, above all the answer groups. 0.4.0 put a row above each
        // group's list (Positive / Neutral / Negative), so every question showed three rows and three text boxes.
        internal static void EnsureActionRow(VisualElement optionItem)
        {
            try
            {
                bool want = SessionType == 6;
                if (!want) { if (ActionRow != null) { try { ActionRow.RemoveFromHierarchy(); } catch { } ActionRow = null; } return; }
                string sig0 = SessionType + "|" + SessionId;
                if (ActionRow != null && ActionRow.panel != null && ActionRow.tooltip == sig0) return;   // already on screen for this conference
                // anchor: the list of answer groups if there is one, else this option's own list
                VisualElement anchor = null;
                for (var e = optionItem; e != null; e = e.parent) if (e.name == "NewsResponseOptionalButtons") { anchor = e; break; }
                if (anchor == null) anchor = optionItem.parent;
                var host = anchor?.parent; if (host == null) return;
                int at = host.IndexOf(anchor); if (at < 0) return;
                string sig = SessionType + "|" + SessionId;
                VisualElement row = null;
                // reuse ours if it is already in place; any other copy of ours on screen goes
                if (ActionRow != null && ActionRow.parent != null && ActionRow.parent.Pointer == host.Pointer) row = ActionRow;
                else if (ActionRow != null) { try { ActionRow.RemoveFromHierarchy(); } catch { } ActionRow = null; }
                for (int i = host.childCount - 1; i >= 0; i--)
                {
                    var k = host.ElementAt(i);
                    if (k.name == "FM27MM-Actions" && (row == null || k.Pointer != row.Pointer)) { if (row == null) row = k; else k.RemoveFromHierarchy(); }
                }
                if (row != null && row.tooltip == sig) { ActionRow = row; return; }
                if (row == null)
                {
                    row = new VisualElement(); row.name = "FM27MM-Actions";
                    row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; row.style.marginBottom = 6;
                    host.Insert(host.IndexOf(anchor), row);
                }
                row.Clear(); row.tooltip = sig; ActionRow = row;
if (SessionType == 6)
                {
                    Button b = null; bool armed = false;
                    System.Action storm = () =>
                    {
                        if (!armed) { armed = true; b.text = "SURE? CLICK AGAIN TO STORM OUT"; return; }
                        FireSession(EV.EventFmxPressConferenceStormOut, SI.Core.TypedValue.Null, "null", b, "Stormed out.");
                    };
                    b = MkButton("STORM OUT", storm, new Color(0.62f, 0.08f, 0.10f, 1f));
                    b.style.marginRight = 6;
                    row.Add(b);
                    Button nc = null;
                    System.Action noComment = () => FireSession(EV.EventFmxPressConferenceNoComment, SI.Core.TypedValue.Null, "null", nc, "No comment.");
                    nc = MkButton("NO COMMENT", noComment, new Color(0.25f, 0.25f, 0.30f, 1f));
                    nc.style.marginRight = 6;
                    row.Add(nc);
                    if (WrapUp)
                    {
                        // 0.6.1: SI's early exit, tested 21 Nov 2026 in a pre-match interview: it closes at once, with no error
                        Button wu = null; bool wuArmed = false;
                        System.Action wrap = () =>
                        {
                            if (!wuArmed) { wuArmed = true; wu.text = "SURE? CLICK AGAIN TO WRAP UP"; return; }
                            FireSession(EV.EventFmxPressConferenceEarlyExit, SI.Core.TypedValue.Null, "null", wu, "Wrapped up.");
                        };
                        wu = MkButton("WRAP IT UP", wrap, new Color(0.70f, 0.33f, 0.04f, 1f));
                        wu.style.marginRight = 6; row.Add(wu);
                    }
                    if (Tests)
                    {
                        // 0.6.0: SI's own command, offered by SI only in some situations; never fired by us before.
                        // What the engine does next (a news story, the conference ending, nothing) is logged for 10 s.
                        Button sa = null; bool saArmed = false;
                        System.Action assistant = () =>
                        {
                            if (!saArmed) { saArmed = true; sa.text = "SURE? CLICK AGAIN (TEST)"; return; }
                            FireSession(EV.EventFmxPressConferenceSendAssistant, SI.Core.TypedValue.Null, "null", sa, "Assistant sent (test).");
                        };
                        sa = MkButton("TEST: SEND ASSISTANT", assistant, new Color(0.30f, 0.22f, 0.45f, 1f));
                        sa.style.marginRight = 6; row.Add(sa);
                    }
                    if (!Say) return;
                    // HAVE YOUR SAY (trial): the engine has an unused "additional comments" command that takes your own words
                    // and attaches them to your next answer. Type, press ADD, then answer as normal.
                    var say = new TextField(); say.name = "FM27MM-Say";
                    say.style.minWidth = 260; say.style.flexGrow = 1; say.style.marginRight = 4;
                    say.value = "";
                    row.Add(say);
                    Button add = null;
                    System.Action addSay = () =>
                    {
                        var words = (say.value ?? "").Trim();
                        if (words.Length == 0) { add.text = "Type something first"; return; }
                        if (words.Length > 280) words = words.Substring(0, 280);
                        FireSession(EV.EventFmxPressConferenceAdditionalComments, SI.Core.TypedValue.Create(words), "\"" + words + "\"", add, "Added. Now answer.");
                        W("say", words, "");
                    };
                    add = MkButton("HAVE YOUR SAY", addSay, new Color(0.10f, 0.35f, 0.25f, 1f));
                    row.Add(add);
                }
            }
            catch (Exception e) { Fail("actions", e); }
        }

        internal static SI.Interop.InteropReference RefFor(string want, out string key)
        {
            key = null;
            if (want == "chat")
            {
                foreach (var k in new[] { "chat" }) if (Refs.TryGetValue(k, out var r)) { key = "last chat " + RefSeenWith.GetValueOrDefault(k, ""); return r; }
                return null;
            }
            if (Target != null && Refs.TryGetValue(Target, out var t)) { key = Target; return t; }
            return null;
        }

        internal static void Fire(Cmd c)
        {
            try
            {
                if (Interop == null) { Status("No game link yet: open any screen first."); return; }
                var r = RefFor(c.Want, out var key);
                if (r == null) { Status(c.Want == "chat" ? "No chat seen yet: talk to a player first, then press this." : "No target yet."); return; }
                SI.Core.TypedValue data = null;
                try { data = SI.Core.TypedValue.Null; } catch { data = Empty; }
                W("fire", c.Ev.ToString(), $"target {key} | data {(data == null ? "null" : "TypedValue.Null")}");
                L.LogInfo($"[MediaLab] firing {c.Ev} at {key}");
                WatchUntil = DateTime.Now.AddSeconds(10); WatchLabel = c.Ev.ToString();
                var fired = DateTime.Now;
                Firing = true;
                try { Interop.SendEvent((uint)(int)c.Ev, r, data); }
                finally { Firing = false; }
                Status($"Sent {c.Label}. Watching for 10 s...");
                // look for files the game wrote, 5 s later
                System.Action scan = () => ScanFiles(fired, c.Label);
                Panel?.schedule.Execute(scan).ExecuteLater(5000);
            }
            catch (Exception e) { Fail("fire " + c.Ev, e); Status("Error: " + e.Message); }
        }

        internal static void ScanFiles(DateTime since, string label)
        {
            try
            {
                var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sports Interactive", "Football Manager 27");
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sports Interactive", "Football Manager 27");
                var game = Paths.GameRootPath;
                int n = 0;
                foreach (var root in new[] { docs, local, game })
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var f in SafeFiles(root, 0))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            if (fi.LastWriteTime >= since.AddSeconds(-1) && !f.Contains("FM27MediaMod") && !f.EndsWith("LogOutput.log") && !f.Contains("Caches"))
                            { W("file", label, $"{f} ({fi.Length} bytes)"); n++; }
                        }
                        catch { }
                    }
                }
                W("scan", label, n + " new or changed files");
                Status($"{label}: {n} new or changed files (see medialab log).");
            }
            catch (Exception e) { Fail("scan", e); }
        }

        static IEnumerable<string> SafeFiles(string dir, int depth)
        {
            string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(dir); } catch { }
            foreach (var f in files) yield return f;
            if (depth >= 3) yield break;
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (var d in dirs)
            {
                var n = Path.GetFileName(d);
                if (n == "games" || n == "graphics" || n == "skins" || n == "data" || n == "shared" || n == "fm_Data" || n == "dotnet" || n == "interop" || n == "Caches") continue;
                foreach (var f in SafeFiles(d, depth + 1)) yield return f;
            }
        }

        // ---------- UI ----------
        static Label StatusLabel, TargetLabel;
        internal static void Status(string s) { try { if (StatusLabel != null) StatusLabel.text = s; } catch { } }

        static Button MkButton(string text, System.Action act, Color bg)
        {
            var b = new Button(); b.text = text;
            b.style.backgroundColor = bg; b.style.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            b.style.marginBottom = 3; b.style.paddingLeft = 8; b.style.paddingRight = 8; b.style.paddingTop = 3; b.style.paddingBottom = 3;
            b.style.fontSize = 12;
            b.clickable.add_clicked(act);
            return b;
        }

        internal static void EnsurePanel(VisualElement anyElement)
        {
            if (!ShowPanel) return;
            if (DateTime.Now < NextHostCheck) return;
            NextHostCheck = DateTime.Now.AddSeconds(2);
            try
            {
                if (Panel != null && Panel.panel != null) return;     // still attached
                VisualElement host = null;
                for (var e = anyElement; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { host = e; break; }
                if (host == null) return;
                Host = host;
                Panel = new VisualElement(); Panel.name = "FM27MM-MediaLab";
                Panel.style.position = Position.Absolute; Panel.style.right = 14; Panel.style.top = 70;
                Panel.style.width = 300;
                Panel.style.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 0.94f);
                Panel.style.paddingLeft = 6; Panel.style.paddingRight = 6; Panel.style.paddingTop = 4; Panel.style.paddingBottom = 4;
                Panel.style.borderTopLeftRadius = 8; Panel.style.borderTopRightRadius = 8; Panel.style.borderBottomLeftRadius = 8; Panel.style.borderBottomRightRadius = 8;
                Body = new VisualElement(); Body.style.display = DisplayStyle.None;
                var toggle = MkButton("MEDIA LAB", () => { try { Body.style.display = Body.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; RefreshTarget(); } catch (Exception e) { Fail("toggle", e); } }, new Color(0.62f, 0.12f, 0.18f, 1f));
                Panel.Add(toggle);
                TargetLabel = new Label(); TargetLabel.style.color = new Color(0.8f, 0.8f, 0.85f, 1f); TargetLabel.style.fontSize = 11; TargetLabel.style.marginBottom = 3;
                Body.Add(MkButton("Target: (tap to change)", () => { CycleTarget(); }, new Color(0.25f, 0.25f, 0.30f, 1f)));
                Body.Add(TargetLabel);
                foreach (var c in Cmds) { var cc = c; Body.Add(MkButton(c.Label, () => Fire(cc), c.Want == "chat" ? new Color(0.55f, 0.10f, 0.35f, 1f) : new Color(0.18f, 0.22f, 0.30f, 1f))); }
                StatusLabel = new Label(); StatusLabel.style.color = new Color(1f, 0.85f, 0.4f, 1f); StatusLabel.style.fontSize = 11; StatusLabel.style.whiteSpace = WhiteSpace.Normal;
                StatusLabel.text = "Save first. The leak and storm-out buttons now sit inside chats and press conferences.";
                Body.Add(StatusLabel);
                Panel.Add(Body);
                host.Add(Panel);
                W("ui", "panel added", "");
            }
            catch (Exception e) { Fail("panel", e); }
        }

        internal static void RefreshTarget()
        {
            if (Target == null) foreach (var k in new[] { "825307168:54", "825307168:5" }) if (Refs.ContainsKey(k)) { Target = k; break; }
            if (Target == null && RefOrder.Count > 0) Target = RefOrder[0];
            if (TargetLabel != null) TargetLabel.text = "Target " + (Target ?? "none") + (Target != null && RefSeenWith.TryGetValue(Target, out var w) ? " (seen with " + w + ")" : "") + " | chat: " + (Refs.ContainsKey("chat") ? "yes" : "no");
        }

        internal static void CycleTarget()
        {
            var keys = RefOrder.FindAll(k => k != "chat");
            if (keys.Count == 0) return;
            int i = Target == null ? -1 : keys.IndexOf(Target);
            Target = keys[(i + 1) % keys.Count];
            RefreshTarget();
        }
    }

    // UI -> game: learn the interop object, references and an empty value; log while watching
    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class SendPatch
    {
        static void Prefix(FM.GamePlugin.GameInteropSubsystem __instance, uint eventID, SI.Interop.InteropReference objectTo, SI.Core.TypedValue data)
        {
            try
            {
                MediaLab.Interop = __instance;
                if (MediaLab.Firing) return;
                string name; try { name = ((EV)(int)eventID).ToString(); } catch { name = eventID.ToString(); }
                if (objectTo != null)
                {
                    var k = MediaLab.Key(objectTo);
                    if (k.Length > 0)
                    {
                        if (!MediaLab.Refs.ContainsKey(k)) MediaLab.RefOrder.Add(k);
                        MediaLab.Refs[k] = objectTo; MediaLab.RefSeenWith[k] = name;

                    }
                }
                if (objectTo != null)
                {
                    var id = ""; try { id = objectTo.GetPersistentIdentity() ?? ""; } catch { }
                    int st = MediaLab.SubtypeOf(id);
                    if (id.StartsWith("825307168:102,") && st >= 0 && id != MediaLab.SessionId)
                    { MediaLab.Session = objectTo; MediaLab.SessionType = st; MediaLab.SessionId = id; MediaLab.LeakVariant = 0; MediaLab.W("session", st.ToString(), id); }
                }
                if (MediaLab.Empty == null && data != null) { try { if (data.IsNull) MediaLab.Empty = data; } catch { } }
                if (DateTime.Now < MediaLab.WatchUntil) MediaLab.W("ui-event", name, MediaLab.Key(objectTo));
            }
            catch (Exception e) { MediaLab.Fail("send", e); }
        }
    }

    // game -> UI: log everything while watching a trial
    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class ReceivePatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                string name; try { name = ((EV)(int)eventID).ToString(); } catch { name = eventID.ToString(); }
                if (name == "EventFmxDateChange") MediaLab.ForgetSessions("new day");
                if (DateTime.Now >= MediaLab.WatchUntil) return;
                if (name == "EventFmxSyncSentToClient") return;
                MediaLab.W("game-event", MediaLab.WatchLabel, name);
            }
            catch { }
        }
    }

    // text on screen: host the panel, and log media texts
    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class TextPatch
    {
        // SI's 17 Nov patch: bound text now goes through ApplyText, so the same code runs there too (see TextPatch_Apply);
        // a call that comes from inside SetText on the same element is left to the SetText hook.
        [System.ThreadStatic] internal static System.IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance) { InSet = __instance.Pointer; }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { InSet = System.IntPtr.Zero; Post(__instance, txt); }

        internal static void Post(SI.Bindable.SIText __instance, string txt)
        {
            try
            {
                MediaLab.EnsurePanel(__instance);
                if (MediaLab.Session != null)
                {
                    VisualElement b = __instance; int d = 0;
                    while (b != null && d < 18 && b.name != "SpeakToMeetingAnswerActionButton") { b = b.parent; d++; }
                    if (b != null && b.name == "SpeakToMeetingAnswerActionButton")
                    {
                        var item = b;
                        for (int i = 0; i < 4 && item.parent != null; i++) { if (item.parent.name == "View" || item.parent.childCount > 1) break; item = item.parent; }
                        MediaLab.EnsureActionRow(item);
                    }
                }
                if (string.IsNullOrEmpty(txt) || txt.Length < 4) return;
                var sb = new StringBuilder(); bool hit = false; VisualElement e = __instance;
                for (int i = 0; i < 16 && e != null; i++, e = e.parent)
                {
                    var nm = e.name ?? ""; if (nm.Length == 0) continue;
                    sb.Insert(0, nm + "/");
                    var low = nm.ToLowerInvariant();
                    if (!hit) foreach (var k in new[] { "news", "inbox", "article", "headline", "media", "social", "rumour", "feed", "story", "journalist", "quote", "tweet", "post" }) if (low.Contains(k)) { hit = true; break; }
                }
                if (!hit) return;
                var line = sb + "\t" + MediaLab.C(txt);
                if (line == MediaLab.LastMedia) return;
                MediaLab.LastMedia = line;
                MediaLab.MediaOut?.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{line}");
            }
            catch (Exception ex) { MediaLab.Fail("text", ex); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class TextPatch_Apply
    {
        static void Postfix(SI.Bindable.SIText __instance, string txt) { if (__instance.Pointer == TextPatch.InSet) return; TextPatch.Post(__instance, txt); }
    }

    // leaving a chat or press conference: forget its session so no button can fire at a finished one
    [HarmonyPatch(typeof(FM.UI.NewsActionHandlerModule), nameof(FM.UI.NewsActionHandlerModule.TriggerSpeakToEvent))]
    static class ExitPatch
    {
        static void Prefix(FM.UI.SpeakToActionEvent speakToActionEvent)
        {
            try
            {
                if (speakToActionEvent != null && speakToActionEvent.IsExitButton)
                {
                    MediaLab.Session = null; MediaLab.SessionType = -1; MediaLab.SessionId = "";
                    try { MediaLab.ActionRow?.RemoveFromHierarchy(); } catch { }
                    MediaLab.W("session", "exit", "");
                }
            }
            catch (Exception e) { MediaLab.Fail("exit", e); }
        }
    }
}
