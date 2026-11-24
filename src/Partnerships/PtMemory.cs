// FM27 Media Mod - Partnerships: memory that follows your saves (the same scheme as Press Room's and the Treatment Room's).
// Every time the game finishes a save, the Partnerships files (PtLedger.Files) are copied into
// BepInEx\FM27MediaMod\saves\<key>, the key being the save file's UTC write time and size (the same key the other mods use,
// so all their files sit side by side). When a save finishes loading, the files kept with it come back; a save never
// seen starts empty. A new game starts empty. The switch runs on the main thread (the next text the game sets).
using System;
using System.IO;
using System.Linq;

namespace FM27MediaMod
{
    internal static class PtMemory
    {
        static string Root => Partnerships.Dir;
        static string Store => Path.Combine(Root, "saves");
        static string GamesDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sports Interactive", "Football Manager 27", "games");

        static volatile Action Queued;
        internal static void Pump() { var a = Queued; if (a == null) return; Queued = null; try { a(); } catch (Exception e) { Partnerships.Fail("memory pump", e); } }

        static string PendingSave = "", PendingLoad = "", CurrentPath = "";
        static DateTime PendingSaveAt = DateTime.MinValue, PendingLoadAt = DateTime.MinValue, NewGameAt = DateTime.MinValue;
        internal static event Action Switched = () => { };

        internal static string Key(string path)
        {
            try { var fi = new FileInfo(path); return fi.Exists ? fi.LastWriteTimeUtc.ToString("yyyyMMdd'T'HHmmss") + "_" + fi.Length : null; }
            catch { return null; }
        }
        static string Name(string path) => string.IsNullOrEmpty(path) ? "?" : Path.GetFileNameWithoutExtension(path);

        static string PathIn(SI.Core.Record r)
        {
            try
            {
                if (r == null) return null;
                foreach (var kv in r)
                {
                    string s = ""; try { s = kv.Value.IsNull ? "" : kv.Value.AsString() ?? ""; } catch { }
                    if (s.EndsWith(".fm", StringComparison.OrdinalIgnoreCase)) return s;
                    if (s.Length > 0 && s.Length < 200 && s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && File.Exists(Path.Combine(GamesDir, s + ".fm"))) return Path.Combine(GamesDir, s + ".fm");
                }
            }
            catch (Exception e) { Partnerships.Fail("memory record", e); }
            return null;
        }

        internal static void OnStartLoading(SI.Core.Record r) { var p = PathIn(r); PendingLoad = p ?? ""; PendingLoadAt = DateTime.Now; Partnerships.W("memory", "loading", p ?? "(no file named)"); }
        internal static void OnDoSave(SI.Core.Record r) { var p = PathIn(r); if (p != null) { PendingSave = p; PendingSaveAt = DateTime.Now; } Partnerships.W("memory", "saving", p ?? "(no file named)"); }

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

        internal static void OnSaved()
        {
            try
            {
                string path, how;
                if (PendingSave.Length > 0 && (DateTime.Now - PendingSaveAt).TotalMinutes < 10 && File.Exists(PendingSave)) { path = PendingSave; how = "named by the game"; }
                else if (CurrentPath.Length > 0 && File.Exists(CurrentPath) && DateTime.UtcNow - new FileInfo(CurrentPath).LastWriteTimeUtc < TimeSpan.FromMinutes(2)) { path = CurrentPath; how = "the game in play"; }
                else { path = Newest(TimeSpan.FromMinutes(2)); how = "newest save file"; }
                PendingSave = "";
                if (path == null) { Partnerships.W("memory", "saved", "could not find the file just saved; not copied"); return; }
                var key = Key(path); if (key == null) return;
                CurrentPath = path;
                PtLedger.Save();
                var dir = Path.Combine(Store, key); Directory.CreateDirectory(dir);
                foreach (var f in PtLedger.Files) { var src = Path.Combine(Root, f); var dst = Path.Combine(dir, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
                Partnerships.W("memory", "saved (" + how + ")", Name(path) + " -> saves\\" + key);
            }
            catch (Exception e) { Partnerships.Fail("memory save", e); }
        }

        internal static void OnLoaded()
        {
            try
            {
                string path, how;
                if (PendingLoad.Length > 0 && (DateTime.Now - PendingLoadAt).TotalMinutes < 10) { path = PendingLoad; how = "named by the game"; }
                else { path = Newest(TimeSpan.Zero); how = "a guess: the newest save"; }
                PendingLoad = "";
                Queued = () => Use(path, how);
            }
            catch (Exception e) { Partnerships.Fail("memory load", e); }
        }

        internal static void OnNewGame()
        {
            if ((DateTime.Now - NewGameAt).TotalSeconds < 60) return; NewGameAt = DateTime.Now;
            Queued = () => { Backup(); PtLedger.Clear(); PtLedger.Save(); CurrentPath = ""; Partnerships.W("memory", "new game", "ledger cleared"); Switched(); };
        }

        static void Use(string path, string how)
        {
            Backup();
            var key = path != null ? Key(path) : null;
            var dir = key != null ? Path.Combine(Store, key) : null;
            if (dir != null && Directory.Exists(dir) && PtLedger.Files.Any(f => File.Exists(Path.Combine(dir, f))))
            {
                foreach (var f in PtLedger.Files) { var src = Path.Combine(dir, f); var dst = Path.Combine(Root, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
                PtLedger.Load();
                Partnerships.W("memory", "loaded (" + how + ")", Name(path) + " <- saves\\" + key + $" ({PtLedger.Pairs.Count} pairs, {PtLedger.TrackedMatches} matches)");
            }
            else
            {
                PtLedger.Clear(); PtLedger.Save();
                Partnerships.W("memory", "loaded (" + how + ")", Name(path) + (key != null ? " (" + key + ")" : "") + ": nothing kept yet, the ledger starts empty (it fills from the screens)");
            }
            CurrentPath = path ?? "";
            Switched();
        }

        static void Backup()
        {
            try
            {
                var dir = Path.Combine(Store, "_partnerships before last load"); Directory.CreateDirectory(dir);
                foreach (var f in PtLedger.Files) { var src = Path.Combine(Root, f); if (File.Exists(src)) File.Copy(src, Path.Combine(dir, f), true); }
            }
            catch (Exception e) { Partnerships.Fail("memory backup", e); }
        }
    }
}
