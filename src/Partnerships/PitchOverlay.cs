// FM27 Media Mod - Partnerships 0.2.1: our lines on SI's own tactics pitch (design stage 4).
// A small PAIRS button of our own sits in the pitch's bottom-left corner; click it and our seven-step lines join the shirts of
// every pair the ledger holds, on the tactics screen's own pitch; click again and they go (remembered in the config).
// 0.2.0 waited for SI's eye-menu toggle, read from ShouldDrawPartnershipLines: that is a fixed "this pitch may draw lines"
// setting, not the toggle (the toggle reaches the pitch through its data binding, ShowPartnershipLinesBinding, which we never
// touch), so 0.2.0 never drew (Nik, 23 Nov 09:18). Switch SI's own Player Relationships off yourself so the two don't stack.
// Everything is our own: one layer, clicks pass straight through (pickingMode Ignore, so dragging players is untouched),
// inserted just above SI's screens (MenuBody) and below every tab, page and anything added later; one clipped box per
// pitch, placed over the pitch from its on-screen rectangle. Nothing of SI's is moved, re-parented or restyled. A tick every
// second follows the pitch (scrolling, dragging a player, switching phase) and clears the layer when the pitch has gone.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class PitchOverlay
    {
        static VisualElement Over;
        static readonly List<FM.UI.FMTacticsPanel> Panels = new List<FM.UI.FMTacticsPanel>();
        static string LastKey = "";
        static int LastCount = -1;
        static bool NoMenuLogged;

        internal static void Track(FM.UI.FMTacticsPanel p)
        {
            if (!Partnerships.OnSiPitch.Value || p == null) return;
            IntPtr ptr; try { ptr = p.Pointer; } catch { return; }
            bool known = false;
            foreach (var q in Panels) { try { if (q.Pointer == ptr) { known = true; break; } } catch { } }
            if (!known) { Panels.Add(p); if (Panels.Count > 6) Panels.RemoveAt(0); }
            Ensure();
        }

        static void Ensure()
        {
            try
            {
                if (Over != null && Over.panel != null) return;
                var host = PairsPage.HostEl; if (host == null) return;
                int at = -1, n = host.childCount;
                for (int i = 0; i < n; i++) { VisualElement c = null; try { c = host.ElementAt(i); } catch { } if (c != null && c.name == "MenuBody") at = i; }
                if (at < 0) { if (!NoMenuLogged) { NoMenuLogged = true; Partnerships.W("pitch", "not drawn", "no MenuBody under the main container"); } return; }
                Over = new VisualElement(); Over.name = "FM27MM-PairsOnPitch"; Over.pickingMode = PickingMode.Ignore;
                Over.style.position = Position.Absolute; Over.style.left = 0; Over.style.top = 0; Over.style.right = 0; Over.style.bottom = 0;
                host.Insert(at + 1, Over);
                Over.schedule.Execute((System.Action)Tick).Every(1000);
                LastKey = ""; LastCount = -1;
                Partnerships.W("pitch", "layer added", "above MenuBody (child " + (at + 1) + ")");
            }
            catch (Exception e) { Partnerships.Fail("pitch layer", e); }
        }

        static bool Shown(VisualElement p)
        {
            try
            {
                if (p == null || p.panel == null) return false;
                var wb = p.worldBound; if (float.IsNaN(wb.width) || wb.width < 2f || wb.height < 2f) return false;
                for (VisualElement a = p; a != null; a = a.parent) if (a.resolvedStyle.display == DisplayStyle.None) return false;
                return true;
            }
            catch { return false; }
        }

        static void Tick()
        {
            try
            {
                if (Over == null || Over.panel == null) return;
                var host = PairsPage.HostEl; if (host == null) return;
                var hb = host.worldBound;
                var parts = new List<(Rect r, List<Slot> slots)>();
                var key = new StringBuilder(PtLedger.Version + "|" + PtDate.Text + "|" + Partnerships.OnSiPitch.Value + "|" + Partnerships.ShowOnSiPitch.Value);
                for (int i = Panels.Count - 1; i >= 0; i--)
                {
                    var p = Panels[i];
                    bool alive; try { alive = p != null && p.panel != null; } catch { alive = false; }
                    if (!alive) { Panels.RemoveAt(i); continue; }
                    if (!Partnerships.OnSiPitch.Value || !Shown(p)) continue;
                    var slots = PtSlots.FromTactics(PtScreens.TextsOf(p));
                    if (slots.Count < 7) continue;
                    var r = p.worldBound;
                    parts.Add((r, slots));
                    key.Append($"|{r.xMin:0},{r.yMin:0},{r.width:0},{r.height:0}");
                    foreach (var s in slots) key.Append($";{s.Name}@{s.SX:0},{s.SY:0}");
                }
                var k = key.ToString();
                if (k == LastKey) return;
                LastKey = k;
                Over.Clear();
                int drawn = 0;
                var today = PtDate.Text;
                foreach (var (r, slots) in parts)
                {
                    var box = new VisualElement(); box.pickingMode = PickingMode.Ignore;
                    box.style.position = Position.Absolute; box.style.overflow = Overflow.Hidden;
                    box.style.left = r.xMin - hb.xMin; box.style.top = r.yMin - hb.yMin; box.style.width = r.width; box.style.height = r.height;
                    Over.Add(box);
                    // our own on/off button, bottom-left inside the pitch (SI's own buttons sit top-left and bottom-right)
                    var on = Partnerships.ShowOnSiPitch.Value;
                    var btn = new Button(); btn.text = "PARTNERSHIPS"; btn.tooltip = on ? "Hide the partnership lines (FM27 Media Mod)" : "Show the partnership lines (FM27 Media Mod)";
                    btn.pickingMode = PickingMode.Position;
                    btn.style.position = Position.Absolute; btn.style.left = r.xMin - hb.xMin + 10; btn.style.top = r.yMax - hb.yMin - 34; btn.style.height = 22;
                    btn.style.fontSize = 11; btn.style.paddingLeft = 8; btn.style.paddingRight = 8; btn.style.paddingTop = 2; btn.style.paddingBottom = 2;
                    btn.style.marginLeft = 0; btn.style.marginTop = 0; btn.style.marginRight = 0; btn.style.marginBottom = 0;
                    btn.style.backgroundColor = on ? new Color(0.39f, 0.60f, 0.13f, 1f) : new Color(0.17f, 0.17f, 0.17f, 0.9f);
                    btn.style.color = on ? new Color(0.05f, 0.05f, 0.05f, 1f) : new Color(0.85f, 0.85f, 0.82f, 1f);
                    btn.style.borderTopLeftRadius = 5; btn.style.borderTopRightRadius = 5; btn.style.borderBottomLeftRadius = 5; btn.style.borderBottomRightRadius = 5;
                    System.Action flip = () =>
                    {
                        try { Partnerships.ShowOnSiPitch.Value = !Partnerships.ShowOnSiPitch.Value; LastKey = ""; Partnerships.W("pitch", "button", Partnerships.ShowOnSiPitch.Value ? "lines on" : "lines off"); Tick(); }
                        catch (Exception e) { Partnerships.Fail("pitch button", e); }
                    };
                    btn.clickable.add_clicked(flip);
                    Over.Add(btn);
                    if (!on) continue;
                    var at = new Dictionary<string, Slot>();
                    foreach (var s in slots) if (!at.ContainsKey(s.Name) && !float.IsNaN(s.SX)) at[s.Name] = s;
                    var draw = new List<(PairView v, Slot a, Slot b)>();
                    foreach (var p in PtLedger.Pairs.Values)
                        if (at.TryGetValue(p.A, out var sa) && at.TryGetValue(p.B, out var sb)) draw.Add((PtLedger.View(p, today), sa, sb));
                    foreach (var d in draw.OrderBy(x => x.v.Step == -1 ? 0.5 : x.v.Step))
                    {
                        var a = new V(d.a.SX - r.xMin, d.a.SY - r.yMin); var b = new V(d.b.SX - r.xMin, d.b.SY - r.yMin);
                        var pts = Bend(a, b, slots.Where(s => s != d.a && s != d.b && !float.IsNaN(s.SX)).Select(s => new V(s.SX - r.xMin, s.SY - r.yMin)).ToList(), d.a.Band <= 2 && d.b.Band <= 2);
                        Trim(pts, 16f);
                        for (int i = 0; i + 1 < pts.Count; i++) PairsPage.Line(box, pts[i], pts[i + 1], PairsPage.Thick(d.v.Step) + 1f, PairsPage.StepColour(d.v.Step), 0.92f, PairsPage.Pattern(d.v));
                        drawn++;
                    }
                }
                if (drawn != LastCount) { LastCount = drawn; Partnerships.W("pitch", parts.Count == 0 ? "cleared" : Partnerships.ShowOnSiPitch.Value ? "drawn" : "button only", drawn + " lines on " + parts.Count + " pitch(es)"); }
            }
            catch (Exception e) { Partnerships.Fail("pitch tick", e); }
        }

        // straight, or bent round a shirt standing in the way (back line: towards its own goal; further up: towards the opposition's)
        static List<V> Bend(V a, V b, List<V> others, bool back)
        {
            var l = new List<V> { a };
            float len = (b - a).magnitude;
            foreach (var o in others)
            {
                var ab = b - a; float t = ab.sqrMagnitude < 1e-3f ? 0 : Math.Max(0f, Math.Min(1f, V.Dot(o - a, ab) / ab.sqrMagnitude));
                if (t > 0.1f && t < 0.9f && (o - (a + t * ab)).magnitude < 22f)
                {
                    float off = Math.Max(26f, len * 0.18f) * (back ? 1f : -1f);
                    l.Add(new V((a.x + b.x) / 2f, (a.y + b.y) / 2f + off));
                    break;
                }
            }
            l.Add(b);
            return l;
        }

        // pull both ends in, so the line stops short of the shirts
        static void Trim(List<V> pts, float r)
        {
            if (pts.Count < 2) return;
            var d0 = pts[1] - pts[0]; float l0 = d0.magnitude; if (l0 > 2 * r + 4) pts[0] = pts[0] + d0 / l0 * r;
            int n = pts.Count;
            var d1 = pts[n - 2] - pts[n - 1]; float l1 = d1.magnitude; if (l1 > 2 * r + 4) pts[n - 1] = pts[n - 1] + d1 / l1 * r;
        }
    }
}
