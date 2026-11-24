// FM27 Media Mod - Press Room 0.15.0: the Press Office's new look (Nik, 23 Nov: "need to bring press office up to this
// standard too now"). The same base as the Treatment Room (near-black cards, heading bands, pill tabs, the game's display font
// for headings) with the Press Office's orange. The sketch is the Design canvas "Press Office: the new look".
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class MediaOffice
    {
        static Color Hex(string h, float a = 1f)
        {
            h = h.TrimStart('#');
            float C(int i) => Convert.ToInt32(h.Substring(i, 2), 16) / 255f;
            return new Color(C(0), C(2), C(4), a);
        }
        static readonly Color PageBg = Hex("151517", 0.985f), CardBg = Hex("1C1C1F"), BandBg = Hex("232327"), Edge = Hex("34343A"), RowRule = Hex("2C2C31"),
            Text1 = Hex("F4F2EF"), Text2 = Hex("BDB8B1"), Orange = Hex("FF992E"), OnOrange = Hex("1A1206"), OrangeLight = Hex("FFB866"), OrangePale = Hex("FFD9A8"),
            OrangeDark = Hex("4A2E0E"), AlertEdge = Hex("7A4A14"), BtnBg = Hex("2B2B30"), BtnEdge = Hex("4A4A52"), IncBg = Hex("3A3A40"), Track = Hex("2F2F35"),
            WinBg = Hex("2E4A22"), WinFg = Hex("CFEFB6"), LossBg = Hex("5C1F25"), LossFg = Hex("FFC9CE");

        // the game's display font, from its own title bar (as the Treatment Room; Unity's FontDefinition has its own ==, so compare as objects)
        static bool FontTried; static int FontTries; static FontDefinition Display;
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
                if (hit != null) { Display = hit.resolvedStyle.unityFontDefinition; FontTried = true; PressRoom.W("office", "display font", "taken from the title bar"); }
            }
            catch (Exception e) { FontTried = true; PressRoom.Fail("office font", e); }
        }
        static T Disp<T>(T e) where T : VisualElement { try { if ((object)Display != null) e.style.unityFontDefinition = new StyleFontDefinition(Display); } catch { } return e; }

        static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r; }
        static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
        static void Pad(VisualElement e, float v, float h) { e.style.paddingTop = v; e.style.paddingBottom = v; e.style.paddingLeft = h; e.style.paddingRight = h; }
        static Label L(string t, float size, Color c, bool bold = false, float below = 0)
        {
            var l = new Label(t); l.style.fontSize = size; l.style.color = c; l.style.whiteSpace = WhiteSpace.Normal; l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.marginTop = 0; l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginBottom = below;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }
        static Label Caps(string t, Color c, float below = 6) { var l = L(t, 12, c, true, below); l.style.letterSpacing = 1.3f; return l; }
        static Label Chip(string t, Color bg, Color fg, bool pill = false)
        {
            var l = L(t, 12, fg, true); l.style.backgroundColor = bg; Pad(l, 3, pill ? 10 : 8); Radius(l, pill ? 10 : 6); l.style.alignSelf = Align.FlexStart; return l;
        }
        static VisualElement Flex(bool row, bool wrap = false)
        {
            var v = new VisualElement(); v.style.flexDirection = row ? FlexDirection.Row : FlexDirection.Column;
            if (wrap) v.style.flexWrap = Wrap.Wrap;
            if (row) v.style.alignItems = Align.Center;
            return v;
        }
        static VisualElement Panel(Color bg, Color edge, float radius = 12) { var v = new VisualElement(); v.style.backgroundColor = bg; Border(v, 1, edge); Radius(v, radius); return v; }
        static VisualElement Spacer(float h) { var v = new VisualElement(); v.style.height = h; return v; }
        static VisualElement Cell(VisualElement e, float grow) { e.style.flexGrow = grow; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.paddingRight = 10; return e; }
        // where to go in the game's own screens or another tab of the office (not a button)
        static Label GoTo(string t) { var l = Chip(t + "  ›", BtnBg, Text1); Border(l, 1, BtnEdge); Pad(l, 6, 12); l.style.marginTop = 4; l.style.unityFontStyleAndWeight = FontStyle.Normal; return l; }
        // a card with a heading band: the title, an optional pill, and a line on the right
        static (VisualElement Card, VisualElement Inner) Shell(string title, string chip, Color chipBg, Color chipFg, string right, VisualElement rightEl = null)
        {
            var card = Panel(CardBg, Edge, 14); card.style.marginBottom = 16; card.style.overflow = Overflow.Hidden;
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg;
            Pad(band, 14, 18); band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var left = Flex(true); left.style.marginRight = 12;
            var t = Disp(L(title, 18, Text1, true)); t.style.marginRight = 10; left.Add(t);
            if (chip.Length > 0) left.Add(Chip(chip, chipBg, chipFg, true));
            band.Add(left);
            if (rightEl != null) band.Add(rightEl); else if (right.Length > 0) band.Add(L(right, 13, Text2));
            card.Add(band);
            var inner = Flex(false); Pad(inner, 16, 18); card.Add(inner);
            return (card, inner);
        }
        static Label Foot(string t) { var f = L(t, 13, Text2); f.style.marginTop = 8; f.style.paddingTop = 12; f.style.borderTopWidth = 1; f.style.borderTopColor = RowRule; return f; }
        static Label MoodChip(int rel) => rel >= 6 ? Chip("Friendly", WinBg, WinFg) : rel >= 3 ? Chip("Warm", WinBg, WinFg) : rel >= -2 ? Chip("Neutral", IncBg, Text1) : rel >= -5 ? Chip("Cool", LossBg, LossFg) : Chip("Hostile", Hex("8A2C35"), White1);
        static readonly Color White1 = Hex("FFFFFF");
        static readonly Color RedLight = Hex("FF8A93"), AlertBg = Hex("2A1E12");
        static bool HasLetter(string t) { foreach (var ch in t) if (char.IsLetter(ch)) return true; return false; }
        static bool Same(Color a, Color b) => Math.Abs(a.r - b.r) < 0.01f && Math.Abs(a.g - b.g) < 0.01f && Math.Abs(a.b - b.b) < 0.01f;
        // the tabs along the top, as the Treatment Room's
        static Button TabPill(string t, System.Action act, bool on)
        {
            var b = new Button(); b.text = t;
            b.style.fontSize = 13; b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.color = on ? OnOrange : Text1;
            b.style.backgroundColor = on ? Orange : BtnBg; Border(b, 1, on ? Orange : BtnEdge); Radius(b, 8);
            Pad(b, 8, 14); b.style.marginLeft = 0; b.style.marginRight = 8; b.style.marginTop = 2; b.style.marginBottom = 2; b.style.minHeight = 34;
            System.Action safe = () => { try { act(); } catch (Exception e) { PressRoom.Fail("office click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        // a link to another tab of the office, for a card's heading band
        static Button TabLink(string label, string key)
        {
            var b = new Button(); b.text = label + "  ›";
            b.style.fontSize = 12.5f; b.style.color = Text1; b.style.backgroundColor = BtnBg; Border(b, 1, BtnEdge); Radius(b, 8);
            Pad(b, 5, 12); b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            System.Action safe = () => { try { TabNow = key; Refresh(); } catch (Exception e) { PressRoom.Fail("office click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
