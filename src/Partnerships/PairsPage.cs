// FM27 Media Mod - Partnerships 0.3.0: the Partnerships page in the new look (the Treatment Room's and the Press Office's base, in green).
// The approved sketch is the Design canvas "Partnerships: the new look" (approved by Nik, 24 Nov).
//   This XI: the pitch (name chips as wide as the name; a pair that skips a player curves round below the row; click a player to
//   focus on their partnerships, click a line's dot for that pair), RIGHT NOW beside it, then a card per partnership in the XI with
//   results with and without and who's next. Every partnership: the same cards for every pair on record. Coming through: the
//   neighbours not recognised yet. What the players say: the happiness lines, warnings first.
// No in or out of possession switch: the lines are the same in both (probe, 22 Nov). Our own elements only; nothing of the game's is touched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    // a point on our pitch (plain managed maths: no calls into Unity's own vector code)
    internal struct V
    {
        public float x, y;
        public V(float x, float y) { this.x = x; this.y = y; }
        public static V operator +(V a, V b) => new V(a.x + b.x, a.y + b.y);
        public static V operator -(V a, V b) => new V(a.x - b.x, a.y - b.y);
        public static V operator *(V a, float k) => new V(a.x * k, a.y * k);
        public static V operator *(float k, V a) => new V(a.x * k, a.y * k);
        public static V operator /(V a, float k) => new V(a.x / k, a.y / k);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static float Dot(V a, V b) => a.x * b.x + a.y * b.y;
    }

    internal static class PairsPage
    {
        static VisualElement Host, Tab, Page, Body, TabsRow;
        static Label TitleL, SubL;
        internal static VisualElement HostEl => Host;
        static DateTime NextCheck = DateTime.MinValue;
        static string View = "xi", SelPair = "", SelPlayer = "";
        static bool NoRotate, Pending;

        static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        static readonly Color PageBg = Hex(0x151517, 0.985f), CardBg = Hex(0x1C1C1F), BandBg = Hex(0x232327), Edge = Hex(0x34343A), RowRule = Hex(0x2C2C31),
            Text1 = Hex(0xF4F2EF), Text2 = Hex(0xBDB8B1), Green = Hex(0x5CD65C), OnGreen = Hex(0x0D1A0D), GreenLight = Hex(0xA8F0A0), GreenDark = Hex(0x1F3A1F), GreenPale = Hex(0xCFEFB6),
            BtnBg = Hex(0x2B2B30), BtnEdge = Hex(0x4A4A52), IncBg = Hex(0x3A3A40), Turf = Hex(0x13261A), TurfEdge = Hex(0x2C4A33), Chalk = Hex(0x2F5A3A),
            AmberBg = Hex(0x4A3510), AmberFg = Hex(0xF5C98A), AmberEdge = Hex(0x5C4A1F), RedBg = Hex(0x5C1F25), RedFg = Hex(0xFFC9CE), ChipBg = Hex(0x232327);

        // the scale (seven steps and untested; the tactics pitch's own strong-line green at the top). The pitch overlay uses these too.
        internal static Color StepColour(int step)
        {
            switch (step)
            {
                case 7: return Hex(0x4EEB3D);
                case 6: return Hex(0x63B52E);
                case 5: return Hex(0x639922);
                case 4: case 3: return Hex(0xEF9F27);
                case 2: case 1: return Hex(0xE24B4A);
                case -1: return Hex(0xA8D08D);
                default: return Hex(0x888780);
            }
        }
        internal static float Thick(int step) { switch (step) { case 7: return 7f; case 6: return 5.5f; case 5: return 4.5f; case 4: return 5f; case 3: return 3.6f; case 2: return 3f; case 1: return 4.6f; case -1: return 2.6f; default: return 2.2f; } }
        internal enum Dash { Solid, Dashed, Dotted }
        internal static Dash Pattern(PairView v) => v.State == "Breaking down" ? Dash.Dashed : v.Step == 0 ? Dash.Dotted : v.Step <= 2 || v.Step == -1 ? Dash.Dashed : Dash.Solid;

        // ---------- the tab ----------
        internal static void EnsureTab(VisualElement any)
        {
            if (DateTime.Now < NextCheck) return;
            NextCheck = DateTime.Now.AddSeconds(2);
            try
            {
                if (Tab != null && Tab.panel != null) { Place(); PtPopout.Update(); return; }   // 0.2.2: the popout's status, pill and notes
                VisualElement host = null;
                for (var e = any; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { host = e; break; }
                if (host == null) return;
                Host = host;
                // 0.3.0: the tab says PARTNERSHIPS (Core's popout, when installed, hides it and shows its own button)
                Tab = OldBtn("P\nA\nR\nT\nN\nE\nR\nS\nH\nI\nP\nS", Toggle, Green);
                Tab.name = "FM27MM-PairsTab"; Tab.tooltip = "Partnerships"; Tab.style.color = OnGreen;
                Tab.style.position = Position.Absolute; Tab.style.right = 0; Tab.style.top = Partnerships.TabTop.Value;
                Tab.style.width = 22; Tab.style.fontSize = 10; Tab.style.whiteSpace = WhiteSpace.Normal;
                Tab.style.paddingLeft = 2; Tab.style.paddingRight = 2; Tab.style.paddingTop = 6; Tab.style.paddingBottom = 6; Tab.style.marginRight = 0;
                Tab.style.borderTopLeftRadius = 6; Tab.style.borderBottomLeftRadius = 6; Tab.style.borderTopRightRadius = 0; Tab.style.borderBottomRightRadius = 0;
                host.Add(Tab); LastTop = -1f;
                PtPopout.Attach(Tab);   // 0.2.2: Core's popout reads these (hidden) children
                Partnerships.W("page", "tab added", "top " + Partnerships.TabTop.Value);
            }
            catch (Exception e) { Partnerships.Fail("tab", e); }
        }

        // other tabs share the right edge: start at TabTop and move below any narrow element there that would overlap
        static float LastTop = -1f;
        static void Place()
        {
            try
            {
                if (Tab == null || Host == null) return;
                var hb = Host.worldBound; var tb = Tab.worldBound;
                if (float.IsNaN(hb.width) || hb.width < 100) return;
                float h = !float.IsNaN(tb.height) && tb.height > 20 ? tb.height : 150f;
                var taken = new List<(float a, float b, string n)>();
                int n = Host.childCount;
                for (int i = 0; i < n; i++)
                {
                    VisualElement c = null; try { c = Host.ElementAt(i); } catch { }
                    if (c == null || c.Pointer == Tab.Pointer || (Page != null && c.Pointer == Page.Pointer)) continue;
                    var r = c.worldBound;
                    if (float.IsNaN(r.width) || r.width < 4 || r.width > 48 || r.height < 20 || r.height > 400) continue;
                    if (r.xMax < hb.xMax - 4) continue;
                    taken.Add((r.yMin - hb.yMin, r.yMax - hb.yMin, c.name ?? ""));
                }
                float y = Partnerships.TabTop.Value;
                for (int round = 0; round < 12; round++)
                {
                    bool moved = false;
                    foreach (var t in taken) if (y < t.b + 8 && y + h + 8 > t.a) { y = t.b + 10; moved = true; }
                    if (!moved) break;
                }
                if (Math.Abs(y - LastTop) > 2)
                {
                    Tab.style.top = y;
                    Partnerships.W("page", "tab placed", $"top {y:0} (asked for {Partnerships.TabTop.Value}); other tabs on the right edge: " + string.Join(", ", taken.Select(t => $"{(t.n.Length > 0 ? t.n : "?")} {t.a:0}-{t.b:0}")));
                    LastTop = y;
                }
            }
            catch (Exception e) { Partnerships.Fail("tab place", e); }
        }

        // ---------- the page ----------
        static void Toggle()
        {
            if (Page != null && Page.panel != null) { Page.RemoveFromHierarchy(); Page = null; return; }
            if (Host == null) return;
            PtPopout.Seen();
            PtInsight.OppName = OppFromFacts;
            try { Shell(); } catch (Exception e) { Partnerships.Fail("page shell", e); return; }
            if (PtDate.Text.Length > 0) PtLedger.Recompute(PtDate.Text);
            Build();
            Partnerships.W("page", "opened", PtLedger.Summary(PtDate.Text) + ((object)Display != null ? " · game display font" : ""));
        }

        // the ledger changed: rebuild if the page is open (at most a few times a second)
        internal static void Refresh()
        {
            if (Page == null || Page.panel == null || Pending) return;
            Pending = true;
            try { Page.schedule.Execute((System.Action)(() => { Pending = false; try { Build(); } catch (Exception e) { Partnerships.Fail("page", e); } })).ExecuteLater(250); }
            catch (Exception e) { Pending = false; Partnerships.Fail("page refresh", e); }
        }

        static void Shell()
        {
            float hw = 1600f, hh = 900f;
            try { var b = Host.worldBound; if (!float.IsNaN(b.width) && b.width > 400) { hw = b.width; hh = b.height; } } catch { }
            FindFont();
            Page = new VisualElement(); Page.name = "FM27MM-Partnerships";
            Page.style.position = Position.Absolute; Page.style.right = 40; Page.style.top = 50;
            Page.style.width = Math.Min(1180f, hw - 120f); Page.style.maxHeight = hh - 90f;
            Page.style.backgroundColor = PageBg; Border(Page, 1, Edge); Radius(Page, 14); Page.style.overflow = Overflow.Hidden;
            var head = Flex(true, true); head.style.justifyContent = Justify.SpaceBetween; Pad(head, 16, 22); head.style.borderBottomWidth = 1; head.style.borderBottomColor = Edge;
            var left = Flex(true);
            // the mark: two linked rings on a green tile
            var mark = new VisualElement(); mark.style.width = 40; mark.style.height = 40; mark.style.backgroundColor = Green; Radius(mark, 10); mark.style.marginRight = 12;
            foreach (var x in new[] { 6f, 16f })
            {
                var ring = new VisualElement(); ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute;
                ring.style.left = x; ring.style.top = 11; ring.style.width = 18; ring.style.height = 18; Border(ring, 2.5f, OnGreen); Radius(ring, 9);
                mark.Add(ring);
            }
            left.Add(mark);
            var titles = Flex(false);
            var cap = L("PARTNERSHIPS", 12, GreenLight, true, 1); cap.style.letterSpacing = 2f; titles.Add(cap);
            TitleL = Disp(L("This XI", 20, Text1, true)); titles.Add(TitleL);
            SubL = L("", 13, Text2); SubL.style.marginTop = 2; titles.Add(SubL);
            left.Add(titles); head.Add(left);
            head.Add(Pill("Close", Toggle, false));
            Page.Add(head);
            TabsRow = Flex(true, true); Pad(TabsRow, 10, 22); TabsRow.style.borderBottomWidth = 1; TabsRow.style.borderBottomColor = Edge; Page.Add(TabsRow);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; scroll.style.maxHeight = Math.Max(240f, hh - 250f);
            try { scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; } catch { }
            Body = new VisualElement(); Pad(Body, 18, 22); scroll.Add(Body);
            Page.Add(scroll);
            Host.Add(Page);
        }

        static readonly (string Key, string Label, string Title)[] Views =
            { ("xi", "This XI", "This XI"), ("all", "Every partnership", "Every partnership"), ("next", "Coming through", "Coming through"), ("say", "What the players say", "What the players say") };

        static void Build()
        {
            if (Page == null || Body == null) return;
            var today = PtDate.Text;
            try
            {
                if (TitleL != null) TitleL.text = Views.FirstOrDefault(v => v.Key == View).Title ?? "Partnerships";
                if (SubL != null) SubL.text = (today.Length > 0 ? Dow(today) + today + "  ·  " : "") + PtLedger.Summary(today);
                TabsRow.Clear();
                foreach (var v in Views) { var k = v.Key; TabsRow.Add(Pill(v.Label, () => { View = k; Build(); }, View == k)); }
            }
            catch (Exception e) { Partnerships.Fail("page header", e); }
            Body.Clear();
            var shape = ShapeNow();
            Part(() =>
            {
                switch (View)
                {
                    case "all": AllView(shape, today); break;
                    case "next": NextView(shape, today); break;
                    case "say": SayView(); break;
                    default: XiView(shape, today); break;
                }
            }, View);
            var foot = L("Read from the tactics pitch, Personal › Happiness and the match screen. Nothing is changed.", 12.5f, Text2);
            foot.style.marginTop = 8; Body.Add(foot);
            if (PtLedger.Dirty) { try { PtLedger.Save(); } catch (Exception e) { Partnerships.Fail("save after page", e); } }
        }
        static List<Slot> ShapeNow() => PtLedger.Shape.TryGetValue("in possession", out var sh) ? sh : PtLedger.Shape.Values.FirstOrDefault();
        static string Dow(string d)
        {
            try { var p = d.Split('/'); return new DateTime(int.Parse(p[2]), int.Parse(p[1]), int.Parse(p[0])).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture) + " "; } catch { return ""; }
        }
        static void Part(System.Action build, string what)
        {
            try { build(); }
            catch (Exception e) { Partnerships.Fail("page " + what, e); Body.Add(L("This part could not be drawn (" + e.GetType().Name + "). The Partnerships log has the details.", 13, Text2, false, 10)); }
        }

        // a club's short code on the scoreboard ("SOU") to its name, from the fact sheet's last and next opponents
        static readonly Dictionary<string, string> OppSeen = new Dictionary<string, string>();
        static string OppFromFacts(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;
            if (OppSeen.TryGetValue(code, out var n)) return n;
            try
            {
                var f = System.IO.Path.Combine(PtLedger.Dir, "game facts.tsv");
                if (File.Exists(f))
                    foreach (var line in File.ReadAllLines(f))
                    {
                        var p = line.Split('\t');
                        if (p.Length < 2 || (p[0] != "previous_opponent" && p[0] != "next_opponent")) continue;
                        var name = p[1].Trim();
                        var letters = new string(name.Where(char.IsLetter).ToArray()).ToUpperInvariant();
                        if (name.Length > 0 && letters.StartsWith(code.ToUpperInvariant(), StringComparison.Ordinal)) { OppSeen[code] = name; return name; }
                    }
            }
            catch { }
            return code;
        }

        // ---------- This XI ----------
        static void XiView(List<Slot> shape, string today)
        {
            var row = Flex(true, true); row.style.alignItems = Align.FlexStart; row.style.marginBottom = 22;
            var left = Flex(false); left.style.width = PW; left.style.flexShrink = 0; left.style.marginRight = 20;
            BuildPitch(left, shape, today);
            var right = Flex(false); right.style.flexGrow = 1; right.style.flexShrink = 1; right.style.flexBasis = 0; right.style.minWidth = 300;
            PairView sel = null;
            if (SelPair.Length > 0)
            {
                sel = PtLedger.Views(today).FirstOrDefault(v => v.P.Key == SelPair);
                if (sel == null) { var ab = SelPair.Split('|'); if (ab.Length == 2) sel = PtLedger.Transient(ab[0], ab[1], today); }
            }
            if (sel != null) right.Add(PairDetail(sel, shape));
            else if (SelPlayer.Length > 0) right.Add(PlayerCard(SelPlayer, shape, today));
            else RightNow(right, shape, today);
            row.Add(left); row.Add(right); Body.Add(row);

            var names = new HashSet<string>(shape?.Select(s => s.Name) ?? Enumerable.Empty<string>());
            var inXi = PtLedger.Views(today).Where(v => PtInsight.Recognised(v.P) && names.Contains(v.P.A) && names.Contains(v.P.B)).ToList();
            Body.Add(Caps("PARTNERSHIPS IN THIS XI", Text2, 4));
            bool early = inXi.Any(v => PtInsight.TooEarly(PtInsight.Compare(v.P)));
            int m = PtInsight.TrackedMatches;
            Body.Add(L("Results by who started: both, or one without the other." + (early ? " Early days: " + m + (m == 1 ? " match" : " matches") + " on record, so no verdict until each side has " + PtInsight.Verdict + "." : ""), 14, Text2, false, 12));
            if (inXi.Count == 0) Body.Add(L(shape == null ? "Open the tactics screen once and your XI appears here." : "No recognised partnership in this XI yet.", 14, Text1, false, 12));
            foreach (var v in inXi) Part(() => Body.Add(PairBlock(v, shape, true)), "pair " + v.P.Key);
        }

        static VisualElement Tile(Color edge)
        {
            var t = Panel(CardBg, edge, 12); Pad(t, 14, 16); t.style.marginBottom = 12; return t;
        }
        static void RightNow(VisualElement right, List<Slot> shape, string today)
        {
            right.Add(Caps("RIGHT NOW", Text2, 8));
            var names = new HashSet<string>(shape?.Select(s => s.Name) ?? Enumerable.Empty<string>());
            var rec = PtLedger.Views(today).Where(v => PtInsight.Recognised(v.P)).ToList();
            var inXi = rec.Where(v => names.Contains(v.P.A) && names.Contains(v.P.B)).ToList();
            var t1 = Tile(Edge);
            t1.Add(L("Together in this XI", 13, Text2, false, 4));
            var big = Flex(true); big.style.alignItems = Align.FlexEnd; big.style.marginBottom = 4;
            big.Add(Disp(L(inXi.Count.ToString(), 30, Text1, true))); var of = L("  of " + rec.Count, 16, Text2); of.style.marginBottom = 4; big.Add(of);
            t1.Add(big);
            if (inXi.Count > 0)
            {
                var words = inXi.Select(v => v.Word.ToLowerInvariant()).Distinct().ToList();
                t1.Add(L(string.Join(" · ", inXi.Select(v => v.P.A + " and " + v.P.B + Zoned(v.P, shape))) + "." + (words.Count == 1 ? (inXi.Count == 1 ? " " + Cap(words[0]) + "." : " All " + Num(inXi.Count) + " " + words[0] + ".") : ""), 14, Text1));
            }
            right.Add(t1);
            var split = rec.Where(v => shape != null && (names.Contains(v.P.A) ^ names.Contains(v.P.B))).OrderByDescending(v => v.Step).ToList();
            foreach (var v in split.Take(2))
            {
                var missing = names.Contains(v.P.A) ? v.P.B : v.P.A;
                var w = PtInsight.Compare(v.P);
                var t = Tile(AmberEdge);
                t.Add(L("Split up here", 13, AmberFg, true, 4));
                t.Add(L(v.P.A + " and " + v.P.B, 16, Text1, true, 4));
                var why = missing + " isn't in this XI.";
                if (w.Together.N > 0) why += " They started together " + Times(w.Together.N) + ": " + string.Join(", ", w.Together.Games.Select(g => g.R.Text)) + ".";
                t.Add(L(why, 14, Text2));
                right.Add(t);
            }
            var say = PtInsight.PlayersSay();
            var warn = say.Where(s => s.Warning).ToList();
            var t3 = Tile(warn.Count > 0 ? AmberEdge : Edge);
            t3.Add(L("What the players say", 13, Text2, false, 4));
            t3.Add(L(warn.Count == 0 ? "No warnings" : warn.Count == 1 ? "1 warning" : warn.Count + " warnings", 16, warn.Count > 0 ? AmberFg : Text1, true, 4));
            if (warn.Count > 0) foreach (var s in warn.Take(2)) t3.Add(L(PtInsight.SayLine(s), 14, Text1, false, 2));
            else t3.Add(L((say.Count > 0 ? PtInsight.SayLine(say[0]) + " " : "") + "Nobody has said a partnership is weak.", 14, Text2));
            right.Add(t3);
            var ct = PtInsight.ComingThrough(shape, today).Count(p => p.Fledgling);
            if (ct > 0)
            {
                var t4 = Tile(Edge);
                t4.Add(L("Coming through", 13, Text2, false, 4));
                t4.Add(L(Num(ct, true) + " fledgling " + (ct == 1 ? "pair" : "pairs") + " in this XI.", 14, Text1, false, 6));
                t4.Add(Pill("See who", () => { View = "next"; Build(); }, false));
                right.Add(t4);
            }
        }
        static string Zoned(Pair p, List<Slot> shape) { var z = PtLedger.Zone(p, shape); return z.Length > 0 ? " " + z : ""; }

        // a partnership's card: results with and without, and who's next
        static VisualElement PairBlock(PairView v, List<Slot> shape, bool inXi)
        {
            var card = Panel(CardBg, Edge, 14); card.style.marginBottom = 16; card.style.overflow = Overflow.Hidden;
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg;
            Pad(band, 14, 18); band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var lt = Flex(true); lt.style.marginRight = 12;
            var t = Disp(L(v.P.A + " and " + v.P.B, 18, Text1, true)); t.style.marginRight = 10; lt.Add(t);
            lt.Add(StepChip(v));
            band.Add(lt);
            var rt = Flex(true);
            var zone = PtLedger.Zone(v.P, shape);
            var since = v.P.Si && v.P.SiFirst.Length > 0 ? "recognised since " + v.P.SiFirst : v.P.FeelDate.Length > 0 ? "in " + v.P.FeelBy + "'s words since " + v.P.FeelDate : "";
            var info = string.Join(" · ", new[] { Cap(zone), since, inXi ? "" : "not together in this XI" }.Where(x => x.Length > 0));
            if (info.Length > 0) { var il = L(info, 13, Text2); il.style.marginRight = 12; rt.Add(il); }
            var vv = v; rt.Add(Pill("On the pitch", () => { View = "xi"; SelPair = vv.P.Key; SelPlayer = ""; Build(); }, false));
            band.Add(rt);
            card.Add(band);

            var inner = Flex(true, true); inner.style.alignItems = Align.FlexStart; Pad(inner, 16, 18);
            var w = PtInsight.Compare(v.P);
            var ww = Flex(false); ww.style.flexGrow = 1; ww.style.flexShrink = 1; ww.style.flexBasis = 460; ww.style.minWidth = 320; ww.style.marginRight = 20;
            ww.Add(Caps("WITH AND WITHOUT", Text2, 8));
            var grid = Flex(true); grid.style.alignItems = Align.Stretch;
            grid.Add(SideTile("Started together", w.Together, v.P, false));
            grid.Add(SideTile("One without the other", w.Apart, v.P, true, w.Neither));
            ww.Add(grid);
            if (w.NoScore > 0) ww.Add(L(Num(w.NoScore, true) + (w.NoScore == 1 ? " match" : " matches") + " without a score read are left out.", 12.5f, Text2, false, 0));
            // 0.3.0 (Nik: "Are Mengi and Tang playing well when DCR and DCL vs. DCC and DCR?"): each arrangement they've played in
            var pos = PtInsight.ByPosition(v.P);
            if (pos.Count > 0)
            {
                var ph = Caps("BY POSITION", Text2, 6); ph.style.marginTop = 14; ww.Add(ph);
                foreach (var a in pos.Take(4)) ww.Add(L(PtInsight.ArrangementLine(v.P, a), 14, Text1, false, 4));
                ww.Add(L(pos.Count == 1 ? "Only ever together in these positions so far." : "Ratings are each player's for the whole match, averaged for the pair.", 12.5f, Text2, false, 0));
            }
            inner.Add(ww);
            var nx = Flex(false); nx.style.flexGrow = 1; nx.style.flexShrink = 1; nx.style.flexBasis = 380; nx.style.minWidth = 280;
            nx.Add(Caps("WHO'S NEXT", Text2, 8));
            foreach (var (keep, miss) in new[] { (v.P.B, v.P.A), (v.P.A, v.P.B) })
            {
                var lines = PtInsight.WhoIsNext(keep, miss);
                var l = Rich("<b>If " + miss + " is out:</b> " + (lines.Count > 0 ? string.Join(" ", lines) : "nobody has played in " + miss + "'s place beside " + keep + " yet."), 14, Text1);
                l.style.marginBottom = 8; nx.Add(l);
            }
            inner.Add(nx);
            card.Add(inner);
            var foot = PtInsight.TooEarly(w)
                ? "Too early to judge: " + Num(w.Together.N) + " started together, " + Num(w.Apart.N) + " one without the other. A verdict needs " + PtInsight.Verdict + " each way."
                : Verdict(w);
            var f = L(foot, 13, Text2); Pad(f, 12, 18); f.style.borderTopWidth = 1; f.style.borderTopColor = RowRule;
            card.Add(f);
            return card;
        }
        static string Verdict(PtInsight.WithWithout w)
        {
            double a = w.Together.Pts / (double)w.Together.N, b = w.Apart.Pts / (double)w.Apart.N;
            double ca = w.Together.PerGame(r => r.Against), cb = w.Apart.PerGame(r => r.Against);
            if (a - b >= 0.3) return "Better together: " + (a - b).ToString("0.0") + " more points a game" + (cb - ca >= 0.2 ? ", and " + (cb - ca).ToString("0.0") + " fewer goals conceded." : ".");
            if (b - a >= 0.3) return "No better together so far: " + (b - a).ToString("0.0") + " more points a game with one of them missing.";
            return "Much the same with or without each other so far.";
        }
        static VisualElement SideTile(string title, PtInsight.Side s, Pair p, bool apart, int neither = 0)
        {
            var t = Panel(BandBg, BandBg, 10); Pad(t, 12, 14); t.style.flexGrow = 1; t.style.flexShrink = 1; t.style.flexBasis = 0; if (!apart) t.style.marginRight = 10;
            t.Add(L(title + (s.N > 0 ? " · " + s.N + (s.N == 1 ? " match" : " matches") : ""), 13, Text2, false, 4));
            if (s.N == 0)
            {
                t.Add(Disp(L("None yet", 22, Text2, true, 4)));
                t.Add(L(apart ? (neither > 0 ? "Both rested " + Times(neither) + "; never one without the other." : "Always started together.") : "Never started together yet.", 13, Text2));
                return t;
            }
            var big = Flex(true); big.style.alignItems = Align.FlexEnd; big.style.marginBottom = 4;
            big.Add(Disp(L(s.Ppg, 24, Text1, true))); var pg = L("  points a game", 13, Text2); pg.style.marginBottom = 3; big.Add(pg);
            t.Add(big);
            string list = s.N <= 3
                ? string.Join(", ", s.Games.Select(g => g.R.Text + (apart && g.Only.Length > 0 ? " (no " + (g.Only == p.A ? p.B : p.A) + ")" : "")))
                : s.Record;
            t.Add(L("Scored " + s.Scored + ", conceded " + s.Conceded + " a game · " + list, 13, Text2));
            return t;
        }
        static Label StepChip(PairView v)
        {
            var (bg, fg) = v.Step >= 5 ? (GreenDark, GreenPale) : v.Step >= 3 ? (AmberBg, AmberFg) : v.Step >= 1 ? (RedBg, RedFg) : v.Step == -1 ? (GreenDark, GreenPale) : (IncBg, Text1);
            return Chip(v.Word + (v.Trend.Length > 0 ? ", " + v.Trend : "") + (v.State.Length > 0 ? " · " + v.State.ToLowerInvariant() : ""), bg, fg, true);
        }

        // ---------- Every partnership ----------
        static void AllView(List<Slot> shape, string today)
        {
            var names = new HashSet<string>(shape?.Select(s => s.Name) ?? Enumerable.Empty<string>());
            var all = PtLedger.Views(today).Where(v => PtInsight.Recognised(v.P) || v.P.Feel.Length > 0).ToList();
            Body.Add(L("Every partnership on record, the ones in this XI first. Results by who started; who's next from the matches seen.", 14, Text2, false, 14));
            if (all.Count == 0) { Body.Add(L("None yet. Partnerships are read from the tactics pitch and from what players say on Personal › Happiness.", 14, Text1, false, 10)); return; }
            foreach (var v in all.OrderByDescending(v => names.Contains(v.P.A) && names.Contains(v.P.B)).ThenByDescending(v => v.Step))
                Part(() => Body.Add(PairBlock(v, shape, names.Contains(v.P.A) && names.Contains(v.P.B))), "pair " + v.P.Key);
        }

        // ---------- Coming through ----------
        static void NextView(List<Slot> shape, string today)
        {
            var l = PtInsight.ComingThrough(shape, today);
            var fl = l.Where(p => p.Fledgling).ToList();
            var near = l.Where(p => !p.Fledgling && p.V.Starts >= 2).Take(4).ToList();
            var card = Panel(CardBg, Edge, 14); card.style.overflow = Overflow.Hidden; card.style.marginBottom = 16;
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg; Pad(band, 14, 18);
            band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var lt = Flex(true); var t = Disp(L("Coming through", 18, Text1, true)); t.style.marginRight = 10; lt.Add(t);
            lt.Add(Chip(fl.Count + " fledgling", GreenDark, GreenPale, true)); band.Add(lt);
            band.Add(L("Neighbours not recognised yet · fledgling = " + PtLedger.FledglingStarts + "+ starts together, rated " + PtLedger.FledglingRating.ToString("0.0") + "+ when paired", 13, Text2));
            card.Add(band);
            var inner = Flex(false); Pad(inner, 10, 18);
            var grow = new[] { 1.6f, 1.1f, 0.9f, 1f, 1.4f };
            var head = Flex(true); head.style.paddingTop = 6; head.style.paddingBottom = 6; head.style.borderBottomWidth = 1; head.style.borderBottomColor = RowRule;
            var heads = new[] { "PAIR", "WHERE", "STARTS TOGETHER", "RATED WHEN PAIRED", "STATUS" };
            for (int i = 0; i < heads.Length; i++) head.Add(Cell(Caps(heads[i], Text2, 0), grow[i]));
            inner.Add(head);
            if (fl.Count + near.Count == 0) inner.Add(L(shape == null ? "Open the tactics screen once and your XI appears here." : "Nobody in this XI is close yet: pairs show here once they've started together.", 14, Text2, false, 6));
            foreach (var p in fl.Concat(near))
            {
                var r = Flex(true); Pad(r, 9, 0); r.style.borderBottomWidth = 1; r.style.borderBottomColor = RowRule;
                var pp = p;
                var nameB = Pill(p.V.P.A + " and " + p.V.P.B, () => { View = "xi"; SelPair = pp.V.P.Key; SelPlayer = ""; Build(); }, false);
                nameB.style.unityTextAlign = TextAnchor.MiddleLeft;
                r.Add(Cell(nameB, grow[0]));
                r.Add(Cell(L(Cap(p.Zone), 13, Text2), grow[1]));
                r.Add(Cell(L(p.V.Starts.ToString(), 14, Text1), grow[2]));
                r.Add(Cell(L(p.V.RatedN > 0 ? p.V.RatingAvg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-", 14, Text1), grow[3]));
                var why = p.Fledgling ? "Fledgling" : p.V.Starts < PtLedger.FledglingStarts ? "Not yet: " + Num(p.V.Starts) + (p.V.Starts == 1 ? " start" : " starts") : "Not yet: rated under " + PtLedger.FledglingRating.ToString("0.0");
                r.Add(Cell(L(why, 13, p.Fledgling ? GreenLight : Text2), grow[4]));
                inner.Add(r);
            }
            var recognisedSince = PtLedger.Pairs.Values.Where(x => x.Si && x.SiFirst.Length > 0).Select(x => x.SiFirst).Distinct().ToList();
            var note = "How long a pair takes to be recognised isn't known yet in this save. Once a pair is recognised after the record began, the time it took becomes the yardstick. Cup ties and dead rubbers are where a fledgling pair gets minutes cheaply.";
            var nl = L(note, 13, Text2); nl.style.marginTop = 12; nl.style.paddingTop = 12; nl.style.borderTopWidth = 1; nl.style.borderTopColor = RowRule;
            inner.Add(nl);
            card.Add(inner);
            Body.Add(card);
        }

        // ---------- What the players say ----------
        static void SayView()
        {
            var say = PtInsight.PlayersSay();
            var warn = say.Count(s => s.Warning);
            var card = Panel(CardBg, Edge, 14); card.style.overflow = Overflow.Hidden; card.style.marginBottom = 16;
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg; Pad(band, 14, 18);
            band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var lt = Flex(true); var t = Disp(L("What the players say", 18, Text1, true)); t.style.marginRight = 10; lt.Add(t);
            lt.Add(warn > 0 ? Chip(warn + (warn == 1 ? " warning" : " warnings"), AmberBg, AmberFg, true) : Chip("No warnings", IncBg, Text1, true)); band.Add(lt);
            band.Add(L("From each player's Personal › Happiness, when it's opened", 13, Text2));
            card.Add(band);
            var inner = Flex(false); Pad(inner, 16, 18);
            if (say.Count == 0) inner.Add(L("Nothing yet. Open a player's Personal › Happiness: any partnership they mention is read from there.", 14, Text1, false, 10));
            foreach (var s in say)
            {
                var r = Flex(true, true); r.style.paddingBottom = 10; r.style.marginBottom = 10; r.style.borderBottomWidth = 1; r.style.borderBottomColor = RowRule;
                var c = s.Warning ? Chip(Cap(s.Grade), RedBg, RedFg) : Chip(Cap(s.Grade), GreenDark, GreenPale); c.style.marginRight = 12; r.Add(c);
                var tx = L(PtInsight.SayLine(s), 14, Text1); tx.style.flexShrink = 1; tx.style.marginRight = 12; r.Add(tx);
                r.Add(L(s.Date, 13, Text2));
                inner.Add(r);
            }
            inner.Add(L("Warnings come here first, and to the popout: a player who feels a partnership is weak, which the happiness panel says could be contributing to the club's underachievement. More starts together usually mends it; a partner who's coming through is the other way.", 14, Text2));
            card.Add(inner);
            Body.Add(card);
        }

        // ---------- the pitch ----------
        const float PW = 560f, PH = 600f, MX = 64f, MY = 40f;
        static float LX(int lane) => MX + (4 - lane) * (PW - 2 * MX) / 4f;   // the team's left on the left, as the tactics pitch draws it
        static float BY(int band) => PH - MY - band * (PH - 2 * MY) / 5f;
        // 0.3.0 (Nik: "The name tabs could scale to fit the player names"): a chip as wide as its name
        static float ChipW(string name) => Math.Max(56f, 7.6f * (name ?? "").Length + 26f);
        const float ChipH = 30f;

        static void BuildPitch(VisualElement left, List<Slot> shape, string today)
        {
            var pitch = new VisualElement(); pitch.name = "FM27MM-PairsPitch";
            pitch.style.width = PW; pitch.style.height = PH; pitch.style.backgroundColor = Turf; Border(pitch, 1, TurfEdge);
            pitch.style.overflow = Overflow.Hidden; Radius(pitch, 12);
            left.Add(pitch);
            Box(pitch, 18, 14, PW - 36, PH - 28, 1.5f);
            Box(pitch, PW / 2 - 110, PH - 14 - 80, 220, 80, 1.5f);
            Box(pitch, PW / 2 - 110, 14, 220, 80, 1.5f);
            Strip(pitch, 18, PH / 2f, PW - 36, 1.5f);

            if (shape == null || shape.Count == 0)
            {
                var l = L("Your XI appears here once the tactics screen has been opened.", 14, Text2);
                l.style.position = Position.Absolute; l.style.left = 60; l.style.top = PH / 2 - 12; l.style.width = PW - 120; l.style.unityTextAlign = TextAnchor.MiddleCenter;
                pitch.Add(l);
                Legend(left);
                return;
            }

            // the pairs to draw: every pair on record with both players in this XI, then untested neighbours
            var at = new Dictionary<string, Slot>();
            foreach (var s in shape) if (!at.ContainsKey(s.Name)) at[s.Name] = s;
            var draw = new List<(PairView v, Slot a, Slot b)>();
            foreach (var p in PtLedger.Pairs.Values)
                if (at.TryGetValue(p.A, out var sa) && at.TryGetValue(p.B, out var sb)) draw.Add((PtLedger.View(p, today), sa, sb));
            foreach (var (a, b) in PtLedger.Neighbours(shape))
            {
                var k = Key(a.Name, b.Name);
                if (PtLedger.Pairs.ContainsKey(k)) continue;
                var tv = PtLedger.Transient(a.Name, b.Name, today);
                if (tv.Step == -1 || Partnerships.ShowUntested.Value) draw.Add((tv, a, b));   // fledglings always; untested on request
            }
            draw = draw.OrderBy(d => d.v.Step == 0 ? -2 : d.v.Step == -1 ? 0.5 : d.v.Step).ToList();   // the strongest on top
            var paths = new Dictionary<string, List<V>>();
            foreach (var d in draw) paths[d.v.P.Key] = Path(d.a, d.b, shape);

            bool focus = SelPair.Length > 0 || SelPlayer.Length > 0;
            foreach (var d in draw)
            {
                bool lit = Lit(d.v.P);
                float op = !focus ? (d.v.Step == 0 ? 0.55f : 1f) : lit ? 1f : 0.15f;
                var pts = paths[d.v.P.Key];
                if (d.v.P.Key == SelPair) for (int i = 0; i + 1 < pts.Count; i++) Line(pitch, pts[i], pts[i + 1], Thick(d.v.Step) + 4f, new Color(1f, 1f, 1f, 0.85f), 1f, Dash.Solid);
                for (int i = 0; i + 1 < pts.Count; i++) Line(pitch, pts[i], pts[i + 1], Thick(d.v.Step), StepColour(d.v.Step), op, Pattern(d.v));
            }
            // the players, each chip as wide as the name
            foreach (var s in shape)
            {
                var ss = s;
                bool me = s.Name == SelPlayer;
                var b = new Button(); b.text = s.Name;
                System.Action act = () => { SelPlayer = SelPlayer == ss.Name ? "" : ss.Name; SelPair = ""; Build(); };
                System.Action safe = () => { try { act(); } catch (Exception e) { Partnerships.Fail("click", e); } };
                b.clickable.add_clicked(safe);
                b.tooltip = s.Name + (s.Role.Length > 0 ? " · " + s.Role : "");
                float w = ChipW(s.Name);
                b.style.position = Position.Absolute; b.style.width = w; b.style.height = ChipH; b.style.left = LX(s.Lane) - w / 2f; b.style.top = BY(s.Band) - ChipH / 2f;
                b.style.fontSize = 12.5f; b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.color = me ? OnGreen : Text1; b.style.backgroundColor = me ? Green : ChipBg;
                b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0; Pad(b, 0, 8);
                Border(b, 1, me ? Green : BtnEdge); Radius(b, 8);
                if (SelPlayer.Length > 0 && !me && !draw.Any(d => d.v.P.Key == Key(s.Name, SelPlayer))) b.style.opacity = 0.45f;
                pitch.Add(b);
            }
            // a dot on each line picks that pair, placed clear of the name chips
            foreach (var d in draw)
            {
                if (d.v.Step == 0) continue;
                var pts = paths[d.v.P.Key];
                var mid = Knob(pts, shape);
                var dv = d.v;
                float r = d.v.P.Key == SelPair ? 9f : 7f;
                var k = new Button(); k.text = "";
                System.Action pick = () => { SelPair = SelPair == dv.P.Key ? "" : dv.P.Key; SelPlayer = ""; Build(); };
                System.Action safe = () => { try { pick(); } catch (Exception e) { Partnerships.Fail("click", e); } };
                k.clickable.add_clicked(safe);
                k.tooltip = dv.Word + ": " + dv.P.A + " and " + dv.P.B;
                k.style.backgroundColor = StepColour(dv.Step);
                k.style.position = Position.Absolute; k.style.width = 2 * r; k.style.height = 2 * r; k.style.left = mid.x - r; k.style.top = mid.y - r;
                Pad(k, 0, 0); k.style.marginLeft = 0; k.style.marginRight = 0; k.style.marginTop = 0; k.style.marginBottom = 0;
                Border(k, 2.5f, Turf); Radius(k, r);
                if (focus && !Lit(dv.P)) k.style.opacity = 0.2f;
                pitch.Add(k);
            }
            Legend(left);
            var hint = Flex(true, true); hint.style.marginTop = 8;
            var hl = L("Click a player: only their partnerships stay lit. Click the dot on a line for that pair.", 13, Text2); hl.style.flexShrink = 1; hl.style.marginRight = 10;
            hint.Add(hl);
            if (focus) hint.Add(Pill("Clear", () => { SelPair = ""; SelPlayer = ""; Build(); }, false));
            left.Add(hint);
        }

        static bool Lit(Pair p) => SelPair.Length > 0 ? p.Key == SelPair : SelPlayer.Length > 0 ? (p.A == SelPlayer || p.B == SelPlayer) : true;
        static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        // straight; or, when another player stands in the way (DCL to DCR over the DC), a curve round below the row (the back,
        // towards its own goal) or above it (further forward); neighbours whose chips nearly touch get a gentle bend so the line shows
        static List<V> Path(Slot a, Slot b, List<Slot> shape)
        {
            var p1 = new V(LX(a.Lane), BY(a.Band)); var p2 = new V(LX(b.Lane), BY(b.Band));
            bool blocked = shape.Any(s => s != a && s != b && DistToSeg(new V(LX(s.Lane), BY(s.Band)), p1, p2) < 20f);
            float gap = Math.Abs(p1.x - p2.x) - (ChipW(a.Name) + ChipW(b.Name)) / 2f;
            bool tight = a.Band == b.Band && !blocked && gap < 26f;
            if (!blocked && !tight) return new List<V> { p1, p2 };
            float bend = blocked ? 52f : 22f;
            V off;
            if (a.Band == b.Band) off = new V(0, a.Band <= 2 ? bend : -bend);
            else
            {
                var d = p2 - p1; var n = new V(-d.y, d.x) / Math.Max(1f, d.magnitude);
                if ((n.x > 0) == ((p1.x + p2.x) / 2f < PW / 2f)) n = n * -1f;   // bow away from the middle of the pitch
                off = n * bend;
            }
            var c = (p1 + p2) / 2f + off * 2f;   // the control point: the curve's apex sits at half of it
            var l = new List<V>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f, u = 1 - t;
                l.Add(p1 * (u * u) + c * (2 * u * t) + p2 * (t * t));
            }
            return l;
        }
        // the dot: the middle of the path, moved along it if a name chip sits there
        static V Knob(List<V> pts, List<Slot> shape)
        {
            foreach (var f in new[] { 0.5f, 0.38f, 0.62f, 0.28f, 0.72f })
            {
                var p = Along(pts, f);
                bool clash = shape.Any(s => Math.Abs(p.x - LX(s.Lane)) < ChipW(s.Name) / 2f + 9f && Math.Abs(p.y - BY(s.Band)) < ChipH / 2f + 9f);
                if (!clash) return p;
            }
            return Along(pts, 0.5f);
        }
        static V Along(List<V> pts, float f)
        {
            float total = 0; for (int i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).magnitude;
            float want = total * f, run = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float seg = (pts[i + 1] - pts[i]).magnitude;
                if (run + seg >= want && seg > 0) return pts[i] + (pts[i + 1] - pts[i]) * ((want - run) / seg);
                run += seg;
            }
            return pts[pts.Count - 1];
        }
        static float DistToSeg(V p, V a, V b)
        {
            var ab = b - a; float t = ab.sqrMagnitude < 1e-3f ? 0 : Math.Max(0f, Math.Min(1f, V.Dot(p - a, ab) / ab.sqrMagnitude));
            return (p - (a + t * ab)).magnitude;
        }

        internal static void Line(VisualElement host, V a, V b, float t, Color c, float op, Dash dash)
        {
            float len = (b - a).magnitude; if (len < 1f) return;
            if (dash == Dash.Solid) { Seg(host, a, b, t, c, op); return; }
            float on = dash == Dash.Dashed ? 9f : Math.Max(2.5f, t), off = 6f;
            var dir = (b - a) / len;
            for (float s = 0; s < len; s += on + off)
            {
                float e = Math.Min(len, s + on);
                Seg(host, a + dir * s, a + dir * e, t, c, op);
            }
        }

        // one strip from a to b: a rotated element (or, if rotation is refused, a row of squares)
        static void Seg(VisualElement host, V a, V b, float t, Color c, float op)
        {
            float len = (b - a).magnitude; if (len < 0.5f) return;
            if (!NoRotate)
            {
                try
                {
                    var s = new VisualElement(); s.pickingMode = PickingMode.Ignore;
                    s.style.position = Position.Absolute; s.style.width = len + t * 0.6f; s.style.height = t;   // a little overlap so curves don't gap
                    s.style.left = (a.x + b.x) / 2f - (len + t * 0.6f) / 2f; s.style.top = (a.y + b.y) / 2f - t / 2f;
                    s.style.backgroundColor = c; s.style.opacity = op; Radius(s, t / 2f);
                    float deg = (float)(Math.Atan2(b.y - a.y, b.x - a.x) * 180.0 / Math.PI);
                    s.style.rotate = new StyleRotate(new Rotate(new Angle(deg, AngleUnit.Degree)));
                    host.Add(s);
                    return;
                }
                catch (Exception e) { NoRotate = true; Partnerships.Fail("rotate (falling back to dots)", e); }
            }
            var dir = (b - a) / len;
            for (float d = 0; d <= len; d += Math.Max(2f, t * 0.8f))
            {
                var q = new VisualElement(); q.pickingMode = PickingMode.Ignore;
                var p = a + dir * d;
                q.style.position = Position.Absolute; q.style.width = t; q.style.height = t; q.style.left = p.x - t / 2f; q.style.top = p.y - t / 2f;
                q.style.backgroundColor = c; q.style.opacity = op; Radius(q, t / 2f);
                host.Add(q);
            }
        }

        static void Box(VisualElement host, float x, float y, float w, float h, float t)
        {
            var b = new VisualElement(); b.pickingMode = PickingMode.Ignore;
            b.style.position = Position.Absolute; b.style.left = x; b.style.top = y; b.style.width = w; b.style.height = h; Border(b, t, Chalk);
            host.Add(b);
        }
        static void Strip(VisualElement host, float x, float y, float w, float h)
        {
            var b = new VisualElement(); b.pickingMode = PickingMode.Ignore;
            b.style.position = Position.Absolute; b.style.left = x; b.style.top = y; b.style.width = w; b.style.height = h; b.style.backgroundColor = Chalk;
            host.Add(b);
        }

        // the scale, under the pitch: a short line and its word for each step
        static void Legend(VisualElement left)
        {
            var box = Flex(true, true); box.style.marginTop = 10;
            foreach (var st in new[] { 7, 6, 5, 4, 3, 2, 1, -1, 0 })
            {
                var item = Flex(true); item.style.marginRight = 14; item.style.marginBottom = 6;
                var sw = new VisualElement(); sw.style.width = 26; sw.style.height = 12; sw.style.marginRight = 6; sw.style.flexShrink = 0; sw.pickingMode = PickingMode.Ignore;
                var fake = new PairView { Step = st, P = new Pair() };
                Line(sw, new V(1, 6), new V(25, 6), Math.Min(Thick(st), 6f), StepColour(st), st == 0 ? 0.7f : 1f, Pattern(fake));
                item.Add(sw);
                item.Add(L(st == -1 ? "Fledgling" : PtLedger.StepWords[st], 12.5f, Text2));
                box.Add(item);
            }
            left.Add(box);
        }

        // ---------- the cards beside the pitch ----------
        static VisualElement PairDetail(PairView v, List<Slot> shape)
        {
            var c = Panel(CardBg, Edge, 12); Pad(c, 16, 18);
            var top = Flex(true, true); top.style.justifyContent = Justify.SpaceBetween; top.style.marginBottom = 6;
            top.Add(Disp(L(v.P.A + " and " + v.P.B, 18, Text1, true)));
            top.Add(Pill("Clear", () => { SelPair = ""; Build(); }, false));
            c.Add(top);
            var zone = PtLedger.Zone(v.P, shape);
            if (zone.Length > 0) c.Add(L(Cap(zone), 13, Text2, false, 8));
            c.Add(L(v.Word + (v.Trend.Length > 0 ? ", " + v.Trend : ""), 20, StepColour(v.Step), true, 6));
            c.Add(Scale(v.Step));
            if (v.State.Length > 0) c.Add(L(v.State + (v.P.Dropped.Length > 0 ? ": the tactics pitch no longer draws their line in the same slots." : ": a drop of two steps or more lately."), 13, RedFg, false, 6));
            var coach = L("“" + v.Coach + "”", 15, Text1, false, 2); coach.style.unityFontStyleAndWeight = FontStyle.Italic; c.Add(coach);
            c.Add(L("From the coaching staff", 12, Text2, false, 10));
            foreach (var f in v.Facts) c.Add(L("•  " + f, 13, Text2, false, 3));
            var hist = PtLedger.History(v.P);
            if (hist.Count > 0)
            {
                var h = Caps("MATCH BY MATCH", Text2, 4); h.style.marginTop = 8; c.Add(h);
                foreach (var x in hist) c.Add(L(x, 13, Text2, false, 2));
            }
            if (Partnerships.ShowScores.Value)
                c.Add(L($"Raw: step {v.Step} · band '{v.Band}' · line {(v.P.Si ? "level " + v.P.SiLevel + " (" + v.P.SiCodes + ")" : "none")} · starts {v.Starts} · together {v.Played} · rated {v.RatedN} · avg {v.RatingAvg:0.00} · history {v.P.Hist}", 11, Text2, false, 0));
            return c;
        }

        // seven boxes, filled to the step, so the step reads at a glance without a number
        static VisualElement Scale(int step)
        {
            var r = Flex(true); r.style.marginBottom = 10;
            for (int i = 1; i <= 7; i++)
            {
                var b = new VisualElement(); b.style.width = 30; b.style.height = step == i ? 12 : 8; b.style.marginRight = 3; Radius(b, 2);
                b.style.backgroundColor = step >= i ? StepColour(i) : new Color(1f, 1f, 1f, 0.08f);
                r.Add(b);
            }
            if (step == -1 || step == 0) { var l = L(step == -1 ? "  not recognised yet" : "  not on the scale yet", 12, Text2); r.Add(l); }
            return r;
        }

        static VisualElement PlayerCard(string name, List<Slot> shape, string today)
        {
            var c = Panel(CardBg, Edge, 12); Pad(c, 16, 18);
            var top = Flex(true, true); top.style.justifyContent = Justify.SpaceBetween; top.style.marginBottom = 4;
            top.Add(Disp(L(name, 18, Text1, true)));
            top.Add(Pill("Clear", () => { SelPlayer = ""; Build(); }, false));
            c.Add(top);
            var s = shape?.FirstOrDefault(x => x.Name == name);
            if (s != null) c.Add(L(s.SlotName + (s.Role.Length > 0 ? " · " + s.Role : ""), 13, Text2, false, 10));
            var mine = PtLedger.Pairs.Values.Where(p => p.A == name || p.B == name).Select(p => PtLedger.View(p, today)).ToList();
            if (shape != null) foreach (var (a, b) in PtLedger.Neighbours(shape))
                    if ((a.Name == name || b.Name == name) && !mine.Any(m => m.P.Key == Key(a.Name, b.Name))) mine.Add(PtLedger.Transient(a.Name, b.Name, today));
            mine = mine.OrderByDescending(v => v.Step == -1 ? 0.5 : v.Step).ToList();
            if (mine.Count == 0) c.Add(L("No partnerships seen yet.", 14, Text2));
            foreach (var v in mine)
            {
                var other = v.P.A == name ? v.P.B : v.P.A;
                var vv = v;
                var r = Flex(true); r.style.marginBottom = 6;
                var sw = new VisualElement(); sw.style.width = 6; sw.style.height = 22; sw.style.marginRight = 10; sw.style.backgroundColor = StepColour(v.Step); Radius(sw, 3);
                r.Add(sw);
                var b = Pill(v.Word + " · with " + other, () => { SelPair = vv.P.Key; SelPlayer = ""; Build(); }, false);
                b.style.unityTextAlign = TextAnchor.MiddleLeft; b.style.flexShrink = 1;
                r.Add(b);
                c.Add(r);
            }
            return c;
        }

        // ---------- the look (the Treatment Room's base, in green) ----------
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
                if (hit != null) { Display = hit.resolvedStyle.unityFontDefinition; FontTried = true; }
            }
            catch (Exception e) { FontTried = true; Partnerships.Fail("page font", e); }
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
            var l = new Label(t); l.pickingMode = PickingMode.Ignore; l.style.fontSize = size; l.style.color = c; l.style.whiteSpace = WhiteSpace.Normal; l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            l.style.marginTop = 0; l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginBottom = below;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }
        // a line with a bold lead-in ("<b>If Mengi is out:</b> ..."); plain if rich text is refused
        static Label Rich(string t, float size, Color c)
        {
            var l = L(t, size, c);
            try { l.enableRichText = true; } catch { l.text = t.Replace("<b>", "").Replace("</b>", ""); }
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
        static VisualElement Cell(VisualElement e, float grow) { e.style.flexGrow = grow; e.style.flexShrink = 1; e.style.flexBasis = 0; e.style.paddingRight = 10; return e; }
        // tabs and the page's own buttons
        static Button Pill(string t, System.Action act, bool on)
        {
            var b = new Button(); b.text = t;
            b.style.fontSize = 13; b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.color = on ? OnGreen : Text1;
            b.style.backgroundColor = on ? Green : BtnBg; Border(b, 1, on ? Green : BtnEdge); Radius(b, 8);
            Pad(b, 6, 14); b.style.marginLeft = 0; b.style.marginRight = 8; b.style.marginTop = 2; b.style.marginBottom = 2; b.style.minHeight = 32;
            System.Action safe = () => { try { act(); } catch (Exception e) { Partnerships.Fail("click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        static Button OldBtn(string t, System.Action act, Color bg)
        {
            var b = new Button(); b.text = t; b.style.backgroundColor = bg; b.style.color = Text1; b.style.fontSize = 12;
            System.Action safe = () => { try { act(); } catch (Exception e) { Partnerships.Fail("click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : n + " times";
        static readonly string[] Words = { "none", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
        static string Num(int n, bool cap = false) { var w = n >= 0 && n <= 10 ? Words[n] : n.ToString(); return cap ? Cap(w) : w; }
    }
}
