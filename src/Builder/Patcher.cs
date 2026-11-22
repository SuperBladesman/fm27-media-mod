// FM27 Media Mod builder: the BepInEx preloader patcher. It runs before FM starts reading its data, builds the mod's
// language file and media changes from the player's own copy of FM27 when needed, and logs what it did to the BepInEx
// console and to BepInEx\FM27MediaMod\builder.log. It patches no code.
using System;
using System.IO;
using BepInEx;
using BepInEx.Preloader.Core.Patching;

namespace FM27MediaMod.Builder
{
    [PatcherPluginInfo("uk.nik.fm27mediamod.builder", "FM27 Media Mod Builder", "1.0.1")]
    public class BuilderPatcher : BasePatcher
    {
        // data\ beside this DLL (BepInEx\patchers\FM27MediaMod Builder\data), wherever BepInEx loaded it from
        static string DataFolder()
        {
            try { var loc = typeof(BuilderPatcher).Assembly.Location; if (!string.IsNullOrEmpty(loc)) { var d = Path.Combine(Path.GetDirectoryName(loc), "data"); if (Directory.Exists(d)) return d; } } catch { }
            return Path.Combine(Paths.PatcherPluginPath, "FM27MediaMod Builder", "data");
        }

        public override void Initialize()
        {
            var logFile = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "builder.log");
            void Say(string m)
            {
                Log.LogInfo(m);
                try { Directory.CreateDirectory(Path.GetDirectoryName(logFile)); File.AppendAllText(logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + m + Environment.NewLine); } catch { }
            }
            try
            {
                var enabled = Config.Bind("Builder", "Enabled", true, "Build the mod's language file and media changes from your own FM27 files when needed (first run, after an SI patch, after a mod update).").Value;
                if (!enabled) { Say("builder switched off in the config"); return; }
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var langDir = Config.Bind("Builder", "LanguagesFolder", "", "FM27's languages folder. Empty = Documents\\Sports Interactive\\Football Manager 27\\languages.").Value;
                if (string.IsNullOrWhiteSpace(langDir)) langDir = Path.Combine(docs, "Sports Interactive", "Football Manager 27", "languages");
                var s = new BuildSettings
                {
                    GameDir = Paths.GameRootPath,
                    DataDir = DataFolder(),
                    StateDir = Path.Combine(Paths.BepInExRootPath, "FM27MediaMod"),
                    LanguagesDir = langDir,
                    CachesDir = Path.Combine(local, "Sports Interactive", "Football Manager 27", "Caches"),
                    Media = Config.Bind("Builder", "Media", true, "Patch media.fmf and simatch.fmf (questions, chats, commentary).").Value,
                    Language = Config.Bind("Builder", "Language", true, "Build the mod's language file.").Value,
                    MoveCaches = Config.Bind("Builder", "MoveCachesAfterMediaBuild", true, "After a media build, move FM's caches aside (into _to_delete beside them) so FM rebuilds them.").Value,
                };
                var t = System.Diagnostics.Stopwatch.StartNew();
                Say("FM27 Media Mod Builder 1.0.1 starting");
                BuildRunner.Run(s, Say);
                Say($"builder finished in {t.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception e) { Say("builder failed: " + e); }
        }
    }
}
