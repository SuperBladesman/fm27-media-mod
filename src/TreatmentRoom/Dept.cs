// FM27 Media Mod - Treatment Room 0.4.0-0.5.5: the medical department (pure C#, tested offline). Kept per save in "treatment staff.tsv".
// Read from the game's own screens: Club > Staff > Medical Team (0.4.1: every member's job, team and medical skills from the
// table, the posts filled against the board's allowance, the board's feedback, and how the department ranks in the division
// for each skill), and a staff member's profile (0.4.0). The game's scale, from its language file, top to bottom:
// Elite, Outstanding, Very Good, Good, Average, Competent, Reasonable, Unsuited (so Competent sits below Average).
// What the department is decides what the Treatment Room knows (Nik, 23 Nov: realism, and differences between clubs):
//   the sports science of whoever signs the risk assessments sets how far the watch list reaches (the game: Sports Science
//   governs "the level and accuracy of the reports"); physiotherapy, head count and the staff's own advice are shown in
//   the game's words. The game's injuries and numbers are never changed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class Dept
    {
        internal const string FileName = "treatment staff.tsv";
        internal static readonly string[] Scale = { "Unsuited", "Reasonable", "Competent", "Average", "Good", "Very Good", "Outstanding", "Elite" };
        internal static int Band(string w)
        {
            if (string.IsNullOrEmpty(w)) return -1;
            var t = w.Trim();
            for (int i = 0; i < Scale.Length; i++) if (string.Equals(Scale[i], t, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
        internal static bool IsBand(string w) => Band(w) >= 0;

        internal sealed class Member
        {
            public string Key = "", Name = "", Job = "", Team = "", Physio = "", Sports = "", Motivating = "", Seen = "", Profiled = "", Joined = "", Left = "", Personality = "";
        }
        internal static readonly Dictionary<string, Member> Staff = new Dictionary<string, Member>();
        // the staff's own advice about the department (the latest of each kind): "short" = the game says more staff are needed,
        // "improve" / "vacancies" = the assistant's weekly Staffing Advice ("Medical Team Improvements (3)")
        internal static readonly Dictionary<string, (string Date, string Text)> Advice = new Dictionary<string, (string, string)>();
        // 0.4.1: posts filled against the board's allowance ("Physio 4/5"), and how the department compares in its division
        internal static readonly Dictionary<string, (int Filled, int Allowed, string Date)> Posts = new Dictionary<string, (int, int, string)>();
        internal static readonly Dictionary<string, (string Rank, string Verdict, string Best, string Worst, string Date)> Compare = new Dictionary<string, (string, string, string, string, string)>();
        internal static readonly Regex RankRx = new Regex(@"^(\d+)(st|nd|rd|th) in (.+)$", RegexOptions.Compiled);

        static readonly Regex JobRx = new Regex(@"\b(physio|doctor|sports scien|fitness coach|masseu|nutrition|rehab|medical)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex NotJob = new Regex(@"^(Medical|Medical Team|Medical Centre|Medical Attributes|Physiotherapy|Sports Science)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // a job title as the staff lists show it ("Head Physio", "Physio", "Chief Doctor", "Fitness Coach", "Sports Scientist")
        internal static bool MedicalJob(string job)
        {
            if (string.IsNullOrEmpty(job) || job.Length > 40) return false;
            var t = job.Trim();
            return JobRx.IsMatch(t) && !NotJob.IsMatch(t) && !t.StartsWith("Medical Team ", StringComparison.Ordinal);
        }
        // 0.6.4: the Medical Team table writes "U21 Squad" and "U18 Squad"; 0.6.3 took only "U21", so youth staff kept no team and counted as first team
        static readonly Regex TeamRx = new Regex(@"^(Senior Squad|First Team|Reserves|B Team|Second Team|(Under |U)\d\ds?( Squad)?|[A-Za-z0-9 ]+ Squad|Youth.*|Academy.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static bool IsTeam(string t) => !string.IsNullOrEmpty(t) && t.Length <= 30 && TeamRx.IsMatch(t.Trim());
        internal static bool FirstTeam(Member m) => m.Team.Length == 0 || Regex.IsMatch(m.Team, @"^(Senior Squad|First Team)$", RegexOptions.IgnoreCase);

        internal static readonly Regex ShortRx = new Regex(@"more first team physios|increase the number of physios|add a sports scientist|increase the size of our first team medical department|more medical staff",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static readonly Regex TeamAdviceRx = new Regex(@"^Medical Team (Improvements|Vacancies) \((\d+)\)$", RegexOptions.Compiled);

        static Member Get(string key, string name)
        {
            if (key.Length == 0) key = "name:" + name;
            if (Staff.TryGetValue(key, out var m)) return m;
            // a name-only row (from a list without links) is taken over by the row with the person id
            if (!key.StartsWith("name:"))
            {
                var old = Staff.Values.FirstOrDefault(x => x.Key.StartsWith("name:") && SameName(x.Name, name));
                if (old != null) { Staff.Remove(old.Key); old.Key = key; Staff[key] = old; return old; }
            }
            m = new Member { Key = key, Name = name };
            Staff[key] = m;
            return m;
        }
        // "W. Tullett" on the title bar and "Will Tullett" in a list are the same person
        internal static bool SameName(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            string la = a.Split(' ').Last(), lb = b.Split(' ').Last();
            if (la != lb) return false;
            return char.ToUpperInvariant(a[0]) == char.ToUpperInvariant(b[0]);
        }
        // 0.6.3: the personality from a staff profile on any attributes tab, for a member the department already knows
        internal static bool SetPersonality(string key, string personality, string date)
        {
            if (string.IsNullOrEmpty(key) || !Staff.TryGetValue(key, out var m)) return false;
            personality = (personality ?? "").Trim();
            if (personality.Length == 0 || personality.Length > 40) return false;
            bool ch = Set(ref m.Personality, personality);
            if (ch) m.Seen = date;
            return ch;
        }
        internal static bool Knows(string key) => !string.IsNullOrEmpty(key) && Staff.ContainsKey(key);
        static bool Set(ref string field, string v)
        {
            if (string.IsNullOrEmpty(v) || field == v) return false;
            field = v; return true;
        }
        static void Fuller(Member m, string name) { if (!string.IsNullOrEmpty(name) && name.Length > m.Name.Length) m.Name = name; }

        // a staff profile's medical attributes; returns true when something changed
        internal static bool Profile(string key, string name, string physio, string sports, string motivating, string date, string personality = "")
        {
            if (!IsBand(physio) && !IsBand(sports)) return false;
            var m = Get(key ?? "", name ?? "");
            Fuller(m, name);
            bool ch = false;
            if (!string.IsNullOrEmpty(personality) && personality.Length <= 40) ch |= Set(ref m.Personality, personality.Trim());   // 0.5.5
            if (IsBand(physio)) ch |= Set(ref m.Physio, physio.Trim());
            if (IsBand(sports)) ch |= Set(ref m.Sports, sports.Trim());
            if (IsBand(motivating)) ch |= Set(ref m.Motivating, motivating.Trim());
            if (ch || m.Profiled.Length == 0) { m.Profiled = date; ch = true; }
            m.Seen = date;
            return ch;
        }
        // a row of a staff list: name, job, team
        internal static bool ListRow(string key, string name, string job, string team, string date)
        {
            if (!MedicalJob(job)) return false;
            var m = Get(key ?? "", name ?? "");
            Fuller(m, name);
            bool ch = Set(ref m.Job, job.Trim());
            if (IsTeam(team)) ch |= Set(ref m.Team, team.Trim());
            if (m.Seen != date) { m.Seen = date; ch = true; }
            return ch;
        }
        // 0.4.1: a row of the Medical Team table with its skill columns (the game shows "-" where a job has no rating)
        internal static bool TableRow(string key, string name, string job, string team, string physio, string sports, string motivating, string date)
        {
            bool ch = ListRow(key, name, job, team, date);
            var m = Get(key ?? "", name ?? "");
            foreach (var (v, which) in new[] { (physio, 0), (sports, 1), (motivating, 2) })
            {
                if (string.IsNullOrEmpty(v)) continue;
                var t = v.Trim();
                if (!IsBand(t) && t != "-") continue;
                if (which == 0) ch |= Set(ref m.Physio, t); else if (which == 1) ch |= Set(ref m.Sports, t); else ch |= Set(ref m.Motivating, t);
            }
            if (ch && (IsBand(m.Physio) || IsBand(m.Sports) || m.Physio == "-")) m.Profiled = date;
            return ch;
        }
        internal static bool SetPost(string job, int filled, int allowed, string date)
        {
            if (string.IsNullOrEmpty(job) || filled < 0 || allowed < 0 || !MedicalJob(job)) return false;
            if (Posts.TryGetValue(job, out var had) && had.Filled == filled && had.Allowed == allowed) { Posts[job] = (filled, allowed, date); return false; }
            Posts[job] = (filled, allowed, date);
            return true;
        }
        internal static bool SetCompare(string skill, string rank, string verdict, string best, string worst, string date)
        {
            if (string.IsNullOrEmpty(skill) || !RankRx.IsMatch(rank ?? "")) return false;
            if (Compare.TryGetValue(skill, out var had) && had.Rank == rank && had.Verdict == verdict && had.Best == best && had.Worst == worst) return false;
            Compare[skill] = (rank, verdict ?? "", best ?? "", worst ?? "", date);
            return true;
        }
        internal static bool Said(string kind, string text, string date)
        {
            if (Advice.TryGetValue(kind, out var had) && had.Text == text && had.Date == date) return false;
            Advice[kind] = (date, text);
            return true;
        }

        // ---------- what the department is ----------
        internal sealed class Level
        {
            public int Physio = -1, Sports = -1, Motiv = -1;
            public string PhysioBy = "", SportsBy = "", MotivBy = "";
            public bool SportsFromAssessor, SportsShort, PhysioShort;
            public int Known, Profiled, Physios, Doctors, Scientists, Fitness;
            public List<string> PhysioTop = new List<string>();
        }
        // the department as it stands: staff seen on the medical lists or with medical skills, less anyone who has left (0.5.0)
        // 0.6.4: only people seen on the club's staff lists (a job); a profile opened elsewhere (F. Hunt, 23/4) is kept on file but isn't a member
        internal static List<Member> Medical() => Staff.Values.Where(m => m.Left.Length == 0 && MedicalJob(m.Job)).ToList();

        // 0.5.0: comings and goings. A complete read of the Medical Team table (as many medical rows as posts filled) is compared
        // with the department as it stood: anyone missing has left, anyone new has joined. The first complete read is the baseline.
        static List<string> TableKeys = new List<string>(); static string TableDate = "";
        internal static void Table(IEnumerable<string> keys, string date) { TableKeys = keys.Where(k => k.Length > 0 && !k.StartsWith("name:")).Distinct().ToList(); TableDate = date; }
        internal static (List<Member> Joined, List<Member> Left) TryRoster()
        {
            var joined = new List<Member>(); var left = new List<Member>();
            int filled = Posts.Values.Sum(p => p.Filled);
            if (TableKeys.Count == 0 || TableDate.Length == 0 || filled == 0 || TableKeys.Count != filled) return (joined, left);
            bool baseline = !Advice.TryGetValue("roster", out var prevRoster);
            var before = baseline ? new HashSet<string>() : new HashSet<string>(prevRoster.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var seen = new HashSet<string>(TableKeys);
            foreach (var k in TableKeys)
            {
                if (!Staff.TryGetValue(k, out var m)) continue;
                if (m.Left.Length > 0) { m.Left = ""; m.Joined = TableDate; joined.Add(m); }          // back on the list
                else if (!baseline && !before.Contains(k)) { m.Joined = TableDate; joined.Add(m); }   // new since the last complete read
            }
            foreach (var m in Staff.Values)
                if (!m.Key.StartsWith("name:") && m.Left.Length == 0 && (MedicalJob(m.Job) || m.Physio.Length > 0 || m.Sports.Length > 0) && !seen.Contains(m.Key))
                { m.Left = TableDate; left.Add(m); }
            Advice["roster"] = (TableDate, string.Join(",", TableKeys));
            TableKeys = new List<string>();
            return (joined, left);
        }
        internal static Level Now(string assessor)
        {
            var l = new Level();
            var med = Medical();
            l.Known = med.Count; l.Profiled = med.Count(m => m.Physio.Length > 0 || m.Sports.Length > 0);
            var first = med.Where(FirstTeam).OrderBy(m => JobRank(m.Job)).ThenBy(m => m.Name).ToList();
            foreach (var m in first)
            {
                int p = Band(m.Physio), s = Band(m.Sports), v = Band(m.Motivating);
                if (p > l.Physio) { l.Physio = p; l.PhysioBy = m.Name; l.PhysioTop.Clear(); }
                if (p >= 0 && p == l.Physio) l.PhysioTop.Add(m.Name);
                if (s > l.Sports) { l.Sports = s; l.SportsBy = m.Name; }
                if (v > l.Motiv) { l.Motiv = v; l.MotivBy = m.Name; }
                var j = m.Job.ToLowerInvariant();
                if (j.Contains("physio")) l.Physios++;
                else if (j.Contains("doctor")) l.Doctors++;
                else if (j.Contains("sports scien")) l.Scientists++;
                else if (j.Contains("fitness")) l.Fitness++;
            }
            // whoever signs the risk assessments decides how good they are (the game: Sports Science improves "the level and
            // accuracy of the reports")
            if (!string.IsNullOrEmpty(assessor))
            {
                var a = med.FirstOrDefault(m => SameName(m.Name, assessor) && IsBand(m.Sports));
                if (a != null) { l.Sports = Band(a.Sports); l.SportsBy = a.Name; l.SportsFromAssessor = true; }
            }
            // posts the board allows but nobody fills (Club > Staff > Medical Team)
            foreach (var kv in Posts)
            {
                if (kv.Value.Filled >= kv.Value.Allowed) continue;
                var j = kv.Key.ToLowerInvariant();
                if (j.Contains("sports scien")) l.SportsShort = true;
                if (j.Contains("physio")) l.PhysioShort = true;
            }
            return l;
        }

        // How far the watch list reaches, as the lowest risk level it takes from the latest assessment:
        //   2 = also players whose risk has stayed raised (Increased on the last two assessments), 1 = high and very high,
        //   0 = very high only, -1 = no risk flags (only the medical team's recurring list and repeat injuries).
        // Unknown sports science (nobody profiled yet) keeps 0.3's reach (1).
        // 0.4.1: an empty sports science post costs one step (the game: a bigger department does "a vastly improved job" of
        // preventing injuries)
        internal static int Reach(Level l)
        {
            if (l == null || l.Sports < 0) return 1;
            int r = l.Sports >= 6 ? 2 : l.Sports >= 4 ? 1 : l.Sports >= 2 ? 0 : -1;
            if (l.SportsShort) r = Math.Max(-1, r - 1);
            return r;
        }
        internal static string ReachText(Level l)
        {
            var pre = l != null && l.SportsShort && l.Sports >= 0 ? "With a sports science post empty, the list is cut back a step. " : "";
            return pre + ReachBody(l);
        }
        static string ReachBody(Level l)
        {
            switch (Reach(l))
            {
                case 2: return "The watch list takes high and very high risk, and players whose risk has stayed raised on the last two assessments.";
                case 1: return l.Sports < 0 ? "The watch list takes high and very high risk." : "The watch list takes high and very high risk on the latest assessment.";
                case 0: return "The watch list only picks up very high risk.";
                default: return "Nobody here has the sports science to track risk closely, so the watch list keeps only repeat injuries and the medical team's recurring list.";
            }
        }

        static string Skills(Member m)
        {
            var s = new List<string>();
            if (IsBand(m.Physio)) s.Add("Physiotherapy " + m.Physio);
            if (IsBand(m.Sports)) s.Add("Sports Science " + m.Sports);
            if (IsBand(m.Motivating)) s.Add("Motivating " + m.Motivating);
            if (s.Count > 0) return string.Join(", ", s);
            return m.Physio == "-" ? "no medical ratings for this job" : "skills not read yet (open the profile, or show the Physiotherapy column in Club > Staff > Medical Team)";
        }
        internal static int JobRank(string j)
        {
            j = (j ?? "").ToLowerInvariant();
            if (j == "head physio") return 0;
            if (j.Contains("doctor")) return j.StartsWith("chief") ? 1 : 3;
            if (j.Contains("physio")) return 2;
            if (j.Contains("sports scien")) return j.StartsWith("head") ? 4 : 5;
            if (j.Contains("fitness")) return 6;
            return j.Length == 0 ? 8 : 7;
        }
        static string Plural(string job, int n)
        {
            var j = job.ToLowerInvariant();
            if (n == 1) return "1 " + j;
            return n + " " + (j.EndsWith("s") ? j : j + "s");
        }
        static string Join(List<string> l) => l.Count == 0 ? "" : l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];

        // the department, member by member (first team first, then by job)
        internal static List<(string Name, string Job, string Team, string Skills, string Personality)> Members()
            => Medical().OrderBy(m => FirstTeam(m) ? 0 : 1).ThenBy(m => JobRank(m.Job)).ThenBy(m => m.Name)
                .Select(m => (m.Name, m.Job.Length > 0 ? m.Job : "job not seen yet", m.Team, Skills(m), m.Personality)).ToList();

        // 0.6.3: the same facts as Lines, as fields, for the Department page's tiles (Nik: "bit wordy at the top")
        internal sealed class Over
        {
            public bool Met, PostsKnown, SportsSigner; public int Known, Profiled, FirstTeamCount, FirstKnown, FirstUnread;
            public List<string> Counts = new List<string>(), Empty = new List<string>(), Moves = new List<string>(), Doctors = new List<string>();
            public string PhysioBand = "", PhysioBy = "", SportsBand = "", SportsBy = "", MotivBand = "", MotivBy = "", Reach = "";
            public List<(string Area, string Rank, string Verdict, string Best, string Worst, string Date)> Division = new List<(string, string, string, string, string, string)>();
            public List<(string Date, string Text)> Advice = new List<(string, string)>();
        }
        internal static Over Overview(string assessor)
        {
            var o = new Over(); var l = Now(assessor);
            o.Known = l.Known; o.Profiled = l.Profiled; o.Met = l.Known > 0;
            if (!o.Met) return o;
            var firstTeam = Medical().Where(FirstTeam).ToList();
            // doctors: the game's Medical Team table gives them "-" in every skill column, so there is nothing to read
            o.FirstKnown = firstTeam.Count(m => !IsDoctor(m.Job)); o.FirstUnread = firstTeam.Count(m => !IsDoctor(m.Job) && m.Physio.Length == 0 && m.Sports.Length == 0);
            o.Doctors = firstTeam.Where(m => IsDoctor(m.Job)).Select(m => m.Name).ToList();
            if (Posts.Count > 0)
            {
                o.PostsKnown = true;
                foreach (var kv in Posts.Where(p => p.Value.Filled > 0).OrderBy(p => JobRank(p.Key))) { o.Counts.Add(Plural(kv.Key, kv.Value.Filled)); o.FirstTeamCount += kv.Value.Filled; }
                o.Empty = Posts.Where(p => p.Value.Filled < p.Value.Allowed).OrderBy(p => JobRank(p.Key))
                    .Select(p => (p.Value.Allowed - p.Value.Filled == 1 ? "1 " + p.Key.ToLowerInvariant() + " post" : (p.Value.Allowed - p.Value.Filled) + " " + p.Key.ToLowerInvariant() + " posts")).ToList();
            }
            else
                foreach (var g in Medical().Where(FirstTeam).Where(m => m.Job.Length > 0).GroupBy(m => m.Job).OrderBy(g => JobRank(g.Key))) { o.Counts.Add(Plural(g.Key, g.Count())); o.FirstTeamCount += g.Count(); }
            if (l.Physio >= 0) { o.PhysioBand = Scale[l.Physio]; o.PhysioBy = Join(l.PhysioTop); }
            if (l.Sports >= 0) { o.SportsBand = Scale[l.Sports]; o.SportsBy = l.SportsBy; o.SportsSigner = l.SportsFromAssessor; o.Reach = ReachText(l); }
            if (l.Motiv >= 6) { o.MotivBand = Scale[l.Motiv]; o.MotivBy = l.MotivBy; }
            foreach (var kv in Compare.OrderBy(c => c.Key == "Physiotherapy" ? 0 : c.Key == "Sports Science" ? 1 : 2).ThenBy(c => c.Key))
                o.Division.Add((kv.Key, kv.Value.Rank, kv.Value.Verdict, kv.Value.Best, kv.Value.Worst, kv.Value.Date));
            foreach (var k in new[] { "board", "short", "vacancies", "improve" })
                if (Advice.TryGetValue(k, out var a)) o.Advice.Add((a.Date, (k == "board" ? "Board feedback: " : k == "short" ? "Staff advice: " : "Staffing advice: ") + a.Text.Trim().TrimEnd('.') + "."));
            o.Moves = Staff.Values.Where(m => m.Joined.Length > 0 || m.Left.Length > 0)
                .Select(m => (Date: m.Left.Length > 0 ? m.Left : m.Joined, Text: m.Name + " (" + (m.Job.Length > 0 ? m.Job : "medical staff") + (m.Left.Length > 0 ? ") left the list on " + m.Left : ") joined on " + m.Joined)))
                .OrderByDescending(x => GameDate.Day(x.Date)).Take(5).Select(x => x.Text).ToList();
            return o;
        }
        internal static bool IsDoctor(string job) => (job ?? "").IndexOf("doctor", StringComparison.OrdinalIgnoreCase) >= 0;
        internal static List<Member> MemberList() => Medical().OrderBy(m => FirstTeam(m) ? 0 : 1).ThenBy(m => JobRank(m.Job)).ThenBy(m => m.Name).ToList();

        // what it means, in the game's own descriptions (its language file) and what has been seen
        internal static List<string> Lines(string assessor)
        {
            var o = new List<string>();
            var l = Now(assessor);
            if (l.Known == 0)
            {
                o.Add("The Treatment Room has not met the medical staff yet. Open Club > Staff > Medical Team once.");
                return o;
            }
            // who is in post, against what the board allows (Club > Staff > Medical Team), or as seen on the staff lists
            var counts = new List<string>();
            if (Posts.Count > 0)
            {
                foreach (var kv in Posts.Where(p => p.Value.Filled > 0).OrderBy(p => JobRank(p.Key))) counts.Add(Plural(kv.Key, kv.Value.Filled));
                o.Add("First team medical staff: " + Join(counts) + ".");
                var empty = Posts.Where(p => p.Value.Filled < p.Value.Allowed).OrderBy(p => JobRank(p.Key))
                    .Select(p => (p.Value.Allowed - p.Value.Filled == 1 ? "1 " + p.Key.ToLowerInvariant() + " post" : (p.Value.Allowed - p.Value.Filled) + " " + p.Key.ToLowerInvariant() + " posts") + " (" + p.Value.Filled + " of " + p.Value.Allowed + ")").ToList();
                o.Add(empty.Count == 0 ? "Every medical post the board allows is filled." : "Empty posts the board allows: " + Join(empty) + ".");
            }
            else
            {
                var byJob = Medical().Where(FirstTeam).Where(m => m.Job.Length > 0).GroupBy(m => m.Job).OrderBy(g => JobRank(g.Key)).ToList();
                foreach (var g in byJob) counts.Add(Plural(g.Key, g.Count()));
                if (counts.Count > 0) o.Add("First team medical staff seen: " + Join(counts) + ".");
            }
            if (Advice.TryGetValue("board", out var bd)) o.Add("Board feedback (" + bd.Date + "): " + bd.Text);
            if (l.Physio >= 0)
                o.Add("Treatment: the best physiotherapy here is " + Scale[l.Physio] + " (" + Join(l.PhysioTop) + "). Physiotherapy governs how well injuries are prevented, treated and rehabilitated, and how quickly players recover.");
            if (l.Sports >= 0)
                o.Add((l.SportsFromAssessor ? "Risk: the assessments are signed by " + l.SportsBy + " (Sports Science " + Scale[l.Sports] + "). "
                                            : "Risk: the best sports science here is " + Scale[l.Sports] + " (" + l.SportsBy + "). ")
                      + "Sports science governs how accurate the fitness and injury-risk reports are. " + ReachText(l));
            if (l.Motiv >= 6) o.Add("Rehab: " + l.MotivBy + " is " + Scale[l.Motiv].ToLowerInvariant() + " at motivating, which helps injured players work harder during recovery.");
            foreach (var kv in Compare.OrderBy(c => c.Key == "Physiotherapy" ? 0 : c.Key == "Sports Science" ? 1 : 2).ThenBy(c => c.Key))
            {
                var c = kv.Value;
                var t = "Against the division (" + c.Date + "): " + kv.Key.ToLowerInvariant() + " " + c.Rank + (c.Verdict.Length > 0 ? " (" + c.Verdict.ToLowerInvariant() + ")" : "") + ".";
                if (c.Best.Length > 0) t += " Most respected: " + c.Best + (c.Worst.Length > 0 ? "; room for improvement: " + c.Worst : "") + ".";
                o.Add(t);
            }
            if (Advice.TryGetValue("short", out var s)) o.Add("Staff advice on " + s.Date + ": " + s.Text);
            if (Advice.TryGetValue("vacancies", out var v)) o.Add("Staffing advice on " + v.Date + ": " + v.Text + ".");
            if (Advice.TryGetValue("improve", out var im)) o.Add("Staffing advice on " + im.Date + ": " + im.Text + ".");
            var moves = Staff.Values.Where(m => m.Joined.Length > 0 || m.Left.Length > 0)
                .Select(m => (Date: m.Left.Length > 0 ? m.Left : m.Joined, Text: m.Name + " (" + (m.Job.Length > 0 ? m.Job : "medical staff") + (m.Left.Length > 0 ? ") left the list on " + m.Left : ") joined on " + m.Joined)))
                .OrderByDescending(x => GameDate.Day(x.Date)).Take(5).Select(x => x.Text).ToList();
            if (moves.Count > 0) o.Add("Comings and goings: " + Join(moves) + ".");
            if (l.Profiled < l.Known) o.Add((l.Known - l.Profiled) + " of the " + l.Known + " have no skills read yet: open their profiles, or switch the Club > Staff > Medical Team table to a view with the Physiotherapy column, which reads them all at once.");
            return o;
        }

        // ---------- files ----------
        internal const string Header = "key\tname\tjob\tteam\tphysiotherapy\tsports science\tmotivating\tlast seen\tprofile read\tjoined\tleft\tpersonality";
        internal static IEnumerable<string> Rows(Func<string, string> c)
        {
            foreach (var m in Staff.Values.OrderBy(x => x.Name))
                yield return $"{m.Key}\t{c(m.Name)}\t{c(m.Job)}\t{c(m.Team)}\t{m.Physio}\t{m.Sports}\t{m.Motivating}\t{m.Seen}\t{m.Profiled}\t{m.Joined}\t{m.Left}\t{c(m.Personality)}";
            foreach (var kv in Advice.OrderBy(x => x.Key))
                yield return $"advice:{kv.Key}\t{c(kv.Value.Text)}\t\t\t\t\t\t{kv.Value.Date}\t";
            foreach (var kv in Posts.OrderBy(x => x.Key))
                yield return $"post:{c(kv.Key)}\t{kv.Value.Filled}\t{kv.Value.Allowed}\t\t\t\t\t{kv.Value.Date}\t";
            foreach (var kv in Compare.OrderBy(x => x.Key))
                yield return $"compare:{c(kv.Key)}\t{c(kv.Value.Rank)}\t{c(kv.Value.Verdict)}\t{c(kv.Value.Best)}\t{c(kv.Value.Worst)}\t\t\t{kv.Value.Date}\t";
        }
        internal static void From(string[] a, Func<string[], int, string> at)
        {
            var k = at(a, 0);
            if (k.Length == 0) return;
            if (k.StartsWith("advice:")) { Advice[k.Substring(7)] = (at(a, 7), at(a, 1)); return; }
            if (k.StartsWith("post:") && !MedicalJob(k.Substring(5))) return;   // 0.5.5: drops the coaching posts 0.5.4 stored by mistake
            if (k.StartsWith("post:")) { int.TryParse(at(a, 1), out var f); int.TryParse(at(a, 2), out var al); Posts[k.Substring(5)] = (f, al, at(a, 7)); return; }
            if (k.StartsWith("compare:")) { Compare[k.Substring(8)] = (at(a, 1), at(a, 2), at(a, 3), at(a, 4), at(a, 7)); return; }
            Staff[k] = new Member { Key = k, Name = at(a, 1), Job = at(a, 2), Team = at(a, 3), Physio = at(a, 4), Sports = at(a, 5), Motivating = at(a, 6), Seen = at(a, 7), Profiled = at(a, 8), Joined = at(a, 9), Left = at(a, 10), Personality = at(a, 11) };
        }
        internal static void Clear() { Staff.Clear(); Advice.Clear(); Posts.Clear(); Compare.Clear(); TableKeys = new List<string>(); TableDate = ""; }
    }
}
