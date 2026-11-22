// FM27 Media Mod - emoji for the fans' posts. MIT licence (see LICENSE).
// FM's text settings have Unity's colour-emoji support switched off and no emoji font, so emoji in text are dropped.
// Once, when the UI is up, this makes a font asset from Windows' own emoji font (Segoe UI Emoji) and adds it as the
// emoji fallback in the game's text settings. Only characters FM's own fonts lack are drawn from it. If anything
// fails, nothing changes and emoji stay invisible.
using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace FM27MediaMod
{
    internal static class EmojiFont
    {
        static bool Tried;
        static FontAsset Font;
        static void W(string a, string b) { try { Tabloid.Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\temoji\t{a}\t{b}"); } catch { } }

        internal static void Ensure()
        {
            if (Tried || !Terraces.Emoji) return;
            Tried = true;
            try
            {
                // where Windows keeps the emoji font: Unity's own list of OS fonts first, then the usual place
                string path = null;
                try { foreach (var f in UnityEngine.Font.GetPathsToOSFonts()) if (f != null && f.IndexOf("seguiemj", StringComparison.OrdinalIgnoreCase) >= 0) { path = f; break; } } catch (Exception e) { W("os fonts", e.Message); }
                var win = Environment.GetEnvironmentVariable("WINDIR"); if (string.IsNullOrEmpty(win)) win = @"C:\Windows";
                var usual = Path.Combine(win, "Fonts", "seguiemj.ttf");
                if (path == null) path = usual;
                bool exists = false; try { exists = File.Exists(path); } catch { }
                W("font file", path + (exists ? " (found)" : " (not found by .NET)"));
                try { W("font engine", "LoadFontFace: " + FontEngine.LoadFontFace(path, 64f, 0)); } catch (Exception e) { W("font engine", e.Message); }
                // several routes; the first that can draw a test emoji wins (colour first, outline last)
                var tries = new List<(string how, Func<FontAsset> make)>
                {
                    ("file, colour", () => FontAsset.CreateFontAsset(path, 0, 64, 6, GlyphRenderMode.COLOR, 2048, 2048, AtlasPopulationMode.Dynamic, true)),
                    ("file, colour hinted", () => FontAsset.CreateFontAsset(path, 0, 64, 6, GlyphRenderMode.COLOR_HINTED, 2048, 2048, AtlasPopulationMode.Dynamic, true)),
                    ("OS font, colour", () => { var f = UnityEngine.Font.CreateDynamicFontFromOSFont("Segoe UI Emoji", 64); return f == null ? null : FontAsset.CreateFontAsset(f, 64, 6, GlyphRenderMode.COLOR, 2048, 2048, AtlasPopulationMode.Dynamic, true); }),
                    ("family name", () => FontAsset.CreateFontAssetFromFamilyName("Segoe UI Emoji", 64)),
                    ("file, outline", () => FontAsset.CreateFontAsset(path, 0, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true)),
                };
                bool test = false;
                foreach (var t in tries)
                {
                    FontAsset fa = null;
                    try { fa = t.make(); } catch (Exception e) { W("try " + t.how, e.GetType().Name + ": " + e.Message); continue; }
                    if (fa == null) { W("try " + t.how, "no font asset"); continue; }
                    bool ok = false; try { ok = fa.TryAddCharacters("\U0001F4AA\U0001F64C\U0001F602\U0001F525", false); } catch (Exception e) { W("try " + t.how, "glyphs: " + e.Message); }
                    W("try " + t.how, ok ? "glyphs ok" : "made, but glyphs missing");
                    if (ok || Font == null) { Font = fa; test = ok; }
                    if (ok) break;
                }
                if (Font == null) { W("font", "no route worked: emoji stay invisible"); return; }
                Font.name = "FM27MM Emoji";
                Font.hideFlags = HideFlags.DontUnloadUnusedAsset;
                int n = 0;
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextSettings>());
                foreach (var o in all)
                {
                    var ts = o.TryCast<TextSettings>(); if (ts == null) continue;
                    ts.enableEmojiSupport = true;
                    var em = ts.emojiFallbackTextAssets;
                    if (em == null) { em = new Il2CppSystem.Collections.Generic.List<UnityEngine.TextCore.Text.TextAsset>(); ts.emojiFallbackTextAssets = em; }
                    em.Insert(0, Font.Cast<UnityEngine.TextCore.Text.TextAsset>());
                    var fb = ts.fallbackFontAssets;
                    if (fb == null) { fb = new Il2CppSystem.Collections.Generic.List<FontAsset>(); ts.fallbackFontAssets = fb; }
                    fb.Add(Font);
                    n++;
                    W("settings", (ts.name ?? "") + ": emoji support on");
                }
                W("font", $"Segoe UI Emoji added to {n} text settings (test glyphs {(test ? "ok" : "missing")}, {path})");
            }
            catch (Exception e) { Tabloid.Fail("emoji font", e); W("failed", e.GetType().Name + ": " + e.Message); }
        }
    }
}
