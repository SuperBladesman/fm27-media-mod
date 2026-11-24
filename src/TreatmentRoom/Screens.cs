// FM27 Media Mod - Treatment Room: reading the medical screens (read-only).
// Every text on the Medical Centre screens and the player's medical pages reaches SIText (PROVEN 22 Nov, Probe 0.0.10).
// A text inside one of the tables below schedules a scan of that table 0.6 s later; the scan reads every row
// (the children of each innermost element named "View", as in Tabloid's supporter tables), and each row is parsed
// by Parse.Row. Person ids come from the raw text the hook saw for that element (link records), since the element's
// own text holds only the styled name.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Screens
    {
        // table root element names (from the probe, 22 Nov) -> kind
        static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "MCSeasonSummaryTable", "season" },                 // Season Summary's list: our club, this season
            { "InjuryHistory", "history" },                       // a player's history page (owner = the profile)
            { "CometitionStatsInjuryTableCard", "league" },       // every club's injured players, with expected return (SI's spelling)
            { "MedicalCentreCurrentInjuriesReport", "current" },  // our injured players now
            { "MedicalCentreRecentlyReturnedBlock", "returned" }, // back from injury in the last 2 weeks
            { "MedicalCentrePlayerSummaryBlock", "squad" },       // our whole squad (who is ours)
            { "MedicalCentreRiskAssessmentBlock", "risk" },       // the risk tabs
        };

        static bool HeaderMissLogged;
        static readonly Dictionary<IntPtr, string> Raw = new Dictionary<IntPtr, string>();   // element -> last raw text with link records
        internal static string OwnerId = "", OwnerName = ""; internal static DateTime OwnerAt = DateTime.MinValue;
        internal static string LastPerson = ""; internal static DateTime LastPersonAt = DateTime.MinValue;
        static readonly Regex Tag = new Regex("<[^>]+>", RegexOptions.Compiled);
        static readonly Regex OfferRx = new Regex(@"see a specialist|protective equipment|pain management|see a doctor", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex StaffRx = new Regex(@"^(.+?) will provide .+? with a copy of \S+ latest assessment of (.+)$", RegexOptions.Compiled);
        // a name as the inbox writes it ("Tanganga", "Japhet Tanganga") -> the person id, if exactly one player fits
        internal static string WhoByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var hits = new HashSet<string>();
            foreach (var kv in Ledger.Squad) { var n = kv.Value.Name; if (n == name || n.EndsWith(" " + name, StringComparison.Ordinal)) hits.Add(kv.Key); }
            if (hits.Count == 0) foreach (var i in Ledger.Injuries.Values) { var n = i.Person; if (!i.Who.StartsWith("name:") && (n == name || n.EndsWith(" " + name, StringComparison.Ordinal))) hits.Add(i.Who); }
            return hits.Count == 1 ? hits.First() : "";
        }
        internal static string Strip(string s) => Tag.Replace(PersonLinks.Plain(s ?? "") ?? "", "").Trim();

        internal static void OnText(SI.Bindable.SIText el, string txt)
        {
            try
            {
                if (!TreatmentRoom.Enabled.Value || string.IsNullOrEmpty(txt)) return;
                TrMemory.Pump();
                Room.EnsureTab(el);   // 0.3.0: the MEDICAL tab (checks the clock first, so it costs nothing most of the time)
                IntPtr ptr = IntPtr.Zero; try { ptr = el.Pointer; } catch { }
                bool link = txt.IndexOf('\u0001') >= 0;
                if (link && ptr != IntPtr.Zero) { if (Raw.Count > 20000) Raw.Clear(); Raw[ptr] = txt; }
                var nm = el.name ?? "";
                if (nm.StartsWith("FM27MM-", StringComparison.Ordinal)) return;   // 0.6.4: our own elements (the name links)

                // the in-game date in the top bar
                if (nm == "Month" && txt.Length <= 10) { var p = el.parent; if (p != null && p.name == "Day") GameDate.Set(txt); }

                // whose profile is open (its PlayerName carries the person link)
                if (link && nm == "PlayerName")
                    foreach (var l in PersonLinks.All(txt)) if (l.IsPerson) { OwnerId = l.Key; OwnerName = l.Visible; OwnerAt = DateTime.Now; StaffPersonality(el, l.Key, l.Visible); break; }

                // Season Summary's card header ("39 injuries this season"): place the Treatment Room panel in the space below it (0.2.0)
                if (txt.Length < 400 && txt.IndexOf("this season", StringComparison.OrdinalIgnoreCase) >= 0 && Panel.HeaderRx.IsMatch(Strip(txt)))
                {
                    // 0.2.1: SI's unnamed wrappers put "Inuries" more than 10 levels up (0.2.0 looked 10 and never fired)
                    VisualElement a = el; int depth = -1;
                    for (int i = 0; i < 40 && a != null; i++, a = a.parent) if ((a.name ?? "") == "Inuries") { depth = i; break; }
                    if (depth >= 0) Panel.OnHeader(el, depth);
                    else TreatmentRoom.W("panel", "header seen", "but no Inuries above it within 40 levels: " + Strip(txt));
                }
                else if (!HeaderMissLogged && txt.IndexOf("injuries this season", StringComparison.OrdinalIgnoreCase) >= 0 && txt.IndexOf("player", StringComparison.OrdinalIgnoreCase) < 0)
                { HeaderMissLogged = true; TreatmentRoom.W("panel", "header text did not match", txt.Length + " chars: " + Strip(txt)); }

                // 0.3.0: the staff the game names for medical reports ("Will Tullett will provide you with a copy of his latest
                // assessment of the Sheffield United players at risk of injury.") and the medical team's rest advice ("Suggest Resting Tanganga")
                if (txt.Length > 30 && txt.IndexOf("latest assessment", StringComparison.Ordinal) >= 0)
                {
                    var m = StaffRx.Match(Strip(txt));
                    if (m.Success && GameDate.Text.Length > 0)
                    {
                        var role = m.Groups[2].Value.IndexOf("risk of injury", StringComparison.OrdinalIgnoreCase) >= 0 ? "risk" : "fitness";
                        if (Notes.Staff(role, m.Groups[1].Value.Trim(), GameDate.Text)) { TreatmentRoom.W("staff", role, m.Groups[1].Value.Trim()); Ledger.Save(); }
                    }
                }
                if (txt.Length < 120 && txt.StartsWith("Suggest Resting ", StringComparison.Ordinal) && GameDate.Text.Length > 0 && Strip(txt).Length > 16)
                {
                    string who = "", name = Strip(txt).Substring(16).Trim();
                    if (link) foreach (var l in PersonLinks.All(txt)) if (l.IsPerson) { who = l.Key; name = l.Visible; break; }
                    if (who.Length == 0) who = WhoByName(name);
                    if (who.Length > 0 && Notes.Advice(who, Files.Name(who), "rest", GameDate.Text)) { TreatmentRoom.W("advice", "rest suggested", name + " [" + who + "]"); Ledger.Save(); }
                    else if (who.Length == 0) TreatmentRoom.W("advice", "rest suggested for someone not in the ledger", name);
                }

                // SI's own treatment offers (specialist, protective equipment) in the medical team's inbox message
                // 0.4.0: the medical department (staff profiles, staff lists, the staff's own advice about the department)
                if (nm == "JobName" && txt.Length <= 40) MedicalPosts(el);   // 0.4.1: "4 / 5 Physio" (Club > Staff > Medical Team)
                else if (nm == "attribute-value" && txt.Length <= 14 && Dept.IsBand(txt)) StaffProfile(el);
                else if (txt.Length <= 40 && nm != "navigation-tab-title-text" && Dept.MedicalJob(Strip(txt))) StaffList(el);
                if (txt.Length <= 40 && txt.StartsWith("Medical Team ", StringComparison.Ordinal) && GameDate.Text.Length > 0)
                {
                    var am = Dept.TeamAdviceRx.Match(Strip(txt));
                    if (am.Success)
                    {
                        var said = "medical team " + am.Groups[1].Value.ToLowerInvariant() + " (" + am.Groups[2].Value + ")";
                        if (Dept.Said(am.Groups[1].Value == "Vacancies" ? "vacancies" : "improve", said, GameDate.Text)) { TreatmentRoom.W("department", "staffing advice", said); Ledger.Save(); }
                    }
                }
                if (txt.Length > 30 && txt.Length < 600 && GameDate.Text.Length > 0 && Dept.ShortRx.IsMatch(txt))
                {
                    var line = Strip(txt);
                    if (Dept.Said("short", line, GameDate.Text)) { TreatmentRoom.W("department", "staff advice", line); Ledger.Save(); }
                }
                if (TreatmentRoom.MapStaff.Value && nm == "navigation-tab-title-text" && txt == "Medical Team") MapStart(el);
                if (txt.Length > 6 && txt.Length <= 80 && char.IsDigit(txt[0]) && Dept.RankRx.IsMatch(Strip(txt))) MedicalCompare(el);   // 0.4.1

                if (txt.Length > 40 && OfferRx.IsMatch(txt)) Ledger.Offer(Strip(txt), link ? txt : "");

                // a text inside one of the medical tables: scan that table shortly
                VisualElement e = el; string kind = null; VisualElement root = null;
                for (int i = 0; i < 40 && e != null; i++, e = e.parent)
                {
                    var n = e.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n == "MenuBody") break;
                    if (Kinds.TryGetValue(n, out var k)) { kind = k; root = e; }   // keep going: the outermost match wins
                }
                if (kind == null) return;
                if (link) foreach (var l in PersonLinks.All(txt)) if (l.IsPerson) { LastPerson = l.Visible + " [" + l.Key + "]"; LastPersonAt = DateTime.Now; break; }
                Schedule(root, kind);
            }
            catch (Exception ex) { TreatmentRoom.Fail("text", ex); }
        }

        // ---------- scanning a table ----------
        static readonly HashSet<string> Pending = new HashSet<string>();
        static void Schedule(VisualElement root, string kind)
        {
            if (Pending.Contains(kind)) return;
            Pending.Add(kind);
            var keep = root; var k = kind;
            System.Action run = () => { Pending.Remove(k); try { Scan(keep, k); } catch (Exception ex) { TreatmentRoom.Fail("scan " + k, ex); } };
            try { keep.schedule.Execute(run).ExecuteLater(600); } catch (Exception ex) { Pending.Remove(k); TreatmentRoom.Fail("schedule " + k, ex); }
        }

        static int Visited;
        static void Scan(VisualElement root, string kind)
        {
            // a history page belongs to the player named in the title bar at the moment the rows are read (0.1.1);
            // the last profile name seen is the fallback
            string ownId = "", ownName = "", ownFrom = "";
            if (kind == "history")
            {
                if (TitleOwner(root, out ownId, out ownName)) ownFrom = "title bar";
                else if ((DateTime.Now - OwnerAt).TotalMinutes <= 10) { ownId = OwnerId; ownName = OwnerName; ownFrom = "last profile"; }
                else { TreatmentRoom.W("scan history", "skipped", "no profile owner known"); return; }
            }
            var views = new List<VisualElement>(); Visited = 0;
            FindAll(root, "View", views, 0);
            int rows = 0, stored = 0;
            foreach (var v in views)
            {
                if (HasNamedBelow(v, "View", 0)) continue;          // only the innermost lists hold rows
                string club = kind == "league" ? ClubOf(v) : "";
                int n = 0; try { n = v.childCount; } catch { }
                for (int i = 0; i < n; i++)
                {
                    VisualElement r = null; try { r = v.ElementAt(i); } catch { }
                    if (r == null) continue;
                    var cells = new List<Cell>(); Visited = 0;
                    Collect(r, cells, 0, false);
                    if (cells.Count == 0) continue;
                    rows++;
                    var row = Parse.Row(cells, kind); row.Kind = kind; row.Club = club;
                    if (kind == "history") { row.PersonId = ownId; row.Person = ownName; }
                    if (TreatmentRoom.LogRows.Value) TreatmentRoom.W("row " + kind, row.Describe(), string.Join(" | ", cells.ConvertAll(c => c.Text)));
                    if (Ledger.Take(row)) stored++;
                }
            }
            TreatmentRoom.W("scan " + kind, rows + " rows", stored + " new or changed" + (ownFrom.Length > 0 ? " | owner " + ownName + " [" + ownId + "] from the " + ownFrom : ""));
            if (stored > 0) { Ledger.Save(); if (kind == "season" || kind == "risk") Panel.Refresh(); }
        }

        // ---------- 0.4.0: the medical department ----------
        // a staff profile (22 Nov probe): ...medical-attributes-visible/medical-attributes-list/View/container/attribute-name|attribute-value,
        // Motivating in .../staff-attributes/attributes-container/right-side/View/container; the owner on the title bar
        static void StaffProfile(SI.Bindable.SIText el)
        {
            VisualElement e = el, list = null;
            for (int i = 0; i < 12 && e != null; i++, e = e.parent) if ((e.name ?? "") == "medical-attributes-list") { list = e; break; }
            if (list == null || Pending.Contains("staff profile")) return;
            Pending.Add("staff profile");
            var keep = list;
            System.Action run = () => { Pending.Remove("staff profile"); try { ReadProfile(keep); } catch (Exception ex) { TreatmentRoom.Fail("staff profile", ex); } };
            try { keep.schedule.Execute(run).ExecuteLater(400); } catch (Exception ex) { Pending.Remove("staff profile"); TreatmentRoom.Fail("schedule staff profile", ex); }
        }
        // 0.6.3: a staff profile can open on any attributes tab (Tivey's opens on Coaching), but the personality sits in the field on the
        // right on every tab. So the title bar of a profile the department knows starts a read of that field, whichever tab shows.
        static readonly Dictionary<string, DateTime> PersRead = new Dictionary<string, DateTime>();
        static void StaffPersonality(VisualElement from, string id, string name)
        {
            if (!Dept.Knows(id) || Ledger.Squad.ContainsKey(id)) return;
            if (PersRead.TryGetValue(id, out var at) && (DateTime.Now - at).TotalSeconds < 20) return;
            PersRead[id] = DateTime.Now;
            VisualElement top = from;
            for (int i = 0; i < 80 && top != null && top.parent != null; i++) { if ((top.name ?? "") == "MainUIDocument-container") break; top = top.parent; }
            var keep = top;
            void Try(int attempt)
            {
                try
                {
                    var pers = FindPersonality(keep);
                    if (pers.Length == 0)
                    {
                        if (attempt == 1) { System.Action again = () => Try(2); try { keep.schedule.Execute(again).ExecuteLater(1500); } catch { } }
                        else TreatmentRoom.W("department", "personality not found", name + " [" + id + "]");
                        return;
                    }
                    if (Dept.SetPersonality(id, pers, GameDate.Text)) { TreatmentRoom.W("department", "personality", name + " [" + id + "] " + pers + " (profile, any tab)"); Ledger.Save(); }
                    else TreatmentRoom.W("department", "personality unchanged", name + " [" + id + "] " + pers);
                }
                catch (Exception ex) { TreatmentRoom.Fail("staff personality", ex); }
            }
            System.Action first = () => Try(1);
            try { keep.schedule.Execute(first).ExecuteLater(700); } catch (Exception ex) { TreatmentRoom.Fail("schedule staff personality", ex); }
        }
        // the profile's personality field: .../staff-info/.../personality/value
        static string FindPersonality(VisualElement top)
        {
            var found = new List<VisualElement>(); Visited = 0;
            FindAll(top, "personality", found, 0);
            foreach (var pe in found)
            {
                bool staff = false; VisualElement a = pe;
                for (int i = 0; i < 6 && a != null; i++, a = a.parent) if ((a.name ?? "") == "staff-info") { staff = true; break; }
                if (!staff) continue;
                var v = Visible(pe);
                if (v.Length > 0) return v;
            }
            // a safety net: any personality field on show (the probe only ever saw staff-info/personality/value)
            foreach (var pe in found) { var v = Visible(pe); if (v.Length > 0) return v; }
            return "";
        }
        static string Visible(VisualElement pe)
        {
            try { if (pe.resolvedStyle.display == DisplayStyle.None || pe.worldBound.height < 1f) return ""; } catch { }
            var v = TextOf(Shallow(pe, "value", 0, 4));
            return v.Length > 0 && v.Length <= 40 ? v : "";
        }
        static void ReadProfile(VisualElement list)
        {
            // only when the medical attributes are on show (a coach's profile may carry the list hidden)
            VisualElement v = list;
            for (int i = 0; i < 6 && v != null && (v.name ?? "") != "medical-attributes-visible"; i++) v = v.parent;
            if (v != null)
            {
                bool hidden = false;
                try { hidden = v.resolvedStyle.display == DisplayStyle.None || v.worldBound.height < 1f; } catch { }
                if (hidden) { TreatmentRoom.W("department", "profile skipped", "medical attributes not on show"); return; }
            }
            string id, name, from = "title bar";
            if (!TitleOwner(list, out id, out name))
            {
                if ((DateTime.Now - OwnerAt).TotalSeconds > 60) { TreatmentRoom.W("department", "profile skipped", "no owner on the title bar"); return; }
                id = OwnerId; name = OwnerName; from = "last profile";
            }
            if (Ledger.Squad.ContainsKey(id)) { TreatmentRoom.W("department", "profile skipped", name + " [" + id + "] is a player"); return; }   // a player, never staff
            var pairs = Pairs(list);
            VisualElement c = list;
            for (int i = 0; i < 12 && c != null && (c.name ?? "") != "attributes-container"; i++) c = c.parent;
            if (c != null) foreach (var kv in Pairs(c)) if (!pairs.ContainsKey(kv.Key)) pairs[kv.Key] = kv.Value;
            pairs.TryGetValue("Physiotherapy", out var ph); pairs.TryGetValue("Sports Science", out var sp); pairs.TryGetValue("Motivating", out var mo);
            // 0.5.5: the personality, from the same profile (staff-info/personality/value)
            string pers = "";
            VisualElement mb = list;
            for (int i = 0; i < 16 && mb != null && (mb.name ?? "") != "main-block"; i++) mb = mb.parent;
            if (mb != null && (mb.name ?? "") == "main-block") { var pe = Shallow(mb, "personality", 0, 6); if (pe != null) pers = TextOf(Shallow(pe, "value", 0, 4)); }
            if (Dept.Profile(id, name, ph ?? "", sp ?? "", mo ?? "", GameDate.Text, pers))
            {
                TreatmentRoom.W("department", "profile", $"{name} [{id}] Physiotherapy {ph}, Sports Science {sp}, Motivating {mo}, personality {pers} ({from})");
                Ledger.Save();
            }
            else TreatmentRoom.W("department", "profile unchanged", $"{name} [{id}] Physiotherapy {ph}, Sports Science {sp}, Motivating {mo}, personality {(pers.Length > 0 ? pers : "not found")} ({from}, {pairs.Count} values on screen)");
        }
        static Dictionary<string, string> Pairs(VisualElement root)
        {
            var d = new Dictionary<string, string>();
            var rows = new List<VisualElement>(); Visited = 0;
            FindAll(root, "container", rows, 0);
            foreach (var r in rows)
            {
                string n = TextOf(Shallow(r, "attribute-name", 0, 4)), val = TextOf(Shallow(r, "attribute-value", 0, 4));
                if (n.Length > 0 && val.Length > 0 && !d.ContainsKey(n)) d[n] = val;
            }
            return d;
        }
        static string TextOf(VisualElement e)
        {
            if (e == null) return "";
            var te = e.TryCast<TextElement>(); string t = null;
            if (te != null) try { t = te.text; } catch { }
            return Strip(t);
        }

        // a staff list (17 Nov probe): .../ClubScreenOverviewBlock/View/<row>: the name (a person link), the team, the job
        static void StaffList(SI.Bindable.SIText el)
        {
            VisualElement e = el, view = null;
            // 0.5.6: the Medical Team tab's table (StaffContractsTable, with the Club Job column) counts as well as the Overview
            // block; 0.5.5 accepted only the Overview block, so the jobs were never read ("job not seen yet", 23 Nov)
            for (int i = 0; i < 24 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                if (n == "MenuBody") return;
                if (n == "View" && view == null) view = e;
                if (n == "ClubScreenOverviewBlock" || n == "StaffContractsTable") break;
            }
            var card = e == null ? "" : (e.name ?? "");
            if ((card != "ClubScreenOverviewBlock" && card != "StaffContractsTable") || view == null || Pending.Contains("staff list")) return;
            Pending.Add("staff list");
            var keep = view;
            System.Action run = () => { Pending.Remove("staff list"); try { ReadList(keep); } catch (Exception ex) { TreatmentRoom.Fail("staff list", ex); } };
            try { keep.schedule.Execute(run).ExecuteLater(600); } catch (Exception ex) { Pending.Remove("staff list"); TreatmentRoom.Fail("schedule staff list", ex); }
        }
        static void ReadList(VisualElement view)
        {
            int n = 0; try { n = view.childCount; } catch { }
            int rows = 0, med = 0, ch = 0, off = 0; var keys = new List<string>();
            // 0.4.1: the table's column headings (one TIFO-TableHeaderTemplate per column) say which cell is which skill
            var heads = Headings(view);
            int jobAt = heads.IndexOf("Club Job");
            for (int i = 0; i < n; i++)
            {
                VisualElement r = null; try { r = view.ElementAt(i); } catch { }
                if (r == null) continue;
                var cells = new List<Cell>(); Visited = 0;
                Collect(r, cells, 0, false);
                if (cells.Count == 0) continue;
                rows++;
                var person = cells.FirstOrDefault(x => x.PersonId.Length > 0);
                var job = cells.FirstOrDefault(x => Dept.MedicalJob(x.Text));
                if (TreatmentRoom.MapStaff.Value && rows <= 60) TreatmentRoom.W("row staff", job != null ? "medical" : "-", string.Join(" | ", cells.ConvertAll(x => x.Text + (x.PersonId.Length > 0 ? " [" + x.PersonId + "]" : ""))));
                if (person == null || job == null || Ledger.Squad.ContainsKey(person.PersonId)) continue;
                var team = cells.FirstOrDefault(x => Dept.IsTeam(x.Text));
                med++;
                string ph = "", sp = "", mo = "";
                int ji = cells.IndexOf(job);
                if (jobAt >= 0)
                {
                    if (cells.Count - ji != heads.Count - jobAt) off++;
                    else for (int c = 1; ji + c < cells.Count; c++)
                    {
                        var h = heads[jobAt + c]; var v = cells[ji + c].Text;
                        if (h == "Phy" || h == "Physiotherapy") ph = v; else if (h == "SpS" || h == "Sports Science") sp = v; else if (h == "Mot" || h == "Motivating") mo = v;
                    }
                }
                if (Dept.TableRow(person.PersonId, person.PersonName.Length > 0 ? person.PersonName : person.Text, job.Text, team != null ? team.Text : "", ph, sp, mo, GameDate.Text)) ch++;
                keys.Add(person.PersonId);
            }
            TreatmentRoom.W("department", "staff list", $"{rows} rows, {med} medical staff, {ch} new or changed" + (heads.Count > 0 ? " | columns " + string.Join(", ", heads) : "") + (off > 0 ? $" | {off} rows did not line up with the columns" : ""));
            // 0.5.0: the Medical Team table (it has a Club Job column) is the whole department: compare it with the last one
            if (jobAt >= 0 && GameDate.Text.Length > 0) { Dept.Table(keys, GameDate.Text); if (Roster()) ch++; }
            if (ch > 0) Ledger.Save();
        }
        static bool Roster()
        {
            var (j, l) = Dept.TryRoster();
            foreach (var m in j) TreatmentRoom.W("department", "joined", m.Name + " (" + m.Job + ")");
            foreach (var m in l) TreatmentRoom.W("department", "left", m.Name + " (" + m.Job + ")");
            return j.Count + l.Count > 0;
        }

        static List<string> Headings(VisualElement view)
        {
            var heads = new List<string>();
            VisualElement t = view;
            for (int i = 0; i < 8 && t != null && (t.name ?? "") != "StaffContractsTable"; i++) t = t.parent;
            if (t == null) return heads;
            var cols = new List<VisualElement>(); Visited = 0;
            FindAll(t, "TIFO-TableHeaderTemplate", cols, 0);
            foreach (var c in cols) { var x = AllTexts(c, 4); heads.Add(x.Count > 0 ? x[0] : ""); }
            return heads;
        }
        static List<string> AllTexts(VisualElement e, int max)
        {
            var acc = new List<string>(); Visited = 0;
            AllTexts(e, acc, 0, max);
            return acc;
        }
        static void AllTexts(VisualElement e, List<string> acc, int depth, int max)
        {
            if (e == null || depth > 30 || acc.Count >= max || ++Visited > 4000) return;
            var t = TextOf(e);
            if (t.Length > 0) acc.Add(t);
            int c = 0; try { c = e.childCount; } catch { }
            for (int i = 0; i < c && acc.Count < max; i++) { VisualElement k = null; try { k = e.ElementAt(i); } catch { } AllTexts(k, acc, depth + 1, max); }
        }

        // 0.4.1: Club > Staff > Medical Team's posts list (.../ClubMedicalStaff/.../StreamedTable_JobsList/View/<row>: CurrentCount,
        // text-second, JobName) and the board's feedback under it (SIVisible_IsClub)
        static void MedicalPosts(SI.Bindable.SIText el)
        {
            VisualElement e = el;
            for (int i = 0; i < 12 && e != null && (e.name ?? "") != "ClubMedicalStaff"; i++) e = e.parent;
            if (e == null || (e.name ?? "") != "ClubMedicalStaff" || Pending.Contains("posts")) return;   // 0.5.5: found, not just 12 levels up
            Pending.Add("posts");
            var keep = e;
            System.Action run = () => { Pending.Remove("posts"); try { ReadPosts(keep); } catch (Exception ex) { TreatmentRoom.Fail("posts", ex); } };
            try { keep.schedule.Execute(run).ExecuteLater(600); } catch (Exception ex) { Pending.Remove("posts"); TreatmentRoom.Fail("schedule posts", ex); }
        }
        static void ReadPosts(VisualElement block)
        {
            int n = 0, ch = 0; var seen = new List<string>();
            var lists = new List<VisualElement>(); Visited = 0;
            FindAll(block, "StreamedTable_JobsList", lists, 0);
            foreach (var jl in lists)
            {
                var view = Shallow(jl, "View", 0, 4); if (view == null) continue;
                int rc = 0; try { rc = view.childCount; } catch { }
                for (int i = 0; i < rc; i++)
                {
                    VisualElement r = null; try { r = view.ElementAt(i); } catch { }
                    if (r == null) continue;
                    string job = TextOf(Shallow(r, "JobName", 0, 8)), cur = TextOf(Shallow(r, "CurrentCount", 0, 10)), al = TextOf(Shallow(r, "text-second", 0, 10));
                    if (job.Length == 0 || !int.TryParse(cur, out var f) || !int.TryParse(al, out var a)) continue;
                    n++; seen.Add(job + " " + f + "/" + a);
                    if (Dept.SetPost(job, f, a, GameDate.Text)) ch++;
                }
            }
            var fb = new List<VisualElement>(); Visited = 0;
            FindAll(block, "SIVisible_IsClub", fb, 0);
            foreach (var f in fb)
                foreach (var t in AllTexts(f, 6))
                    if (t.StartsWith("The board", StringComparison.Ordinal) && t.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0 && Dept.Said("board", t, GameDate.Text)) { ch++; TreatmentRoom.W("department", "board feedback", t); }
            if (n > 0 && Roster()) ch++;   // 0.5.0: the table may have been read first
            TreatmentRoom.W("department", "posts", $"{n} jobs ({string.Join(", ", seen)}), {ch} new or changed");
            if (ch > 0) Ledger.Save();
        }

        // 0.4.1: Medical Team Comparison (.../ClubMedicalStaffComparison/.../AttributeDropdown and HasData): the skill, the rank in
        // the division, the verdict, the most respected club and the club with most room for improvement
        static void MedicalCompare(SI.Bindable.SIText el)
        {
            VisualElement e = el;
            for (int i = 0; i < 16 && e != null && (e.name ?? "") != "ClubMedicalStaffComparison"; i++) e = e.parent;
            if (e == null || (e.name ?? "") != "ClubMedicalStaffComparison" || Pending.Contains("compare")) return;
            Pending.Add("compare");
            var keep = e;
            System.Action run = () => { Pending.Remove("compare"); try { ReadCompare(keep); } catch (Exception ex) { TreatmentRoom.Fail("compare", ex); } };
            try { keep.schedule.Execute(run).ExecuteLater(400); } catch (Exception ex) { Pending.Remove("compare"); TreatmentRoom.Fail("schedule compare", ex); }
        }
        static void ReadCompare(VisualElement block)
        {
            var dd = Shallow(block, "AttributeDropdown", 0, 14);
            var skill = dd != null ? AllTexts(dd, 1).FirstOrDefault() ?? "" : "";
            var has = Shallow(block, "HasData", 0, 14);
            var t = has != null ? AllTexts(has, 20) : new List<string>();
            int ri = t.FindIndex(x => Dept.RankRx.IsMatch(x));
            if (ri < 0) { TreatmentRoom.W("department", "ranking not read", string.Join(" | ", t)); return; }
            if (skill.Length == 0 && ri > 0) skill = t[ri - 1];
            int mi = t.IndexOf("Most respected"), wi = t.IndexOf("Room for improvement");
            string verdict = ri + 1 < t.Count && ri + 1 != mi && ri + 1 != wi ? t[ri + 1] : "";
            string best = mi >= 0 && mi + 1 < t.Count && mi + 1 != wi ? t[mi + 1] : "", worst = wi >= 0 && wi + 1 < t.Count ? t[wi + 1] : "";
            if (Dept.SetCompare(skill, t[ri], verdict, best, worst, GameDate.Text))
            {
                TreatmentRoom.W("department", "ranking", $"{skill}: {t[ri]} ({verdict}); most respected {best}; room for improvement {worst}");
                Ledger.Save();
            }
        }

        // mapping Club > Staff > Medical Team (0.4.0, testing): every text under the staff card, every 2 s for 30 s, logged when it changes
        static VisualElement MapRoot; static int MapLeft; static int MapHash;
        static void MapStart(SI.Bindable.SIText el)
        {
            VisualElement e = el, root = null;
            for (int i = 0; i < 40 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                if (n == "TabbedGridLayoutElement" && root == null) root = e;
                if (n == "Card-frame") { root = e; break; }
                if (n == "MenuBody") break;
            }
            if (root == null) return;
            bool running = MapLeft > 0;
            MapRoot = root; MapLeft = 15;
            if (!running) MapTick();
        }
        static void MapTick()
        {
            try
            {
                if (MapLeft-- <= 0 || MapRoot == null) return;
                var lines = new List<string>(); Visited = 0;
                Dump(MapRoot, "", lines, 0);
                int h = string.Join("\n", lines).GetHashCode();
                if (h != MapHash)
                {
                    MapHash = h;
                    TreatmentRoom.W("map", "staff screen", lines.Count + " texts");
                    foreach (var l in lines.Take(700)) TreatmentRoom.W("map", "text", l);
                }
                MapRoot.schedule.Execute((System.Action)MapTick).ExecuteLater(2000);
            }
            catch (Exception ex) { MapLeft = 0; TreatmentRoom.Fail("map", ex); }
        }
        static void Dump(VisualElement e, string path, List<string> acc, int depth)
        {
            if (e == null || depth > 50 || acc.Count > 1500 || ++Visited > 15000) return;
            var n = e.name ?? "";
            var p = n.Length > 0 ? (path.Length > 0 ? path + "/" + n : n) : path;
            var te = e.TryCast<TextElement>();
            if (te != null)
            {
                string t = null; try { t = te.text; } catch { }
                IntPtr ptr = IntPtr.Zero; try { ptr = e.Pointer; } catch { }
                string who = "";
                if (ptr != IntPtr.Zero && Raw.TryGetValue(ptr, out var raw)) foreach (var l in PersonLinks.All(raw)) if (l.IsPerson) { who = " [" + l.Key + "]"; break; }
                t = Strip(t);
                if (!string.IsNullOrEmpty(t)) acc.Add((p.Length > 150 ? "..." + p.Substring(p.Length - 150) : p) + "\t" + (t.Length > 120 ? t.Substring(0, 120) : t) + who);
            }
            int c = 0; try { c = e.childCount; } catch { }
            for (int i = 0; i < c; i++) { VisualElement k = null; try { k = e.ElementAt(i); } catch { } Dump(k, p, acc, depth + 1); }
        }

        // the profile's title bar (22 Nov probe): MainUIDocument-container/MenuTitle/.../ViewedEntityTypeSwitch/Player/PlayerName
        static bool TitleOwner(VisualElement from, out string id, out string name)
        {
            id = ""; name = "";
            try
            {
                VisualElement top = from;
                for (int i = 0; i < 80 && top.parent != null; i++)
                {
                    if ((top.name ?? "") == "MainUIDocument-container") break;
                    top = top.parent;
                }
                var found = new List<VisualElement>(); Visited = 0;
                var menu = Shallow(top, "MenuTitle", 0, 5);   // SI's unnamed wrappers may sit in between
                if (menu == null) return false;
                FindAll(menu, "ViewedEntityTypeSwitch", found, 0);
                foreach (var sw in found)
                {
                    var names = new List<VisualElement>(); Visited = 0;
                    FindAll(sw, "PlayerName", names, 0);
                    foreach (var pn in names)
                    {
                        string raw = null;
                        IntPtr p = IntPtr.Zero; try { p = pn.Pointer; } catch { }
                        if (p != IntPtr.Zero) Raw.TryGetValue(p, out raw);
                        if (string.IsNullOrEmpty(raw)) { var te = pn.TryCast<TextElement>(); if (te != null) try { raw = te.text; } catch { } }
                        if (string.IsNullOrEmpty(raw)) continue;
                        foreach (var l in PersonLinks.All(raw)) if (l.IsPerson) { id = l.Key; name = l.Visible; return true; }
                    }
                }
            }
            catch (Exception ex) { TreatmentRoom.Fail("title owner", ex); }
            return false;
        }

        static VisualElement Shallow(VisualElement e, string name, int depth, int max)
        {
            if (e == null || depth > max) return null;
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++)
            {
                VisualElement c = null; try { c = e.ElementAt(i); } catch { }
                if (c == null) continue;
                if ((c.name ?? "") == name) return c;
                var d = Shallow(c, name, depth + 1, max); if (d != null) return d;
            }
            return null;
        }

        static void Collect(VisualElement e, List<Cell> acc, int depth, bool personCell)
        {
            if (e == null || depth > 40 || acc.Count > 200 || ++Visited > 4000) return;
            var nm = e.name ?? "";
            if (nm == "ShowPerson" || nm == "player-name-button-template" || nm == "CellButton") personCell = true;
            var te = e.TryCast<TextElement>();
            if (te != null)
            {
                string t = null; try { t = te.text; } catch { }
                if (!string.IsNullOrEmpty(t))
                {
                    var c = new Cell { Name = nm, Text = Strip(t), PersonCell = personCell };
                    IntPtr p = IntPtr.Zero; try { p = e.Pointer; } catch { }
                    if (p != IntPtr.Zero && Raw.TryGetValue(p, out var raw))
                        foreach (var l in PersonLinks.All(raw)) if (l.IsPerson) { c.PersonId = l.Key; c.PersonName = l.Visible; c.PersonCell = true; break; }
                    if (c.Text.Length > 0) acc.Add(c);
                }
            }
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c2 = null; try { c2 = e.ElementAt(i); } catch { } Collect(c2, acc, depth + 1, personCell); }
        }

        static void FindAll(VisualElement e, string name, List<VisualElement> into, int depth)
        {
            if (e == null || depth > 40 || ++Visited > 20000) return;
            if ((e.name ?? "") == name) into.Add(e);
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } FindAll(c, name, into, depth + 1); }
        }

        static bool HasNamedBelow(VisualElement e, string name, int depth)
        {
            if (e == null || depth > 30) return false;
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++)
            {
                VisualElement c = null; try { c = e.ElementAt(i); } catch { }
                if (c == null) continue;
                if ((c.name ?? "") == name) return true;
                if (HasNamedBelow(c, name, depth + 1)) return true;
            }
            return false;
        }

        // the league table groups rows by club: the group is the child of the outer "View" that holds this inner list;
        // the club's name is the group's text outside the inner list that isn't a header or a count
        static readonly Regex CountRx = new Regex(@"^\d+\s+Injur(y|ies)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly HashSet<string> Head = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Player", "Injury", "Date Injured", "Expected Return", "Jump to Top" };
        static string ClubOf(VisualElement inner)
        {
            try
            {
                VisualElement g = inner;
                for (int i = 0; i < 12 && g != null && g.parent != null; i++, g = g.parent)
                    if ((g.parent.name ?? "") == "View") break;
                if (g == null) return "";
                var acc = new List<string>(); Texts(g, inner, acc, 0);
                foreach (var t in acc) if (!Head.Contains(t) && !CountRx.IsMatch(t)) return t;
            }
            catch { }
            return "";
        }
        static void Texts(VisualElement e, VisualElement skip, List<string> acc, int depth)
        {
            if (e == null || depth > 30 || acc.Count > 20) return;
            if (e.Pointer == skip.Pointer) return;
            var te = e.TryCast<TextElement>();
            if (te != null) { string t = null; try { t = te.text; } catch { } t = Strip(t); if (!string.IsNullOrEmpty(t)) acc.Add(t); }
            int n = 0; try { n = e.childCount; } catch { }
            for (int i = 0; i < n; i++) { VisualElement c = null; try { c = e.ElementAt(i); } catch { } Texts(c, skip, acc, depth + 1); }
        }
    }
}
