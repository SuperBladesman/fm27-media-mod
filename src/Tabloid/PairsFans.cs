// FM27 Media Mod - Tabloid 0.6.8: the Terraces notice your partnerships (Partnerships design, stage 5).
// The Partnerships plugin (a sister mod) writes "partnership press.tsv" in the mod's state folder: fresh stories about pairs
// SI recognises in YOUR side (split up or back together in the last match, newly recognised, starting to click, breaking
// down). Under one of our own match stories (a win, a draw or a defeat), one fan post now and then talks about one of them,
// in the fans' words, with the result deciding the mood. At most one such post per story and twice per pair story. Players
// are named (from the line-up and SI's own partnership lines); fans are invented; nothing gendered. No file, nothing happens.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace FM27MediaMod
{
    internal static class PairsFans
    {
        internal static bool Enabled = true;
        internal static int Share = 45;   // percent of our match stories that get one partnership post
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "partnership press.tsv");
        class It { public string Id = "", Kind = "", A = "", B = "", Zone = ""; }
        static List<It> Items = new List<It>();
        static DateTime Stamp = DateTime.MinValue;
        static readonly Dictionary<string, int> Uses = new Dictionary<string, int>();

        static void Fresh()
        {
            try
            {
                if (!File.Exists(FilePath)) { Items = new List<It>(); return; }
                var t = File.GetLastWriteTimeUtc(FilePath); if (t == Stamp) return; Stamp = t;
                var l = new List<It>(); bool first = true;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    if (first) { first = false; continue; }
                    var a = line.Split('\t'); if (a.Length < 5) continue;
                    l.Add(new It { Id = a[0], Kind = a[1], A = a[2], B = a[3], Zone = a[4] });
                }
                Items = l;
            }
            catch { Items = new List<It>(); }
        }

        // [kind/result] -> lines; {A} {B} {zone}
        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            ["split/defeat"] = new[] { "Why split up {A} and {B}? All over the place {zone} without them together.", "Never break up {A} and {B} {zone} again. Simple as." },
            ["split/draw"] = new[] { "Missed {A} and {B} together {zone}. Put them back.", "{A} and {B} apart {zone} and it showed." },
            ["split/win"] = new[] { "Even without {A} and {B} together {zone} we got the job done. Fair play.", "Didn't miss {A} and {B} as a pair {zone} today. Good problem to have." },
            ["reunited/win"] = new[] { "{A} and {B} back together {zone} and look at that. Keep them there.", "Told you. {A} and {B} {zone}. Never split them up again." },
            ["reunited/draw"] = new[] { "Good to see {A} and {B} together {zone} again. Build from that." },
            ["reunited/defeat"] = new[] { "At least {A} and {B} are back together {zone}. Small mercies." },
            ["clicking/win"] = new[] { "{A} and {B} {zone} are turning into a proper partnership.", "Loving {A} and {B} {zone} at the minute." },
            ["clicking/draw"] = new[] { "Plus side: {A} and {B} are really clicking {zone}." },
            ["clicking/defeat"] = new[] { "Only positive today: {A} and {B} {zone} are really clicking." },
            ["telepathic/win"] = new[] { "{A} and {B} {zone}. Telepathic.", "{A} and {B} don't even need to look for each other any more." },
            ["telepathic/draw"] = new[] { "{A} and {B} {zone} are on another level together." },
            ["telepathic/defeat"] = new[] { "Can't fault {A} and {B} {zone}. Best partnership we've got." },
            ["new/win"] = new[] { "Starting to like {A} and {B} together {zone}." },
            ["new/draw"] = new[] { "{A} and {B} {zone} look like they're starting to get each other." },
            ["new/defeat"] = new[] { "Something there with {A} and {B} {zone}, even on a day like this." },
            ["breaking/win"] = new[] { "Result's fine but {A} and {B} {zone} don't look right together lately." },
            ["breaking/draw"] = new[] { "{A} and {B} {zone} just aren't on the same page any more." },
            ["breaking/defeat"] = new[] { "{A} and {B} don't look on the same page any more {zone}. Something's not right." },
        };

        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }

        // one post's words for our match story, or null
        internal static string Line(Story s, int slot, uint h, HashSet<string> used)
        {
            try
            {
                if (!Enabled || s == null || !s.Ours || slot < 1) return null;
                if (s.Kind != "win" && s.Kind != "draw" && s.Kind != "defeat") return null;
                if (used != null && used.Contains("pairs-post")) return null;   // one per story
                if (Hash(s.Head + "|pairs") % 100 >= (uint)Share) return null;   // stable per story
                Fresh();
                foreach (var x in Items)
                {
                    if (x.A.Length == 0 || x.B.Length == 0 || x.Zone.Length == 0) continue;
                    if (Uses.TryGetValue(x.Id, out var n) && n >= 2) continue;
                    if (!Lines.TryGetValue(x.Kind + "/" + s.Kind, out var pool)) continue;
                    var raw = pool[(h / 13) % (uint)pool.Length];
                    var text = raw.Replace("{A}", x.A).Replace("{B}", x.B).Replace("{zone}", x.Zone);
                    if (text.IndexOf('{') >= 0) continue;
                    Uses[x.Id] = (Uses.TryGetValue(x.Id, out var m) ? m : 0) + 1;
                    used?.Add("pairs-post");
                    return text;
                }
            }
            catch { }
            return null;
        }
    }
}
