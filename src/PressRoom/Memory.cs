// FM27 Media Mod - Memory that follows your saves (Press Room 0.8). MIT licence (see LICENSE).
// The mod's memory (on the record, the media office, whispers, deals, grudges, people) lives in BepInEx\FM27MediaMod.
// The game's save files don't carry it, so:
//   - every time the game finishes a save (manual, autosave, save before quit), the memory is copied into
//     BepInEx\FM27MediaMod\saves\<key>, the key being the save file's time and size, which FM keeps when it renames
//     older versions to (v02), (v03);
//   - every time a save finishes loading, the memory saved with that exact file comes back; a save the mod has never
//     seen (a new career, or one from before this version) starts with empty memory.
// The file comes from the game's own load and save handlers (LoadGameHandlerModule.StartLoadingSave and
// SaveGameHandlerModule.DoSaveGame each get a record naming it). If that fails, the mod guesses the newest save and
// says so in the MEDIA OFFICE, where you can pick the right one.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;

namespace FM27MediaMod
{
    internal static class Memory
    {
        static string Root => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");
        static string Store => Path.Combine(Root, "saves");
        static string[] Files => new[] { PressRoom.Record.FileName, "media office.tsv", "whispers.tsv", "deals.tsv", "people.tsv", "grudges.tsv", "fans.tsv", "fan press.tsv", "incidents.tsv", Pack.FileName, Voices.FileName, Medical.StateName, Pairs.StateName, Reactions.FileName };
        static string GamesDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sports Interactive", "Football Manager 27", "games");

        internal static string CurrentPath = "", Status = "";
        // reloading memory touches what the screens read, so it waits for the main thread (the next text the game sets)
        static volatile Action Queued;
        internal static void Pump() { var a = Queued; if (a == null) return; Queued = null; try { a(); } catch (Exception e) { PressRoom.Fail("memory pump", e); } }
        internal static bool Unsure;
        static string PendingSave = "", PendingLoad = ""; static DateTime PendingSaveAt = DateTime.MinValue, PendingLoadAt = DateTime.MinValue;

        internal static string Key(string path)
        {
            try { var fi = new FileInfo(path); return fi.Exists ? fi.LastWriteTimeUtc.ToString("yyyyMMdd'T'HHmmss") + "_" + fi.Length : null; }
            catch { return null; }
        }
        static string Name(string path) => string.IsNullOrEmpty(path) ? "?" : Path.GetFileNameWithoutExtension(path);
        static string FourCC(uint k) { var b = BitConverter.GetBytes(k); var s = new StringBuilder(); foreach (var c in b) s.Append(c >= 32 && c < 127 ? (char)c : '?'); return s.ToString(); }

        // the save file named in the game's own record, logged in full the first times so the fields can be checked
        static int Dumped;
        static string PathIn(SI.Core.Record r, string why)
        {
            string found = null;
            try
            {
                if (r == null) return null;
                foreach (var kv in r)
                {
                    string s = ""; try { s = kv.Value.IsNull ? "" : kv.Value.AsString() ?? ""; } catch { }
                    if (Dumped < 40) { Dumped++; PressRoom.W("memory", why + " field " + FourCC(kv.Key) + " " + kv.Key, s.Length > 160 ? s.Substring(0, 160) : s); }
                    if (found == null && s.EndsWith(".fm", StringComparison.OrdinalIgnoreCase)) found = s;
                    else if (found == null && s.Length > 0 && s.Length < 200 && s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && File.Exists(Path.Combine(GamesDir, s + ".fm"))) found = Path.Combine(GamesDir, s + ".fm");
                }
            }
            catch (Exception e) { PressRoom.Fail("memory record", e); }
            return found;
        }

        internal static void OnStartLoading(SI.Core.Record r)
        {
            var p = PathIn(r, "load");
            PendingLoad = p ?? ""; PendingLoadAt = DateTime.Now;
            PressRoom.W("memory", "loading", p ?? "(no file named in the load record)");
        }
        internal static void OnDoSave(SI.Core.Record r)
        {
            var p = PathIn(r, "save");
            if (p != null) { PendingSave = p; PendingSaveAt = DateTime.Now; }
            PressRoom.W("memory", "saving", p ?? "(no file named in the save record)");
        }

