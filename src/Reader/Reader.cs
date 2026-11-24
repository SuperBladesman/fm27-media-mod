// FM27 Media Mod - Reader 0.2.3. MIT licence (see LICENSE).
// 0.2.3 (release defaults, 24 Nov 2026): only the fact sheet is on. The explorer (READ tab), the UI map, the menu watch
// and the deep dumps are modders' tools, off unless switched on in the cfg. The default facts add the head physio and
// the medical lead (for the Treatment Room).
// Reads values from SI's own data-binding layer by CALLING it (never hooking it: hooks on the binding methods crashed FM
// on 22 Nov). PROVEN 23 Nov 00:59 with 0.1.0: BindOneShot("human.Team.LeaguePosition") etc. returned real values.
// 0.2.2: the READ tab also maps the on-screen layout of the right-hand mod strips (Heffem's hub and our own tabs):
//   names, sizes, positions, colours, fonts, visibility, read from Unity's visual tree three times over 8 s, to
//   reader_ui_<time>.tsv. Observation of what is drawn only; no other mod's code is read.
// 0.2.0 (0.2.1: fact-sheet reads started from the UI thread; the log writer locked):
//  - READ tab: the explorer. Asks each root object (manager, team, club, next / last fixture, next / previous opponent,
//    game) which properties it has (IDataReference.GetProperties), logs them to reader_props_<time>.tsv, then reads every
//    property (except likely lists) with BindOneShot into reader_values_<time>.tsv.
//  - The fact sheet: on each save load and each new game day it reads a short, configurable list of paths and writes
//    BepInEx\FM27MediaMod\game facts.tsv (for Press Room and Tabloid to use later).
//  - SI's own Actions menu: a hook on FM.Game.PluginContextActionsModule.HandleActionEvent logs what SI's menu sends
//    (action, event, target, payload), and an optional SI MENU test tab re-sends the last one through SI's own handler.
//    This hook is new (own switch: [SI menu] Watch). If FM ever fails to start with Reader 0.2, set it to false.
//  - Deeper payload dumps of EventFmxDoAction and the audit events (SendEvent prefix, a proven hook).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using EV = FM.GamePlugin.InteropEventIDs;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Reader", Version)]
    public class Reader : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.reader";
        public const string Version = "0.2.3";
        internal static StreamWriter Out;
        internal static string Dir;
        internal static ConfigEntry<bool> ShowTab, Facts, WatchMenu, MenuTest, DeepDumps, UiMap;
        internal static ConfigEntry<int> MaxPropsPerRoot;
        internal static ConfigEntry<string> Roots, FactPaths, SkipWords, UiPrefixes;

        public override void Load()
        {
            ShowTab = Config.Bind("Reader", "ShowTab", false, "Show the READ tab in the right-hand strip (runs the explorer when clicked).");
            Roots = Config.Bind("Reader", "ExplorerRoots", "Human|Human.Team|Human.Club|Human.Team.NextFixture|Human.Team.LastFixture|Human.Team.NextMatchOpponent|Human.Team.PreviousMatchOpponent|game", "Objects the explorer maps, separated by |.");
            MaxPropsPerRoot = Config.Bind("Reader", "MaxPropertiesPerRoot", 400, "The explorer reads at most this many properties per object.");
            SkipWords = Config.Bind("Reader", "SkipWords", "List|Lists|History|Table|Tables|Graph|Chart|Debug", "Properties whose names contain any of these words are listed but not read (they can be large).");
            UiMap = Config.Bind("UI map", "Enabled", false, "When READ is clicked, also map the right-hand mod strips on screen (reader_ui_<time>.tsv), three times over 8 seconds.");
            UiPrefixes = Config.Bind("UI map", "Prefixes", "Heffem|FM27MM", "Map every top-level element whose name starts with one of these, and all its children.");
            Facts = Config.Bind("Facts", "Enabled", true, "Write game facts.tsv on each save load and each new game day.");
            FactPaths = Config.Bind("Facts", "Paths",
                "position=Human.Team.LeaguePosition|board=Human.Team.TeamConfidence.BoardConfidenceOverallGrade|club_reputation=Human.Club.Reputation|manager_reputation=Human.ManagerReputation|next_opponent=Human.Team.NextMatchOpponent.Name|next_venue=Human.Team.NextMatchLocation|previous_opponent=Human.Team.PreviousMatchOpponent.Name|previous_venue=Human.Team.PreviousMatchLocation|club=Human.Club.Name|manager=Human.Name|date=game.CurrentGameDate|head_physio=Human.Club.HeadPhysio.Name|head_physio_id=Human.Club.HeadPhysio.UniqueId|medical_lead=Human.Team.MedicalPersonForReport.Name|medical_lead_id=Human.Team.MedicalPersonForReport.UniqueId",
                "The facts: name=path pairs separated by |.");
            WatchMenu = Config.Bind("SI menu", "Watch", false, "Log what SI's own Actions menu sends (a hook on PluginContextActionsModule.HandleActionEvent). If FM fails to start, set this to false.");
            MenuTest = Config.Bind("SI menu", "TestTab", false, "Show the SI MENU test tab, which re-sends the last SI menu action through SI's own handler.");
            DeepDumps = Config.Bind("SI menu", "DeepDumps", false, "Log EventFmxDoAction and the audit events' payloads in full (six levels).");
            Dir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
            Directory.CreateDirectory(Dir);
            Out = new StreamWriter(Path.Combine(Dir, "reader_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            W("start", "reader " + Version, "");
            var h = new Harmony(GUID); int ok = 0, bad = 0;
            var types = new List<Type> { typeof(SetTextPatch), typeof(ApplyTextPatch), typeof(ReceivePatch), typeof(SendPatch) };
            if (WatchMenu.Value) types.Add(typeof(MenuPatch));
            foreach (var t in types)
            {
                try { h.CreateClassProcessor(t).Patch(); ok++; }
                catch (Exception e) { bad++; W("patch-error", t.Name, e.GetType().Name + ": " + e.Message); }
            }
            W("patches", ok + " ok", bad + " failed");
            Log.LogInfo("FM27 Media Mod Reader " + Version + " loaded (facts " + (Facts.Value ? "on" : "off") + ", SI menu watch " + (WatchMenu.Value ? "on" : "off") + "; patches " + ok + " ok, " + bad + " failed).");
        }

        internal static void W(string kind, string a, string b)
        {
            try { var o = Out; if (o == null) return; lock (o) o.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{kind}\t{C(a)}\t{C(b)}"); } catch { }
        }
        internal static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Replace("\u0001", "").Replace("\u0002", "=");

        static readonly Regex Link = new Regex("\u0001[A-Za-z0-9+/=]*\u0002", RegexOptions.Compiled);
        internal static string Plain(string s) => s == null ? "" : Link.Replace(s, "").Replace("\u0001", "");

        internal static string Val(SI.Core.TypedValue v, bool plain = false)
        {
            try
            {
                if (v == null) return "(none)";
                if (v.IsNull) return "null";
                string t = "?"; try { t = v.DataType?.Name ?? "?"; } catch { }
                string s = ""; try { s = v.AsString(); } catch (Exception e) { s = "(AsString failed: " + e.GetType().Name + ")"; }
                if (plain) s = Plain(s);
                if (s != null && s.Length > 300) s = s.Substring(0, 300) + "...";
                return t + "=" + s;
            }
            catch (Exception e) { return "(error " + e.GetType().Name + ")"; }
        }

        internal static string Bare(SI.Core.TypedValue v)
        {
            try { if (v == null || v.IsNull) return ""; return Plain(v.AsString() ?? ""); } catch { return ""; }
        }
    }

    // ---- SI's binding layer, called (never hooked) ----
    static class B
    {
        static SI.Bindable.Bindings Cached;
        static Dictionary<uint, string> Names;
        internal static readonly List<SI.Bindable.Bindings.ValueChangedCallback> Keep = new List<SI.Bindable.Bindings.ValueChangedCallback>();

        internal static SI.Bindable.Bindings Get()
        {
            if (Cached != null) return Cached;
            var list = SI.Core.GameSubsystems.s_subsystems;
            for (int i = 0; list != null && i < list.Count; i++) { var bs = list[i].TryCast<SI.Bindable.BindingSubsystem>(); if (bs != null) { Cached = bs; break; } }
            return Cached;
        }

        internal static string Name(uint id)
        {
            if (Names == null)
            {
                var d = new Dictionary<uint, string>();
                try
                {
                    var set = SI.Bindable.Reference.Core.IdentifierSet<SI.Bindable.Reference.Core.PropertyIdentifierSet,
                        SI.Bindable.Reference.Core.PropertyID, SI.Bindable.Reference.Core.PropertyIDInfo>.Instance;
                    foreach (var kv in set.m_idToInfo) d[kv.Key] = kv.Value.Name;
                }
                catch { }
                Names = d;
            }
            return Names.TryGetValue(id, out var n) ? n : "#" + id;
        }

        internal static SI.Core.TypedValue Peek(string path)
        {
            var b = Get(); if (b == null) return null;
            try { return b.Get(Il2CppSystem.MemoryExtensions.AsSpan(path)); } catch { return null; }
        }

        internal static bool Once(string path, Il2CppSystem.Object owner, Action<SI.Core.TypedValue> got)
        {
            var b = Get(); if (b == null) return false;
            try
            {
                var span = Il2CppSystem.MemoryExtensions.AsSpan(path);
                System.Action<SI.Bindable.Bindings.Key, SI.Core.TypedValue> cb = (k, v) => { try { got(v); } catch { } };
                SI.Bindable.Bindings.ValueChangedCallback del = cb;
                Keep.Add(del); if (Keep.Count > 4000) Keep.RemoveRange(0, 2000);
                b.BindOneShot(ref span, owner, del);
                return true;
            }
            catch (Exception e) { Reader.W("bind-error", path, e.GetType().Name + ": " + e.Message); return false; }
        }

        internal static List<string> Properties(string path)
        {
            var o = new List<string>();
            var tv = Peek(path);
            if (tv == null || tv.IsNull) return o;
            Il2CppSystem.Object io = null; try { io = tv.Get(); } catch { }
            if (io == null) return o;
            var list = new Il2CppSystem.Collections.Generic.List<SI.Bindable.Reference.Core.PropertyID>();
            bool done = false;
            try { var r = io.TryCast<SI.Bindable.Reference.Core.IDataReference>(); if (r != null) { r.GetProperties(list); done = true; } } catch (Exception e) { Reader.W("props-error", path, "IDataReference: " + e.GetType().Name + ": " + e.Message); }
            if (!done)
            {
                try { var r = io.TryCast<FM.UI.TeamReference>(); if (r != null) { r.GetProperties(list); done = true; } } catch { }
                try { if (!done) { var r = io.TryCast<FM.UI.ClubReference>(); if (r != null) { r.GetProperties(list); done = true; } } } catch { }
                try { if (!done) { var r = io.TryCast<FM.UI.PersonReference>(); if (r != null) { r.GetProperties(list); done = true; } } } catch { }
                try { if (!done) { var r = io.TryCast<FM.UI.FixtureReference>(); if (r != null) { r.GetProperties(list); done = true; } } } catch { }
                try { if (!done) { var r = io.TryCast<FM.UI.GameReference>(); if (r != null) { r.GetProperties(list); done = true; } } } catch { }
            }
            for (int i = 0; i < list.Count; i++) o.Add(Name(list[i].ID));
            return o;
        }
    }

    // ---- the explorer (READ tab) ----
    static class Explorer
    {
        static bool Busy;
        internal static void Run(VisualElement owner)
        {
            if (Busy) { Reader.W("busy", "explorer already running", ""); return; }
            Busy = true;
            StreamWriter props = null, vals = null;
            try
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                props = new StreamWriter(Path.Combine(Reader.Dir, "reader_props_" + stamp + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                vals = new StreamWriter(Path.Combine(Reader.Dir, "reader_values_" + stamp + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                props.WriteLine("root\tproperty\tread"); vals.WriteLine("time\tpath\tvalue");
                var v2 = vals;
                if (B.Get() == null) { Reader.W("explorer", "no BindingSubsystem", ""); return; }
                var skip = (Reader.SkipWords.Value ?? "").Split('|');
                foreach (var rootRaw in (Reader.Roots.Value ?? "").Split('|'))
                {
                    var root = rootRaw.Trim(); if (root.Length == 0) continue;
                    Reader.W("step", "explore " + root, "");
                    var tv = B.Peek(root);
                    if (tv == null || tv.IsNull)
                    {
                        // not in the tree yet: bind it once so the next click can map it
                        B.Once(root, owner, v => { });
                        Reader.W("explorer", root, "not bound yet (bound now; click READ again)");
                        continue;
                    }
                    var list = B.Properties(root);
                    Reader.W("explorer", root, Reader.Val(tv) + " | " + list.Count + " properties");
                    int n = 0;
                    foreach (var p in list)
                    {
                        bool big = false; foreach (var w in skip) if (w.Length > 0 && p.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) { big = true; break; }
                        bool read = !big && n < Reader.MaxPropsPerRoot.Value;
                        props.WriteLine(root + "\t" + p + "\t" + (read ? "yes" : big ? "skipped (large)" : "skipped (limit)"));
                        if (!read) continue;
                        n++;
                        var path = root + "." + p;
                        B.Once(path, owner, v => { try { v2.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + "\t" + path + "\t" + Reader.C(Reader.Val(v))); } catch { } });
                    }
                }
                Reader.W("step", "explorer bound everything; values arrive over the next seconds (reader_values_" + stamp + ".tsv)", "");
                if (Reader.UiMap.Value) UiMap.Start(owner, stamp);
                System.Action close = () => { try { v2.Flush(); v2.Close(); } catch { } Reader.W("step", "explorer values file closed", ""); };
                try { owner.schedule.Execute(close).ExecuteLater(15000); } catch { }
                vals = null;   // closed later
            }
            catch (Exception e) { Reader.W("error", "explorer", e.ToString()); }
            finally { try { props?.Close(); } catch { } try { vals?.Close(); } catch { } Busy = false; }
        }
    }

    // ---- the UI map (READ tab): what the right-hand strips look like on screen ----
    static class UiMap
    {
        static StreamWriter W;
        static string F(float v) => float.IsNaN(v) ? "NaN" : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        static string Col(Color c) => c.a <= 0.001f ? "none" : "#" + ColorUtility.ToHtmlStringRGBA(c);
        internal static void Start(VisualElement owner, string stamp)
        {
            try
            {
                W = new StreamWriter(Path.Combine(Reader.Dir, "reader_ui_" + stamp + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                W.WriteLine("pass\tdepth\tpath\ttype\tclasses\ttext\tx\ty\tw\th\tdisplay\tvisibility\topacity\tposition\tleft\ttop\tright\tbottom\tbg\tcolor\tfont\tstyle\tradius\tborder\tborderColor\tpadding\tmargin\tflex\talign\tjustify\tpicking\tbgImage\tchildren");
                var host = owner; while (host != null && host.name != "MainUIDocument-container") host = host.parent;
                if (host == null) { Reader.W("ui-map", "no MainUIDocument-container", ""); return; }
                var h = host; var w = W;
                Pass(h, 1);
                System.Action p2 = () => Pass(h, 2); System.Action p3 = () => { Pass(h, 3); try { w.Close(); } catch { } Reader.W("ui-map", "done", "reader_ui_" + stamp + ".tsv"); };
                owner.schedule.Execute(p2).ExecuteLater(4000); owner.schedule.Execute(p3).ExecuteLater(8000);
                Reader.W("ui-map", "pass 1 written; passes 2 and 3 at +4 s and +8 s", "");
            }
            catch (Exception e) { Reader.W("error", "ui-map", e.ToString()); }
        }
        static void Pass(VisualElement host, int pass)
        {
            try
            {
                var pre = (Reader.UiPrefixes.Value ?? "").Split('|');
                // every top-level child, briefly; the mod strips in full
                for (int i = 0; i < host.childCount; i++)
                {
                    var c = host[i]; var n = c.name ?? "";
                    bool deep = false; foreach (var p in pre) if (p.Length > 0 && n.StartsWith(p, StringComparison.Ordinal)) deep = true;
                    int count = 0;
                    Node(pass, 0, n.Length > 0 ? n : "(" + c.GetType().Name + ")", c, deep ? 14 : 0, ref count);
                }
            }
            catch (Exception e) { Reader.W("error", "ui-map pass " + pass, e.Message); }
        }
        static void Node(int pass, int depth, string path, VisualElement e, int maxDepth, ref int count)
        {
            if (++count > 900) return;
            string cls = "";
            try { var en = e.GetClasses().GetEnumerator(); var mv = en.Cast<Il2CppSystem.Collections.IEnumerator>(); var sb = new StringBuilder(); while (mv.MoveNext()) { if (sb.Length > 0) sb.Append(' '); sb.Append(en.Current); } cls = sb.ToString(); } catch { }
            string txt = ""; try { var t = e.TryCast<TextElement>(); if (t != null) txt = Reader.Plain(t.text ?? ""); } catch { }
            if (txt.Length > 60) txt = txt.Substring(0, 60) + "...";
            string line;
            try
            {
                var r = e.worldBound; var st = e.resolvedStyle;
                string img = ""; try { var tex = st.backgroundImage.texture; if (tex != null) img = (tex.name ?? "tex") + " " + tex.width + "x" + tex.height; } catch { }
                line = string.Join("\t", new[] {
                    pass.ToString(), depth.ToString(), path, e.GetType().Name, cls, Reader.C(txt),
                    F(r.x), F(r.y), F(r.width), F(r.height),
                    st.display.ToString(), st.visibility.ToString(), F(st.opacity), st.position.ToString(),
                    F(st.left), F(st.top), F(st.right), F(st.bottom),
                    Col(st.backgroundColor), Col(st.color), F(st.fontSize), st.unityFontStyleAndWeight.ToString(),
                    F(st.borderTopLeftRadius) + "/" + F(st.borderTopRightRadius) + "/" + F(st.borderBottomRightRadius) + "/" + F(st.borderBottomLeftRadius),
                    F(st.borderTopWidth) + "/" + F(st.borderRightWidth) + "/" + F(st.borderBottomWidth) + "/" + F(st.borderLeftWidth),
                    Col(st.borderLeftColor),
                    F(st.paddingTop) + "/" + F(st.paddingRight) + "/" + F(st.paddingBottom) + "/" + F(st.paddingLeft),
                    F(st.marginTop) + "/" + F(st.marginRight) + "/" + F(st.marginBottom) + "/" + F(st.marginLeft),
                    st.flexDirection.ToString(), st.alignItems.ToString(), st.justifyContent.ToString(),
                    e.pickingMode.ToString(), img, e.childCount.ToString() });
            }
            catch (Exception ex) { line = pass + "\t" + depth + "\t" + path + "\t(error " + ex.GetType().Name + ")"; }
            try { W.WriteLine(line); } catch { }
            if (depth >= maxDepth) return;
            for (int i = 0; i < e.childCount; i++)
            {
                var c = e[i]; var n = c.name ?? "";
                Node(pass, depth + 1, path + "/" + (n.Length > 0 ? n : "(" + c.GetType().Name + ")"), c, maxDepth, ref count);
            }
        }
    }

    // ---- the fact sheet ----
    static class FactSheet
    {
        static DateTime Next = DateTime.MinValue;
        internal static VisualElement Owner;
        // Game events can arrive off the UI thread, so the hook only records a request (Ask); the text hook, which
        // always runs on the UI thread, starts the read (Poll).
        static readonly object Gate = new object();
        static string PendingWhy; static DateTime PendingAt;
        internal static void Ask(string why, int ms)
        {
            if (!Reader.Facts.Value) return;
            lock (Gate) { PendingWhy = why; PendingAt = DateTime.Now.AddMilliseconds(ms); }
        }
        internal static void Poll()
        {
            string why = null;
            lock (Gate) { if (PendingWhy != null && DateTime.Now >= PendingAt) { why = PendingWhy; PendingWhy = null; } }
            if (why != null) Soon(why, 0);
        }
        internal static void Soon(string why, int ms)
        {
            if (!Reader.Facts.Value || Owner == null) return;
            System.Action run = () => { try { Read(why); } catch (Exception e) { Reader.W("error", "facts", e.Message); } };
            try { Owner.schedule.Execute(run).ExecuteLater(ms); } catch { }
        }

        static void Read(string why)
        {
            if (DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(3);
            if (B.Get() == null) return;
            var got = new SortedDictionary<string, string>();
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var pr in (Reader.FactPaths.Value ?? "").Split('|'))
            {
                var i = pr.IndexOf('='); if (i <= 0) continue;
                pairs.Add(new KeyValuePair<string, string>(pr.Substring(0, i).Trim(), pr.Substring(i + 1).Trim()));
            }
            foreach (var kv in pairs)
            {
                var key = kv.Key; var path = kv.Value;
                B.Once(path, Owner, v => { lock (got) got[key] = Reader.Bare(v) + "\t" + path; });
            }
            System.Action write = () =>
            {
                try
                {
                    var sb = new StringBuilder("fact\tvalue\tpath\tread at\n");
                    var at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    lock (got) foreach (var kv in got) { var p = kv.Value.Split('\t'); sb.Append(kv.Key + "\t" + p[0].Replace('\t', ' ') + "\t" + (p.Length > 1 ? p[1] : "") + "\t" + at + "\n"); }
                    File.WriteAllText(Path.Combine(Reader.Dir, "game facts.tsv"), sb.ToString(), new UTF8Encoding(false));
                    Reader.W("facts", why, got.Count + " of " + pairs.Count + " read: " + string.Join("; ", Pick(got)));
                }
                catch (Exception e) { Reader.W("error", "facts write", e.Message); }
            };
            try { Owner.schedule.Execute(write).ExecuteLater(4000); } catch { }
        }
        static IEnumerable<string> Pick(SortedDictionary<string, string> got) { foreach (var kv in got) yield return kv.Key + "=" + kv.Value.Split('\t')[0]; }
    }

    // ---- SI's own Actions menu ----
    static class SiMenu
    {
        internal static FM.GamePlugin.ActionID LastAction; internal static uint LastEvent;
        internal static SI.Interop.InteropReference LastRef; internal static SI.Core.TypedValue LastData;
        internal static Il2CppSystem.Collections.Generic.List<SI.Core.TypedValue> LastAudit;
        internal static bool Have, Replaying;

        static string FourCC(uint k)
        {
            var b = new[] { (char)((k >> 24) & 0xff), (char)((k >> 16) & 0xff), (char)((k >> 8) & 0xff), (char)(k & 0xff) };
            foreach (var c in b) if (c < 0x20 || c > 0x7e) return "";
            return new string(b);
        }

        internal static string Dump(SI.Core.TypedValue v, int depth)
        {
            try
            {
                if (v == null) return "(none)";
                if (v.IsNull) return "null";
                string tn = "?"; try { tn = v.DataType?.Name ?? "?"; } catch { }
                Il2CppSystem.Object io = null; try { io = v.Get(); } catch { }
                if (io != null && depth < 6)
                {
                    var dict = io.TryCast<Il2CppSystem.Collections.Generic.Dictionary<uint, SI.Core.TypedValue>>();
                    if (dict != null)
                    {
                        var sb = new StringBuilder(tn + "{"); int n = 0;
                        foreach (var kv in dict)
                        {
                            if (n++ > 0) sb.Append(", ");
                            var fc = FourCC(kv.Key);
                            sb.Append((fc.Length > 0 ? fc + "/" : "") + kv.Key + "=" + Dump(kv.Value, depth + 1));
                            if (n >= 30) { sb.Append(", ..."); break; }
                        }
                        return sb.Append("}").ToString();
                    }
                    var ir = io.TryCast<SI.Interop.InteropReference>();
                    if (ir != null) return tn + "[" + (ir.GetPersistentIdentity() ?? "") + "]";
                    var list = io.TryCast<Il2CppSystem.Collections.Generic.List<SI.Core.TypedValue>>();
                    if (list != null)
                    {
                        var sb = new StringBuilder(tn + "(" + list.Count + ")["); for (int i = 0; i < list.Count && i < 8; i++) { if (i > 0) sb.Append(", "); sb.Append(Dump(list[i], depth + 1)); }
                        return sb.Append(list.Count > 8 ? ", ...]" : "]").ToString();
                    }
                }
                string s = ""; try { s = v.AsString(); } catch { }
                return tn + "=" + Reader.Plain(s);
            }
            catch (Exception e) { return "(dump error " + e.GetType().Name + ")"; }
        }

        internal static void Replay()
        {
            if (!Have) { Reader.W("si-menu", "replay", "nothing captured yet: use an action from SI's own Actions menu first"); return; }
            Reader.W("si-menu", "replay " + LastAction + " (event " + LastEvent + ")", LastRef == null ? "" : LastRef.GetPersistentIdentity());
            try { Replaying = true; FM.Game.PluginContextActionsModule.HandleActionEvent(LastAction, LastEvent, LastRef, LastData, true, Tab.MenuTab, LastAudit); }
            catch (Exception e) { Reader.W("error", "replay", e.GetType().Name + ": " + e.Message); }
            finally { Replaying = false; }
        }
    }

    // ---- the tabs ----
    static class Tab
    {
        internal static Button ReadTab, MenuTab;
        static DateTime Next = DateTime.MinValue;
        static Button Make(string text, string name, float top, Color bg, System.Action act)
        {
            var b = new Button(); b.text = text; b.name = name;
            b.style.position = Position.Absolute; b.style.right = 0; b.style.top = top;
            b.style.width = 22; b.style.fontSize = 11; b.style.whiteSpace = WhiteSpace.Normal;
            b.style.backgroundColor = bg; b.style.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            b.style.paddingLeft = 2; b.style.paddingRight = 2; b.style.paddingTop = 6; b.style.paddingBottom = 6;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.borderTopLeftRadius = 6; b.style.borderBottomLeftRadius = 6; b.style.borderTopRightRadius = 0; b.style.borderBottomRightRadius = 0;
            System.Action safe = () => { try { act(); } catch (Exception ex) { Reader.W("error", name, ex.ToString()); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        internal static void Ensure(VisualElement any)
        {
            if (DateTime.Now < Next) return;
            try { FactSheet.Poll(); } catch { }
            Next = DateTime.Now.AddSeconds(2);
            try
            {
                bool needRead = Reader.ShowTab.Value && (ReadTab == null || ReadTab.panel == null);
                bool needMenu = Reader.MenuTest.Value && Reader.WatchMenu.Value && (MenuTab == null || MenuTab.panel == null);
                if (FactSheet.Owner != null && FactSheet.Owner.panel != null && !needRead && !needMenu) return;
                VisualElement host = null;
                for (var e = any; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { host = e; break; }
                if (host == null) return;
                if (needRead) { ReadTab = Make("R\nE\nA\nD", "FM27MM-ReaderTab", 222, new Color(0.20f, 0.45f, 0.75f, 1f), () => Explorer.Run(ReadTab)); ReadTab.tooltip = "Reader: map and read SI's data (logs to reader_*.tsv)"; host.Add(ReadTab); Reader.W("tab", "READ added", ""); }
                if (needMenu) { MenuTab = Make("S\nI\n\nM\nE\nN\nU", "FM27MM-SiMenuTab", 690, new Color(0.95f, 0.55f, 0.2f, 1f), SiMenu.Replay); MenuTab.tooltip = "Reader test: re-send the last SI menu action through SI's own handler"; host.Add(MenuTab); Reader.W("tab", "SI MENU added", ""); }
                if (FactSheet.Owner == null || FactSheet.Owner.panel == null)
                {
                    // an invisible element of our own, only to schedule the fact reads
                    var o = new VisualElement(); o.name = "FM27MM-ReaderClock"; o.style.position = Position.Absolute; o.style.width = 0; o.style.height = 0; o.pickingMode = PickingMode.Ignore;
                    host.Add(o); bool first = FactSheet.Owner == null; FactSheet.Owner = o;
                    if (first) FactSheet.Soon("first screen", 3000);
                }
            }
            catch (Exception e) { Reader.W("error", "tab", e.GetType().Name + ": " + e.Message); }
        }
    }

    // ---- hooks ----
    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class SetTextPatch
    {
        [ThreadStatic] internal static IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance) { InSet = __instance.Pointer; }
        static void Postfix(SI.Bindable.SIText __instance) { InSet = IntPtr.Zero; Tab.Ensure(__instance); }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class ApplyTextPatch
    {
        static void Postfix(SI.Bindable.SIText __instance) { if (__instance.Pointer == SetTextPatch.InSet) return; Tab.Ensure(__instance); }
    }

    // game -> UI events: a save loaded, a new day
    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class ReceivePatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                if (eventID == (uint)(int)EV.EventFmxLoadedSaveGame) { Reader.W("event", "save loaded", ""); FactSheet.Ask("save loaded", 6000); }
                else if (eventID == (uint)(int)EV.EventFmxDateChange) FactSheet.Ask("new day", 2500);
            }
            catch { }
        }
    }

    // UI -> game events: SI's context actions and the menu audits, in full
    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class SendPatch
    {
        static void Prefix(uint eventID, SI.Interop.InteropReference objectTo, SI.Core.TypedValue data)
        {
            try
            {
                if (!Reader.DeepDumps.Value) return;
                string name; try { name = ((EV)(int)eventID).ToString(); } catch { name = eventID.ToString(); }
                if (eventID != (uint)(int)EV.EventFmxDoAction && !name.StartsWith("EventFmxAudit") && !name.StartsWith("EventFmxSpeakTo") && !name.StartsWith("EventFmxStart")) return;
                if (name == "EventFmxAuditHeartbeat") return;
                string id = ""; try { id = objectTo?.GetPersistentIdentity() ?? ""; } catch { }
                Reader.W("ui-event", name + " " + id, SiMenu.Dump(data, 0));
            }
            catch { }
        }
    }

    // SI's own Actions menu handler, a static method (a new hook: own switch [SI menu] Watch)
    [HarmonyPatch(typeof(FM.Game.PluginContextActionsModule), nameof(FM.Game.PluginContextActionsModule.HandleActionEvent))]
    static class MenuPatch
    {
        static void Prefix(FM.GamePlugin.ActionID actionID, uint eventID, SI.Interop.InteropReference action, SI.Core.TypedValue data, bool checkForConfirmation, Il2CppSystem.Collections.Generic.List<SI.Core.TypedValue> auditTargets)
        {
            try
            {
                string ev; try { ev = ((EV)(int)eventID).ToString(); } catch { ev = eventID.ToString(); }
                string id = ""; try { id = action?.GetPersistentIdentity() ?? ""; } catch { }
                var sb = new StringBuilder();
                if (auditTargets != null) for (int i = 0; i < auditTargets.Count && i < 6; i++) sb.Append("[" + i + "] " + SiMenu.Dump(auditTargets[i], 0) + " ");
                Reader.W("si-menu", actionID + " / " + ev + (SiMenu.Replaying ? " (our replay)" : "") + " / confirm " + checkForConfirmation + " / " + id, "data " + SiMenu.Dump(data, 0) + " | audit " + sb);
                if (!SiMenu.Replaying) { SiMenu.LastAction = actionID; SiMenu.LastEvent = eventID; SiMenu.LastRef = action; SiMenu.LastData = data; SiMenu.LastAudit = auditTargets; SiMenu.Have = true; }
            }
            catch (Exception e) { Reader.W("error", "si-menu hook", e.Message); }
        }
    }
}
