// FM27 Media Mod - Press Room 0.15.2: is the mod's language chosen? (0.15.3: re-checked every 5 s once right, was 30 s.) The builder writes "FM27 Media Mod <N>.ltf" into FM's
// languages folder and records it in BepInEx\FM27MediaMod\builder state.tsv (lang.file). FM keeps the chosen language in
// %LOCALAPPDATA%\Sports Interactive\Football Manager 27\Preferences\version <n>\current_language.xml
// (<symbol id="value" value="FM27 Media Mod 3"/>: the file's name without .ltf). If they differ, a notice at the top of the
// screen says which language to choose, until it is chosen or the player closes the notice for this session.
// Files only: nothing in the game is read or changed. Without the mod's language, the mod's extra questions and chat
// replies show blank (beta reports, 26-27 Nov 2026).
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class LanguageCheck
    {
        internal static bool Enabled = true;
        static DateTime Next = DateTime.MinValue;
        static bool Dismissed, Logged;
        static VisualElement Banner; static Label Text;
        static string Last = "";
        static readonly Regex Value = new Regex("value=\"([^\"]*)\"", RegexOptions.Compiled);

        // the language the builder made ("FM27 Media Mod 2"), or "" if it has made none
        static string Expected(out string ltf)
        {
            ltf = "";
            var st = Path.Combine(BepInEx.Paths.BepInExRootPath, "FM27MediaMod", "builder state.tsv");
            if (!File.Exists(st)) return "";
            foreach (var l in File.ReadAllLines(st))
                if (l.StartsWith("lang.file\t", StringComparison.Ordinal)) { ltf = l.Substring(10).Trim(); return Path.GetFileNameWithoutExtension(ltf); }
            return "";
        }

        // the builder's last word on the language, when it could not build one
        static bool BuildFailed()
        {
            var log = Path.Combine(BepInEx.Paths.BepInExRootPath, "FM27MediaMod", "builder.log");
            if (!File.Exists(log)) return false;
            string last = "";
            foreach (var l in File.ReadAllLines(log)) if (l.Contains("  language: ")) last = l;
            return last.Contains("not found") || last.Contains("is not there yet");
        }

        // FM's chosen language, "" if FM has not written one (it then uses its default English)
        static string Current()
        {
            var prefs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sports Interactive", "Football Manager 27", "Preferences");
            if (!Directory.Exists(prefs)) return "";
            string best = null; int bestN = -1;
            foreach (var d in Directory.GetDirectories(prefs, "version *"))
            {
                int.TryParse(Path.GetFileName(d).Substring(8).Trim(), out var n);
                if (n > bestN && File.Exists(Path.Combine(d, "current_language.xml"))) { bestN = n; best = Path.Combine(d, "current_language.xml"); }
            }
            if (best == null) return "";
            var m = Value.Match(File.ReadAllText(best));
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        // from the text hook: at most every 3 seconds while a notice may be needed, every 5 once the language is right
        internal static void OnText(VisualElement e)
        {
            if (!Enabled || Dismissed || DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(3);
            try
            {
                var exp = Expected(out var ltf); var cur = Current();
                string msg = null;
                if (exp.Length > 0 && File.Exists(ltf) && !string.Equals(cur, exp, StringComparison.OrdinalIgnoreCase))
                    msg = "Choose English (" + exp + ") in Preferences > Language. Until you do, the mod's extra questions and chat replies show up blank.";
                else if (exp.Length == 0 && BuildFailed())
                    msg = "The mod could not build its language. BepInEx\\FM27MediaMod\\builder.log says why.";
                var state = (msg == null ? "ok" : "notice") + " | expected '" + exp + "', chosen '" + cur + "'";
                if (state != Last) { Last = state; PressRoom.W("language", state, ""); }
                if (msg == null) { Hide(); Next = DateTime.Now.AddSeconds(5); return; }
                Show(e, msg);
            }
            catch (Exception ex) { PressRoom.W("language", "check stopped", ex.Message); Enabled = false; Hide(); }
        }

        static void Show(VisualElement any, string msg)
        {
            if (Banner == null || Banner.panel == null)
            {
                VisualElement host = null;
                for (var x = any; x != null; x = x.parent) if (x.name == "MainUIDocument-container") { host = x; break; }
                if (host == null) host = any.panel?.visualTree;
                if (host == null) return;
                Banner = new VisualElement(); Banner.name = "FM27MM-LanguageNotice";
                Banner.style.position = Position.Absolute; Banner.style.left = 0; Banner.style.right = 0; Banner.style.top = 70;
                Banner.style.flexDirection = FlexDirection.Row; Banner.style.justifyContent = Justify.Center; Banner.pickingMode = PickingMode.Ignore;
                var box = new VisualElement();
                box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center; box.style.maxWidth = 760;
                box.style.backgroundColor = new Color(0.09f, 0.09f, 0.10f, 0.97f);
                box.style.paddingLeft = 16; box.style.paddingRight = 10; box.style.paddingTop = 10; box.style.paddingBottom = 10;
                box.style.borderTopLeftRadius = 10; box.style.borderTopRightRadius = 10; box.style.borderBottomLeftRadius = 10; box.style.borderBottomRightRadius = 10;
                box.style.borderLeftWidth = 5; box.style.borderLeftColor = new Color(0.96f, 0.79f, 0.54f);
                box.style.borderTopWidth = 1; box.style.borderRightWidth = 1; box.style.borderBottomWidth = 1;
                var edge = new Color(0.25f, 0.25f, 0.28f); box.style.borderTopColor = edge; box.style.borderRightColor = edge; box.style.borderBottomColor = edge;
                var words = new VisualElement(); words.style.flexShrink = 1; words.pickingMode = PickingMode.Ignore;
                var cap = new Label("FM27 MEDIA MOD"); cap.style.fontSize = 11; cap.style.color = new Color(0.96f, 0.79f, 0.54f); cap.style.unityFontStyleAndWeight = FontStyle.Bold; cap.style.marginBottom = 2; cap.pickingMode = PickingMode.Ignore;
                Text = new Label(""); Text.style.fontSize = 15; Text.style.color = new Color(0.96f, 0.95f, 0.94f); Text.style.whiteSpace = WhiteSpace.Normal; Text.pickingMode = PickingMode.Ignore;
                words.Add(cap); words.Add(Text);
                var ok = new Button(); ok.text = "Close"; ok.style.marginLeft = 14; ok.style.fontSize = 13; ok.style.flexShrink = 0;
                ok.style.color = new Color(0.96f, 0.95f, 0.94f); ok.style.backgroundColor = new Color(0.17f, 0.17f, 0.19f);
                System.Action close = () => { Dismissed = true; Hide(); PressRoom.W("language", "notice closed for this session", ""); };
                ok.clickable.add_clicked(close);
                box.Add(words); box.Add(ok); Banner.Add(box);
                host.Add(Banner);
                if (!Logged) { Logged = true; PressRoom.W("language", "notice shown", msg); }
            }
            if (Text.text != msg) Text.text = msg;
            Banner.style.display = DisplayStyle.Flex;
            Banner.BringToFront();
        }

        static void Hide() { try { if (Banner != null) Banner.style.display = DisplayStyle.None; } catch { } }
    }
}