        static string Newest(TimeSpan within)
        {
            try
            {
                if (!Directory.Exists(GamesDir)) return null;
                var f = new DirectoryInfo(GamesDir).GetFiles("*.fm").OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault();
                if (f == null) return null;
                if (within != TimeSpan.Zero && DateTime.UtcNow - f.LastWriteTimeUtc > within) return null;
                return f.FullName;
            }
            catch { return null; }
        }

        // ---------- the game has saved ----------
        internal static void OnSaved()
        {
            try
            {
                string path = null, how = "";
                if (PendingSave.Length > 0 && (DateTime.Now - PendingSaveAt).TotalMinutes < 10 && File.Exists(PendingSave)) { path = PendingSave; how = "named by the game"; }
                else if (CurrentPath.Length > 0 && File.Exists(CurrentPath) && DateTime.UtcNow - new FileInfo(CurrentPath).LastWriteTimeUtc < TimeSpan.FromMinutes(2)) { path = CurrentPath; how = "the game in play"; }
                else { path = Newest(TimeSpan.FromMinutes(2)); how = "newest save file"; }
                PendingSave = "";
                if (path == null) { PressRoom.W("memory", "saved", "could not find the file just saved; memory not copied"); return; }
                var key = Key(path); if (key == null) return;
                CurrentPath = path; Unsure = false;
                var dir = Path.Combine(Store, key); Directory.CreateDirectory(dir);
                foreach (var f in Files) { var src = Path.Combine(Root, f); var dst = Path.Combine(dir, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
                File.WriteAllText(Path.Combine(dir, "save.txt"), Name(path) + "\n" + path + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\nconference " + PressRoom.Record.Conf + "\n", new UTF8Encoding(false));
                Status = "Memory saved with " + Name(path) + ".";
                PressRoom.W("memory", "saved (" + how + ")", Name(path) + " -> saves\\" + key);
            }
            catch (Exception e) { PressRoom.Fail("memory save", e); }
        }

        // ---------- the game has loaded ----------
        internal static void OnLoaded()
        {
            try
            {
                string path; Unsure = false;
                if (PendingLoad.Length > 0 && (DateTime.Now - PendingLoadAt).TotalMinutes < 10) path = PendingLoad;
                else { path = Newest(TimeSpan.Zero); Unsure = true; }
                PendingLoad = "";
                var how = Unsure ? "a guess: the newest save" : "named by the game";
                Queued = () => Use(path, how);
            }
            catch (Exception e) { PressRoom.Fail("memory load", e); }
        }

        static DateTime NewGameAt = DateTime.MinValue;
        internal static void OnNewGame()
        {
            if ((DateTime.Now - NewGameAt).TotalSeconds < 60) return; NewGameAt = DateTime.Now;
            Queued = () => { Backup(); Clear(); ReloadAll(); CurrentPath = ""; Unsure = false; Status = "New game: the press start with no memory of you."; PressRoom.W("memory", "new game", "memory cleared"); };
            try { }
            catch (Exception e) { PressRoom.Fail("memory new", e); }
        }

        // switch the mod's memory to the one kept with this save file
        internal static void Use(string path, string how)
        {
            Backup();
            var key = path != null ? Key(path) : null;
            var dir = key != null ? Path.Combine(Store, key) : null;
            if (dir != null && Directory.Exists(dir))
            {
                foreach (var f in Files) { var src = Path.Combine(dir, f); var dst = Path.Combine(Root, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
                Status = "Memory for " + Name(path) + " (" + how + ").";
            }
            else
            {
                Clear();
                Status = "No memory kept with " + Name(path) + " yet (" + how + "): the press start fresh.";
            }
            CurrentPath = path ?? "";
            ReloadAll();
            PressRoom.W("memory", "loaded (" + how + ")", Name(path) + (dir != null && Directory.Exists(dir) ? " <- saves\\" + key : " (none kept, cleared)"));
        }

        // the live files as they were before a switch, in case a guess was wrong
        static void Backup()
        {
            try
            {
                var dir = Path.Combine(Store, "_before last load"); Directory.CreateDirectory(dir);
                foreach (var f in Files) { var src = Path.Combine(Root, f); if (File.Exists(src)) File.Copy(src, Path.Combine(dir, f), true); }
            }
            catch (Exception e) { PressRoom.Fail("memory backup", e); }
        }
        static void Clear() { foreach (var f in Files) File.WriteAllText(Path.Combine(Root, f), ""); }

        static void ReloadAll()
        {
            PressRoom.Record.All.Clear(); PressRoom.Record.Conf = 0; PressRoom.Record.ThisConf = 0; PressRoom.Record.ConfAt = DateTime.MinValue; PressRoom.Record.Load();
            PressRoom.Grudges.All.Clear(); PressRoom.Grudges.Load();
            PressRoom.Led.Clear(); PressRoom.LedText.Clear(); PressRoom.DecidedQ = null;
            MediaOffice.Reset(); MediaOffice.Load();
            Word.Reset(); Word.Load();
            Deals.Reset(); Deals.Load();
            Incidents.Reset(); Incidents.Load();
            Reactions.Reset(); Reactions.Load();
            Medical.Reset(); Medical.Load();
            Pairs.Reset(); Pairs.Load();
            Pack.Reset(); Pack.Load();
            Voices.Reset(); Voices.LoadState();
            MediaOffice.Refresh();
        }

        // saves the mod has memory for, newest first (for the chooser)
        internal static List<(string key, string name, string when)> Kept()
        {
            var list = new List<(string, string, string)>();
            try
            {
                if (!Directory.Exists(Store)) return list;
                foreach (var d in new DirectoryInfo(Store).GetDirectories().OrderByDescending(x => x.Name))
                {
                    if (d.Name.StartsWith("_")) continue;
                    var meta = Path.Combine(d.FullName, "save.txt");
                    var lines = File.Exists(meta) ? File.ReadAllLines(meta) : new string[0];
                    list.Add((d.Name, lines.Length > 0 ? lines[0] : d.Name, lines.Length > 2 ? lines[2] : ""));
                }
            }
            catch { }
            return list;
        }
        internal static void Choose(string key)
        {
            try
            {
                var dir = Path.Combine(Store, key); if (!Directory.Exists(dir)) return;
                Backup();
                foreach (var f in Files) { var src = Path.Combine(dir, f); var dst = Path.Combine(Root, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
                var meta = Path.Combine(dir, "save.txt"); var lines = File.Exists(meta) ? File.ReadAllLines(meta) : new string[0];
                if (lines.Length > 1) CurrentPath = lines[1];
                Unsure = false; Status = "Memory for " + (lines.Length > 0 ? lines[0] : key) + " (chosen by you).";
                ReloadAll();
                PressRoom.W("memory", "chosen", key);
            }
            catch (Exception e) { PressRoom.Fail("memory choose", e); }
        }
    }

    [HarmonyPatch(typeof(FM.UI.LoadGameHandlerModule), nameof(FM.UI.LoadGameHandlerModule.StartLoadingSave))]
    static class MemoryLoadPatch { static void Prefix(SI.Core.Record saveRecord) { try { Memory.OnStartLoading(saveRecord); } catch (Exception e) { PressRoom.Fail("load hook", e); } } }

    [HarmonyPatch(typeof(FM.UI.SaveGameHandlerModule), nameof(FM.UI.SaveGameHandlerModule.DoSaveGame))]
    static class MemorySavePatch { static void Prefix(SI.Core.Record saveRecord) { try { Memory.OnDoSave(saveRecord); } catch (Exception e) { PressRoom.Fail("save hook", e); } } }

    // the game tells the screens when a save or load has finished
    [HarmonyPatch(typeof(FM.GamePlugin.GameReceiver), nameof(FM.GamePlugin.GameReceiver.SendEvent))]
    static class MemoryReceiverPatch
    {
        static void Prefix(uint eventID)
        {
            try
            {
                int ev = (int)eventID;
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxSaveGameCompleted) Memory.OnSaved();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxLoadedSaveGame) Memory.OnLoaded();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxNewGameCreated) Memory.OnNewGame();
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxDateChange) Desk.OnDayChange();   // 0.13: the office's week
            }
            catch (Exception e) { PressRoom.Fail("memory event", e); }
        }
    }
}
