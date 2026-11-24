// FM27 Media Mod - Treatment Room 0.6.3: the Department page in the new look (Nik, 24 Nov: "Bit wordy at the top? Could neaten a bit
// here without losing info"). The same facts as before (Dept.Overview gives them as fields), as tiles: the staff, treatment, risk reports,
// against the division; then what's waiting to be looked at; then the staff table with one column per skill.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class Room
    {
        static void DeptView()
        {
            var o = Dept.Overview(Notes.StaffName("risk"));
            Body.Add(L("Who's in the department and how good they are. Club › Staff › Medical Team keeps this up to date; opening a member's profile adds the personality.", 14, Text2, false, 14));
            if (!o.Met)
            {
                Body.Add(L("The Treatment Room has not met the medical staff yet.", 15, Text1, false, 4));
                Body.Add(GoTo("Club › Staff › Medical Team"));
                return;
            }

            // the tiles
            var tiles = Flex(true, true); tiles.style.alignItems = Align.Stretch; tiles.style.marginBottom = 6;
            VisualElement T(string caps)
            {
                var t = Panel(BandBg, Edge); Pad(t, 16, 16);
                t.style.flexGrow = 1; t.style.flexShrink = 1; t.style.flexBasis = new Length(22, LengthUnit.Percent); t.style.minWidth = 230;
                t.style.marginRight = 12; t.style.marginBottom = 12;
                t.Add(Caps(caps, Text2, 8));
                return t;
            }
            var t1 = T("FIRST TEAM STAFF");
            t1.Add(Disp(L(o.FirstTeamCount.ToString(), 32, Text1, true, 4)));
            if (o.Counts.Count > 0) t1.Add(L(string.Join(" · ", o.Counts), 13, Text2, false, 4));
            if (o.PostsKnown) t1.Add(L(o.Empty.Count == 0 ? "Every post the board allows is filled." : "Empty: " + string.Join(", ", o.Empty) + ".", 13, o.Empty.Count == 0 ? Text2 : Pink));
            tiles.Add(t1);

            var t2 = T("TREATMENT · PHYSIOTHERAPY");
            t2.Add(Disp(L(o.PhysioBand.Length > 0 ? o.PhysioBand : "Not read yet", 26, Text1, true, 4)));
            if (o.PhysioBy.Length > 0) t2.Add(L(o.PhysioBy, 14, Pink, false, 4));
            t2.Add(L("How well injuries are prevented, treated and rehabilitated, and how quickly players recover.", 13, Text2, false, 2));
            if (o.Doctors.Count > 0) t2.Add(L("Doctors: " + string.Join(", ", o.Doctors) + ". No skill ratings on the staff table. Doctors give the \"club's doctors\" treatment when an injury is offered it, and the board decides the posts.", 13, Text2, false, 2));
            if (o.MotivBand.Length > 0) t2.Add(L("Rehab: " + o.MotivBy + " is " + o.MotivBand.ToLowerInvariant() + " at motivating, which helps injured players work harder.", 13, Text2));
            tiles.Add(t2);

            var t3 = T("RISK REPORTS · SPORTS SCIENCE");
            t3.Add(Disp(L(o.SportsBand.Length > 0 ? o.SportsBand : "Not read yet", 26, Text1, true, 4)));
            if (o.SportsBy.Length > 0) t3.Add(L(o.SportsBy + (o.SportsSigner ? " · signs the assessments" : ""), 14, Pink, false, 4));
            t3.Add(L("How accurate the fitness and injury-risk reports are.", 13, Text2));
            tiles.Add(t3);

            if (o.Division.Count > 0)
            {
                var t4 = T("AGAINST THE DIVISION");
                var d0 = o.Division[0];
                int sp = d0.Rank.IndexOf(' ');
                var big = sp > 0 ? d0.Rank.Substring(0, sp) : d0.Rank; var rest = sp > 0 ? d0.Rank.Substring(sp + 1) : "";
                t4.Add(Disp(L(big, 26, Text1, true, 4)));
                t4.Add(L(d0.Area + (rest.Length > 0 ? ", " + rest : "") + (d0.Verdict.Length > 0 ? " · " + d0.Verdict.ToLowerInvariant() : ""), 14, Pink, false, 4));
                if (d0.Best.Length > 0) t4.Add(L("Most respected: " + d0.Best + (d0.Worst.Length > 0 ? " · room for improvement: " + d0.Worst : ""), 13, Text2, false, 2));
                foreach (var d in o.Division.Skip(1)) t4.Add(L(d.Area + ": " + d.Rank + (d.Verdict.Length > 0 ? " (" + d.Verdict.ToLowerInvariant() + ")" : ""), 13, Text2, false, 2));
                t4.Add(L("As of " + d0.Date + ".", 12.5f, Text2));
                tiles.Add(t4);
            }
            Body.Add(tiles);
            if (o.Reach.Length > 0) Body.Add(L(o.Reach, 13, Text2, false, 16));

            // what's waiting to be looked at: the staff's and the board's advice, people not read yet, comings and goings
            int unread = o.FirstUnread;   // 0.6.4: the first team's; youth staff don't feed the advice
            if (o.Advice.Count > 0 || unread > 0 || o.Moves.Count > 0)
            {
                var (card, inner) = Shell("To look at", "", 2, "");
                foreach (var a in o.Advice)
                {
                    var r = Flex(true); Pad(r, 6, 0); r.style.borderBottomWidth = 1; r.style.borderBottomColor = RowRule;
                    var c = Chip(a.Date, IncBg, Text1); c.style.marginRight = 12; r.Add(c);
                    var tx = L(a.Text, 14, Text1); tx.style.flexShrink = 1; r.Add(tx);
                    inner.Add(r);
                }
                if (unread > 0)
                {
                    var r = Flex(true, true); r.style.justifyContent = Justify.SpaceBetween; Pad(r, 8, 0); r.style.borderBottomWidth = 1; r.style.borderBottomColor = RowRule;
                    var tx = L(unread + " of the " + o.FirstKnown + " first team staff have no skills read yet: open their profiles, or show the Physiotherapy column on the Medical Team table, which reads them all at once.", 14, Text1);
                    tx.style.flexShrink = 1; tx.style.flexGrow = 1; tx.style.marginRight = 12; r.Add(tx);
                    r.Add(GoTo("Club › Staff › Medical Team"));
                    inner.Add(r);
                }
                if (o.Moves.Count > 0) inner.Add(L("Comings and goings: " + string.Join("; ", o.Moves) + ".", 13, Text2, false, 0));
                Body.Add(card);
            }

            // the staff, one column per skill; 0.6.4: filters by squad and role (Nik: "filterable by squad and role")
            var all = Dept.MemberList();
            if (all.Count == 0) return;
            var squads = new List<(string Key, string Label)> { ("all", "All squads"), ("first", "First team") };
            foreach (var t in all.Where(m => !Dept.FirstTeam(m)).Select(m => m.Team).Distinct().OrderBy(t => t)) squads.Add((t, t));
            var roles = new List<(string Key, string Label)> { ("all", "All roles") };
            foreach (var r in new[] { "Physios", "Sports science", "Doctors", "Fitness", "Other" }) if (all.Any(m => RoleOf(m.Job) == r)) roles.Add((r, r));
            if (!squads.Any(x => x.Key == StaffSquad)) StaffSquad = "all";
            if (!roles.Any(x => x.Key == StaffRole)) StaffRole = "all";
            var mem = all.Where(m => (StaffSquad == "all" || (StaffSquad == "first" ? Dept.FirstTeam(m) : !Dept.FirstTeam(m) && m.Team == StaffSquad))
                                  && (StaffRole == "all" || RoleOf(m.Job) == StaffRole)).ToList();
            int ft = all.Count(Dept.FirstTeam);
            var (sc, si) = Shell("Staff", ft + " first team" + (all.Count > ft ? " · " + (all.Count - ft) + " other teams" : ""), 2, "As their profiles show them");
            var fr = Flex(true, true); fr.style.marginBottom = 10;
            foreach (var (k, lab) in squads) { var kk = k; fr.Add(Pill(lab, () => { StaffSquad = kk; Refresh(); }, StaffSquad == kk)); }
            var gap = new VisualElement(); gap.style.width = 18; fr.Add(gap);
            foreach (var (k, lab) in roles) { var kk = k; fr.Add(Pill(lab, () => { StaffRole = kk; Refresh(); }, StaffRole == kk)); }
            si.Add(fr);
            if (mem.Count == 0) si.Add(L("Nobody in that squad and role.", 14, Text2, false, 6));
            var grow = new[] { 1.7f, 1.6f, 1.0f, 1.3f, 1.1f, 1.1f, 1.0f };
            var head = Flex(true); head.style.paddingBottom = 6; head.style.borderBottomWidth = 1; head.style.borderBottomColor = RowRule;
            var heads = new[] { "NAME", "JOB", "TEAM", "PERSONALITY", "PHYSIOTHERAPY", "SPORTS SCIENCE", "MOTIVATING" };
            for (int i = 0; i < heads.Length; i++) head.Add(Cell(Caps(heads[i], Text2, 0), grow[i]));
            si.Add(head);
            bool otherShown = false;
            foreach (var m in mem)
            {
                if (!otherShown && !Dept.FirstTeam(m))
                {
                    otherShown = true;
                    var oh = Caps("OTHER TEAMS", Text2, 0); oh.style.marginTop = 14; oh.style.paddingBottom = 6; oh.style.borderBottomWidth = 1; oh.style.borderBottomColor = RowRule;
                    si.Add(oh);
                }
                var row = Flex(true); Pad(row, 8, 0); row.style.borderBottomWidth = 1; row.style.borderBottomColor = RowRule;
                row.Add(Cell(NameCell(m.Key, m.Name), grow[0]));
                row.Add(Cell(L(m.Job.Length > 0 ? m.Job : "not seen yet", 13, m.Job.Length > 0 ? Text1 : Text2), grow[1]));
                row.Add(Cell(L(m.Team.Length > 0 ? m.Team : "-", 13, Text2), grow[2]));
                row.Add(Cell(L(m.Personality.Length > 0 ? m.Personality : "-", 13, m.Personality.Length > 0 ? Text1 : Text2), grow[3]));
                bool read = m.Physio.Length > 0 || m.Sports.Length > 0 || m.Motivating.Length > 0 || Dept.IsDoctor(m.Job);   // doctors: "-" as the game shows
                row.Add(Cell(BandL(m.Physio, read), grow[4]));
                row.Add(Cell(BandL(m.Sports, read), grow[5]));
                row.Add(Cell(BandL(m.Motivating, read), grow[6]));
                si.Add(row);
            }
            if (LinksDrawn > 0) { TreatmentRoom.W("room", "name links", LinksDrawn + " of " + mem.Count + " names drawn as the game's links"); LinksDrawn = 0; }
            si.Add(L("A dash under personality: open the member's profile once. \"Not read\": the skills haven't been on screen yet. Doctors carry no skill ratings on the staff table.", 12.5f, Text2, false, 0));
            Body.Add(sc);
        }
        static string StaffSquad = "all", StaffRole = "all";
        // 0.6.4 (Nik: "names in list could hotlink to that person's profile page"): the name as the game's own text element with its
        // own link (the record seen on the game's staff list), so a click is the game's own link click. A plain name if anything fails.
        static bool LinkFailLogged; static int LinksDrawn;
        // 0.6.5: plain names again. Probes 0.0.12-0.0.14 (24 Nov): the click reaches the game's person handler, but the profile never
        // opens, even calling its OpenPersonPanel with the person and the screen's own view key. A name that looks clickable and isn't
        // is worse than a plain one. The link code stays, off, for when the missing step is found.
        internal static bool NameLinks = false;
        static VisualElement NameCell(string key, string name)
        {
            if (NameLinks && !string.IsNullOrEmpty(key) && PersonLinks.Records.TryGetValue(key, out var rec))
            {
                try
                {
                    var t = new SI.Bindable.SIText();
                    t.name = "FM27MM-NameLink";
                    t.EnableEmbeddedLinks = true;
                    t.style.fontSize = 14; t.style.color = Text1; t.style.unityFontStyleAndWeight = FontStyle.Bold;
                    t.style.whiteSpace = WhiteSpace.Normal; t.style.marginTop = 0; t.style.marginBottom = 0; t.style.paddingTop = 0; t.style.paddingBottom = 0;
                    t.SetText("\u0001" + rec + "\u0002" + name + "\u0001");
                    LinksDrawn++;
                    return t;
                }
                catch (Exception e) { if (!LinkFailLogged) { LinkFailLogged = true; TreatmentRoom.Fail("name link", e); } }
            }
            return L(name, 14, Text1, true);
        }
        // the role groups for the filter, from the job the staff list shows
        static string RoleOf(string job)
        {
            var j = (job ?? "").ToLowerInvariant();
            if (j.Contains("physio")) return "Physios";
            if (j.Contains("sports scien")) return "Sports science";
            if (j.Contains("doctor")) return "Doctors";
            if (j.Contains("fitness")) return "Fitness";
            return "Other";
        }
        // a skill band: the top two stand out
        static Label BandL(string band, bool read)
        {
            if (band.Length == 0) return L(read ? "-" : "not read", 13, Text2);
            bool top = band == "Elite" || band == "Outstanding";
            return L(band, 13, top ? Pink : Text1, top);
        }
    }
}
