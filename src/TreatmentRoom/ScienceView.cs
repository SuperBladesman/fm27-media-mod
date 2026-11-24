// FM27 Media Mod - Treatment Room 0.6.2: the Sports science page drawn (Science.cs has the numbers). The same look as Guidance;
// bars are plain elements with percentage widths (UI Toolkit has no charts).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class Room
    {
        static readonly Color Mid = Hex("8A2C35"), Grey6 = Hex("6E6E76"), Track = Hex("2F2F35");

        // a card with a heading band: the title, an optional pill, and a line on the right
        static (VisualElement Card, VisualElement Inner) Shell(string title, string chip, int level, string right)
        {
            var card = Panel(CardBg, Edge, 14); card.style.marginBottom = 16; card.style.overflow = Overflow.Hidden;
            var band = Flex(true, true); band.style.justifyContent = Justify.SpaceBetween; band.style.backgroundColor = BandBg;
            Pad(band, 14, 18); band.style.borderBottomWidth = 1; band.style.borderBottomColor = Edge;
            var left = Flex(true); left.style.marginRight = 12;
            var t = Disp(L(title, 18, Text1, true)); t.style.marginRight = 10; left.Add(t);
            if (chip.Length > 0) left.Add(level == 0 ? Chip(chip, Red, White, true) : level == 1 ? Chip(chip, HighBg, Pink, true) : Chip(chip, IncBg, Text1, true));
            band.Add(left);
            if (right.Length > 0) band.Add(L(right, 13, Text2));
            card.Add(band);
            var inner = Flex(false); Pad(inner, 16, 18); card.Add(inner);
            return (card, inner);
        }
        static VisualElement Track1(float percent, Color fill, float height = 18)
        {
            var tr = new VisualElement(); tr.style.height = height; tr.style.backgroundColor = Track; Radius(tr, 5); tr.style.overflow = Overflow.Hidden;
            tr.style.flexGrow = 1; tr.style.flexShrink = 1;
            var f = new VisualElement(); f.style.height = height; f.style.width = new Length(Math.Max(0f, Math.Min(100f, percent)), LengthUnit.Percent); f.style.backgroundColor = fill;
            tr.Add(f); return tr;
        }
        static VisualElement BarRow(string label, float percent, Color fill, string note, Color noteC)
        {
            var row = Flex(true); row.style.marginBottom = 8;
            var l = L(label, 14, Text1); l.style.width = 76; l.style.flexShrink = 0; row.Add(l);
            row.Add(Track1(percent, fill));
            var n = L(note, 14, noteC); n.style.width = 170; n.style.flexShrink = 0; n.style.marginLeft = 10; row.Add(n);
            return row;
        }
        static void Shows(VisualElement inner, List<string> lines, string none)
        {
            inner.Add(Spacer(6));
            inner.Add(Caps("WHAT IT SHOWS", RedLight, 6));
            if (lines.Count == 0 && none.Length > 0) inner.Add(L(none, 14, Text2, false, 4));
            foreach (var s in lines) { var l = L(s, 14, Text1, false, 6); l.style.maxWidth = 920; inner.Add(l); }
        }

        static void ScienceView()
        {
            var p = Science.Build(Today, Notes.StaffName("risk"), StartMonth);
            var intro = L("The evidence behind the guidance: how hard the squad is working, where this season's injuries come from, and how often the department's flags have been right. Guidance says what to do; this page shows why.", 14, Text2, false, 16);
            intro.style.maxWidth = 900; Body.Add(intro);

            // the latest assessment
            Body.Add(Caps("THE LATEST ASSESSMENT", Text2, 10));
            var tiles = Flex(true, true); tiles.style.alignItems = Align.Stretch; tiles.style.marginBottom = 18;
            var t1 = Panel(BandBg, p.Age > 7 ? AlertEdge : Edge); Pad(t1, 16, 16); t1.style.flexGrow = 1; t1.style.flexBasis = new Length(28, LengthUnit.Percent); t1.style.minWidth = 250; t1.style.marginRight = 12; t1.style.marginBottom = 12;
            if (p.Assessed.Length == 0) { t1.Add(L("No risk assessment read yet", 15, Text1, true, 6)); t1.Add(GoTo("Training › Medical Centre › Risk Assessment")); }
            else
            {
                t1.Add(L("Assessed " + p.Assessed, 13, Text2, false, 4));
                t1.Add(Disp(L(p.AgeText.Length > 0 ? Cap1(p.AgeText) : p.Assessed, 28, Text1, true, 4)));
                if (p.Signer.Length > 0) t1.Add(L("Signed by " + p.Signer, 13, Text2, false, 4));
                if (p.Age > 7) t1.Add(GoTo("Ask for an update: Medical Centre › Risk Assessment"));
            }
            tiles.Add(t1);
            if (p.Players > 0)
            {
                var t2 = Panel(BandBg, Edge); Pad(t2, 16, 16); t2.style.flexGrow = 2; t2.style.flexBasis = new Length(56, LengthUnit.Percent); t2.style.minWidth = 300; t2.style.marginRight = 12; t2.style.marginBottom = 12;
                t2.Add(L("Risk across the squad · " + p.Players + " players", 13, Text2, false, 8));
                var bar = Flex(true); bar.style.height = 22; Radius(bar, 6); bar.style.overflow = Overflow.Hidden; bar.style.marginBottom = 8;
                foreach (var (n, c) in new[] { (p.VeryHigh, Red), (p.High, Mid), (p.Increased, Grey6), (p.Normal, BtnEdge), (p.Low, Track) })
                {
                    if (n == 0) continue;
                    var seg = new VisualElement(); seg.style.height = 22; seg.style.width = new Length(100f * n / p.Players, LengthUnit.Percent); seg.style.backgroundColor = c; bar.Add(seg);
                }
                t2.Add(bar);
                var legend = Flex(true, true); legend.style.marginBottom = 8;
                foreach (var (n, w, c) in new[] { (p.VeryHigh, "very high", RedLight), (p.High, "high", Pink), (p.Increased, "increased", Text1), (p.Normal, "normal", Text1), (p.Low, "low", Text1) })
                {
                    if (n == 0) continue;
                    var num = L(n.ToString(), 13, c, true); num.style.marginRight = 4; legend.Add(num);
                    var word = L(w, 13, Text2); word.style.marginRight = 16; legend.Add(word);
                }
                t2.Add(legend);
                if (p.Thread.Length > 0) t2.Add(L(p.Thread, 14, Text1));
                tiles.Add(t2);
            }
            Body.Add(tiles);

            // workload
            if (p.MatchLoad.Count > 0)
            {
                var (card, inner) = Shell("Workload", "", 2, "From " + p.Signer);
                var cols = Flex(true, true); cols.style.alignItems = Align.FlexStart;
                var c1 = Flex(false); c1.style.flexGrow = 1; c1.style.flexBasis = new Length(45, LengthUnit.Percent); c1.style.minWidth = 320; c1.style.marginRight = 24; c1.style.marginBottom = 8;
                c1.Add(Caps("MATCH LOAD · MINUTES IN 14 DAYS", RedLight, 8));
                int max = Math.Max(1, p.MatchLoad.Max(b => b.Count));
                foreach (var b in p.MatchLoad) c1.Add(BarRow(b.Label, 100f * b.Count / max, b.Level == 0 ? Red : b.Level == 1 ? Mid : Grey6, b.Count + " · " + b.Note, b.Level == 0 ? Pink : Text2));
                cols.Add(c1);
                var c2 = Flex(false); c2.style.flexGrow = 1; c2.style.flexBasis = new Length(45, LengthUnit.Percent); c2.style.minWidth = 320; c2.style.marginBottom = 8;
                c2.Add(Caps("TRAINING LOAD", RedLight, 8));
                int tmax = Math.Max(1, p.TrainLoad.Count == 0 ? 1 : p.TrainLoad.Max(b => b.Count));
                foreach (var b in p.TrainLoad) c2.Add(BarRow(b.Label, 100f * b.Count / tmax, Grey6, b.Count + (b.Note.Length > 0 ? " " + b.Note : ""), Text2));
                if (p.Individual.Length > 0) c2.Add(L(p.Individual, 13, Text2));
                cols.Add(c2);
                inner.Add(cols);
                Shows(inner, p.LoadShows, p.ReadsNot);
                Body.Add(card);
            }

            // this season's injuries
            {
                var (card, inner) = Shell("This season's injuries", "", 2, "From the club's injury record");
                if (p.Injuries == 0) inner.Add(L("Open the Medical Centre's Season Summary once and the season fills in here.", 14, Text2));
                else
                {
                    var stats = Flex(true, true); stats.style.alignItems = Align.Stretch; stats.style.marginBottom = 14;
                    void Stat(string label, string big, string small, bool hot)
                    {
                        var s = Panel(BandBg, BandBg, 10); Pad(s, 12, 14); s.style.flexGrow = 1; s.style.flexBasis = new Length(22, LengthUnit.Percent); s.style.minWidth = 190; s.style.marginRight = 10; s.style.marginBottom = 10;
                        s.Add(L(label, 13, Text2, false, 2)); s.Add(Disp(L(big, 26, Text1, true, 2))); if (small.Length > 0) s.Add(L(small, 13, hot ? Pink : Text2));
                        stats.Add(s);
                    }
                    Stat("Injuries", p.Injuries.ToString(), p.Training + " training · " + p.Match + " match · " + p.Other + " other", false);
                    Stat("Days lost", p.Days.ToString(), p.TrainingDays > 0 ? p.TrainingDays + " of them from training" : "", p.TrainingDays * 2 > p.Days);
                    if (p.MuscleN > 0) Stat("Muscle injuries", p.MuscleN.ToString(), p.MuscleParts, false);
                    if (p.Tight > 0) Stat("'Tight' niggles", p.Tight.ToString(), p.TightNote, false);
                    inner.Add(stats);
                    if (p.Months.Count > 0)
                    {
                        inner.Add(Caps("MONTH BY MONTH", RedLight, 6));
                        var key = Flex(true, true); key.style.marginBottom = 8;
                        foreach (var (w, c) in new[] { ("training", Red), ("match", Pink), ("other", Grey6) })
                        {
                            var sw = new VisualElement(); sw.style.width = 10; sw.style.height = 10; sw.style.backgroundColor = c; Radius(sw, 2); sw.style.marginRight = 5; key.Add(sw);
                            var kl = L(w, 12, Text2); kl.style.marginRight = 16; key.Add(kl);
                        }
                        inner.Add(key);
                        int mmax = Math.Max(1, p.Months.Max(m => m.Training + m.Match + m.Other));
                        foreach (var m in p.Months)
                        {
                            var row = Flex(true); row.style.marginBottom = 6;
                            var ml = L(m.Label, 13, Text1); ml.style.width = 80; ml.style.flexShrink = 0; row.Add(ml);
                            var tr = Flex(true); tr.style.flexGrow = 1; tr.style.height = 16;
                            foreach (var (n, c) in new[] { (m.Training, Red), (m.Match, Pink), (m.Other, Grey6) })
                            {
                                if (n == 0) continue;
                                var seg = new VisualElement(); seg.style.height = 16; seg.style.width = new Length(100f * n / mmax * 0.6f, LengthUnit.Percent); seg.style.backgroundColor = c; Radius(seg, 4); seg.style.marginRight = 2; tr.Add(seg);
                            }
                            row.Add(tr);
                            var dl = L(m.Days + (m.Days == 1 ? " day" : " days"), 13, m.Days >= 50 ? Pink : Text2); dl.style.width = 80; dl.style.flexShrink = 0; dl.style.unityTextAlign = TextAnchor.MiddleRight; row.Add(dl);
                            inner.Add(row);
                        }
                    }
                    Shows(inner, p.SeasonShows, p.ReadsNot);
                }
                Body.Add(card);
            }

            // how good are the flags?
            {
                var (card, inner) = Shell("How good are the flags?", p.FlagDays == 1 ? "1 assessment checked" : p.FlagDays + " assessments checked", 2, "");
                if (p.FlagDays > 0)
                {
                    var two = Flex(true, true); two.style.alignItems = Align.Stretch; two.style.marginBottom = 12;
                    foreach (var (label, a, b) in new[] { ("Flagged high or very high", p.FlaggedHurt, p.Flagged), ("Everyone else", p.OthersHurt, p.Others) })
                    {
                        var s = Panel(BandBg, BandBg, 10); Pad(s, 12, 14); s.style.flexGrow = 1; s.style.flexBasis = new Length(40, LengthUnit.Percent); s.style.minWidth = 260; s.style.marginRight = 10; s.style.marginBottom = 10;
                        s.Add(L(label, 13, Text2, false, 2)); s.Add(Disp(L(a + " of " + b, 24, Text1, true, 2))); s.Add(L("injured within 14 days", 13, Text2));
                        two.Add(s);
                    }
                    inner.Add(two);
                }
                inner.Add(L(p.FlagNote, 14, Text1, false, 6));
                var f = L(p.FlagWhy, 13, Text2); f.style.marginTop = 6; f.style.paddingTop = 12; f.style.borderTopWidth = 1; f.style.borderTopColor = RowRule; inner.Add(f);
                Body.Add(card);
            }

            // the full assessment
            if (p.Table.Count > 0)
            {
                var (card, inner) = Shell("The full assessment", "", 2, p.Assessed + " · " + p.Players + " players · as the Medical Centre shows it");
                var grow = new[] { 1.6f, 0.9f, 2f, 0.8f, 0.9f, 1.3f, 1.1f };
                var table = Panel(CardBg, Edge, 10); table.style.overflow = Overflow.Hidden;
                var head = Flex(true); head.style.backgroundColor = BandBg; Pad(head, 8, 14);
                foreach (var (c, i) in new[] { "Player", "Risk", "Main factor", "Match load", "Minutes", "Training", "Susceptibility" }.Select((c, i) => (c, i))) head.Add(Cell(L(c, 12, Text2), grow[i]));
                table.Add(head);
                for (int r = 0; r < p.Table.Count; r++)
                {
                    var row = Flex(true); Pad(row, 7, 14); row.style.borderTopWidth = 1; row.style.borderTopColor = RowRule;
                    var cells = p.Table[r];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        VisualElement cell;
                        if (i == 1) { var h = new VisualElement(); h.style.flexDirection = FlexDirection.Row; h.Add(RiskChip(p.TableLevel[r], cells[i])); cell = h; }
                        else cell = L(cells[i], 13.5f, i == 2 && cells[i] == "-" ? Text2 : Text1, i == 0);
                        row.Add(Cell(cell, grow[i]));
                    }
                    table.Add(row);
                }
                inner.Add(table);
                Body.Add(card);
            }
        }
        static string Cap1(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
