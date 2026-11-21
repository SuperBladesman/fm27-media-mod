// FM27 Media Mod - Press Room: Voices (0.11.0; 0.12.0 journalist voice banks)
// The language marks some lines with an invisible id tag at the end: U+2063, the text id in base 4 written with
// U+200B U+200C U+200D U+2060, U+2063. This module
//  - strips every tag from every text the game shows (so a tag can never show, whatever the font does);
//  - logs which marked ids were shown (so we learn which lines players actually see);
//  - swaps a marked answer for the chosen persona's wording, and a marked question for the asking journalist's
//    voice (hard for attack dogs and contrarians, warm for cheerleaders), filling every token from the text SI drew.
// Variants live in voices.tsv beside the DLL: id <tab> key <tab> text, where key is base, persona:<name> or
// journo:<voice>. 0.12.0: a question can hold many versions, by the asking outlet (redtop, phonein, broadsheet,
// broadcast, radio, local, away, digital) and the journalist's character (attack, contra, cheer, oldschool, insider;
// hard and warm as before). Candidates = the character's versions + the outlet's versions + the language's own
// wording; the one used least in this save is shown, so the same version doesn't come round again for a long time. The base row is the language's own text for that id; a variant may use only its tokens.
// The persona is kept per save in BepInEx\FM27MediaMod\voice.tsv. Default = the language's own voice.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Voices
    {
        internal static bool On = true, JournoOn = true;
        internal static string Persona = "default";
        internal const string FileName = "voice.tsv";
        internal static readonly string[] Personas = { "default", "firebrand", "professor", "arm" };
        internal static string Label(string p)
        {
            switch (p)
            {
                case "firebrand": return "Firebrand";
                case "professor": return "Professor";
                case "arm": return "Arm round the shoulder";
                default: return "Your own (Resilient & Convincing)";
            }
        }
        internal static readonly Dictionary<string, int> QId = new Dictionary<string, int>();   // shown question text -> its id
        static readonly HashSet<long> Logged = new HashSet<long>();
        internal static readonly Dictionary<string, int> Used = new Dictionary<string, int>();   // id:hash -> times shown (per save)
        static string UKey(int id, string v) => id + ":" + VoiceCore.Hash(v).ToString("x8");
        internal static int Shown, Swapped;
        static string Root => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");

        internal static void Init()
        {
            try
            {
                var dir = Path.GetDirectoryName(typeof(Voices).Assembly.Location);
                VoiceCore.LoadFile(Path.Combine(dir ?? "", "voices.tsv"));
            }
            catch (Exception e) { PressRoom.Fail("voices load", e); }
            LoadState();
        }
        internal static void Reset() { Persona = "default"; QId.Clear(); Logged.Clear(); Used.Clear(); }
        internal static void LoadState()
        {
            try
            {
                var f = Path.Combine(Root, FileName);
                if (!File.Exists(f)) return;
                foreach (var l in File.ReadAllLines(f, Encoding.UTF8))
                {
                    var p = l.Split('\t');
                    if (p.Length >= 2 && p[0] == "persona" && Array.IndexOf(Personas, p[1]) >= 0) Persona = p[1];
                    else if (p.Length >= 3 && p[0] == "used" && int.TryParse(p[2], out var n)) Used[p[1]] = n;
                }
            }
            catch (Exception e) { PressRoom.Fail("voice state", e); }
        }
        internal static void SetPersona(string p)
        {
            if (Array.IndexOf(Personas, p) < 0) return;
            Persona = p;
            Save();
            PressRoom.W("voices", "persona", p);
        }
        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Root);
                var sb = new StringBuilder("persona\t" + Persona + "\n");
                foreach (var kv in Used) sb.Append("used\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                File.WriteAllText(Path.Combine(Root, FileName), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { PressRoom.Fail("voice save", e); }
        }

        static bool Under(VisualElement e, string name, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent) if (e.name == name) return true;
            return false;
        }

        // called first in the text prefix for any text holding a tag
        internal static string OnShown(VisualElement el, string txt)
        {
            var clean = VoiceCore.Strip(txt, out var id);
            if (id < 0) return clean;
            string where = "other";
            try
            {
                if (Under(el, "SpeakToMeetingAnswerActionButton", 16)) where = "answer";
                else if (Under(el, "BubbleBackground", 3) && Under(el, "NPCBubbleFrame", 6)) where = "question";
                long key = ((long)PressRoom.Record.Conf << 32) | (uint)id;
                if (Logged.Add(key)) { Shown++; PressRoom.W("voices", "shown " + where, id + "\t" + PressRoom.Plain(clean)); }
                if (!On) return clean;
                if (where == "question") { QId[clean] = id; while (QId.Count > 200) QId.Clear(); return clean; }
                if (where == "answer" && Persona != "default")
                {
                    var v = VoiceCore.Fill(id, "persona:" + Persona, clean, (uint)(PressRoom.Record.Conf * 31 + id), out var why);
                    if (v != null) { Swapped++; PressRoom.W("voices", "persona " + Persona, id + "\t" + PressRoom.Plain(v)); return v; }
                    if (VoiceCore.Has(id, "persona:" + Persona)) PressRoom.W("voices", "persona miss", id + "\t" + why);
                }
            }
            catch (Exception e) { PressRoom.Fail("voices", e); }
            return clean;
        }

        // the asking journalist's own way of putting a marked question; null = the language's wording stays
        internal static string[] CharKeys(Pack.Trait t)
        {
            switch (t)
            {
                case Pack.Trait.AttackDog: return new[] { "journo:attack", "journo:hard" };
                case Pack.Trait.Contrarian: return new[] { "journo:contra", "journo:hard" };
                case Pack.Trait.Cheerleader: return new[] { "journo:cheer", "journo:warm" };
                case Pack.Trait.OldSchool: return new[] { "journo:oldschool" };
                case Pack.Trait.Insider: return new[] { "journo:insider" };
                default: return new string[0];
            }
        }
        internal static string[] OutletKeys(Pack.J j)
        {
            var k = PressRoom.Classify(j.Outlet);
            switch (k)
            {
                case PressRoom.Kind.RedTop: return new[] { "journo:redtop" };
                case PressRoom.Kind.PhoneIn: return new[] { "journo:phonein", "journo:redtop" };
                case PressRoom.Kind.Broadsheet: return new[] { "journo:broadsheet" };
                case PressRoom.Kind.Broadcast: return (j.Outlet ?? "").IndexOf("Radio", StringComparison.OrdinalIgnoreCase) >= 0 ? new[] { "journo:radio", "journo:broadcast" } : new[] { "journo:broadcast" };
                case PressRoom.Kind.Local: return j.OurTown ? new[] { "journo:local" } : j.Town.Length > 0 ? new[] { "journo:away", "journo:broadcast", "journo:broadsheet" } : new[] { "journo:local" };
                default: return new[] { "journo:digital", "journo:phonein" };
            }
        }
        internal static string ForQuestion(string shown, string journalist)
        {
            try
            {
                if (!On || !JournoOn || !QId.TryGetValue(shown, out var id)) return null;
                var j = Pack.Get(journalist); if (j == null) return null;
                var cands = VoiceCore.Candidates(id, new[] { CharKeys(j.T), OutletKeys(j) }, true);
                if (cands.Count <= 1) return null;
                var vals = VoiceCore.Values(id, shown, out var why);
                if (vals == null) { PressRoom.W("voices", "journo miss", id + "\t" + why); return null; }
                var ok = new List<KeyValuePair<string, string>>(); var filled = new Dictionary<string, string>();
                foreach (var c in cands)
                {
                    if (c.Value.Length == 0) { ok.Add(c); continue; }
                    var f = VoiceCore.Apply(c.Value, vals);
                    if (f != null) { ok.Add(c); filled[c.Value] = f; } else PressRoom.W("voices", "journo token miss", id + "\t" + c.Key);
                }
                int pick = VoiceCore.Pick(ok, v => Used.TryGetValue(UKey(id, v), out var n) ? n : 0, (uint)(PressRoom.Record.Conf * 17 + id));
                if (pick < 0) return null;
                var ch = ok[pick]; var uk = UKey(id, ch.Value);
                Used[uk] = (Used.TryGetValue(uk, out var u) ? u : 0) + 1; Save();
                if (ch.Value.Length == 0) { PressRoom.W("voices", "journo base " + journalist, id + "\t" + ok.Count + " choices"); return null; }
                Swapped++; PressRoom.W("voices", ch.Key + " " + journalist, id + "\t" + PressRoom.Plain(filled[ch.Value]));
                return filled[ch.Value];
            }
            catch (Exception e) { PressRoom.Fail("voices question", e); return null; }
        }
    }
}
