// FM27 Media Mod - Treatment Room 0.6.2: real faces from the player's own facepacks, never a made-up one.
// Nik (23 Nov): "pull the facepacks to go alongside those treatment tiles for head physio and head of SS... No real face, don't
// show face." Facepacks are folders of <database unique id>.png under Documents\Sports Interactive\Football Manager 27\graphics.
// Our person keys are the game's record numbers, not database ids, so the id comes from the Reader's fact sheet
// (game facts.tsv: head_physio / head_physio_id, read from Human.Club.HeadPhysio). No file, no face.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FM27MediaMod
{
    internal static class Faces
    {
        static string Graphics => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sports Interactive", "Football Manager 27", "graphics");
        static string FactsFile => Path.Combine(TreatmentRoom.Dir, "game facts.tsv");
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> Missing = new HashSet<string>();
        static List<string> Folders; static DateTime FoldersAt = DateTime.MinValue;
        static readonly HashSet<string> NotFaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "kits", "logos", "backgrounds", "badges", "flags", "trophies" };

        // the fact sheet's value for a key ("" if none)
        internal static string Fact(string key)
        {
            try
            {
                if (!File.Exists(FactsFile)) return "";
                foreach (var line in File.ReadAllLines(FactsFile)) { var a = line.Split('\t'); if (a.Length >= 2 && a[0] == key) return a[1].Trim(); }
            }
            catch { }
            return "";
        }
        // a face for a member of staff the fact sheet names (role: "head_physio"), if the names agree and a facepack has one
        internal static Texture2D ForRole(string role, string name)
        {
            var who = PersonLinks.Plain(Fact(role)).Trim(); var id = Fact(role + "_id");
            if (who.Length == 0 || id.Length == 0 || !Dept.SameName(who, name)) return null;
            return For(id);
        }
        // 0.6.3: any member the fact sheet names (the head physio; the medical lead for reports)
        static readonly string[] Roles = { "head_physio", "medical_lead" };
        internal static Texture2D ForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var r in Roles) { var t = ForRole(r, name); if (t != null) return t; }
            return null;
        }
        internal static Texture2D For(string id)
        {
            try
            {
                id = new string((id ?? "").Where(char.IsDigit).ToArray());
                if (id.Length == 0 || Missing.Contains(id)) return null;
                if (Cache.TryGetValue(id, out var t) && t != null) return t;
                if (Folders == null || (DateTime.Now - FoldersAt).TotalMinutes > 10)
                {
                    Folders = Directory.Exists(Graphics) ? Directory.GetDirectories(Graphics).Where(d => !NotFaces.Contains(Path.GetFileName(d))).ToList() : new List<string>();
                    FoldersAt = DateTime.Now;
                }
                foreach (var d in Folders)
                {
                    var f = Path.Combine(d, id + ".png");
                    if (!File.Exists(f)) continue;
                    var tex = new Texture2D(2, 2);
                    if (ImageConversion.LoadImage(tex, File.ReadAllBytes(f)))
                    {
                        Cache[id] = tex;
                        TreatmentRoom.W("room", "face", id + " from " + Path.GetFileName(d));
                        return tex;
                    }
                }
                Missing.Add(id);
            }
            catch (Exception e) { TreatmentRoom.Fail("face " + id, e); }
            return null;
        }
    }
}
