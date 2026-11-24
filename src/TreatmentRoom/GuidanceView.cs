// FM27 Media Mod - Treatment Room 0.6.0: the Guidance page drawn, and the page's new look (Nik, 23 Nov: "as professional as
// the sketch... standard game fonts and presentational layouts. Less old skool"). The sketch is the Design canvas
// "Treatment Room: guidance from the team". Headings use the game's own display font, taken from its title bar
// (MainTabText); body text keeps the game's default font. UI Toolkit has no CSS gap or grid: spacing is margins.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class Room
    {
        // ---------- the look (one base for every part, the Treatment Room's red) ----------
        static Color Hex(string h, float a = 1f)
        {
            h = h.TrimStart('#');
            float C(int i) => Convert.ToInt32(h.Substring(i, 2), 16) / 255f;
            return new Color(C(0), C(2), C(4), a);
        }
        static readonly Color PageBg = Hex("151517", 0.985f), CardBg = Hex("1C1C1F"), BandBg = Hex("232327"), Edge = Hex("34343A"), RowRule = Hex("2C2C31"),
            Text1 = Hex("F4F2EF"), Text2 = Hex("BDB8B1"), Red = Hex("CF2E3B"), RedLight = Hex("FF8A93"), Pink = Hex("FFC9CE"), HighBg = Hex("5C1F25"),
            IncBg = Hex("3A3A40"), BtnBg = Hex("2B2B30"), BtnEdge = Hex("4A4A52"), AlertEdge = Hex("8A2C35"), Beyond = Hex("18181B"), White = Hex("FFFFFF");

        // the game's display font, from its own title bar
        static bool FontTried, FontFailLogged; static int FontTries; static FontDefinition Display;
        static void FindFont()
        {
            if (FontTried || Host == null) return;
            if (++FontTries > 5) { FontTried = true; return; }
            try
            {
                int seen = 0; TextElement hit = null;
                void Walk(VisualElement e, int d)
                {
                    if (e == null || hit != null || d > 60 || seen > 6000) return;
                    seen++;
                    if (e.name == "MainTabText") { var t = e.TryCast<TextElement>(); if (t != null) { hit = t; return; } }
                    for (int i = 0; i < e.childCount; i++) Walk(e.ElementAt(i), d + 1);
                }
                Walk(Host, 0);
                if (hit != null)
                {
                    Display = hit.resolvedStyle.unityFontDefinition; FontTried = true;
                    TreatmentRoom.W("room", "display font", "taken from the title bar (" + seen + " elements looked at)");
                }
            }
            catch (Exception e) { FontTried = true; TreatmentRoom.Fail("room font", e); }
        }
        static T Disp<T>(T e) where T : VisualElement { try { if ((object)Display != null) e.style.unityFontDefinition = new StyleFontDefinition(Display); } catch (Exception ex) { if (!FontFailLogged) { FontFailLogged = true; TreatmentRoom.Fail("room font apply", ex); } } return e; }

        static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r; }
        static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
        static void Pad(VisualElement e, float v, float h) { e.style.paddingTop = v; e.style.paddingBottom = v; e.style.paddingLeft = h; e.style.paddingRight = h; }
        // a label at exactly this size (the new pages), bold optional
        static Label L(string t, float size, Color c, bool bold = false, float below = 0)
        {
            var l = new Label(t); l.style.fontSize = size; l.style.color = c; l.style.whiteSpace = WhiteSpace.Normal;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.marginTop = 0; l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginBottom = below;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }
        static Label Caps(string t, Color c, float below = 6) { var l = L(t, 12, c, true, below); l.style.letterSpacing = 1.3f; return l; }
        static Label Chip(string t, Color bg, Color fg, bool pill = false)
        {
            var l = L(t, 12, fg, true); l.style.backgroundColor = bg; Pad(l, 3, pill ? 10 : 8); Radius(l, pill ? 10 : 6);
            l.style.alignSelf = Align.FlexStart; return l;
        }
        static Label RiskChip(int level, string word)
        {
            switch (level)
            {
                case 0: return Chip(word, Red, White);
                case 1: return Chip(word, HighBg, Pink);
                case 2: return Chip(word, IncBg, Text1);
                default: { var c = Chip(word, new Color(0, 0, 0, 0), Text2); Border(c, 1, BtnEdge); return c; }
            }
        }
        static VisualElement Flex(bool row, bool wrap = false)
        {
            var v = new VisualElement(); v.style.flexDirection = row ? FlexDirection.Row : FlexDirection.Column;
            if (wrap) v.style.flexWrap = Wrap.Wrap;
            if (row) v.style.alignItems = Align.Center;
            return v;
        }
        // a button in the new look: red for the main action, dark for the rest
        static Button Pill(string t, System.Action act, bool main)
        {
            var b = new Button(); b.text = t;
            b.style.fontSize = 13; b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.color = main ? White : Text1;
            b.style.backgroundColor = main ? Red : BtnBg; Border(b, 1, main ? Red : BtnEdge); Radius(b, 8);
            Pad(b, 8, 14); b.style.marginLeft = 0; b.style.marginRight = 8; b.style.marginTop = 2; b.style.marginBottom = 2; b.style.minHeight = 34;
            System.Action safe = () => { try { act(); } catch (Exception e) { TreatmentRoom.Fail("room click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        // where to go in the game's own screens (not a button: the Treatment Room never presses the game's buttons)
        static Label GoTo(string t) { var l = Chip(t + "  ›", BtnBg, Text1); Border(l, 1, BtnEdge); Pad(l, 7, 12); l.style.marginTop = 4; l.style.unityFontStyleAndWeight = FontStyle.Normal; return l; }
        static VisualElement Panel(Color bg, Color edge, float radius = 12)
        {
            var v = new VisualElement(); v.style.backgroundColor = bg; Border(v, 1, edge); Radius(v, radius); return v;
        }

        // ---------- the Guidance page ----------
        static void GuidanceView()
        {
            var g = Guidance.Build(Today, Notes.StaffName("risk"), Popout.Waiting());
            // 0.6.3 (Nik: "maybe also these should be at the top?"): who the advice comes from, with real faces where a facepack has them
            if (g.Dept.Count > 0)
            {
                var dh = Flex(true, true); dh.style.justifyContent = Justify.SpaceBetween; dh.style.marginBottom = 8;
                dh.Add(Caps("YOUR MEDICAL TEAM", Text2, 0));
                if (g.DeptRead.Length > 0) dh.Add(L(g.DeptRead, 13, Text2));
                Body.Add(dh);
                var row = Flex(true, true); row.style.alignItems = Align.Stretch; row.style.marginBottom = 12;
                foreach (var t in g.Dept)
                {
                    var tile = Panel(CardBg, Edge, 12); Pad(tile, 12, 14);
                    tile.style.flexGrow = 1; tile.style.flexShrink = 1; tile.style.flexBasis = new Length(22, LengthUnit.Percent); tile.style.minWidth = 220;
                    tile.style.marginRight = 10; tile.style.marginBottom = 10;
                    // a real face from the player's facepacks, for a member of staff the fact sheet names; none otherwise
                    Texture2D face = t.Name.Length > 0 ? Faces.ForName(t.Name) : null;
                    VisualElement words = tile;
                    if (face != null)
                    {
                        tile.style.flexDirection = FlexDirection.Row; tile.style.alignItems = Align.Center;
                        var pic = new VisualElement(); pic.style.width = 56; pic.style.height = 56; pic.style.flexShrink = 0; pic.style.marginRight = 12;
                        pic.style.backgroundColor = BtnBg; Radius(pic, 10);
                        pic.style.backgroundImage = new StyleBackground(face); pic.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                        tile.Add(pic);
                        words = Flex(false); words.style.flexGrow = 1; words.style.flexShrink = 1; tile.Add(words);
                    }
                    words.Add(L(t.Role, 13, Text2, false, 3));
                    if (t.Name.Length > 0) words.Add(Disp(L(t.Name, 15, Text1, true, 3)));
                    if (t.Line.Length > 0) words.Add(L(t.Line, 13, t.Name.Length > 0 ? Pink : Text1, false, 2));
                    if (t.Note.Length > 0) words.Add(L(t.Note, 13, Pink));
                    row.Add(tile);
                }
                Body.Add(row);
            }
            // right now
            Body.Add(Caps("RIGHT NOW", Text2, 10));
            var tiles = Flex(true, true); tiles.style.alignItems = Align.Stretch; tiles.style.marginBottom = 18;
            VisualElement Tile(bool alert)
            {
                var t = Panel(BandBg, alert ? AlertEdge : Edge); Pad(t, 16, 16);
                t.style.flexGrow = 1; t.style.flexShrink = 1; t.style.flexBasis = new Length(30, LengthUnit.Percent); t.style.minWidth = 250;
                t.style.marginRight = 12; t.style.marginBottom = 12; return t;
            }
            var t1 = Tile(false);
            t1.Add(L("Out injured", 13, Text2, false, 4));
            t1.Add(Disp(L(g.Out.ToString(), 32, Text1, true, 4)));
            t1.Add(L(g.OutSub, 13, Text2));
            tiles.Add(t1);
            var t2 = Tile(false);
            t2.Add(L("At risk", 13, Text2, false, 4));
            var nums = Flex(true); nums.style.alignItems = Align.FlexEnd; nums.style.marginBottom = 4;
            nums.Add(Disp(L(g.VeryHigh.ToString(), 32, Text1, true))); var vhw = L("very high", 14, Pink); vhw.style.marginLeft = 6; vhw.style.marginRight = 16; vhw.style.marginBottom = 6; nums.Add(vhw);
            nums.Add(Disp(L(g.High.ToString(), 32, Text1, true))); var hw = L("high", 14, Pink); hw.style.marginLeft = 6; hw.style.marginBottom = 6; nums.Add(hw);
            t2.Add(nums);
            t2.Add(L(g.RiskSub, 13, Text2));
            tiles.Add(t2);
            var t3 = Tile(g.Waiting.Count > 0);
            t3.Add(L(g.Waiting.Count > 0 ? "Waiting on you (" + g.Waiting.Count + ")" : "Waiting on you", 13, g.Waiting.Count > 0 ? Pink : Text2, true, 8));
            if (g.Waiting.Count == 0) t3.Add(L("Nothing for now.", 14, Text1));
            foreach (var w in g.Waiting)
            {
                var item = Flex(false); item.style.marginBottom = 10;
                item.Add(L(w.Text, 14, Text1));
                if (w.Path.Length > 0) item.Add(GoTo(w.Path));
                t3.Add(item);
            }
            tiles.Add(t3);
            Body.Add(tiles);

            // the guidance
            Body.Add(Caps("GUIDANCE FROM THE TEAM", Text2, 4));
            var intro = L(g.Intro, 14, Text2, false, 14); intro.style.maxWidth = 860; Body.Add(intro);
            if (g.Cards.Count == 0) { var none = Panel(CardBg, Edge, 14); Pad(none, 18, 20); none.Add(L(g.Assessed.Length == 0 ? "No risk assessment read yet. Open Training > Medical Centre > Risk Assessment once." : "Nothing in the latest assessment needs a word from the team.", 14, Text1)); none.style.marginBottom = 16; Body.Add(none); }
            foreach (var c in g.Cards) Body.Add(CardView(c));
            if (g.Beyond.Length > 0)
            {
                var b = Panel(Beyond, BtnEdge, 14); Pad(b, 16, 20); b.style.marginBottom = 16;
                b.Add(L("Beyond this department", 15, Text1, true, 6));
                var bt = L(g.Beyond, 14, Text2, false, 4); bt.style.maxWidth = 900; b.Add(bt);
                b.Add(GoTo("Medical Centre › Risk Assessment"));
                Body.Add(b);
            }

            // the department behind the advice (0.6.3: the team itself sits at the top; what it means stays here)
            if (g.DeptLines.Count > 0)
            {
                var d = Panel(CardBg, Edge, 14); Pad(d, 18, 20); d.style.marginBottom = 8;
                d.Add(Caps("THE DEPARTMENT BEHIND THIS ADVICE", Text2, 10));
                foreach (var line in g.DeptLines) d.Add(L(line, 14, Text1, false, 4));
                Body.Add(d);
            }
        }

        static VisualElement CardView(Guidance.Card c)
        {
            var card = Panel(CardBg, Edge, 14); card.style.marginBottom = 16; card.style.overflow = Overflow.Hidden;
            // the band: the problem, how many, who is advising
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg;
            Pad(band, 14, 18); band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var left = Flex(true); left.style.marginRight = 12;
            var title = Disp(L(c.Title, 18, Text1, true)); title.style.marginRight = 10; left.Add(title);
            left.Add(c.Level == 0 ? Chip(c.Count, Red, White, true) : c.Level == 1 ? Chip(c.Count, HighBg, Pink, true) : Chip(c.Count, IncBg, Text1, true));
            band.Add(left);
            band.Add(L("Advised by " + c.Advisor, 13, Text2));
            card.Add(band);
            var inner = Flex(false); Pad(inner, 16, 18);
            // what it is
            inner.Add(Caps("WHAT IT IS", RedLight, 4));
            var what = L(c.What, 14, Text1, false, 14); what.style.maxWidth = 900; inner.Add(what);
            // who to watch
            if (c.Rows.Count > 0)
            {
                inner.Add(Caps("WHO TO WATCH", RedLight, 6));
                var grow = c.Cols.Length == 5 ? new[] { 1.5f, 0.8f, 1.4f, 1f, 1.8f } : new[] { 1.5f, 1.6f, 1f, 2.2f };
                int riskCol = c.Cols.Length == 5 ? 1 : 2;
                var table = Panel(CardBg, Edge, 10); table.style.overflow = Overflow.Hidden; table.style.marginBottom = 8;
                var head = Flex(true); head.style.backgroundColor = BandBg; Pad(head, 8, 14);
                for (int i = 0; i < c.Cols.Length; i++) head.Add(Cell(L(c.Cols[i], 12, Text2), grow[i]));
                table.Add(head);
                for (int r = 0; r < c.Rows.Count; r++)
                {
                    var row = Flex(true); Pad(row, 8, 14); row.style.borderTopWidth = 1; row.style.borderTopColor = RowRule;
                    var cells = c.Rows[r];
                    for (int i = 0; i < cells.Length && i < grow.Length; i++)
                    {
                        VisualElement cell;
                        if (i == riskCol) { var holder = new VisualElement(); holder.style.flexDirection = FlexDirection.Row; holder.Add(RiskChip(c.RowLevel[r], cells[i])); cell = holder; }
                        else cell = L(cells[i], 14, i == 0 ? Text1 : i == cells.Length - 1 ? Text2 : Text1, i == 0);
                        row.Add(Cell(cell, grow[i]));
                    }
                    table.Add(row);
                }
                inner.Add(table);
                if (c.Extra.Length > 0) inner.Add(L(c.Extra, 13, Text2, false, 6));
                inner.Add(Spacer(8));
            }
            // what we'd do
            if (c.Acts.Count > 0)
            {
                inner.Add(Caps("WHAT WE'D DO", RedLight, 6));
                foreach (var a in c.Acts)
                {
                    var row = Flex(true, true); row.style.justifyContent = Justify.SpaceBetween; row.style.marginBottom = 8;
                    var t = L(a.Text, 14, Text1); t.style.flexGrow = 1; t.style.flexShrink = 1; t.style.flexBasis = new Length(55, LengthUnit.Percent); t.style.marginRight = 12; t.style.minWidth = 300;
                    row.Add(t);
                    if (a.Plan.Length > 0)
                    {
                        var open = a.Players.Select(p => Notes.OpenPlan(p.Who)).Where(n => n != null).ToList();
                        if (open.Count == a.Players.Count)
                            row.Add(Chip("Planned: " + Notes.PlanLabel(open[0].What).ToLowerInvariant() + " (" + open[0].Status + ")", BandBg, Text2));
                        else
                        {
                            var act = a; var adviser = c.Advisor.Split(',')[0];
                            row.Add(Pill(a.Label, () =>
                            {
                                Note p = null;
                                foreach (var who in act.Players) if (Notes.OpenPlan(who.Who) == null) p = Notes.Plan(who.Who, who.Name, act.Plan, act.Reason + " (advice from " + adviser + ")", Today);
                                Ledger.Save();
                                Flash = Notes.PlanLabel(act.Plan) + " for " + string.Join(", ", act.Players.Select(x => x.Name)) + ": " + (p != null ? Notes.Guide(p) : "");
                                Refresh();
                            }, a.Strong));
                        }
                    }
                    inner.Add(row);
                }
            }
            // why this much
            if (c.Footer.Length > 0)
            {
                var f = L(c.Footer, 13, Text2); f.style.marginTop = 6; f.style.paddingTop = 12; f.style.borderTopWidth = 1; f.style.borderTopColor = RowRule;
                inner.Add(f);
            }
            card.Add(inner);
            return card;
        }
        static VisualElement Cell(VisualElement e, float grow) { e.style.flexGrow = grow; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.paddingRight = 10; return e; }
        static VisualElement Spacer(float h) { var v = new VisualElement(); v.style.height = h; return v; }
    }
}
