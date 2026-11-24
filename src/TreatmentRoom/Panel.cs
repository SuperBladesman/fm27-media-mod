// FM27 Media Mod - Treatment Room: the panel on Season Summary.
//   0.2.0 one panel under the legend; 0.2.1 found the card (10 levels up); 0.2.2 full width, any screen, scroll bar;
//   0.2.3 both empty bands, tighter lines, in-world wording;
//   0.2.4 (Nik: "Can we not move the pills down? Or the graph up? ... or both?"): the severity pills are nudged down to sit
//   just above the chart (a visual translate only: SI's layout underneath is untouched), which leaves one clear space under
//   the legend. The panel fills it: THE SEASON SO FAR on the left, WHO TO WATCH (a table, one row per player) on the right;
//   stacked on a narrow card; a scroll bar when it doesn't fit. If the pills can't be found or moved, 0.2.3's two bands.
// Everything is an element of our own, absolutely positioned inside the card's Block (Inuries/Block, SI's spelling), and
// measured in the UI's own units, so any resolution or UI scale works. Geometry is logged every time.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Panel
    {
        const string PanelName = "FM27MM-TreatmentPanel";
        internal static readonly Regex HeaderRx = new Regex(@"^\d+\s+injur(y|ies) this season$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex PillRx = new Regex(@"^(Major|Moderate|Minor|Slight)\b", RegexOptions.Compiled);
        static VisualElement Header;          // SI's "39 injuries this season" text
        static bool Pending;
        static int Tries;
        static readonly Dictionary<IntPtr, float> Moved = new Dictionary<IntPtr, float>();   // SI element -> the translate we gave it

        // 0.5.7: the shared near-black with the Treatment Room's red accent, as the page
        static readonly Color Bg = new Color(0.06f, 0.06f, 0.06f, 0.82f), Ink = new Color(0.97f, 0.96f, 0.95f, 1f),
            Quiet = new Color(0.70f, 0.68f, 0.67f, 1f), Gold = new Color(0.91f, 0.36f, 0.39f, 1f), Edge = new Color(0.81f, 0.18f, 0.23f, 1f),
            Rule = new Color(1f, 1f, 1f, 0.07f);

        // from the text hook: SI has set the card's header; place the panel once the card is laid out
        static int Depth = -1;
        internal static void OnHeader(VisualElement el, int depth)
        {
            if (!TreatmentRoom.SeasonPanel.Value) return;
            if (Header != el || Depth != depth) TreatmentRoom.W("panel", "header seen", "Inuries " + depth + " levels up");
            Header = el; Depth = depth; Tries = 0; Schedule(500);
        }
        // the ledger changed (a scan stored rows): refresh the text if the panel is on screen
        internal static void Refresh() { if (Header != null && Header.panel != null) Schedule(150); }

        static void Schedule(long ms)
        {
            if (Pending || Header == null) return;
            Pending = true;
            var h = Header;
            System.Action run = () => { Pending = false; try { Show(h); } catch (Exception e) { TreatmentRoom.Fail("panel", e); } };
            try { h.schedule.Execute(run).ExecuteLater(ms); } catch (Exception e) { Pending = false; TreatmentRoom.Fail("panel schedule", e); }
        }

        static VisualElement Up(VisualElement e, string name, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent) if ((e.name ?? "") == name) return e;
            return null;
        }
        static bool Bad(Rect r) => float.IsNaN(r.width) || float.IsNaN(r.yMin) || r.width <= 1f || r.height <= 0f;
        static IntPtr Ptr(VisualElement e) { try { return e.Pointer; } catch { return IntPtr.Zero; } }

        // text elements below e whose (plain) text passes the test
        static void FindTexts(VisualElement e, Func<string, bool> test, List<VisualElement> into, int depth, ref int seen, int max)
        {
            if (e == null || depth > 25 || ++seen > 3000 || into.Count >= max) return;
            var te = e.TryCast<TextElement>();
            if (te != null) { string t = null; try { t = te.text; } catch { } t = Screens.Strip(t); if (!string.IsNullOrEmpty(t) && test(t)) { into.Add(e); return; } }
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } FindTexts(c, test, into, depth + 1, ref seen, max); }
        }
        static VisualElement FindText(VisualElement e, Func<string, bool> test)
        {
            var l = new List<VisualElement>(); int seen = 0; FindTexts(e, test, l, 0, ref seen, 1); return l.Count > 0 ? l[0] : null;
        }
        static VisualElement FindName(VisualElement e, string name, int depth, ref int seen)
        {
            if (e == null || depth > 25 || ++seen > 3000) return null;
            if ((e.name ?? "") == name) return e;
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } var f = FindName(c, name, depth + 1, ref seen); if (f != null) return f; }
            return null;
        }
        static string R(Rect r) => Bad(r) ? "-" : $"{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}";

        // the lowest element that holds all of them
        static VisualElement Common(List<VisualElement> els)
        {
            if (els.Count == 0) return null;
            var chain = new List<VisualElement>();
            for (var e = els[0]; e != null; e = e.parent) chain.Add(e);
            foreach (var a in chain)
            {
                var pa = Ptr(a); bool all = true;
                foreach (var o in els)
                {
                    bool inside = false;
                    for (var e = o; e != null; e = e.parent) if (Ptr(e) == pa) { inside = true; break; }
                    if (!inside) { all = false; break; }
                }
                if (all) return a;
            }
            return null;
        }

        // 0.2.4: nudge SI's severity pills down to just above the chart. A translate moves only what is drawn, never the
        // layout, so SI's other elements stay where SI put them. Returns the pills' top edge after the move (NaN if not moved).
        static float MovePills(VisualElement cc, Rect cb, out string note)
        {
            note = "";
            var pills = new List<VisualElement>(); int seen = 0;
            FindTexts(cc, t => PillRx.IsMatch(t), pills, 0, ref seen, 8);
            if (pills.Count < 2) { note = "pills not found (" + pills.Count + ")"; return float.NaN; }
            var row = Common(pills);
            if (row == null || Ptr(row) == Ptr(cc)) { note = "the pills have no row of their own"; return float.NaN; }
            var rb = row.worldBound; var pb = pills[0].worldBound;
            if (Bad(rb) || Bad(cb) || rb.height > Math.Max(60f, pb.height * 3f)) { note = "pill row " + R(rb) + " looks wrong"; return float.NaN; }
            // where SI laid the row out: its layout (in its parent's space) ignores any translate, so redrawing never drifts
            var par = row.parent; if (par == null) { note = "pill row has no parent"; return float.NaN; }
            var pw = par.worldBound; var lay = row.layout;
            if (Bad(pw) || float.IsNaN(lay.yMin)) { note = "pill row not laid out yet"; return float.NaN; }
            var key = Ptr(row);
            float origTop = pw.yMin + lay.yMin, origBottom = origTop + lay.height;
            float dy = (cb.yMin - 10f) - origBottom;
            if (dy < 4f) { note = "pills already sit above the chart"; return origTop; }
            try { row.style.translate = new StyleTranslate(new Translate(new Length(0f), new Length(dy))); Moved[key] = dy; }
            catch (Exception e) { note = "translate failed: " + e.GetType().Name; return float.NaN; }
            note = $"pills moved down {dy:0} (row laid out at {origTop:0}, {lay.height:0} high)";
            return origTop + dy;
        }

        static void Show(VisualElement header)
        {
            if (header == null || header.panel == null) return;
            var block = Up(header, "Block", 40);
            var cc = Up(header, "custom-contents", 40);
            if (block == null || Up(block, "Inuries", 12) == null) { TreatmentRoom.W("panel", "not placed", "no Inuries/Block above the header (block " + (block != null) + ", custom-contents " + (cc != null) + ")"); return; }
            var bb = block.worldBound; var hb = header.worldBound;
            if (Bad(bb) || Bad(hb))
            {
                if (++Tries <= 3) Schedule(500); else TreatmentRoom.W("panel", "not placed", "the card has no layout yet");
                return;
            }
            var legend = cc != null ? FindText(cc, t => t == "This Season") : null;
            var pill = cc != null ? FindText(cc, t => PillRx.IsMatch(t)) : null;
            int seen = 0;
            var chart = cc != null ? FindName(cc, "axis_container", 0, ref seen) : null;
            var lb = legend != null ? legend.worldBound : new Rect(float.NaN, float.NaN, 0, 0);
            var pb = pill != null ? pill.worldBound : new Rect(float.NaN, float.NaN, 0, 0);
            var cb = chart != null ? chart.worldBound : new Rect(float.NaN, float.NaN, 0, 0);

            float headBottom = !Bad(lb) ? lb.yMax : hb.yMax;
            float left = Math.Max(8f, hb.xMin - bb.xMin);
            float width = Math.Max(240f, bb.width - left - 24f);
            var lines = Summary.Season(GameDate.Text, TreatmentRoom.SeasonStartMonth.Value);
            var season = lines.FindAll(l => l.Col == 0); var watchLines = lines.FindAll(l => l.Col == 1);
            var watch = Summary.Watch(GameDate.Text, TreatmentRoom.SeasonStartMonth.Value, 14);

            // one clear space: the pills moved down to just above the chart
            string note = "pills left where they are";
            float pillsTop = float.NaN;
            if (TreatmentRoom.MovePills.Value && cc != null && !Bad(cb) && cb.yMin > headBottom + 120f) pillsTop = MovePills(cc, cb, out note);
            if (!float.IsNaN(pillsTop) && pillsTop > headBottom + 60f)
            {
                float h = pillsTop - headBottom - 18f;
                bool side = width >= 900f;
                Draw(block, PanelName, headBottom - bb.yMin + 8f, left, width, h, side, season, watch, watchLines);
                Remove(block, PanelName + "2");
                TreatmentRoom.W("panel", "placed", $"one space ({h:0} high), {(side ? "side by side" : "stacked")}, {watch.Count} players to watch | {note} | left {left:0} width {width:0} | block {R(bb)} header {R(hb)} legend {R(lb)} pills {R(pb)} chart {R(cb)}");
                return;
            }

            // 0.2.3's fallback: the two bands SI leaves, legend to pills and pills to chart
            float aTop = headBottom, aEnd = !Bad(pb) && pb.yMin > headBottom ? pb.yMin : (!Bad(cb) && cb.yMin > headBottom ? cb.yMin : headBottom + 160f);
            float bTop = !Bad(pb) && pb.yMin > headBottom ? pb.yMax : -1f, bEnd = !Bad(cb) && bTop > 0 && cb.yMin > bTop ? cb.yMin : -1f;
            float aH = aEnd - aTop - 16f, bH = bEnd > 0 ? bEnd - bTop - 16f : 0f;
            if (bH >= 56f && aH >= 56f)
            {
                Draw(block, PanelName, aTop - bb.yMin + 8f, left, width, aH, false, season, null, null);
                Draw(block, PanelName + "2", bTop - bb.yMin + 8f, left, width, bH, false, null, watch, watchLines);
                TreatmentRoom.W("panel", "placed", $"two bands ({aH:0} and {bH:0} high) | {note} | left {left:0} width {width:0} | block {R(bb)} header {R(hb)} legend {R(lb)} pills {R(pb)} chart {R(cb)}");
            }
            else
            {
                float h = Math.Max(56f, Math.Max(aH, bH)); bool useB = bH > aH;
                Draw(block, PanelName, (useB ? bTop : aTop) - bb.yMin + 8f, left, width, h, width >= 900f, season, watch, watchLines);
                Remove(block, PanelName + "2");
                TreatmentRoom.W("panel", "placed", $"one band ({h:0} high) | {note} | left {left:0} width {width:0} | block {R(bb)} header {R(hb)} legend {R(lb)} pills {R(pb)} chart {R(cb)}");
            }
        }

        static void Remove(VisualElement block, string name)
        {
            for (int i = block.childCount - 1; i >= 0; i--) { var c = block.ElementAt(i); if ((c.name ?? "") == name) c.RemoveFromHierarchy(); }
        }

        // one panel of our own: absolutely positioned in the card's Block; its sections inside a ScrollView
        static void Draw(VisualElement block, string name, float top, float left, float width, float maxH, bool side,
            List<Summary.Line> season, List<Summary.WatchRow> watch, List<Summary.Line> watchLines)
        {
            VisualElement p = null;
            int n = block.childCount;
            for (int i = 0; i < n; i++) { var c = block.ElementAt(i); if ((c.name ?? "") == name) { p = c; break; } }
            if (p == null)
            {
                p = new VisualElement(); p.name = name;
                p.pickingMode = PickingMode.Ignore;
                p.style.position = Position.Absolute;
                p.style.flexDirection = FlexDirection.Column;
                p.style.backgroundColor = Bg;
                p.style.paddingLeft = 12; p.style.paddingRight = 12; p.style.paddingTop = 6; p.style.paddingBottom = 6;
                p.style.borderLeftWidth = 3; p.style.borderLeftColor = Edge;
                p.style.borderTopRightRadius = 6; p.style.borderBottomRightRadius = 6;
                p.style.overflow = Overflow.Hidden;
                block.Add(p);
            }
            p.style.top = top; p.style.left = left; p.style.width = width; p.style.maxHeight = maxH;
            p.Clear();

            var body = new VisualElement(); body.pickingMode = PickingMode.Ignore; body.style.flexDirection = side ? FlexDirection.Row : FlexDirection.Column;
            if (season != null)
            {
                var s = Section(side, 4f, watch != null || watchLines != null);
                s.Add(Text("TREATMENT ROOM: THE SEASON SO FAR", 11f, Gold, 4));
                foreach (var l in season) s.Add(Line(l));
                if (TreatmentRoom.RoomOn.Value)
                {   // 0.3.0: into the Treatment Room page (the button is clickable; the rest of the panel lets clicks through)
                    var b = new Button(); b.text = "Open the Treatment Room";
                    b.style.backgroundColor = new Color(0.81f, 0.18f, 0.23f, 1f); b.style.color = Ink; b.style.fontSize = 11.5f;
                    b.style.paddingLeft = 8; b.style.paddingRight = 8; b.style.paddingTop = 2; b.style.paddingBottom = 2;
                    b.style.marginLeft = 0; b.style.marginTop = 3; b.style.alignSelf = Align.FlexStart;
                    var bref = b;
                    System.Action go = () => { try { Room.Open("overview", bref); } catch (Exception e) { TreatmentRoom.Fail("panel button", e); } };
                    b.clickable.add_clicked(go);
                    s.Add(b);
                }
                body.Add(s);
            }
            if (watch != null || watchLines != null)
            {
                var w = Section(side, 6f, false);
                // 0.5.4: who compiled it (the risk assessments' signer), as the department sees it
                var signer = Notes.StaffName("risk");
                var title = (season != null ? "WHO TO WATCH" : "TREATMENT ROOM: WHO TO WATCH") + (signer.Length > 0 ? " (" + signer.ToUpperInvariant() + ")" : "");
                w.Add(Text(title, 11f, Gold, 4));
                if (watch != null && watch.Count > 0) w.Add(Table(watch));
                else if (watchLines != null) foreach (var l in watchLines) w.Add(Line(l));
                body.Add(w);
            }
            var sv = new ScrollView(); sv.name = name + "-Scroll";
            sv.style.flexShrink = 1; sv.style.flexGrow = 0; sv.style.maxHeight = Math.Max(24f, maxH - 12f);
            try { sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden; sv.verticalScrollerVisibility = ScrollerVisibility.Auto; } catch { }
            sv.Add(body);
            p.Add(sv);

            // after it is drawn: its real size, and whether it needed the scroll bar
            var pp = p; var ss = sv; float mh = maxH; string nm = name;
            System.Action check = () =>
            {
                try
                {
                    var r = pp.worldBound; float content = ss.contentContainer.worldBound.height, view = ss.contentViewport.worldBound.height;
                    TreatmentRoom.W("panel", "drawn", $"{nm}: {R(r)} (max height {mh:0}) | content {content:0} high in a view of {view:0}{(content > view + 1 ? ": SCROLLS" : ": all visible")}");
                }
                catch (Exception e) { TreatmentRoom.Fail("panel drawn", e); }
            };
            try { p.schedule.Execute(check).ExecuteLater(400); } catch { }
        }

        static VisualElement Section(bool side, float grow, bool gapAfter)
        {
            var s = new VisualElement(); s.pickingMode = PickingMode.Ignore;
            if (side) { s.style.flexGrow = grow; s.style.flexShrink = 1; s.style.flexBasis = 0; if (gapAfter) s.style.marginRight = 28; }
            else if (gapAfter) s.style.marginBottom = 8;
            return s;
        }

        static Label Line(Summary.Line l) => Text(l.Text, l.Quiet ? 12f : 13f, l.Quiet ? Quiet : l.Flag ? Gold : Ink, 3);

        // WHO TO WATCH: player | injuries | days out | recurring | risk today
        static readonly float[] ColGrow = { 2.4f, 0.9f, 0.9f, 2.2f, 2.6f };
        static VisualElement Table(List<Summary.WatchRow> rows)
        {
            var t = new VisualElement(); t.pickingMode = PickingMode.Ignore;
            t.Add(TRow(new[] { "Player", "Injuries", "Days out", "Recurring", "Risk today" }, true, null));
            foreach (var r in rows)
            {
                var risk = r.Risk.Length == 0 ? "-" : r.Risk + (r.Factor.Length > 0 ? " (" + r.Factor.ToLowerInvariant() + ")" : "");
                var cells = new[] { r.Name, r.Injuries.ToString(), r.Days > 0 ? r.Days.ToString() : "-", r.Recurring.Length > 0 ? r.Recurring : "-", risk };
                t.Add(TRow(cells, false, r));
            }
            return t;
        }
        static VisualElement TRow(string[] cells, bool head, Summary.WatchRow r)
        {
            var row = new VisualElement(); row.pickingMode = PickingMode.Ignore; row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 1; row.style.paddingBottom = 2; row.style.borderBottomWidth = 1; row.style.borderBottomColor = Rule;
            for (int i = 0; i < cells.Length; i++)
            {
                var c = head ? Quiet : Ink;
                if (!head && r != null)
                {
                    if (i == 3 && r.Recurring.Length > 0) c = Gold;
                    if (i == 4 && r.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase)) c = Gold;
                    if (i == 4 && r.Risk.Length == 0) c = Quiet;
                }
                var l = Text(cells[i], head ? 11f : 12.5f, c, 0);
                l.style.flexGrow = ColGrow[i]; l.style.flexShrink = 1; l.style.flexBasis = 0; l.style.paddingRight = 8;
                row.Add(l);
            }
            return row;
        }

        static Label Text(string t, float size, Color c, float gap)
        {
            var l = new Label(t); l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = size; l.style.color = c; l.style.whiteSpace = WhiteSpace.Normal;
            // no default label padding or margins (they made each line about 26 units tall), just a small gap
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.marginTop = 0; l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginBottom = gap;
            return l;
        }
    }
}
