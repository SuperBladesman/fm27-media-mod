// Offline test for PtInsight (Partnerships 0.3.0) on Nik's ledgers of 1/5/2027: the figures in the approved sketch.
using System;
using System.Linq;
using System.Collections.Generic;
using FM27MediaMod;
static class InsightTest
{
    static int Fails;
    static void Check(string what, bool ok) { if (!ok) Fails++; Console.WriteLine((ok ? "ok   " : "FAIL ") + what); }
    static int Main(string[] a)
    {
        PtLedger.Dir = a[0]; PtLedger.Load();
        var names = new Dictionary<string, string> { ["SOU"] = "Southampton", ["MIL"] = "Millwall", ["BOL"] = "Bolton" };
        PtInsight.OppName = c => names.TryGetValue(c, out var n) ? n : c;
        var today = "1/5/2027";
        foreach (var d in new[] { "17/4/2027", "20/4/2027", "24/4/2027", "1/5/2027" }) { var r = PtInsight.ResultOf(d); Console.WriteLine("  " + d + ": " + (r == null ? "no result" : r.Text)); }
        Check("Southampton read as a 5-1 defeat away", PtInsight.ResultOf("1/5/2027")?.Text == "L 1-5 at Southampton");
        Check("Bolton 1-1 away", PtInsight.ResultOf("20/4/2027")?.Text == "D 1-1 at Bolton");
        Check("Millwall 3-0 at home", PtInsight.ResultOf("24/4/2027")?.Text == "W 3-0 v Millwall");
        Check("Cardiff 1-1 at home (from the fixture list)", PtInsight.ResultOf("17/4/2027")?.Text == "D 1-1 at home");
        PtLedger.Pairs.TryGetValue("Mengi|Tanganga", out var mt); PtLedger.Pairs.TryGetValue("Arblaster|Phillips", out var ap); PtLedger.Pairs.TryGetValue("Bamford|Donovan", out var bd);
        foreach (var p in new[] { mt, ap, bd })
        {
            var w = PtInsight.Compare(p);
            Console.WriteLine($"  {p.A} and {p.B}: together {w.Together.N} ({w.Together.Ppg} pts, conceded {w.Together.Conceded}, scored {w.Together.Scored}, {w.Together.Record}); apart {w.Apart.N} ({w.Apart.Ppg} pts, conceded {w.Apart.Conceded}) [{string.Join("; ", w.Apart.Games.Select(g => g.R.Text + " only " + g.Only))}] neither {w.Neither} noscore {w.NoScore}");
            foreach (var l in PtInsight.WhoIsNext(p.A, p.B)) Console.WriteLine("     if " + p.B + " is out: " + l);
            foreach (var l in PtInsight.WhoIsNext(p.B, p.A)) Console.WriteLine("     if " + p.A + " is out: " + l);
        }
        var m = PtInsight.Compare(mt);
        Check("Mengi-Tanganga: together 1 (1.0, conceded 1.0), apart 3 (1.3, conceded 2.0)", m.Together.N == 1 && m.Together.Ppg == "1.0" && m.Together.Conceded == "1.0" && m.Apart.N == 3 && m.Apart.Ppg == "1.3" && m.Apart.Conceded == "2.0");
        var x = PtInsight.Compare(ap);
        Check("Arblaster-Phillips: together 3 (1.7, conceded 0.7), apart 1 (0.0, conceded 5.0)", x.Together.N == 3 && x.Together.Ppg == "1.7" && x.Together.Conceded == "0.7" && x.Apart.N == 1 && x.Apart.Conceded == "5.0");
        var y = PtInsight.Compare(bd);
        Check("Bamford-Donovan: together 3, never apart, both out at Southampton", y.Together.N == 3 && y.Apart.N == 0 && y.Neither == 1);
        Check("too early to judge (needs 5 each way)", PtInsight.TooEarly(m) && PtInsight.TooEarly(x));
        var kit = PtInsight.WhoIsNext("Tanganga", "Mengi");
        Check("if Mengi's out: Kitching started beside Tanganga 24/4, then Tati", kit.Count >= 2 && kit[0].StartsWith("Kitching has started beside Tanganga (24/4, 7.4, W 3-0 v Millwall)") && kit.Any(l => l.StartsWith("Tati")));
        var peck = PtInsight.WhoIsNext("Phillips", "Arblaster");
        Check("if Arblaster's out: Peck on 3 times, never started", peck.Count >= 1 && peck[0] == "Peck, on beside Phillips 3 times (6.6, 6.9, 6.5); never started there.");
        var mos = PtInsight.WhoIsNext("Arblaster", "Phillips");
        Check("if Phillips is out: Mosengo started 1/5, off at half time", mos.Count >= 1 && mos[0].StartsWith("Daren Mosengo has started beside Arblaster (1/5, 6.0, L 1-5 at Southampton, off at half time)"));
        var kit2 = PtInsight.WhoIsNext("Mengi", "Tanganga");
        Check("if Tanganga's out: Kitching started beside Mengi 20/4, off at half time", kit2.Count >= 1 && kit2[0].StartsWith("Kitching has started beside Mengi (20/4, 6.4, D 1-1 at Bolton, off at half time)"));
        foreach (var p in new[] { mt, ap, bd }) foreach (var arr in PtInsight.ByPosition(p)) Console.WriteLine("  by position: " + PtInsight.ArrangementLine(p, arr));
        var pos = PtInsight.ByPosition(mt);
        Check("Mengi-Tanganga: DCL+DCR twice (1 start), DC+DCR once from the bench", pos.Count == 2 && pos[0].SlotA == "DCL" && pos[0].SlotB == "DCR" && pos[0].Together == 2 && pos[0].Starts == 1 && pos[1].SlotA == "DC" && pos[1].SlotB == "DCR" && pos[1].Together == 1 && pos[1].Starts == 0);
        Check("DC+DCR rated (7.3+6.8)/2 = 7.05", pos.Count == 2 && pos[1].RatingText == "7.05");
        PtLedger.Shape.TryGetValue("in possession", out var shape);
        var ct = PtInsight.ComingThrough(shape, today);
        foreach (var p in ct) Console.WriteLine($"  coming through: {p.V.P.A} and {p.V.P.B} · {p.Zone} · {p.V.Word} · starts {p.V.Starts} · together {p.V.Played} · rated {p.V.RatingAvg:0.00}");
        Check("five fledgling pairs coming through", ct.Count(p => p.Fledgling) == 5);
        Check("Burrows and Arblaster listed, not fledgling", ct.Any(p => p.V.P.Key == "Arblaster|Burrows" && !p.Fledgling));
        Check("Kumbedi and Phillips: right side", ct.Any(p => p.V.P.Key == "Kumbedi|Phillips" && p.Zone == "right side"));
        Check("no recognised pair among them", !ct.Any(p => p.V.P.Key == "Mengi|Tanganga" || p.V.P.Key == "Arblaster|Phillips"));
        var say = PtInsight.PlayersSay();
        foreach (var s in say) Console.WriteLine("  says: " + PtInsight.SayLine(s) + (s.Warning ? " [WARNING]" : ""));
        Check("Tanganga's decent partnership with Mengi, no warnings", say.Count == 1 && !say[0].Warning && PtInsight.SayLine(say[0]) == "Tanganga feels there's a decent partnership at the back with Mengi.");
        var all = new[] { mt, ap, bd }.SelectMany(p => PtInsight.WhoIsNext(p.A, p.B).Concat(PtInsight.WhoIsNext(p.B, p.A))).Concat(say.Select(PtInsight.SayLine)).ToList();
        Check("no he/she or SI in any line", !all.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"\b(he|she|his|her|him|SI)\b")));
        Console.WriteLine(Fails == 0 ? "\nALL PASS" : "\n" + Fails + " FAILED");
        return Fails;
    }
}
