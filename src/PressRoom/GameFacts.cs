// FM27 Media Mod - Press Room 0.15.0: the Reader's fact sheet (BepInEx\FM27MediaMod\game facts.tsv: fact, value, path, read at).
// Read when it changes; a missing file or fact gives "". The Press Office uses the next opponent and the league position.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace FM27MediaMod
{
    internal static class GameFacts
    {
        static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        static DateTime Stamp = DateTime.MinValue, Checked = DateTime.MinValue;
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "game facts.tsv");

        internal static string Get(string key)
        {
            Fresh();
            return Values.TryGetValue(key, out var v) ? v : "";
        }
        internal static int Int(string key) => int.TryParse(Get(key), out var n) ? n : -1;

        static void Fresh()
        {
            if ((DateTime.Now - Checked).TotalSeconds < 5) return;
            Checked = DateTime.Now;
            try
            {
                if (!File.Exists(FilePath)) { Values.Clear(); return; }
                var t = File.GetLastWriteTimeUtc(FilePath);
                if (t == Stamp) return;
                Stamp = t; Values.Clear();
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t');
                    if (p.Length < 2 || p[0] == "fact") continue;
                    Values[p[0]] = p[1].Trim();
                }
            }
            catch (Exception e) { PressRoom.Fail("game facts", e); }
        }
    }
}
