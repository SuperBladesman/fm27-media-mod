// FM27 Media Mod - Treatment Room 0.3.3 (reads the medical screens; the Treatment Room page, the Season Summary panel, stories for the press; sends no commands). MIT licence (see LICENSE).
// Reads the Medical Centre screens and the player's medical pages into a per-player injury ledger, captures the
// physio's forecasts from the league injury table and Current Injuries, notes returns (Recently Returned), logs every
// medical action and news response (rest, reports, specialist choices) and keeps all of it per save.
// It changes nothing in the game. 0.2.0 adds one panel of its own on Season Summary (Panel.cs, Summary.cs) and reads
// Risk Assessment by column (SI's recurring injury, risk level, main risk factor).
// Design: "Design - the treatment room (Nov 2026)" and "Design - treatment room part 2" in the project.
// Hooks: only the six that Press Room has run in game for days: SIText.SetText / ApplyText,
// GameInteropSubsystem.SendEvent, NewsActionHandlerModule.TriggerSpeakToEvent, LoadGameHandlerModule.StartLoadingSave,
// SaveGameHandlerModule.DoSaveGame, GameReceiver.SendEvent. Never a hook on FM's data binding (Probe 0.0.9 crashed FM).
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Treatment Room", Version)]
    public class TreatmentRoom : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.treatmentroom";
        public const string Version = "0.6.6";
        internal static string Dir;
        internal static ConfigEntry<bool> Enabled, LogRows, SeasonPanel, MovePills, RoomOn, MapStaff;
        internal static ConfigEntry<int> SeasonStartMonth, TabTop;
        static StreamWriter LogW;
        static readonly object LogLock = new object();

        public override void Load()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Read the medical screens into the Treatment Room ledger (read-only; changes nothing in the game).");
            LogRows = Config.Bind("Logs", "LogRows", false, "Write every table row read to treatmentroom_<time>.tsv (testing).");
            MapStaff = Config.Bind("Logs", "MapStaffScreen", false, "Log every text on Club > Staff while the Medical Team tab is open, for mapping the screen (testing; 0.4.0).");
            SeasonPanel = Config.Bind("Panel", "SeasonPanel", true, "Show the Treatment Room panel in the empty space on Medical Centre > Season Summary.");
            MovePills = Config.Bind("Panel", "MovePills", true, "Nudge Season Summary's severity pills down to just above the chart, so the panel gets one clear space (drawing only; the game's layout is untouched).");
            RoomOn = Config.Bind("Treatment Room", "Page", true, "The MEDICAL tab at the right edge of the screen and the Treatment Room page it opens (screening notes, watch list, plans, diary, sports science).");
            TabTop = Config.Bind("Treatment Room", "TabTop", 620, "Where the MEDICAL tab starts on the right edge (Press Room's MEDIA tab sits at 120, Heffem's around 560). It moves down by itself past any other tab that would overlap.");
            SeasonStartMonth = Config.Bind("Panel", "SeasonStartMonth", 7, "The month the season starts in (7 = July, the English season). Injuries before it count as last season.");
            Dir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
            Directory.CreateDirectory(Dir);
            try { LogW = new StreamWriter(Path.Combine(Dir, "treatmentroom_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true }; } catch { }
            W("start", "Treatment Room " + Version, Enabled.Value ? "enabled" : "disabled in config");
            Ledger.StartMonth = SeasonStartMonth.Value;
            Ledger.Load();
            var h = new Harmony(GUID); int ok = 0, bad = 0;
            foreach (var t in new[] { typeof(TrTextPatch), typeof(TrTextPatch_Apply), typeof(TrEventPatch), typeof(TrSpeakToPatch), typeof(TrLoadPatch), typeof(TrSavePatch), typeof(TrReceiverPatch) })
            {
                try { h.PatchAll(t); ok++; }
                catch (Exception e) { bad++; W("patch-error", t.Name, e.GetType().Name + ": " + e.Message); Log.LogWarning("Treatment Room: could not patch " + t.Name + ": " + e.Message); }
            }
            W("patches", ok + " ok", bad + " failed");
            Log.LogInfo($"FM27 Media Mod Treatment Room {Version} loaded (injury ledger, forecasts, risk, medical actions, the medical department; Season Summary panel {(SeasonPanel.Value ? "on" : "off")}; Treatment Room page {(RoomOn.Value ? "on" : "off")}; {Ledger.Injuries.Count} injuries on file; patches {ok} ok, {bad} failed).");
        }

        internal static void W(string kind, string a, string b)
        {
            lock (LogLock) try { LogW?.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{GameDate.Text}\t{kind}\t{C(a)}\t{C(b)}"); } catch { }
        }
        internal static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        internal static void Fail(string where, Exception e) => W("error", where, e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\r", " ").Replace("\n", " ").Trim());
    }

    // the in-game date, from the top bar ("TopRow/Day/Month" shows e.g. 11/4/2027)
    internal static class GameDate
    {
        internal static string Text = "";
        static readonly Regex Rx = new Regex(@"^(\d{1,2})/(\d{1,2})/(\d{4})$", RegexOptions.Compiled);
        internal static void Set(string t)
        {
            if (t == Text || !Rx.IsMatch(t)) return;
            Text = t; TreatmentRoom.W("date", t, "");
            // 0.3.2: the press file follows the day (fresh stories only; loading a save alone doesn't save the ledger)
            try { Press.Write(TreatmentRoom.Dir, t, Ledger.StartMonth, TreatmentRoom.C); } catch (Exception e) { TreatmentRoom.Fail("press file", e); }
            try { Room.Refresh(); } catch { }   // 0.3.0: an open Treatment Room page follows the day
        }
        // days since 1/1/2000, for comparing game dates ("d/m/yyyy"); -1 if unreadable
        internal static int Day(string t)
        {
            var m = Rx.Match(t ?? ""); if (!m.Success) return -1;
            try { return (int)(new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)) - new DateTime(2000, 1, 1)).TotalDays; }
            catch { return -1; }
        }
    }

    // ---------- hooks (the same six Press Room uses) ----------
    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class TrTextPatch
    {
        [ThreadStatic] internal static IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance) { try { InSet = __instance.Pointer; } catch { } }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { InSet = IntPtr.Zero; Screens.OnText(__instance, txt); }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class TrTextPatch_Apply
    {
        static void Postfix(SI.Bindable.SIText __instance, string txt)
        {
            try { if (__instance.Pointer == TrTextPatch.InSet) return; } catch { return; }
            Screens.OnText(__instance, txt);
        }
    }

    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class TrEventPatch
    {
        static void Prefix(uint eventID, SI.Interop.InteropReference objectTo, SI.Core.TypedValue data)
        {
            try
            {
                if (!TreatmentRoom.Enabled.Value) return;
                int ev = (int)eventID;
                if (ev != (int)FM.GamePlugin.InteropEventIDs.EventFmxDoAction && ev != (int)FM.GamePlugin.InteropEventIDs.EventFmxNewsResponseAction) return;
                string target = ""; try { target = objectTo == null ? "" : objectTo.GetPersistentIdentity(); } catch { }
                string value = ""; try { value = data == null || data.IsNull ? "" : data.AsString(); } catch { }
                Actions.OnEvent(ev, target, value);
            }
            catch (Exception e) { TreatmentRoom.Fail("event hook", e); }
        }
    }

    [HarmonyPatch(typeof(FM.UI.NewsActionHandlerModule), nameof(FM.UI.NewsActionHandlerModule.TriggerSpeakToEvent))]
    static class TrSpeakToPatch
    {
        static void Prefix(FM.UI.SpeakToActionEvent speakToActionEvent)
        {
            try { if (TreatmentRoom.Enabled.Value) Actions.OnSpeakTo(speakToActionEvent); }
            catch (Exception e) { TreatmentRoom.Fail("speakto hook", e); }
        }
    }

    [HarmonyPatch(typeof(FM.UI.LoadGameHandlerModule), nameof(FM.UI.LoadGameHandlerModule.StartLoadingSave))]
    static class TrLoadPatch { static void Prefix(SI.Core.Record saveRecord) { try { TrMemory.OnStartLoading(saveRecord); } catch (Exception e) { TreatmentRoom.Fail("load hook", e); } } }

    [HarmonyPatch(typeof(FM.UI.SaveGameHandlerModule), nameof(FM.UI.SaveGameHandlerModule.DoSaveGame))]
    static class TrSavePatch { static void Prefix(SI.Core.Record saveRecord) { try { TrMemory.OnDoSave(saveRecord); } catch (Exception e) { TreatmentRoom.Fail("save hook", e); } } }

    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class TrReceiverPatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxSaveGameCompleted) TrMemory.OnSaved();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxLoadedSaveGame) TrMemory.OnLoaded();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxNewGameCreated) TrMemory.OnNewGame();
            }
            catch (Exception e) { TreatmentRoom.Fail("memory event", e); }
        }
    }
}
