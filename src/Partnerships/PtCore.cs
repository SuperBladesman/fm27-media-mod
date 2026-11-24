// FM27 Media Mod - Partnerships: the core (pure C#, no Unity). MIT licence (see LICENSE).
// Everything here works on plain text and numbers read from SI's own screens, so it can be tested offline on the probe's logs:
//   PtSlots    the tactics pitch's slot texts -> bands and lanes -> SI's slot codes; the match screen's named slots
//   PtParse    the happiness partnership lines and SI's five "selected roles" sentences
//   PtLedger   every pair SI recognises, every feeling a player states, every match XI read; steps, states, coach lines
// Design: "Design - the partnerships view (Nov 2026)"; evidence: "Player partnerships - what FM27 holds (Nov 2026)".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    // one text read from a pitch: its on-screen centre (UI units), its path below the pitch, its plain text
    internal sealed class PitchText { public float X, Y; public string Path = "", Text = ""; }

    internal sealed class Slot
    {
        public string Name = "", Role = "", Number = "", Rating = "";
        public float X, Y;
        public float SX = float.NaN, SY = float.NaN;   // the shirt's centre on screen (0.2.0: lines on SI's own pitch join the shirts)
        public int Band = -1, Lane = -1;
        public int Code => PtSlots.CodeOf(Band, Lane);
        public string SlotName => PtSlots.SlotName(Band, Lane);
        public override string ToString() => $"{SlotName}:{Name}";
    }

    internal static class PtSlots
    {
        // bands: 0 goalkeeper, 1 defence, 2 wing-backs and defensive midfield, 3 midfield, 4 attacking midfield, 5 attack.
        // lanes (the team's own right to left): 0 right, 1 right of centre, 2 centre, 3 left of centre, 4 left.
        // SI's code (seen on the tactics pitch's rows): a position bit + index x 0x100000 (index 0 centre, 1 left, 2 right).
        // Seen: DC 0x10, DM 0x80, AM centre 0x2000, ST 0x4000. The goalkeeper and side bits are BELIEVED (FM's usual order).
        static readonly int[] CentralBit = { 0x1, 0x10, 0x80, 0x400, 0x2000, 0x4000 };
        static readonly int[] RightBit = { 0, 0x4, 0x20, 0x100, 0x800, 0 };
        static readonly int[] LeftBit = { 0, 0x8, 0x40, 0x200, 0x1000, 0 };
        static readonly string[,] Names =
        {
            { "GK", "GK", "GK", "GK", "GK" },
            { "DR", "DCR", "DC", "DCL", "DL" },
            { "WBR", "DMR", "DM", "DML", "WBL" },
            { "MR", "MCR", "MC", "MCL", "ML" },
            { "AMR", "AMCR", "AMC", "AMCL", "AML" },
            { "STR", "STR", "ST", "STL", "STL" },
        };
        internal static readonly string[] BandWord = { "in goal", "at the back", "in front of the defence", "in the middle", "behind the attack", "in attack" };

        public static string SlotName(int band, int lane) => band < 0 || band > 5 || lane < 0 || lane > 4 ? "?" : Names[band, lane];

        public static int CodeOf(int band, int lane)
        {
            if (band < 0 || band > 5 || lane < 0 || lane > 4) return 0;
            if (band == 0) return CentralBit[0];
            if (lane == 0 && RightBit[band] != 0) return RightBit[band];
            if (lane == 4 && LeftBit[band] != 0) return LeftBit[band];
            int idx = lane == 2 ? 0 : (lane == 3 || lane == 4) ? 1 : 2;
            return CentralBit[band] | (idx << 20);
        }

        public static bool Decode(int code, out int band, out int lane)
        {
            band = -1; lane = -1;
            int bit = code & 0xFFFFF, idx = (code >> 20) & 0xF;
            for (int b = 0; b <= 5; b++)
            {
                if (CentralBit[b] == bit) { band = b; lane = idx == 0 ? 2 : idx == 1 ? 3 : idx == 2 ? 1 : 2; return true; }
                if (RightBit[b] != 0 && RightBit[b] == bit) { band = b; lane = 0; return true; }
                if (LeftBit[b] != 0 && LeftBit[b] == bit) { band = b; lane = 4; return true; }
            }
            return false;
        }

        // the match screen names its slots (GK, DCL, DC, DCR, WBR, AMC, STL, STR ...)
        static readonly Regex SlotRx = new Regex(@"^(GK|SK|WB|DM|AM|ST|D|M)(C|R|L|CR|CL|RC|LC)?$", RegexOptions.Compiled);
        public static bool IsSlotName(string s) => !string.IsNullOrEmpty(s) && SlotRx.IsMatch(s);
        public static bool ParseSlotName(string s, out int band, out int lane)
        {
            band = -1; lane = -1;
            var m = SlotRx.Match(s ?? ""); if (!m.Success) return false;
            string p = m.Groups[1].Value, q = m.Groups[2].Value;
            if (p == "GK" || p == "SK") { band = 0; lane = 2; return true; }
            band = p == "D" ? 1 : p == "WB" || p == "DM" ? 2 : p == "M" ? 3 : p == "AM" ? 4 : 5;
            bool centralOnly = p == "DM" || p == "ST";          // their R and L are the central lanes; the wide lanes are WB and the wingers
            switch (q)
            {
                case "": case "C": lane = 2; break;
                case "CR": case "RC": lane = 1; break;
                case "CL": case "LC": lane = 3; break;
                case "R": lane = centralOnly ? 1 : 0; break;
                case "L": lane = centralOnly ? 3 : 4; break;
            }
            if (p == "WB" && q == "") lane = 2;
            return lane >= 0;
        }

        static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        public static string Clean(string t) => Tags.Replace(PersonLinks.Plain(t) ?? "", "").Trim();

        // ---------- the tactics pitch: shirt number, role, name per slot, in tree order ----------
        public static List<Slot> FromTactics(List<PitchText> texts)
        {
            var slots = new List<Slot>(); string role = "", num = ""; float sx = float.NaN, sy = float.NaN;
            foreach (var t in texts)
            {
                var p = t.Path ?? "";
                if (p.Contains("fm-tactics-shirt-number")) { num = Clean(t.Text); sx = t.X; sy = t.Y; }
                else if (p.Contains("RoleButton")) role = Clean(t.Text);
                else if (p.Contains("PlayerNameText"))
                {
                    var n = Clean(t.Text); if (n.Length == 0) continue;
                    slots.Add(new Slot { Name = n, Role = role, Number = num, X = t.X, Y = t.Y, SX = sx, SY = sy });
                    role = ""; num = ""; sx = float.NaN; sy = float.NaN;
                }
            }
            AssignBands(slots);
            return slots;
        }

        // bands from the slots' heights, lanes from their sideways position; the goalkeeper is the lowest and the centre line
        public static string AssignBands(List<Slot> slots)
        {
            if (slots.Count == 0) return "no slots";
            var sorted = slots.OrderByDescending(s => s.Y).ToList();
            var clusters = new List<List<Slot>>();
            foreach (var s in sorted)
            {
                if (clusters.Count == 0 || Math.Abs(s.Y - clusters[clusters.Count - 1].Average(x => x.Y)) > 45f) clusters.Add(new List<Slot>());
                clusters[clusters.Count - 1].Add(s);
            }
            foreach (var s in clusters[0]) s.Band = 0;
            string note = clusters.Count + " rows";
            if (clusters.Count > 1)
            {
                var mean = clusters.Select(c => c.Average(x => x.Y)).ToList();
                float gk = mean[0] - mean[1];
                var gaps = new List<float>();
                for (int i = 1; i + 1 < mean.Count; i++) gaps.Add(mean[i] - mean[i + 1]);
                var cands = gaps.Where(g => g > 40f && g < 1.6f * gk).ToList();
                float unit = cands.Count > 0 ? cands.Min() : gk;
                if (unit < 40f) unit = 40f;
                int prev = 0;
                for (int i = 1; i < clusters.Count; i++)
                {
                    int band = 1 + (int)Math.Round((mean[1] - mean[i]) / unit);
                    if (band <= prev) band = prev + 1;
                    if (band > 5) band = 5;
                    prev = band;
                    foreach (var s in clusters[i]) s.Band = band;
                }
                note += $", unit {unit:0}";
            }
            float cx = clusters[0].Average(s => s.X);
            float hw = slots.Max(s => Math.Abs(s.X - cx)); if (hw < 60f) hw = 60f;
            foreach (var s in slots)
            {
                if (s.Band == 0) { s.Lane = 2; continue; }
                float f = (s.X - cx) / hw;
                bool sideOk = s.Band >= 1 && s.Band <= 4;
                if (sideOk && Math.Abs(f) > 0.75f) s.Lane = f < 0 ? 4 : 0;
                else if (Math.Abs(f) < 0.2f) s.Lane = 2;
                else s.Lane = f < 0 ? 3 : 1;
            }
            // two players can't share a slot: nudge a clash to the free lane next to it
            foreach (var g in slots.GroupBy(s => s.Band * 10 + s.Lane).Where(g => g.Count() > 1))
                foreach (var s in g.OrderBy(x => x.X).Skip(1))
                    for (int d = 1; d <= 4; d++)
                    {
                        int l1 = s.Lane - d, l2 = s.Lane + d;
                        if (l1 >= 0 && !slots.Any(o => o.Band == s.Band && o.Lane == l1)) { s.Lane = l1; break; }
                        if (l2 <= 4 && !slots.Any(o => o.Band == s.Band && o.Lane == l2)) { s.Lane = l2; break; }
                    }
            return note;
        }

        public static Slot Find(List<Slot> slots, int band, int lane)
        {
            var exact = slots.FirstOrDefault(s => s.Band == band && s.Lane == lane);
            if (exact != null) return exact;
            var same = slots.Where(s => s.Band == band).OrderBy(s => Math.Abs(s.Lane - lane)).ToList();
            return same.Count == 1 ? same[0] : null;   // only when the band leaves no doubt
        }

        // neighbours: same band within two lanes (SI joined DCL and DCR over DC), or the next occupied band within one lane
        public static bool Related(Slot a, Slot b, ICollection<int> occupiedBands)
        {
            if (a == null || b == null || a.Band <= 0 || b.Band <= 0) return false;
            if (a.Band == b.Band) return Math.Abs(a.Lane - b.Lane) <= 2;
            int lo = Math.Min(a.Band, b.Band), hi = Math.Max(a.Band, b.Band);
            if (occupiedBands.Any(x => x > lo && x < hi)) return false;
            return Math.Abs(a.Lane - b.Lane) <= 1;
        }

        // ---------- the match screen: <SLOT>/Player/Name, <SLOT>/RatingAndPositionBlock/RatingBG/Rating/text, .../PositionContainer ----------
        public static List<Slot> FromMatch(List<PitchText> texts)
        {
            var bySlot = new Dictionary<string, Slot>();
            var order = new List<string>();
            foreach (var t in texts)
            {
                var segs = (t.Path ?? "").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int k = -1;
                for (int i = segs.Length - 1; i >= 0; i--) if (IsSlotName(segs[i])) { k = i; break; }
                if (k < 0) continue;
                var name = segs[k];
                if (!bySlot.TryGetValue(name, out var s))
                {
                    s = new Slot(); if (!ParseSlotName(name, out s.Band, out s.Lane)) continue;
                    bySlot[name] = s; order.Add(name);
                }
                var rest = string.Join("/", segs.Skip(k + 1));
                var txt = Clean(t.Text);
                if (rest.StartsWith("Player/Name")) { s.Name = txt; s.X = t.X; s.Y = t.Y; }
                else if (rest.Contains("Rating/text") || rest.EndsWith("Rating")) s.Rating = txt;
                else if (rest.Contains("PositionContainer")) s.Role = txt;
                else if (rest.Contains("shirt-number")) s.Number = txt;
            }
            return order.Select(n => bySlot[n]).Where(s => s.Name.Length > 0).ToList();
        }

        // decode one row of SI's partnership data on these slots
        public static bool DecodeRow(List<Slot> slots, IList<int> row, out Slot a, out Slot b, out int level)
        {
            a = b = null; level = 0;
            if (row == null || row.Count < 2) return false;
            level = row.Count > 2 ? row[2] : 0;
            if (!Decode(row[0], out int ba, out int la) || !Decode(row[1], out int bb, out int lb)) return false;
            a = Find(slots, ba, la); b = Find(slots, bb, lb);
            return a != null && b != null && a != b;
        }
    }

    // ---------- SI's partnership sentences ----------
    internal static class PtParse
    {
        internal sealed class Feeling { public string Grade = "", Zone = "", Partner = ""; public bool Hurting; }

        // 657569-657598 and 657599-657622
        static readonly Regex Feels = new Regex(@"^Feels (?:he|she|they) (?:has|have) an? (strong|good|decent|weak|very weak) partnership (on the right|on the left|at the back|in the middle|in attack|going forward) with (.+?)\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Hurts = new Regex(@"^Feels (?:his|her|their) (very weak|weak) partnership (on the right|on the left|at the back|in the middle|in attack|going forward) with (.+?) could be contributing to the club's (?:considerable )?underachievement\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Feeling Feel(string plain)
        {
            var t = (plain ?? "").Trim();
            var m = Feels.Match(t);
            if (m.Success) return new Feeling { Grade = m.Groups[1].Value.ToLowerInvariant(), Zone = m.Groups[2].Value.ToLowerInvariant(), Partner = m.Groups[3].Value.Trim() };
            m = Hurts.Match(t);
            if (m.Success) return new Feeling { Grade = m.Groups[1].Value.ToLowerInvariant(), Zone = m.Groups[2].Value.ToLowerInvariant(), Partner = m.Groups[3].Value.Trim(), Hurting = true };
            return null;
        }

        // 464199-464203: "<A> and <B> have a strong partnership and play well together in their selected roles." etc.
        static readonly Regex Roles = new Regex(@"^(.+?) and (.+?) (?:have a (strong|good|fairly decent) partnership|(have played fairly poorly) when paired|are (an untested) partnership)", RegexOptions.Compiled);
        public static bool Sentence(string plain, out string a, out string b, out string grade)
        {
            a = b = grade = "";
            var m = Roles.Match((plain ?? "").Trim()); if (!m.Success) return false;
            a = m.Groups[1].Value.Trim(); b = m.Groups[2].Value.Trim();
            grade = m.Groups[3].Success ? (m.Groups[3].Value == "fairly decent" ? "decent" : m.Groups[3].Value)
                  : m.Groups[4].Success ? "poor" : "untested";
            return true;
        }
    }

    // ---------- the ledger ----------
    internal sealed class Pair
    {
        public string A = "", B = "";                 // pitch names, A < B
        public string IdA = "", IdB = "";             // SI person ids when a link named them
        public string First = "";                     // game date first seen
        public bool Si; public int SiLevel; public string SiFirst = "", SiLast = "", SiCodes = "", Dropped = "";
        public string Feel = "", FeelBy = "", FeelZone = "", FeelDate = "";
        public string Sentence = "", SentenceDate = "";
        public string Hist = "";                      // "d/m/yyyy=step;..." each time the step changed
        public string Key => A + "|" + B;
    }

    internal sealed class MatchRow
    {
        public string Date = "", Slot = "", Name = "", Role = "", Rating = "";
        public bool Started; public int FirstSnap, LastSnap;
    }

    // 0.2.0: one row per match read: home or away (from which pitch is ours), and SI's scoreboard as read (UNDER TEST)
    internal sealed class Fixture
    {
        public string Date = "", Venue = "", Home = "", Away = "", HomeGoals = "", AwayGoals = "", Raw = "";
        public string Line => HomeGoals.Length > 0 && AwayGoals.Length > 0
            ? (Home.Length > 0 && Away.Length > 0 ? $"{Home} {HomeGoals}–{AwayGoals} {Away}" : $"{Venue}, {HomeGoals}–{AwayGoals}")
            : Venue;
    }

    // 0.2.0: a fresh partnership story for the press and the fans (written to "partnership press.tsv")
    internal sealed class PressItem { public string Id = "", Kind = "", A = "", B = "", Zone = "", Date = "", When = "", Starts = "", Word = ""; }

    internal sealed class PairView
    {
        public Pair P;
        public int Step;                  // 0 untested, 1..7; -1 fledgling
        public string Word = "", Band = "", State = "", Trend = "", Coach = "";
        public int Starts, Played, RatedN; public double RatingAvg;
        public bool Recognised => P.Si && P.Dropped.Length == 0;
        public List<string> Facts = new List<string>();
    }

    internal static class PtLedger
    {
        internal static readonly string[] Files = { "partnership pairs.tsv", "partnership sightings.tsv", "partnership matches.tsv", "partnership shape.tsv", "partnership fixtures.tsv" };
        internal const string PressFile = "partnership press.tsv";   // derived from the ledger on every save and load; read by Press Room and Tabloid
        internal static readonly Dictionary<string, Fixture> Fixtures = new Dictionary<string, Fixture>();
        internal static int Version;   // goes up on every save (the overlay redraws when it changes)
        internal static string Dir = ".";
        internal static readonly Dictionary<string, Pair> Pairs = new Dictionary<string, Pair>();
        internal static readonly List<string> Sightings = new List<string>();      // date \t kind \t A \t B \t detail
        internal static readonly List<MatchRow> Matches = new List<MatchRow>();
        internal static readonly Dictionary<string, List<Slot>> Shape = new Dictionary<string, List<Slot>>();   // "in possession" / "out of possession" -> our XI
        internal static readonly HashSet<string> Squad = new HashSet<string>();     // pitch names seen in our XI
        internal static readonly Dictionary<string, string> Ids = new Dictionary<string, string>();   // pitch name -> SI id
        internal static readonly Dictionary<string, string> Numbers = new Dictionary<string, string>();   // shirt number -> tactics-pitch name (the match screen shortens some names: "Pablo", "Diego")
        internal static Action<string, string, string> Log = (k, a, b) => { };
        internal static bool Dirty;

        // settings (the plugin sets these from its config)
        internal static int MinTracked = 5, TrendDays = 28, BreakingDays = 42, FledglingStarts = 3;
        internal static double UpperShare = 0.5, UpperRating = 6.85, FledglingRating = 6.7;

        // ---------- names ----------
        static readonly Regex Initial = new Regex(@"^\p{Lu}\.\s+", RegexOptions.Compiled);
        // the pitch shows surnames (or a common name such as "Pablo Felipe"); links show "J. Tanganga" or "Teden Mengi"
        public static string Canon(string name)
        {
            var n = PtSlots.Clean(name);
            if (n.Length == 0) return n;
            if (Squad.Contains(n)) return n;
            var s = Initial.Replace(n, "");
            if (Squad.Contains(s)) return s;
            string best = null;
            foreach (var q in Squad) if ((n.EndsWith(" " + q) || n.StartsWith(q + " ")) && (best == null || q.Length > best.Length)) best = q;
            if (best != null) return best;
            if (s != n) return s;
            var parts = n.Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1] : n;
        }

        static Pair Get(string a, string b, string date)
        {
            if (string.CompareOrdinal(a, b) > 0) { var t = a; a = b; b = t; }
            var key = a + "|" + b;
            if (!Pairs.TryGetValue(key, out var p)) { p = new Pair { A = a, B = b, First = date }; Pairs[key] = p; Dirty = true; }
            return p;
        }
        static void Sight(string date, string kind, string a, string b, string detail)
        {
            Sightings.Add($"{date}\t{kind}\t{a}\t{b}\t{detail}");
            if (Sightings.Count > 4000) Sightings.RemoveRange(0, Sightings.Count - 4000);
            Dirty = true;
        }

        // ---------- what the screens tell us ----------
        // a tactics pitch with SI's lines on: our XI in this phase, and the rows SI drew
        public static int SeeTactics(string date, string phase, List<Slot> slots, List<List<int>> rows, bool linesOn)
        {
            if (slots == null || slots.Count < 7) return 0;
            foreach (var s in slots) { Squad.Add(s.Name); if (s.Number.Length > 0) Numbers[s.Number] = s.Name; }
            Shape[phase] = slots.Select(s => new Slot { Name = s.Name, Role = s.Role, Number = s.Number, Band = s.Band, Lane = s.Lane, X = s.X, Y = s.Y }).ToList();
            Dirty = true;
            if (!linesOn || rows == null) return 0;
            int seen = 0; var drawn = new HashSet<string>();
            foreach (var r in rows)
            {
                if (!PtSlots.DecodeRow(slots, r, out var a, out var b, out int level)) { Log("row", "not matched to two slots", string.Join(" ", r)); continue; }
                var p = Get(a.Name, b.Name, date);
                var codes = string.Join(",", new[] { a.Code, b.Code }.OrderBy(x => x));
                if (!p.Si || p.Dropped.Length > 0) { Sight(date, "line", p.A, p.B, $"level {level} {a.SlotName}-{b.SlotName} {phase}"); Log("pair", "SI line", $"{p.A} - {p.B} level {level} ({a.SlotName}-{b.SlotName}, {phase})"); }
                else if (level != p.SiLevel) Sight(date, "level", p.A, p.B, $"level {p.SiLevel} -> {level}");
                p.Si = true; p.SiLevel = level; p.SiCodes = codes; p.Dropped = "";
                if (p.SiFirst.Length == 0) p.SiFirst = date;
                p.SiLast = date; drawn.Add(p.Key); seen++; Dirty = true;
            }
            // a pair SI drew before, standing in the very same two slots, with lines on and no line now: SI has dropped it
            // (0.2.1: only when the pitch shows rows at all; ShouldDrawPartnershipLines is a fixed setting, not SI's eye-menu toggle,
            // so with SI's lines switched off an empty list must never read as every pair dropped)
            if (rows.Count == 0) return seen;
            var byCode = new Dictionary<int, string>();
            foreach (var s in slots) if (!byCode.ContainsKey(s.Code)) byCode[s.Code] = s.Name;
            foreach (var p in Pairs.Values.Where(x => x.Si && x.Dropped.Length == 0 && !drawn.Contains(x.Key) && x.SiCodes.Length > 0))
            {
                var cs = p.SiCodes.Split(','); if (cs.Length != 2) continue;
                if (!int.TryParse(cs[0], out int c1) || !int.TryParse(cs[1], out int c2)) continue;
                if (!byCode.TryGetValue(c1, out var n1) || !byCode.TryGetValue(c2, out var n2)) continue;
                if (!((n1 == p.A && n2 == p.B) || (n1 == p.B && n2 == p.A))) continue;
                p.Dropped = date; Sight(date, "dropped", p.A, p.B, "no line in the same slots"); Log("pair", "SI no longer draws", p.A + " - " + p.B);
            }
            return seen;
        }

        public static bool SeeFeeling(string date, string owner, string ownerId, string partner, string partnerId, PtParse.Feeling f)
        {
            if (f == null || string.IsNullOrEmpty(owner)) return false;
            var a = Canon(owner); var b = Canon(partner);
            if (a.Length == 0 || b.Length == 0 || a == b) return false;
            if (ownerId.Length > 0) Ids[a] = ownerId;
            if (partnerId.Length > 0) Ids[b] = partnerId;
            var p = Get(a, b, date);
            bool changed = p.Feel != f.Grade || p.FeelZone != f.Zone || p.FeelBy != a;
            p.Feel = f.Grade; p.FeelZone = f.Zone; p.FeelBy = a; p.FeelDate = date;
            if (ownerId.Length > 0) { if (p.A == a) p.IdA = ownerId; else p.IdB = ownerId; }
            if (partnerId.Length > 0) { if (p.A == b) p.IdA = partnerId; else p.IdB = partnerId; }
            if (changed) { Sight(date, "feel", p.A, p.B, $"{a}: {f.Grade} {f.Zone}{(f.Hurting ? " (hurting the club)" : "")}"); Log("pair", "feeling", $"{a} feels a {f.Grade} partnership {f.Zone} with {b}"); }
            Dirty = true;
            return true;
        }

        public static bool SeeSentence(string date, string a, string b, string grade)
        {
            a = Canon(a); b = Canon(b); if (a.Length == 0 || b.Length == 0 || a == b) return false;
            var p = Get(a, b, date);
            if (p.Sentence != grade) Sight(date, "sentence", p.A, p.B, grade);
            p.Sentence = grade; p.SentenceDate = date; Dirty = true;
            return true;
        }

        // the match screen: our XI in its named slots, once per read; players who come on appear in later reads
        static string MatchDate = ""; static int Snap;
        // which of the two pitches is ours: the one whose names we know from the tactics pitch
        public static int Ours(List<Slot> xi) => xi == null ? 0 : xi.Count(s => Squad.Contains(s.Name) || (s.Number.Length > 0 && Numbers.TryGetValue(s.Number, out var n) && n.StartsWith(s.Name)));
        public static int SeeMatch(string date, List<Slot> xi)
        {
            if (xi == null || xi.Count < 7) return 0;
            // the match screen's names are sometimes shorter than the tactics pitch's: the shirt number decides
            foreach (var s in xi) if (s.Number.Length > 0 && Numbers.TryGetValue(s.Number, out var n) && n != s.Name) s.Name = n;
            // a pitch left over from the last match (same players, same final ratings) on a later day is not a new match
            if (!Matches.Any(m => m.Date == date))
            {
                var prevDate = Matches.Select(m => m.Date).Where(d => d != date).LastOrDefault();
                if (prevDate != null)
                {
                    var prev = Matches.Where(m => m.Date == prevDate).ToList();
                    if (xi.All(s => s.Rating.Length > 0 && prev.Any(m => m.Name == s.Name && m.Rating == s.Rating))) { Log("match", "stale", "the " + prevDate + " pitch again on " + date + ": not counted"); return 0; }
                }
            }
            if (date != MatchDate) { MatchDate = date; Snap = Matches.Where(m => m.Date == date).Select(m => m.LastSnap).DefaultIfEmpty(0).Max(); }
            Snap++;
            int added = 0;
            foreach (var s in xi)
            {
                Squad.Add(s.Name);
                var row = Matches.FirstOrDefault(m => m.Date == date && m.Name == s.Name);
                if (row == null)
                {
                    bool started = !Matches.Any(m => m.Date == date);   // the first read of a match is the starting XI
                    if (!started) started = Matches.Where(m => m.Date == date).All(m => m.FirstSnap == Snap);
                    row = new MatchRow { Date = date, Name = s.Name, Slot = s.SlotName, Role = s.Role, Started = started, FirstSnap = Snap };
                    Matches.Add(row); added++;
                    if (!started) Log("match", "on", s.Name + " (" + s.SlotName + ")");
                }
                row.LastSnap = Snap;
                if (s.Rating.Length > 0) row.Rating = s.Rating;
            }
            Dirty = true;
            return added;
        }

        // the match screen's scoreboard (SI's ScoreBug), read as it stands; UNDER TEST: the layout is guessed (names, then the two
        // scores, in screen order) and the raw texts are kept, so a wrong guess shows nothing rather than a wrong score
        static readonly Regex Goals = new Regex(@"^\d{1,2}$", RegexOptions.Compiled);
        static readonly Regex NotScore = new Regex(@"(?i)time|clock|minute|added|stoppage|stat|tooltip|button|possession|shots", RegexOptions.Compiled);
        public static bool SeeScoreBug(string date, string venue, List<PitchText> texts)
        {
            if (string.IsNullOrEmpty(date) || date == "?") return false;
            if (!Fixtures.TryGetValue(date, out var f)) { f = new Fixture { Date = date }; Fixtures[date] = f; Dirty = true; }
            if (venue.Length > 0 && f.Venue != venue) { f.Venue = venue; Dirty = true; }
            if (texts == null || texts.Count == 0) return false;
            var raw = string.Join(" | ", texts.Select(t => PtSlots.Clean(t.Text)).Where(t => t.Length > 0));
            if (raw.Length > 300) raw = raw.Substring(0, 300);
            var nums = texts.Where(t => Goals.IsMatch(PtSlots.Clean(t.Text)) && !NotScore.IsMatch(t.Path ?? "")).ToList();
            var names = texts.Where(t => { var c = PtSlots.Clean(t.Text); return c.Length >= 2 && c.Length <= 30 && Regex.IsMatch(c, @"\p{L}") && !c.Contains(":") && !NotScore.IsMatch(t.Path ?? "") && Regex.IsMatch(t.Path ?? "", "(?i)name|team|club"); }).ToList();
            bool changed = raw != f.Raw;
            f.Raw = raw;
            if (nums.Count == 2) { f.HomeGoals = PtSlots.Clean(nums[0].Text); f.AwayGoals = PtSlots.Clean(nums[1].Text); }
            if (names.Count == 2) { f.Home = PtSlots.Clean(names[0].Text); f.Away = PtSlots.Clean(names[1].Text); }
            if (changed) { Dirty = true; Log("score", f.Line, raw); }
            return changed;
        }

        // ---------- what it all adds up to ----------
        static double Rate(string r) => double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : double.NaN;

        public static int TrackedMatches => Matches.Select(m => m.Date).Distinct().Count();

        public static void MatchStats(Pair p, out int starts, out int played, out double avg, out int ratedN, out string last, out int trackedSince)
        {
            starts = played = ratedN = 0; avg = 0; last = ""; double sum = 0;
            int firstDay = PtDate.Day(p.First);
            var dates = Matches.Select(m => m.Date).Distinct().ToList();
            trackedSince = dates.Count(d => firstDay < 0 || PtDate.Day(d) >= firstDay);
            foreach (var d in dates)
            {
                var rows = Matches.Where(m => m.Date == d).ToList();
                var ra = rows.FirstOrDefault(m => m.Name == p.A); var rb = rows.FirstOrDefault(m => m.Name == p.B);
                if (ra == null || rb == null) continue;
                if (ra.LastSnap < rb.FirstSnap || rb.LastSnap < ra.FirstSnap) continue;      // never on the pitch together
                var sa = SlotOf(ra); var sb = SlotOf(rb);
                var bands = new HashSet<int>(rows.Where(m => m.Started).Select(m => SlotOf(m).Band));
                if (!PtSlots.Related(sa, sb, bands)) continue;
                played++; last = d;
                if (ra.Started && rb.Started) starts++;
                double x = Rate(ra.Rating), y = Rate(rb.Rating);
                if (!double.IsNaN(x) && !double.IsNaN(y)) { sum += (x + y) / 2; ratedN++; }
            }
            avg = ratedN > 0 ? sum / ratedN : 0;
        }
        static Slot SlotOf(MatchRow m) { var s = new Slot { Name = m.Name }; PtSlots.ParseSlotName(m.Slot, out s.Band, out s.Lane); return s; }

        // 0.2.0: each match seen with either of the pair, newest first: together, or one without the other
        public static List<string> History(Pair p, int max = 8)
        {
            var l = new List<string>();
            foreach (var d in Matches.Select(m => m.Date).Distinct().OrderByDescending(x => PtDate.Day(x)))
            {
                var ra = Matches.FirstOrDefault(m => m.Date == d && m.Name == p.A); var rb = Matches.FirstOrDefault(m => m.Date == d && m.Name == p.B);
                if (ra == null && rb == null) continue;
                var fx = Fixtures.TryGetValue(d, out var f) ? f.Line : "";
                var head = d + (fx.Length > 0 ? " · " + fx : "");
                string R(MatchRow r) => r.Rating.Length > 0 ? " " + r.Rating : "";
                if (ra != null && rb != null)
                {
                    bool together = !(ra.LastSnap < rb.FirstSnap || rb.LastSnap < ra.FirstSnap);
                    var how = ra.Started && rb.Started ? "both started" : ra.Started ? p.B + " came on" : rb.Started ? p.A + " came on" : "both came on";
                    l.Add($"{head} · {p.A}{R(ra)}, {p.B}{R(rb)} · {(together ? how : "never on together")}");
                }
                else { var r = ra ?? rb; var other = ra != null ? p.B : p.A; l.Add($"{head} · {r.Name}{R(r)} without {other}"); }
                if (l.Count >= max) break;
            }
            return l;
        }

        internal static readonly string[] StepWords = { "Untested", "Not working", "Uneasy", "Finding their feet", "Steady", "Clicking", "On the same wavelength", "Telepathic" };

        public static PairView View(Pair p, string today)
        {
            var v = new PairView { P = p };
            MatchStats(p, out v.Starts, out v.Played, out v.RatingAvg, out v.RatedN, out var last, out int trackedSince);
            // SI's band: a player's own words first, then SI's line on the pitch
            string band = "";
            if (p.Feel.Length > 0) band = p.Feel;
            else if (p.Sentence.Length > 0 && p.Sentence != "untested") band = p.Sentence == "poor" ? "weak" : p.Sentence;
            else if (p.Si && p.Dropped.Length == 0) band = p.SiLevel <= 0 ? "decent" : p.SiLevel == 1 ? "good" : "strong";
            v.Band = band;
            bool upper;
            if (trackedSince < MinTracked) upper = p.Si && p.Dropped.Length == 0 || p.Feel.Length > 0;   // SI recognising a pair already means "played together often" (464201)
            else
            {
                double share = trackedSince > 0 ? (double)v.Starts / trackedSince : 0;
                upper = share >= UpperShare && (v.RatedN < 3 || v.RatingAvg >= UpperRating);
            }
            switch (band)
            {
                case "strong": v.Step = 7; break;
                case "good": v.Step = upper ? 6 : 5; break;
                case "decent": v.Step = upper ? 4 : 3; break;
                case "weak": v.Step = 2; break;
                case "very weak": v.Step = 1; break;
                default: v.Step = v.Starts >= FledglingStarts && (v.RatedN == 0 || v.RatingAvg >= FledglingRating) ? -1 : 0; break;
            }
            v.Word = v.Step == -1 ? "Fledgling" : StepWords[Math.Max(0, v.Step)];

            // trend: the step now against the step at least TrendDays ago
            int today_ = PtDate.Day(today), cur = v.Step < 0 ? 0 : v.Step;
            var hist = ParseHist(p.Hist);
            int then = -99;
            foreach (var (d, s) in hist) if (today_ >= 0 && d >= 0 && today_ - d >= TrendDays) then = s;
            if (then != -99 && cur > then) v.Trend = "rising"; else if (then != -99 && cur < then) v.Trend = "falling";
            // breaking down: SI has stopped drawing them, or a fall of two steps or more from Clicking or better
            if (p.Dropped.Length > 0) v.State = "Breaking down";
            else
            {
                int peak = -1;
                foreach (var (d, s) in hist) if (today_ >= 0 && d >= 0 && today_ - d <= BreakingDays && s > peak) peak = s;
                if (peak >= 5 && peak - cur >= 2) v.State = "Breaking down";
            }

            // the facts behind it, in the card
            if (p.Feel.Length > 0) v.Facts.Add($"{p.FeelBy} feels it is {(p.Feel == "strong" || p.Feel == "good" ? "a " : p.Feel.StartsWith("very") ? "a " : "a ")}{p.Feel} partnership {p.FeelZone} ({p.FeelDate}).");
            if (p.Sentence.Length > 0) v.Facts.Add($"The report: {SentenceWords(p.Sentence)} ({p.SentenceDate}).");
            if (p.Si && p.Dropped.Length == 0) v.Facts.Add($"Recognised as a partnership: a line on the tactics pitch since {p.SiFirst}.");
            else if (p.Dropped.Length > 0) v.Facts.Add($"Recognised until {p.Dropped}: in the same slots the tactics pitch no longer draws their line.");
            else if (band.Length == 0) v.Facts.Add("Not recognised as a partnership yet.");
            if (v.Played > 0)
            {
                var s = $"Started together {Times(v.Starts)}, on the pitch together in {v.Played} match{(v.Played == 1 ? "" : "es")} of {trackedSince} seen";
                if (v.RatedN > 0) s += $", average rating together {v.RatingAvg.ToString("0.00", CultureInfo.InvariantCulture)}";
                v.Facts.Add(s + ".");
            }
            else v.Facts.Add(trackedSince > 0 ? $"Not on the pitch together in the {trackedSince} match{(trackedSince == 1 ? "" : "es")} seen so far." : "No matches seen yet: starts together are counted from the match screen.");
            v.Coach = Coach(v);
            return v;
        }
        static string Times(int n) => n == 0 ? "never" : n == 1 ? "once" : n == 2 ? "twice" : n + " times";
        static string SentenceWords(string g) => g == "poor" ? "they have played fairly poorly when paired" : g == "untested" ? "an untested partnership" : "a " + (g == "decent" ? "fairly decent" : g) + " partnership in their selected roles";

        public static string Coach(PairView v)
        {
            if (v.State == "Breaking down") return "Something's gone between them lately.";
            switch (v.Step)
            {
                case 7: return "Those two barely need to talk any more.";
                case 6: return v.Trend == "rising" ? "They're reading each other well. Keep them together." : "They're on the same wavelength. That doesn't happen by accident.";
                case 5: return v.Trend == "rising" ? "It's starting to click between them." : "It's clicking. A few more games and it could be special.";
                case 4: return "Solid enough. Nothing special yet.";
                case 3: return "Still working each other out.";
                case 2: return "There's some hesitation between them.";
                case 1: return "It isn't working. Worth a rethink.";
                case -1: return "Early days, but the signs are good.";
                default: return "We haven't seen enough of them together to say.";
            }
        }

        static List<(int, int)> ParseHist(string h)
        {
            var l = new List<(int, int)>();
            foreach (var e in (h ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = e.Split('='); if (kv.Length != 2) continue;
                if (int.TryParse(kv[1], out int s)) l.Add((PtDate.Day(kv[0]), s));
            }
            return l;
        }

        // record each pair's step when it changes (for trends); called after new data
        public static void Recompute(string today)
        {
            foreach (var p in Pairs.Values)
            {
                var v = View(p, today); int s = v.Step < 0 ? 0 : v.Step;
                var hist = ParseHist(p.Hist);
                if (hist.Count == 0 || hist[hist.Count - 1].Item2 != s)
                {
                    p.Hist = (p.Hist.Length > 0 ? p.Hist + ";" : "") + today + "=" + s;
                    if (hist.Count > 0) Sight(today, "step", p.A, p.B, $"{StepWords[hist[hist.Count - 1].Item2]} -> {StepWords[s]}");
                    Dirty = true;
                }
            }
        }

        // a pair the ledger doesn't hold (neighbours SI doesn't recognise): Untested, or Fledgling from our own counts
        public static PairView Transient(string a, string b, string today)
        {
            if (string.CompareOrdinal(a, b) > 0) { var t = a; a = b; b = t; }
            return View(new Pair { A = a, B = b }, today);
        }

        // neighbours in a shape: the pairs SI could draw a line between
        public static List<(Slot a, Slot b)> Neighbours(List<Slot> shape)
        {
            var l = new List<(Slot, Slot)>();
            if (shape == null) return l;
            var bands = new HashSet<int>(shape.Where(s => s.Band > 0).Select(s => s.Band));
            for (int i = 0; i < shape.Count; i++)
                for (int j = i + 1; j < shape.Count; j++)
                    if (PtSlots.Related(shape[i], shape[j], bands)) l.Add((shape[i], shape[j]));
            return l;
        }

        // where on the pitch a pair plays: the player's own words if any, else from their slots
        public static string Zone(Pair p, List<Slot> shape)
        {
            if (p.FeelZone.Length > 0) return p.FeelZone;
            var a = shape?.FirstOrDefault(s => s.Name == p.A); var b = shape?.FirstOrDefault(s => s.Name == p.B);
            if (a == null || b == null)
            {
                // the slots SI drew them in
                var cs = p.SiCodes.Split(',');
                if (cs.Length == 2 && int.TryParse(cs[0], out int c1) && int.TryParse(cs[1], out int c2) && PtSlots.Decode(c1, out int b1, out int l1) && PtSlots.Decode(c2, out int b2, out int l2))
                { a = new Slot { Band = b1, Lane = l1 }; b = new Slot { Band = b2, Lane = l2 }; }
                else return "";
            }
            if (a.Lane <= 1 && b.Lane <= 1 && (a.Lane == 0 || b.Lane == 0)) return "on the right";
            if (a.Lane >= 3 && b.Lane >= 3 && (a.Lane == 4 || b.Lane == 4)) return "on the left";
            double m = (a.Band + b.Band) / 2.0;
            return m <= 1.5 ? "at the back" : m <= 3.5 ? "in the middle" : "in attack";
        }

        public static List<PairView> Views(string today)
        {
            return Pairs.Values.Select(p => View(p, today))
                .OrderByDescending(v => v.Step == -1 ? 0.5 : v.Step).ThenByDescending(v => v.Starts).ThenBy(v => v.P.A).ToList();
        }

        // ---------- 0.2.0: stories for the press and the fans ----------
        static int LedgerStart()
        {
            int s = int.MaxValue;
            foreach (var p in Pairs.Values) { int d = PtDate.Day(p.First); if (d >= 0 && d < s) s = d; }
            foreach (var m in Matches) { int d = PtDate.Day(m.Date); if (d >= 0 && d < s) s = d; }
            return s == int.MaxValue ? -1 : s;
        }
        static bool Started(string date, string name) => Matches.Any(m => m.Date == date && m.Name == name && m.Started);
        static readonly Dictionary<string, int> KindOrder = new Dictionary<string, int> { ["split"] = 0, ["breaking"] = 1, ["reunited"] = 2, ["telepathic"] = 3, ["clicking"] = 4, ["new"] = 5 };

        public static List<PressItem> Stories(string today)
        {
            var l = new List<PressItem>();
            int t = PtDate.Day(today); if (t < 0) return l;
            int start = LedgerStart();
            var shape = Shape.TryGetValue("in possession", out var sh) ? sh : null;
            var dates = Matches.Select(m => m.Date).Distinct().OrderBy(d => PtDate.Day(d)).ToList();
            string last = dates.Count > 0 ? dates[dates.Count - 1] : null, prev = dates.Count > 1 ? dates[dates.Count - 2] : null;
            bool fresh = last != null && t - PtDate.Day(last) <= 10;
            foreach (var p in Pairs.Values)
            {
                var v = View(p, today);
                var zone = Zone(p, shape);
                PressItem It(string kind, string date) => new PressItem { Id = kind + "|" + p.Key + "|" + date, Kind = kind, A = p.A, B = p.B, Zone = zone, Date = date, When = date == today ? "today" : "last time out", Starts = v.Starts.ToString(), Word = v.Word.ToLowerInvariant() };
                // together again, or split up, in the last match seen (a recognised pair only)
                if (fresh && prev != null && p.Si && p.Dropped.Length == 0 && v.Step >= 3)
                {
                    bool now = Started(last, p.A) && Started(last, p.B), before = Started(prev, p.A) && Started(prev, p.B);
                    bool one = Started(last, p.A) || Started(last, p.B);
                    if (now && !before) l.Add(It("reunited", last));
                    else if (!now && one && before) l.Add(It("split", last));
                }
                // SI has only lately recognised them (not in the ledger's first fortnight, when everything is new to us)
                int sf = PtDate.Day(p.SiFirst);
                if (p.Si && p.Dropped.Length == 0 && sf >= 0 && start >= 0 && sf >= start + 14 && t - sf <= 14) l.Add(It("new", p.SiFirst));
                // a step up to Clicking or better in the last fortnight
                var hist = ParseHist(p.Hist);
                if (hist.Count >= 2)
                {
                    var (d2, s2) = hist[hist.Count - 1]; var s1 = hist[hist.Count - 2].Item2;
                    if (d2 >= 0 && t - d2 <= 14 && s2 >= 5 && s2 > s1)
                    {
                        var hd = p.Hist.Split(';').Last().Split('=')[0];
                        l.Add(It(s2 == 7 ? "telepathic" : "clicking", hd));
                    }
                }
                if (v.State == "Breaking down")
                {
                    var bd = p.Dropped.Length > 0 ? p.Dropped : p.Hist.Split(';').Last().Split('=')[0];
                    if (t - PtDate.Day(bd) <= 21) l.Add(It("breaking", bd));
                }
            }
            return l.OrderBy(x => KindOrder.TryGetValue(x.Kind, out var o) ? o : 9).ThenByDescending(x => PtDate.Day(x.Date)).ToList();
        }

        public static void WritePress(string today)
        {
            try
            {
                var sb = new StringBuilder("id\tkind\ta\tb\tzone\tdate\twhen\tstarts\tword\n");
                foreach (var x in Stories(today))
                    sb.Append(string.Join("\t", new[] { x.Id, x.Kind, x.A, x.B, x.Zone, x.Date, x.When, x.Starts, x.Word }.Select(E))).Append('\n');
                var path = Path.Combine(Dir, PressFile);
                var text = sb.ToString();
                if (!File.Exists(path) || File.ReadAllText(path) != text) File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch (Exception e) { Log("error", "press file", e.GetType().Name + ": " + e.Message); }
        }

        public static string Summary(string today)
        {
            int si = Pairs.Values.Count(p => p.Si && p.Dropped.Length == 0), feel = Pairs.Values.Count(p => p.Feel.Length > 0), m = TrackedMatches;
            return $"{si} recognised partnership{(si == 1 ? "" : "s")} · {m} match{(m == 1 ? "" : "es")} on record" + (feel > 0 ? $" · {feel} player feeling{(feel == 1 ? "" : "s")}" : "");
        }

        // ---------- files ----------
        static string E(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder("a\tb\tid_a\tid_b\tfirst\tsi\tsi_level\tsi_first\tsi_last\tsi_codes\tdropped\tfeel\tfeel_by\tfeel_zone\tfeel_date\tsentence\tsentence_date\thist\n");
                foreach (var p in Pairs.Values.OrderBy(x => x.Key))
                    sb.Append(string.Join("\t", new[] { p.A, p.B, p.IdA, p.IdB, p.First, p.Si ? "1" : "0", p.SiLevel.ToString(), p.SiFirst, p.SiLast, p.SiCodes, p.Dropped, p.Feel, p.FeelBy, p.FeelZone, p.FeelDate, p.Sentence, p.SentenceDate, p.Hist }.Select(E))).Append('\n');
                File.WriteAllText(Path.Combine(Dir, Files[0]), sb.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(Dir, Files[1]), "date\tkind\ta\tb\tdetail\n" + string.Join("", Sightings.Select(s => s + "\n")), new UTF8Encoding(false));
                var mb = new StringBuilder("date\tslot\tname\trole\tstarted\tfirst_read\tlast_read\trating\n");
                foreach (var m in Matches) mb.Append(string.Join("\t", new[] { m.Date, m.Slot, m.Name, m.Role, m.Started ? "1" : "0", m.FirstSnap.ToString(), m.LastSnap.ToString(), m.Rating }.Select(E))).Append('\n');
                File.WriteAllText(Path.Combine(Dir, Files[2]), mb.ToString(), new UTF8Encoding(false));
                var shb = new StringBuilder("phase\tslot\tband\tlane\tname\trole\tnumber\tid\n");
                foreach (var kv in Shape) foreach (var s in kv.Value) shb.Append(string.Join("\t", new[] { kv.Key, s.SlotName, s.Band.ToString(), s.Lane.ToString(), s.Name, s.Role, s.Number, Ids.TryGetValue(s.Name, out var id) ? id : "" }.Select(E))).Append('\n');
                foreach (var kv in Ids.Where(k => !Shape.Values.Any(l => l.Any(s => s.Name == k.Key)))) shb.Append($"id\t\t\t\t{E(kv.Key)}\t\t\t{E(kv.Value)}\n");
                foreach (var kv in Numbers) shb.Append($"number\t\t\t\t{E(kv.Value)}\t\t{E(kv.Key)}\t\n");
                File.WriteAllText(Path.Combine(Dir, Files[3]), shb.ToString(), new UTF8Encoding(false));
                var fb = new StringBuilder("date\tvenue\thome\taway\thome_goals\taway_goals\traw\n");
                foreach (var f in Fixtures.Values.OrderBy(x => PtDate.Day(x.Date))) fb.Append(string.Join("\t", new[] { f.Date, f.Venue, f.Home, f.Away, f.HomeGoals, f.AwayGoals, f.Raw }.Select(E))).Append('\n');
                File.WriteAllText(Path.Combine(Dir, Files[4]), fb.ToString(), new UTF8Encoding(false));
                Dirty = false; Version++;
                if (PtDate.Text.Length > 0) WritePress(PtDate.Text);
            }
            catch (Exception e) { Log("error", "save", e.GetType().Name + ": " + e.Message); }
        }

        public static void Clear() { Pairs.Clear(); Sightings.Clear(); Matches.Clear(); Shape.Clear(); Squad.Clear(); Ids.Clear(); Numbers.Clear(); Fixtures.Clear(); Version++; MatchDate = ""; Snap = 0; Dirty = false; }

        static List<string[]> Rows(string file)
        {
            var l = new List<string[]>(); var path = Path.Combine(Dir, file);
            if (!File.Exists(path)) return l;
            bool head = true;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) { if (head) { head = false; continue; } if (line.Length > 0) l.Add(line.Split('\t')); }
            return l;
        }
        static string At(string[] r, int i) => i < r.Length ? r[i] : "";

        public static void Load()
        {
            Clear();
            try
            {
                foreach (var r in Rows(Files[0]))
                {
                    var p = new Pair
                    {
                        A = At(r, 0), B = At(r, 1), IdA = At(r, 2), IdB = At(r, 3), First = At(r, 4), Si = At(r, 5) == "1",
                        SiLevel = int.TryParse(At(r, 6), out int lv) ? lv : 0, SiFirst = At(r, 7), SiLast = At(r, 8), SiCodes = At(r, 9), Dropped = At(r, 10),
                        Feel = At(r, 11), FeelBy = At(r, 12), FeelZone = At(r, 13), FeelDate = At(r, 14), Sentence = At(r, 15), SentenceDate = At(r, 16), Hist = At(r, 17)
                    };
                    if (p.A.Length > 0 && p.B.Length > 0) Pairs[p.Key] = p;
                    if (p.IdA.Length > 0) Ids[p.A] = p.IdA;
                    if (p.IdB.Length > 0) Ids[p.B] = p.IdB;
                }
                foreach (var r in Rows(Files[1])) Sightings.Add(string.Join("\t", r));
                foreach (var r in Rows(Files[2]))
                    Matches.Add(new MatchRow { Date = At(r, 0), Slot = At(r, 1), Name = At(r, 2), Role = At(r, 3), Started = At(r, 4) == "1", FirstSnap = int.TryParse(At(r, 5), out int f) ? f : 0, LastSnap = int.TryParse(At(r, 6), out int l) ? l : 0, Rating = At(r, 7) });
                foreach (var r in Rows(Files[3]))
                {
                    if (At(r, 0) == "id") { if (At(r, 4).Length > 0) Ids[At(r, 4)] = At(r, 7); continue; }
                    if (At(r, 0) == "number") { if (At(r, 6).Length > 0) Numbers[At(r, 6)] = At(r, 4); continue; }
                    var s = new Slot { Band = int.TryParse(At(r, 2), out int b) ? b : -1, Lane = int.TryParse(At(r, 3), out int ln) ? ln : -1, Name = At(r, 4), Role = At(r, 5), Number = At(r, 6) };
                    if (!Shape.TryGetValue(At(r, 0), out var list)) Shape[At(r, 0)] = list = new List<Slot>();
                    list.Add(s);
                    if (At(r, 7).Length > 0) Ids[s.Name] = At(r, 7);
                }
                foreach (var r in Rows(Files[4]))
                    if (At(r, 0).Length > 0) Fixtures[At(r, 0)] = new Fixture { Date = At(r, 0), Venue = At(r, 1), Home = At(r, 2), Away = At(r, 3), HomeGoals = At(r, 4), AwayGoals = At(r, 5), Raw = At(r, 6) };
                foreach (var l in Shape.Values) foreach (var s in l) Squad.Add(s.Name);
                foreach (var m in Matches) Squad.Add(m.Name);
                Dirty = false; Version++;
                if (PtDate.Text.Length > 0) WritePress(PtDate.Text);
            }
            catch (Exception e) { Log("error", "load", e.GetType().Name + ": " + e.Message); }
        }
    }

    // game dates ("d/m/yyyy", as the top bar shows them)
    internal static class PtDate
    {
        internal static string Text = "";
        internal static Action Changed = () => { };
        internal static void Set(string t)
        {
            if (t == Text || !IsDate(t)) return;
            Text = t; PtLedger.Log("date", t, ""); Changed();
        }
        static readonly Regex Rx = new Regex(@"^(\d{1,2})/(\d{1,2})/(\d{4})$", RegexOptions.Compiled);
        public static bool IsDate(string t) => Rx.IsMatch(t ?? "");
        public static int Day(string t)
        {
            var m = Rx.Match(t ?? ""); if (!m.Success) return -1;
            try { return (int)(new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)) - new DateTime(2000, 1, 1)).TotalDays; }
            catch { return -1; }
        }
    }
}
