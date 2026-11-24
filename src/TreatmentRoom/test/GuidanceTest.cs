// Offline test for Guidance.cs (0.6.0): Nik's real ledgers (24/4/2027) through three departments.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FM27MediaMod;
namespace FM27MediaMod
{
    public class TreatmentRoom
    {
        internal static string Dir = Path.Combine(Path.GetTempPath(), "trguide");
        internal static void W(string k, string a, string b) { }
        internal static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        internal static void Fail(string w, Exception e) => Console.WriteLine("  FAIL " + w + ": " + e);
    }
    internal static class GameDate
    {
        internal static string Text = "";
        internal static int Day(string t)
        {
            var p = (t ?? "").Split('/'); if (p.Length != 3) return -1;
            try { return (int)(new DateTime(int.Parse(p[2]), int.Parse(p[1]), int.Parse(p[0])) - new DateTime(2000, 1, 1)).TotalDays; } catch { return -1; }
        }
    }
}
static class GuidanceTest
{
    static int Fails;
    static void Check(string what, bool ok) { if (!ok) { Fails++; Console.WriteLine("FAIL " + what); } else Console.WriteLine("ok   " + what); }
    static readonly Regex Pronoun = new Regex(@"\b(he|she|his|her|hers|him|himself|herself|lads|boys|guys)\b", RegexOptions.IgnoreCase);
    static readonly Regex Meta = new Regex(@"\bSI\b|Sports Interactive|the game\b");
    static List<string> All = new List<string>();
    static void Load(string real)
    {
        Directory.CreateDirectory(TreatmentRoom.Dir);
        foreach (var f in Ledger.Files) { var src = Path.Combine(real, f); var dst = Path.Combine(TreatmentRoom.Dir, f); if (File.Exists(src)) File.Copy(src, dst, true); else File.WriteAllText(dst, ""); }
        Ledger.Load(); Dept.Clear(); Notes.All.Clear();
        GameDate.Text = "24/4/2027";
    }
    static void Nik()
    {
        foreach (var r in new[] {
            ("01021034", "Alex Worthington", "Chief Doctor", "-", "-", "-"), ("0100cca8", "Will Tullett", "Head of Sports Science", "Competent", "Outstanding", "Average"),
            ("0100346b", "Bryan English", "Head Physio", "Elite", "Competent", "Good"), ("010034d4", "Jonathan Chatfield", "Physio", "Very Good", "Good", "Average"),
            ("010246d4", "Joseph Sowden", "Physio", "Outstanding", "Competent", "Average"), ("0102a0f1", "Elliott Howell", "Physio", "Very Good", "Average", "Good"),
            ("0100c6ed", "Richie Porter", "Physio", "Elite", "Competent", "Average"), ("0100cd02", "Oliver Morgan", "Sports Scientist", "Reasonable", "Very Good", "Good"),
            ("010036fc", "Richard Hawkins", "Sports Scientist", "Good", "Very Good", "Very Good"), ("0100d2da", "David Tivey", "Sports Scientist", "Competent", "Very Good", "Average") })
            Dept.TableRow(r.Item1, r.Item2, r.Item3, "Senior Squad", r.Item4, r.Item5, r.Item6, "17/4/2027");
        foreach (var (job, f, a) in new[] { ("Head Physio", 1, 1), ("Chief Doctor", 1, 1), ("Head of Sports Science", 1, 1), ("Physio", 4, 5), ("Doctor", 0, 0), ("Sports Scientist", 3, 3) })
            Dept.SetPost(job, f, a, "17/4/2027");
        Dept.SetCompare("Physiotherapy", "3rd in Sky Bet Championship", "Above Average", "", "", "17/4/2027");
        Dept.Said("vacancies", "medical team vacancies (2)", "22/4/2027");
        Notes.Staff("risk", "Will Tullett", "11/4/2027");
    }
    static Guidance.Page Show(string head, string signer, string waiting = "")
    {
        var p = Guidance.Build("24/4/2027", signer, waiting);
        Console.WriteLine("\n================ " + head);
        Console.WriteLine($"assessed {p.Assessed} ({p.AgeText}) | out {p.Out}: {p.OutSub} | risk {p.VeryHigh} VH, {p.High} H, hidden {p.Hidden}: {p.RiskSub}");
        foreach (var w in p.Waiting) Console.WriteLine("  WAIT " + w.Text + "  [" + w.Path + "]");
        Console.WriteLine("  " + p.Intro); All.Add(p.Intro); All.Add(p.OutSub); All.Add(p.RiskSub); All.AddRange(p.Waiting.Select(w => w.Text));
        foreach (var c in p.Cards)
        {
            Console.WriteLine($"  -- {c.Title} [{c.Count}, level {c.Level}] advised by {c.Advisor}");
            Console.WriteLine("     what: " + c.What);
            for (int i = 0; i < c.Rows.Count; i++) Console.WriteLine("     " + c.RowLevel[i] + " | " + string.Join(" | ", c.Rows[i]));
            if (c.Extra.Length > 0) Console.WriteLine("     extra: " + c.Extra);
            foreach (var a in c.Acts) Console.WriteLine("     do: " + a.Text + (a.Plan.Length > 0 ? "  <" + a.Label + " = " + a.Plan + " x" + a.Players.Count + ">" : ""));
            Console.WriteLine("     why: " + c.Footer);
            All.Add(c.Title); All.Add(c.What); All.Add(c.Extra); All.Add(c.Footer); All.AddRange(c.Acts.Select(a => a.Text)); All.AddRange(c.Acts.Select(a => a.Label)); All.Add(c.Count);
        }
        if (p.Beyond.Length > 0) { Console.WriteLine("  BEYOND " + p.Beyond); All.Add(p.Beyond); }
        foreach (var t in p.Dept) { Console.WriteLine($"  DEPT {t.Role} | {t.Name} | {t.Line} | {t.Note}"); All.Add(t.Line); All.Add(t.Note); }
        foreach (var l in p.DeptLines) { Console.WriteLine("  DEPT " + l); All.Add(l); }
        Console.WriteLine("  DEPT " + p.DeptRead); All.Add(p.DeptRead);
        return p;
    }
    static int Main(string[] a)
    {
        var real = a[0];
        // 1. Nik's department (as read 17/4)
        Load(real); Nik();
        var p1 = Show("NIK'S DEPARTMENT", "Will Tullett", "Emile Witbooi (the physios or pain management)");
        var cond = p1.Cards.FirstOrDefault(c => c.Key == "condition");
        Check("low condition first, 7 players, Tanganga top", p1.Cards[0].Key == "condition" && cond.Rows.Count == 7 && cond.Rows[0][0] == "Japhet Tanganga");
        Check("advised by Will Tullett (Outstanding)", cond.Advisor.StartsWith("Will Tullett, Head of Sports Science · Sports Science: Outstanding"));
        Check("Tanganga exhausted shown", cond.Rows[0][2].StartsWith("Exhausted · 270 of 270"));
        Check("rest plan for Tanganga, lighter for the five", cond.Acts.Any(x => x.Plan == "rest" && x.Players.Count == 1 && x.Players[0].Name == "Japhet Tanganga") && cond.Acts.Any(x => x.Plan == "lighter" && x.Players.Count == 5 && x.Text.StartsWith("The other five")));
        var sharp = p1.Cards.FirstOrDefault(c => c.Key == "sharpness");
        Check("sharpness: Mosengo and Davies, plus players without minutes", sharp != null && sharp.Rows.Count == 2 && sharp.Extra.Contains("Kyle Walker"));
        Check("sharpness: Davies the cup goalkeeper", sharp.Acts.Any(x => x.Text.StartsWith("Davies: a reserve match keeps the cup goalkeeper ready")));
        var inten = p1.Cards.FirstOrDefault(c => c.Key == "intensity");
        Check("intensity: Felipe and Bamford, lighter for Felipe", inten != null && inten.Rows.Count == 2 && inten.Acts.Any(x => x.Plan == "lighter" && x.Players[0].Name == "Pablo Felipe"));
        var rec = p1.Cards.FirstOrDefault(c => c.Key == "recurring");
        Check("recurring last: specialists for the two hamstrings, Phillips on file", p1.Cards.Last().Key == "recurring" && rec.Acts.Any(x => x.Plan == "specialist" && x.Players.Count == 2) && rec.Acts.Any(x => x.Text.StartsWith("Phillips: the dislocated shoulder is on file")));
        Check("assessment age flagged (13 days)", p1.Waiting.Any(w => w.Text == "The risk assessment is 13 days old."));
        Check("decision waiting shown", p1.Waiting.Any(w => w.Text.StartsWith("Decision waiting: Emile Witbooi")));
        Check("board vacancies shown", p1.Waiting.Any(w => w.Text.Contains("medical team vacancies (2)")));
        Check("department tiles: English Elite, Tullett Outstanding, physios 4 of 5", p1.Dept.Any(t => t.Name == "Bryan English" && t.Line == "Physiotherapy: Elite") && p1.Dept.Any(t => t.Line == "Sports Science: Outstanding") && p1.Dept.Any(t => t.Role == "Physios · 4 of 5 posts" && t.Note == "One post empty"));
        Check("nothing hidden from an Outstanding signer", p1.Hidden == 0 && p1.Beyond.Length == 0);
        int nikCards = p1.Cards.Sum(c => c.Rows.Count);

        // 2. a smaller department: Average sports science, Competent head physio, 1 of 3 physios
        Load(real);
        Dept.TableRow("h1", "Sam Archer", "Head Physio", "Senior Squad", "Competent", "Average", "Average", "17/4/2027");
        Dept.TableRow("s1", "Lee Moss", "Head of Sports Science", "Senior Squad", "Reasonable", "Average", "Average", "17/4/2027");
        foreach (var (job, f, al) in new[] { ("Head Physio", 1, 1), ("Physio", 1, 3), ("Head of Sports Science", 1, 1) }) Dept.SetPost(job, f, al, "17/4/2027");
        Notes.Staff("risk", "Lee Moss", "11/4/2027");
        var p2 = Show("SMALLER DEPARTMENT", "Lee Moss");
        var c2 = p2.Cards.FirstOrDefault(c => c.Key == "condition");
        Check("smaller: condition lists the two very high only, hedged", c2 != null && c2.Rows.Count == 2 && c2.Acts[0].Text.StartsWith("Both look tired"));
        Check("smaller: no sharpness or intensity card", !p2.Cards.Any(c => c.Key == "sharpness" || c.Key == "intensity"));
        Check("smaller: beyond this department names what's missing", p2.Beyond.Contains("low match sharpness") && p2.Beyond.Contains("match fatigue"));
        Check("smaller: recurring without specialists", p2.Cards.Any(c => c.Key == "recurring" && c.Acts.All(x => x.Plan != "specialist") && c.Footer.StartsWith("A head physio rated Good or better")));
        Check("smaller: empty physio posts waiting", p2.Waiting.Any(w => w.Text == "2 of 3 physio posts are empty."));
        Check("a better department tells you more", nikCards > p2.Cards.Sum(c => c.Rows.Count));

        // 3. not met yet
        Load(real);
        var p3 = Show("NOT MET YET", "");
        Check("unknown: says to open the Medical Team screen", p3.DeptRead.StartsWith("The Treatment Room hasn't met the medical staff yet") && p3.Cards.Where(c => c.Key != "recurring").All(c => c.Footer.StartsWith("Who signs the risk assessments")));

        Check("no he/she anywhere", !All.Any(t => Pronoun.IsMatch(t ?? "")));
        Check("no 'SI' or 'the game' anywhere", !All.Any(t => Meta.IsMatch(t ?? "")));
        foreach (var t in All.Where(t => t != null && (Pronoun.IsMatch(t) || Meta.IsMatch(t)))) Console.WriteLine("   offending: " + t);
        Console.WriteLine(Fails == 0 ? "\nALL PASS" : "\n" + Fails + " FAILED");
        return Fails;
    }
}
