// FM27 Media Mod - Partnerships 0.2.1 (reads only; draws its own page, and lines on SI's pitch from our own PAIRS button). MIT licence (see LICENSE).
// Makes SI's player partnerships readable, on a seven-step scale anchored to SI's own bands:
//   - every pair SI recognises (the lines on the tactics pitch: FMTacticsPanel's partnership rows, read, never changed),
//   - every partnership a player states on Personal > Happiness ("Feels he has a decent partnership at the back with ..."),
//   - our own count from the match screen: who started together, who was on the pitch together, their ratings.
// The PAIRS tab at the right edge opens the Partnerships page: your XI on a pitch of our own with a line per pair,
// a card for the pair you pick (with their match-by-match record), and every pair seen so far. With SI's own lines
// switched off, our lines go on the tactics pitch itself (PitchOverlay.cs). Fresh pair stories (split up, back together,
// newly recognised, clicking, breaking down) go to "partnership press.tsv" for Press Room and Tabloid.
// Everything is kept per save, beside Press Room's files.
// Design: "Design - the partnerships view (Nov 2026)"; evidence: "Player partnerships - what FM27 holds (Nov 2026)".
// Hooks: only five that Press Room and the Treatment Room have run in game for days: SIText.SetText / ApplyText,
// LoadGameHandlerModule.StartLoadingSave, SaveGameHandlerModule.DoSaveGame, GameReceiver.SendEvent.
// Never a hook on FM's data binding (Probe 0.0.9 crashed FM); never SI's position records (m_positions, a struct array).
using System;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Partnerships", Version)]
    public class Partnerships : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.partnerships";
        public const string Version = "0.3.1";
        internal static string Dir;
        internal static ConfigEntry<bool> Enabled, PairsTab, ShowScores, ShowUntested, LogReads, OnSiPitch, ShowOnSiPitch;
        internal static ConfigEntry<int> TabTop;
        static StreamWriter LogW;
        static readonly object LogLock = new object();

        public override void Load()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Read SI's partnership lines, players' partnership feelings and the match screen into the Partnerships ledger (read-only; changes nothing in the game).");
            PairsTab = Config.Bind("Page", "PairsTab", true, "Show the PAIRS tab at the right edge of the screen (it opens the Partnerships page).");
            TabTop = Config.Bind("Page", "TabTop", 440, "Where the PAIRS tab starts on the right edge; it moves down past any other tab there (MEDIA, READ, SI ACTIONS, MEDICAL, Heffem's).");
            ShowUntested = Config.Bind("Page", "ShowUntested", true, "Draw a faint dotted line between neighbours in your XI that SI doesn't yet recognise as a partnership.");
            OnSiPitch = Config.Bind("Pitch", "OnSiPitch", true, "0.2.1: put a small PAIRS button in the tactics pitch's bottom-left corner that shows our seven-step lines on the pitch itself (clicks on the pitch pass straight through the lines).");
            ShowOnSiPitch = Config.Bind("Pitch", "ShowOnSiPitch", false, "Whether the lines on the tactics pitch are showing (the PAIRS button on the pitch flips this). Switch SI's own Player Relationships off in the eye menu so the two don't stack.");
            ShowScores = Config.Bind("Page", "ShowScores", false, "Show the raw values behind each pair (SI's line level, the step number, our counts). Off keeps it in words.");
            var minTracked = Config.Bind("Steps", "MinTracked", 5, "Matches seen together before our own counts can split SI's band into its lower or upper step (until then, SI recognising a pair puts it in the upper step).");
            var upperShare = Config.Bind("Steps", "UpperShare", 0.5, "Share of the matches seen that a pair must have started together to reach the upper step of SI's band.");
            var upperRating = Config.Bind("Steps", "UpperRating", 6.85, "...and their average rating together (once three matches are rated).");
            var fledglingStarts = Config.Bind("Steps", "FledglingStarts", 3, "Starts together, with no partnership from SI yet, before a pair reads as Fledgling.");
            var fledglingRating = Config.Bind("Steps", "FledglingRating", 6.70, "...and their average rating together, when rated.");
            var trendDays = Config.Bind("States", "TrendDays", 28, "Game days back that 'rising' and 'falling' compare with.");
            var breakingDays = Config.Bind("States", "BreakingDownDays", 42, "A pair that falls two steps from Clicking or better within this many game days reads as Breaking down.");
            LogReads = Config.Bind("Logs", "LogReads", false, "Write every pitch read to partnerships_<time>.tsv (testing).");
            PtLedger.MinTracked = minTracked.Value; PtLedger.UpperShare = upperShare.Value; PtLedger.UpperRating = upperRating.Value;
            PtLedger.FledglingStarts = fledglingStarts.Value; PtLedger.FledglingRating = fledglingRating.Value;
            PtLedger.TrendDays = trendDays.Value; PtLedger.BreakingDays = breakingDays.Value;

            Dir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
            Directory.CreateDirectory(Dir);
            try { LogW = new StreamWriter(Path.Combine(Dir, "partnerships_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true }; } catch { }
            PtLedger.Dir = Dir;
            PtLedger.Log = (k, a, b) => W(k, a, b);
            W("start", "Partnerships " + Version, Enabled.Value ? "enabled" : "disabled in config");
            PtLedger.Load();
            PtMemory.Switched += () => { PtScreens.Reset(); PairsPage.Refresh(); };
            PtDate.Changed = () => { if (PtDate.Text.Length > 0) PtLedger.WritePress(PtDate.Text); };   // "today" becomes "last time out"
            var h = new Harmony(GUID); int ok = 0, bad = 0;
            foreach (var t in new[] { typeof(PtTextPatch), typeof(PtTextPatch_Apply), typeof(PtLoadPatch), typeof(PtSavePatch), typeof(PtReceiverPatch) })
            {
                try { h.PatchAll(t); ok++; }
                catch (Exception e) { bad++; W("patch-error", t.Name, e.GetType().Name + ": " + e.Message); Log.LogWarning("Partnerships: could not patch " + t.Name + ": " + e.Message); }
            }
            W("patches", ok + " ok", bad + " failed");
            Log.LogInfo($"FM27 Media Mod Partnerships {Version} loaded ({PtLedger.Pairs.Count} pairs, {PtLedger.TrackedMatches} matches on file; PAIRS tab {(PairsTab.Value ? "on" : "off")}; patches {ok} ok, {bad} failed).");
        }

        internal static void W(string kind, string a, string b)
        {
            lock (LogLock) try { LogW?.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{PtDate.Text}\t{kind}\t{C(a)}\t{C(b)}"); } catch { }
        }
        internal static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        internal static void Fail(string where, Exception e) => W("error", where, e.GetType().Name + ": " + e.Message);
    }

    // ---------- hooks (the same ones Press Room and the Treatment Room use) ----------
    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class PtTextPatch
    {
        [ThreadStatic] internal static IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance) { try { InSet = __instance.Pointer; } catch { } }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { InSet = IntPtr.Zero; PtScreens.OnText(__instance, txt); }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class PtTextPatch_Apply
    {
        static void Postfix(SI.Bindable.SIText __instance, string txt)
        {
            try { if (__instance.Pointer == PtTextPatch.InSet) return; } catch { return; }
            PtScreens.OnText(__instance, txt);
        }
    }

    [HarmonyPatch(typeof(FM.UI.LoadGameHandlerModule), nameof(FM.UI.LoadGameHandlerModule.StartLoadingSave))]
    static class PtLoadPatch { static void Prefix(SI.Core.Record saveRecord) { try { PtMemory.OnStartLoading(saveRecord); } catch (Exception e) { Partnerships.Fail("load hook", e); } } }

    [HarmonyPatch(typeof(FM.UI.SaveGameHandlerModule), nameof(FM.UI.SaveGameHandlerModule.DoSaveGame))]
    static class PtSavePatch { static void Prefix(SI.Core.Record saveRecord) { try { PtMemory.OnDoSave(saveRecord); } catch (Exception e) { Partnerships.Fail("save hook", e); } } }

    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class PtReceiverPatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxSaveGameCompleted) PtMemory.OnSaved();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxLoadedSaveGame) PtMemory.OnLoaded();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxNewGameCreated) PtMemory.OnNewGame();
            }
            catch (Exception e) { Partnerships.Fail("memory event", e); }
        }
    }
}
