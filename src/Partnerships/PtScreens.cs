// FM27 Media Mod - Partnerships: reading SI's screens (read-only).
//   - the date in the top bar (TopRow/Day/Month), as the Treatment Room reads it
//   - whose profile is open (the person link in an element named PlayerName), and the happiness partnership lines on it
//   - SI's pitches (FM.UI.FMTacticsPanel), found as Probe 0.0.11 found them (PROVEN 22 Nov): a text with an ancestor whose
//     name mentions "Tactic" starts a search of that panel tree 1.2 s and 4 s later, at most every 2 s; a full walk at most
//     every 30 s, the pitches found last time re-read in between.
//       the tactics screen's own pitch ("interactable-pitch-expanded"): our XI per phase and SI's partnership rows;
//       the match screen's two pitches (MatchExperienceGridHuman, HalfTimeTeamReport): our XI in its named slots, ratings.
// From each pitch it reads three settings and the rows (as the probe did), and the texts inside it with their centres.
// It never reads m_positions (a struct array), never hooks the binding, never changes a thing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class PtScreens
    {
        static DateTime LastTrigger = DateTime.MinValue, LastSave = DateTime.MinValue;
        internal static string OwnerName = "", OwnerId = ""; static DateTime OwnerAt = DateTime.MinValue;
        static bool Changed;

        internal static void OnText(SI.Bindable.SIText el, string txt)
        {
            try
            {
                if (!Partnerships.Enabled.Value || string.IsNullOrEmpty(txt)) return;
                PtMemory.Pump();
                var nm = el.name ?? "";
                if (Partnerships.PairsTab.Value) PairsPage.EnsureTab(el);

                // the date
                if (nm == "Month" && txt.Length <= 10) { var p = el.parent; if (p != null && p.name == "Day") PtDate.Set(txt); }

                // whose profile is open
                bool link = txt.IndexOf('\u0001') >= 0;
                if (link && nm == "PlayerName")
                    foreach (var l in PersonLinks.All(txt)) if (l.IsPerson) { OwnerName = l.Visible; OwnerId = l.Key; OwnerAt = DateTime.Now; break; }

                // partnership sentences (happiness; SI's selected-roles lines if they ever pass the hook)
                if (txt.Length <= 600 && txt.IndexOf("partnership", StringComparison.OrdinalIgnoreCase) >= 0) Sentence(txt);

                // a pitch on screen: search its panel tree shortly
                if ((DateTime.Now - LastTrigger).TotalSeconds >= 2)
                {
                    VisualElement e = el, top = null; bool tactics = false;
                    for (int i = 0; i < 40 && e != null; i++, e = e.parent)
                    {
                        top = e; var n = e.name;
                        if (!string.IsNullOrEmpty(n) && n.IndexOf("Tactic", StringComparison.OrdinalIgnoreCase) >= 0) tactics = true;
                    }
                    if (tactics)
                    {
                        for (; e != null; e = e.parent) top = e;
                        LastTrigger = DateTime.Now;
                        var root = top;
                        root.schedule.Execute((System.Action)(() => { try { Find(root, "1s"); } catch (Exception ex) { Partnerships.Fail("find 1s", ex); } })).ExecuteLater(1200);
                        root.schedule.Execute((System.Action)(() => { try { Find(root, "4s"); } catch (Exception ex) { Partnerships.Fail("find 4s", ex); } })).ExecuteLater(4000);
                    }
                }

                // keep the files current (at most every 5 s) and the page fresh
                if (Changed && (DateTime.Now - LastSave).TotalSeconds >= 5)
                {
                    Changed = false; LastSave = DateTime.Now;
                    if (PtDate.Text.Length > 0) PtLedger.Recompute(PtDate.Text);
                    if (PtLedger.Dirty) PtLedger.Save();
                    PairsPage.Refresh();
                }
            }
            catch (Exception ex) { Partnerships.Fail("text", ex); }
        }

        static void Sentence(string txt)
        {
            var plain = PtSlots.Clean(txt);
            var f = PtParse.Feel(plain);
            if (f != null)
            {
                if ((DateTime.Now - OwnerAt).TotalSeconds > 300 || OwnerName.Length == 0) { Partnerships.W("feel", "no profile owner seen", plain); return; }
                string partner = f.Partner, partnerId = "";
                foreach (var l in PersonLinks.All(txt)) if (l.IsPerson) { partner = l.Visible; partnerId = l.Key; break; }
                if (PtLedger.SeeFeeling(Date(), OwnerName, OwnerId, partner, partnerId, f)) Changed = true;
                return;
            }
            if (PtParse.Sentence(plain, out var a, out var b, out var g) && PtLedger.SeeSentence(Date(), a, b, g)) { Partnerships.W("sentence", a + " - " + b, g); Changed = true; }
        }
        static string Date() => PtDate.Text.Length > 0 ? PtDate.Text : "?";

        // ---------- find every FMTacticsPanel under the root ----------
        static int Visited;
        static IntPtr CachedRoot = IntPtr.Zero;
        static DateTime CachedAt = DateTime.MinValue;
        static List<FM.UI.FMTacticsPanel> Cached = new List<FM.UI.FMTacticsPanel>();
        static void Find(VisualElement root, string when)
        {
            List<FM.UI.FMTacticsPanel> found;
            if (root.Pointer == CachedRoot && (DateTime.Now - CachedAt).TotalSeconds < 30) found = Cached;
            else
            {
                found = new List<FM.UI.FMTacticsPanel>(); Visited = 0; ScoreBug = null;
                Walk(root, found, 0);
                CachedRoot = root.Pointer; CachedAt = DateTime.Now; Cached = found;
            }
            foreach (var p in found) { try { if (p != null && p.panel != null) Read(p, when); } catch (Exception ex) { Partnerships.Fail("read pitch", ex); } }
        }
        static void Walk(VisualElement e, List<FM.UI.FMTacticsPanel> found, int depth)
        {
            if (e == null || depth > 120 || ++Visited > 80000 || found.Count > 8) return;
            try { if (e.name == "ScoreBug") { ScoreBug = e; return; } } catch { }   // 0.2.0: the match screen's scoreboard
            FM.UI.FMTacticsPanel p = null;
            try { p = e.TryCast<FM.UI.FMTacticsPanel>(); } catch { }
            if (p != null) { found.Add(p); return; }
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } Walk(c, found, depth + 1); }
        }

        static string Names(VisualElement e, int levels)
        {
            var sb = new StringBuilder();
            try { for (int i = 0; i < levels && e != null; i++, e = e.parent) { var nm = e.name ?? ""; if (nm.Length > 0) sb.Insert(0, nm + "/"); } } catch { }
            return sb.ToString();
        }

        static readonly Dictionary<string, string> LastRead = new Dictionary<string, string>();
        static VisualElement ScoreBug;
        static DateTime ScoreAt = DateTime.MinValue;
        internal static List<PitchText> TextsOf(VisualElement e) { var l = new List<PitchText>(); Visited = 0; Texts(e, "", l, 0); return l; }
        internal static void Reset() { LastRead.Clear(); CachedRoot = IntPtr.Zero; Cached = new List<FM.UI.FMTacticsPanel>(); OwnerName = ""; OwnerId = ""; }
        static void Read(FM.UI.FMTacticsPanel p, string when)
        {
            var name = p.name ?? "";
            bool tacticsPitch = name.IndexOf("interactable", StringComparison.OrdinalIgnoreCase) >= 0;
            string up = tacticsPitch ? "" : Names(p, 40);
            bool matchPitch = !tacticsPitch && (up.Contains("MatchExperienceGridHuman") || up.Contains("HalfTimeTeamReport")) && !up.Contains("TeamNews");
            if (!tacticsPitch && !matchPitch) return;
            // only a pitch actually on screen (a hidden one keeps its texts)
            try { var wb = p.worldBound; if (float.IsNaN(wb.width) || wb.width < 2f || wb.height < 2f) return; } catch { return; }
            try { for (VisualElement a = p; a != null; a = a.parent) if (a.resolvedStyle.display == DisplayStyle.None) return; } catch { }

            var texts = new List<PitchText>(); Visited = 0;
            Texts(p, "", texts, 0);
            if (texts.Count == 0) return;

            if (tacticsPitch)
            {
                bool inPos = true, lines = false;
                try { inPos = p.m_inPossession; } catch { }
                try { lines = p.ShouldDrawPartnershipLines; } catch { }
                var rows = new List<List<int>>();
                try
                {
                    var data = p.m_partnershipScoresData;
                    if (data != null)
                        for (int i = 0; i < data.Count; i++)
                        {
                            var r = data[i]; var row = new List<int>();
                            if (r != null) for (int j = 0; j < r.Count; j++) row.Add(r[j]);
                            rows.Add(row);
                        }
                }
                catch (Exception ex) { Partnerships.Fail("rows", ex); rows = null; }
                var phase = inPos ? "in possession" : "out of possession";
                var slots = PtSlots.FromTactics(texts);
                var key = "t|" + Date() + "|" + phase + "|" + string.Join(" ", slots.Select(s => s.SlotName + "=" + s.Name)) + "|" + (lines ? "on" : "off") + "|" + (rows == null ? "-" : string.Join(";", rows.Select(r => string.Join(" ", r))));
                bool same = LastRead.TryGetValue("t|" + phase, out var prev) && prev == key;
                LastRead["t|" + phase] = key;
                PitchOverlay.Track(p);
                if (same) return;
                int n = PtLedger.SeeTactics(Date(), phase, slots, rows, lines);
                if (Partnerships.LogReads.Value) Partnerships.W("tactics", phase + ", lines " + (lines ? "on" : "off") + ", " + (rows?.Count ?? -1) + " rows, " + n + " matched", string.Join(" ", slots.Select(s => s.SlotName + "=" + s.Name)));
                Changed = true;
            }
            else
            {
                var xi = PtSlots.FromMatch(texts);
                int ours = PtLedger.Ours(xi);
                if (ours < 6) return;
                var key = Date() + "|" + string.Join(" ", xi.Select(s => s.SlotName + "=" + s.Name + ":" + s.Rating));
                bool same = LastRead.TryGetValue("m", out var prev) && prev == key && when == "4s";
                LastRead["m"] = key;
                if (same) return;
                int added = PtLedger.SeeMatch(Date(), xi);
                if (Partnerships.LogReads.Value && added > 0) Partnerships.W("match", added + " new on the pitch", key);
                Changed = true;
                // 0.2.0: home or away (which formation is ours), and the scoreboard as it stands (at most every 5 s)
                var venue = up.Contains("HomeTeamFormation") ? "home" : up.Contains("AwayTeamFormation") ? "away" : "";
                if ((DateTime.Now - ScoreAt).TotalSeconds >= 5)
                {
                    ScoreAt = DateTime.Now;
                    List<PitchText> sb = null;
                    try { if (ScoreBug != null && ScoreBug.panel != null) sb = TextsOf(ScoreBug); } catch { sb = null; }
                    PtLedger.SeeScoreBug(Date(), venue, sb);
                }
            }
        }

        static void Texts(VisualElement e, string path, List<PitchText> acc, int depth)
        {
            if (e == null || depth > 60 || acc.Count > 3000 || ++Visited > 20000) return;
            var nm = e.name ?? "";
            var p = nm.Length > 0 ? path + nm + "/" : path;
            TextElement te = null; try { te = e.TryCast<TextElement>(); } catch { }
            if (te != null)
            {
                string t = null; try { t = te.text; } catch { }
                if (!string.IsNullOrEmpty(t))
                {
                    float x = 0, y = 0;
                    try { var r = e.worldBound; x = r.center.x; y = r.center.y; } catch { }
                    acc.Add(new PitchText { X = x, Y = y, Path = p, Text = PersonLinks.Plain(t) });
                }
            }
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } Texts(c, p, acc, depth + 1); }
        }
    }
}
