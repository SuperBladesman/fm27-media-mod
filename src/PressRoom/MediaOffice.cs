// FM27 Media Mod - Media Office (part of the Press Room plugin; tabs, the weekly desk and costs since 0.13). MIT licence (see LICENSE).
// A page of your own, opened from the MEDIA OFFICE tab at the right of the screen:
//   - appoint a head of media, whose approach (open door, gatekeeper, fixer) shapes how the press office behaves;
//   - a press list built from everything you have said on the record: each outlet's journalists, how you get on,
//     and a status you set (welcome, favoured, banned);
//   - the head of media's desk: small missions (mend fences, a charm offensive, a freeze-out).
// What reaches the game: a banned outlet's question is cut by the press officer and answered "no comment" through
// SI's own command, so the engine treats it as one. Everything else is the journalists' opening words and memory.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class MediaOffice
    {
        internal enum Approach { None, OpenDoor, Gatekeeper, Fixer }
        internal class Candidate { public string Name, Title, Bio; public Approach A; }
        internal static readonly Candidate[] Candidates =
        {
            new Candidate { Name = "Kirsty Lomax", A = Approach.OpenDoor, Title = "Open door",
                Bio = "Twelve years running a local radio sports desk. Keeps the press inside the tent: grudges fade fast, but the room calls out every ban." },
            new Candidate { Name = "Dan Okafor", A = Approach.Gatekeeper, Title = "Gatekeeper",
                Bio = "A decade in a Premier League press office. Decides who gets in and when. The rest of the room mostly lets a ban go, but grudges last longer." },
            new Candidate { Name = "Ruth Eccleston", A = Approach.Fixer, Title = "Fixer",
                Bio = "Former tabloid football writer. Trades access for friendly coverage: favoured outlets open warmly more often, and the room calls out a ban more often than for a gatekeeper." },
        };

        internal static bool Enabled = true;
        internal static int TabTop = 120;
        internal static string HeadName = ""; internal static Approach Mode = Approach.None; internal static int HeadSince;
        internal static readonly Dictionary<string, string> Status = new Dictionary<string, string>();      // outlet -> welcome | favoured | banned
        internal static readonly Dictionary<string, int> Adjust = new Dictionary<string, int>();           // journalist -> earned or lost on missions
        internal static readonly HashSet<string> Thanks = new HashSet<string>();                           // journalists owed a thank-you opener
        internal static readonly HashSet<string> Returned = new HashSet<string>();                         // outlets owed a "back in the room" opener
        internal static readonly Dictionary<string, int> BanFired = new Dictionary<string, int>();         // outlet -> last conference a ban was applied
        internal class Mission { public string Kind, Target, Outlet, State; public int Progress, Goal, Since; }
        internal static readonly List<Mission> Missions = new List<Mission>();
        internal static string LastNews = "";
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "media office.tsv");
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // ---------- state ----------
        internal static void Reset()
        {
            HeadName = ""; Mode = Approach.None; HeadSince = 0; LastNews = "";
            Status.Clear(); Adjust.Clear(); Thanks.Clear(); Returned.Clear(); BanFired.Clear(); Missions.Clear();
            Desk.Reset();
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t'); if (p.Length < 2) continue;
                    switch (p[0])
                    {
                        case "head": if (p.Length >= 4) { HeadName = p[1]; Enum.TryParse(p[2], out Mode); int.TryParse(p[3], out HeadSince); } break;
                        case "outlet": if (p.Length >= 3) Status[p[1]] = p[2]; break;
                        case "adjust": if (p.Length >= 3 && int.TryParse(p[2], out var a)) Adjust[p[1]] = a; break;
                        case "thanks": Thanks.Add(p[1]); break;
                        case "returned": Returned.Add(p[1]); break;
                        case "banfired": if (p.Length >= 3 && int.TryParse(p[2], out var b)) BanFired[p[1]] = b; break;
                        case "mission":
                            if (p.Length >= 8) Missions.Add(new Mission { Kind = p[1], Target = p[2], Outlet = p[3], State = p[4],
                                Progress = int.TryParse(p[5], out var pr) ? pr : 0, Goal = int.TryParse(p[6], out var g) ? g : 1, Since = int.TryParse(p[7], out var s) ? s : 0 });
                            break;
                        default: Desk.LoadLine(p); break;   // 0.13: the clock, the budget, caps and the office's own history
                    }
                }
            }
            catch (Exception e) { PressRoom.Fail("office load", e); }
            Desk.Migrate();   // 0.13.3: weeks kept before the date was read
            Returned.RemoveWhere(o => !BanFired.ContainsKey(o));   // 0.5.0 owed a welcome-back after a ban that never cut a question
        }

        internal static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                if (Mode != Approach.None) sb.Append("head\t").Append(Clean(HeadName)).Append('\t').Append(Mode).Append('\t').Append(HeadSince).Append('\n');
                foreach (var kv in Status) sb.Append("outlet\t").Append(Clean(kv.Key)).Append('\t').Append(kv.Value).Append('\n');
                foreach (var kv in Adjust) sb.Append("adjust\t").Append(Clean(kv.Key)).Append('\t').Append(kv.Value).Append('\n');
                foreach (var t in Thanks) sb.Append("thanks\t").Append(Clean(t)).Append('\n');
                foreach (var t in Returned) sb.Append("returned\t").Append(Clean(t)).Append('\n');
                foreach (var kv in BanFired) sb.Append("banfired\t").Append(Clean(kv.Key)).Append('\t').Append(kv.Value).Append('\n');
                foreach (var m in Missions) sb.Append("mission\t").Append(m.Kind).Append('\t').Append(Clean(m.Target)).Append('\t').Append(Clean(m.Outlet)).Append('\t')
                    .Append(m.State).Append('\t').Append(m.Progress).Append('\t').Append(m.Goal).Append('\t').Append(m.Since).Append('\n');
                Desk.SaveLines(sb, Clean);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("office save", e); }
        }

        internal static string StatusOf(string outlet) => !string.IsNullOrEmpty(outlet) && Status.TryGetValue(outlet, out var s) ? s : "welcome";
        internal static bool IsBanned(string outlet) => Enabled && Mode != Approach.None && StatusOf(outlet) == "banned";
        internal static bool IsFavoured(string outlet) => Enabled && Mode != Approach.None && StatusOf(outlet) == "favoured";

        // ---------- how you get on ----------
        internal static int Rel(string journalist)
        {
            if (Pack.On) return Pack.Rel(journalist) + (Adjust.TryGetValue(journalist, out var legacy) ? Math.Min(2, legacy) : 0);
            int s = 0;
            foreach (var e in PressRoom.Record.All)
            {
                if (e.Journalist != journalist) continue;
                switch (e.Kind) { case "answer": s += 1; break; case "nocomment": s -= 3; break; case "storm": s -= 6; break; case "ban": s -= 4; break; case "favour": s += 1; break; case "wrapped": s -= e.Value >= PressRoom.WrapUp.Fair ? 0 : 2; break; }
            }
            if (Adjust.TryGetValue(journalist, out var a)) s += a;
            return s;
        }
        internal static string Band(int s) => Pack.On ? Pack.Band(s) : s >= 10 ? "Warm" : s >= 4 ? "Cordial" : s >= -2 ? "Neutral" : s >= -8 ? "Cool" : "Hostile";
        static Color BandColour(int s) => Pack.On
            ? (s >= 6 ? new Color(0.45f, 0.85f, 0.50f, 1f) : s >= 3 ? new Color(0.70f, 0.88f, 0.55f, 1f) : s >= -2 ? new Color(0.80f, 0.80f, 0.84f, 1f) : s >= -5 ? new Color(0.95f, 0.65f, 0.35f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f))
            : (s >= 10 ? new Color(0.45f, 0.85f, 0.50f, 1f) : s >= 4 ? new Color(0.70f, 0.88f, 0.55f, 1f) : s >= -2 ? new Color(0.80f, 0.80f, 0.84f, 1f) : s >= -8 ? new Color(0.95f, 0.65f, 0.35f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f));

        internal class OutletInfo { public string Outlet; public List<string> Journalists = new List<string>(); public int Answers, NoComments, Storms, Bans, LastConf; }
        internal static List<OutletInfo> Outlets()
        {
            var map = new Dictionary<string, OutletInfo>();
            foreach (var e in PressRoom.Record.All)
            {
                if (string.IsNullOrEmpty(e.Outlet)) continue;
                if (!map.TryGetValue(e.Outlet, out var o)) { o = new OutletInfo { Outlet = e.Outlet }; map[e.Outlet] = o; }
                if (!string.IsNullOrEmpty(e.Journalist) && !o.Journalists.Contains(e.Journalist)) o.Journalists.Add(e.Journalist);
                if (e.Kind == "answer") o.Answers++; else if (e.Kind == "nocomment") o.NoComments++; else if (e.Kind == "storm") o.Storms++; else if (e.Kind == "ban") o.Bans++;
                if (e.Conf > o.LastConf) o.LastConf = e.Conf;
            }
            foreach (var kv in Status) if (!map.ContainsKey(kv.Key)) map[kv.Key] = new OutletInfo { Outlet = kv.Key };
            var list = new List<OutletInfo>(map.Values);
            list.Sort((a, b) => b.LastConf != a.LastConf ? b.LastConf.CompareTo(a.LastConf) : string.Compare(a.Outlet, b.Outlet, StringComparison.Ordinal));
            return list;
        }
        static int OutletRel(OutletInfo o) { if (o.Journalists.Count == 0) return 0; int s = 0; foreach (var j in o.Journalists) s += Rel(j); return s / o.Journalists.Count; }

        // ---------- in the press conference ----------
        internal static bool Firing;
        internal static FM.GamePlugin.GameInteropSubsystem Interop;
        internal static SI.Interop.InteropReference Session; internal static DateTime SessionAt = DateTime.MinValue;
        static int ReactedConf = -1;

        // the press officer cuts a banned outlet's question; SI's own "no comment" follows a moment later
        internal static string BanLine(string outlet)
        {
            switch (Mode)
            {
                case Approach.Gatekeeper: return "Press officer: “Not from " + outlet + " today. Next question.”";
                case Approach.Fixer: return "Press officer: “" + outlet + " know why they're not getting answers. Next.”";
                default: return "Press officer: “Sorry, we're not taking questions from " + outlet + " today.”";
            }
        }

        internal static void ScheduleNoComment(VisualElement el, string q, string outlet)
        {
            try
            {
                int conf = PressRoom.Record.Conf;
                System.Action fire = () =>
                {
                    try
                    {
                        if (PressRoom.CurQRaw != q) { PressRoom.W("ban", outlet, "question moved on, nothing sent"); return; }
                        if (Interop == null || Session == null || (DateTime.Now - SessionAt).TotalSeconds > 180) { PressRoom.W("ban", outlet, "no live press-conference session, nothing sent"); return; }
                        Firing = true;
                        try { Interop.SendEvent((uint)(int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceNoComment, Session, SI.Core.TypedValue.Null); }
                        finally { Firing = false; }
                        BanFired[outlet] = conf; Save();
                        PressRoom.W("ban", outlet, "no comment sent");
                    }
                    catch (Exception e) { Firing = false; PressRoom.Fail("ban fire", e); }
                };
                el.schedule.Execute(fire).ExecuteLater(2500);
            }
            catch (Exception e) { PressRoom.Fail("ban schedule", e); }
        }

        // a one-time opener someone is owed: thanks after a mended relationship, or the first question back after a ban
        internal static string OneOff(string outlet, string journalist)
        {
            if (Mode == Approach.None) return null;
            if (!string.IsNullOrEmpty(journalist) && Thanks.Remove(journalist)) { Save(); return "Fair play, you've given us proper answers lately."; }
            if (!string.IsNullOrEmpty(outlet) && Returned.Remove(outlet)) { Save(); return "Good to be back in the room."; }
            return null;
        }

        static readonly string[] Warm = { "Thanks for making the time for us this week.", "Appreciate the access lately.", "Good of you to sit down with us earlier in the week." };
        static readonly string[] Reaction = { "With {0} shut out today, I'll ask what they would have.", "Plenty in this room think the {0} ban is a mistake.", "Since {0} aren't allowed to ask:" };

        // favoured outlets open warmly; once a conference, someone in the room reacts to a ban
        internal static string Opener(string outlet, string journalist, uint h)
        {
            if (Mode == Approach.None) return null;
            if (IsFavoured(outlet))
            {
                int share = Mode == Approach.Fixer ? 80 : 60;
                if (Pack.On && Pack.PressureAt(PressRoom.Record.Conf, true, out _) >= 5) share /= 3;   // favour is hard to keep when results go
                if (h % 100 < (uint)share) { PressRoom.Record.Add("favour", 0, ""); return Warm[(h / 7) % (uint)Warm.Length]; }
                return null;
            }
            if (ReactedConf == PressRoom.Record.Conf) return null;
            string banned = null; foreach (var kv in Status) if (kv.Value == "banned" && kv.Key != outlet) { banned = kv.Key; break; }
            if (banned == null) return null;
            int chance = Mode == Approach.OpenDoor ? 100 : Mode == Approach.Fixer ? 60 : 30;
            if (h % 100 >= (uint)chance) return null;
            ReactedConf = PressRoom.Record.Conf;
            return string.Format(Reaction[(h / 11) % (uint)Reaction.Length], banned);
        }

        internal static int GrudgeLength(string why)
        {
            int n = why == "storm" ? 3 : why == "cutshort" ? 1 : 2;
            if (Mode == Approach.OpenDoor) n = Math.Max(1, n - 1);
            else if (Mode == Approach.Gatekeeper) n += 1;
            return n;
        }

        // ---------- missions ----------
        static string Who(Mission m) => m.Kind == "bridge" ? m.Target + " (" + m.Outlet + ")" : m.Outlet;
        internal static string Title(Mission m)
        {
            switch (m.Kind)
            {
                case "bridge": return "Mend fences with " + Who(m);
                case "charm": return "Charm offensive: " + m.Outlet;
                case "freeze": return "Freeze out " + m.Outlet;
            }
            return m.Kind;
        }
        internal static string Brief(Mission m)
        {
            switch (m.Kind)
            {
                case "bridge": return "Answer " + m.Target + "'s next " + m.Goal + " questions. No \"no comment\", no walking out.";
                case "charm": return "Give " + m.Outlet + "'s next " + m.Goal + " questions your most positive answer (the top one).";
                case "freeze": return "Keep " + m.Outlet + " banned for " + m.Goal + " press conferences. Lift the ban afterwards and they come back chastened.";
            }
            return "";
        }

        internal static void OfferMissions()
        {
            if (Mode == Approach.None) return;
            int open = 0; foreach (var m in Missions) if (m.State == "offered" || m.State == "active") open++;
            if (open >= 2) return;
            var outlets = Outlets();
            bool Has(string kind, string target) { foreach (var m in Missions) if (m.Kind == kind && (m.Target == target || m.Outlet == target) && (m.State == "offered" || m.State == "active")) return true; return false; }
            // mend fences: the journalist you get on worst with, from an outlet that isn't banned
            string worstJ = null, worstO = null; int worst = int.MaxValue;
            foreach (var o in outlets)
            {
                if (StatusOf(o.Outlet) == "banned") continue;
                foreach (var j in o.Journalists) { int r = Rel(j); if (r < worst) { worst = r; worstJ = j; worstO = o.Outlet; } }
            }
            if (open < 2 && worstJ != null && worst < (Pack.On ? 0 : 4) && !Has("bridge", worstJ))
            { Missions.Add(new Mission { Kind = "bridge", Target = worstJ, Outlet = worstO, State = "offered", Goal = 3, Since = PressRoom.Record.Conf }); open++; }
            if (open < 2 && (Mode == Approach.OpenDoor || Mode == Approach.Fixer))
            {
                OutletInfo pick = null;
                foreach (var o in outlets) { var k = PressRoom.Classify(o.Outlet); if ((k == PressRoom.Kind.PhoneIn || k == PressRoom.Kind.RedTop) && StatusOf(o.Outlet) == "welcome") { pick = o; break; } }
                if (pick == null) foreach (var o in outlets) if (StatusOf(o.Outlet) == "welcome" && OutletRel(o) < (Pack.On ? 3 : 10)) { pick = o; break; }
                if (pick != null && !Has("charm", pick.Outlet)) { Missions.Add(new Mission { Kind = "charm", Target = "", Outlet = pick.Outlet, State = "offered", Goal = 2, Since = PressRoom.Record.Conf }); open++; }
            }
            if (open < 2 && (Mode == Approach.Gatekeeper || Mode == Approach.Fixer))
            {
                OutletInfo pick = null; int low = int.MaxValue;
                foreach (var o in outlets) { if (StatusOf(o.Outlet) != "welcome" || o.Journalists.Count == 0) continue; int r = OutletRel(o); if (r < low) { low = r; pick = o; } }
                if (pick != null && !Has("freeze", pick.Outlet)) { Missions.Add(new Mission { Kind = "freeze", Target = "", Outlet = pick.Outlet, State = "offered", Goal = 2, Since = PressRoom.Record.Conf }); open++; }
            }
            Save();
        }

        static void Done(Mission m, string news)
        {
            m.State = "done"; LastNews = "Done: " + Title(m) + ". " + news;
            PressRoom.W("mission", "done", Title(m));
        }

        // every answer, no comment or walk-out on the record moves the missions on
        internal static void OnRecord(PressRoom.Record.E e)
        {
            try
            {
                bool changed = false;
                foreach (var m in Missions)
                {
                    if (m.State != "active") continue;
                    if (m.Kind == "bridge" && e.Journalist == m.Target)
                    {
                        if (e.Kind == "answer") { m.Progress++; changed = true; if (m.Progress >= m.Goal)
                        {
                            if (Pack.On && !Pack.BridgeTakes(m.Target)) { m.State = "failed"; LastNews = "Failed: " + Title(m) + ". " + m.Target + " isn't having it."; Pack.Add(m.Target, -1, "rebuffed"); PressRoom.W("mission", "rebuffed", Title(m)); }
                            else { if (Pack.On) Pack.Add(m.Target, 5, "bridge"); else Adjust[m.Target] = (Adjust.TryGetValue(m.Target, out var a) ? a : 0) + 5; Thanks.Add(m.Target); Done(m, m.Target + " will say so next time."); }
                        } }
                        else if (e.Kind == "nocomment" || e.Kind == "storm" || (e.Kind == "wrapped" && e.Value < PressRoom.WrapUp.Fair)) { m.State = "failed"; LastNews = "Failed: " + Title(m) + "."; changed = true; }
                    }
                    else if (m.Kind == "charm" && e.Outlet == m.Outlet && e.Kind == "answer")
                    {
                        if (e.Value == 5) { m.Progress++; changed = true; }
                        if (m.Progress >= m.Goal)
                        {
                            Status[m.Outlet] = "favoured";
                            if (Pack.On) Pack.Favoured(m.Outlet, 3, "charm");
                            else foreach (var o in Outlets()) if (o.Outlet == m.Outlet) foreach (var j in o.Journalists) Adjust[j] = (Adjust.TryGetValue(j, out var a) ? a : 0) + 3;
                            Done(m, m.Outlet + " are now favoured.");
                        }
                    }
                }
                if (changed) Save();
            }
            catch (Exception ex) { PressRoom.Fail("mission record", ex); }
        }

        internal static void OnConference(int conf)
        {
            Deals.OnConference(conf);
            try
            {
                bool changed = false;
                foreach (var m in Missions)
                {
                    if (m.State != "active" || m.Kind != "freeze") continue;
                    if (StatusOf(m.Outlet) != "banned") { m.State = "failed"; LastNews = "Failed: " + Title(m) + " (the ban was lifted early)."; changed = true; continue; }
                    if (BanFired.TryGetValue(m.Outlet, out var c) && c == conf - 1) { m.Progress++; changed = true; }
                    if (m.Progress >= m.Goal) { Done(m, "Lift the ban when you like: they'll come back chastened."); changed = true; }
                }
                if (changed) Save();
            }
            catch (Exception ex) { PressRoom.Fail("mission conference", ex); }
        }

        // an outlet that ran your planted story owes you
        internal static void Owe(string outlet, int n)
        {
            if (Pack.On) { Pack.Favoured(outlet, n, "planted"); return; }
            foreach (var o in Outlets()) if (o.Outlet == outlet) foreach (var j in o.Journalists) Adjust[j] = (Adjust.TryGetValue(j, out var a) ? a : 0) + n;
            Save();
        }

        // where a planted story goes: your favoured outlet, else the one you get on best with
        static string PlantTarget()
        {
            foreach (var kv in Status) if (kv.Value == "favoured") return kv.Key;
            string best = null; int top = int.MinValue;
            foreach (var o in Outlets()) { if (StatusOf(o.Outlet) == "banned" || o.Journalists.Count == 0) continue; int r = OutletRel(o); if (r > top) { top = r; best = o.Outlet; } }
            return best;
        }

        // which save the memory belongs to; a chooser if the mod had to guess
        static void MemorySection(bool always)
        {
            if (!always && !Memory.Unsure) return;   // 0.13: the save's name lives on the Settings tab unless the mod had to guess
            if (string.IsNullOrEmpty(Memory.Status) && !Memory.Unsure) return;
            var box = Box();
            box.Add(Text(Memory.Status, 11, Memory.Unsure ? Gold : Quiet));
            if (Memory.Unsure)
            {
                box.Add(Text("I couldn't tell which save you loaded. If this is wrong, pick the right one:", 11, Quiet));
                var r = Row(); int n = 0;
                foreach (var k in Memory.Kept()) { if (n++ >= 6) break; var kk = k.key; r.Add(Btn(k.name + (k.when.Length > 0 ? " (" + k.when + ")" : ""), () => { Memory.Choose(kk); }, Navy)); }
                box.Add(r);
            }
            Body.Add(box);
        }

        // ---------- the office's own actions: cost, risk and badge on every button (0.13) ----------
        // A button that spends press-office time. Blocked actions show why instead of doing anything.
        static VisualElement Act(string label, string action, string target, Desk.Risk risk, bool si, Color bg, System.Action run)
        {
            var row = Row(); row.style.marginTop = 2;
            int cost = Desk.Cost(action);
            var why = Desk.Blocked(action, target);
            var text = label + (cost > 0 ? "  · " + cost + "h" : "");
            if (why == null) row.Add(Btn(text, () => { run(); Refresh(); }, bg));
            else { var b = Btn(text, () => { }, Dim); b.style.color = Quiet; row.Add(b); }
            if (Desk.RiskBands && risk != Desk.Risk.None) row.Add(Pill(Desk.RiskName(risk), risk == Desk.Risk.Low ? GreenPill : risk == Desk.Risk.Medium ? AmberPill : RedPill));
            if (Desk.Badges) row.Add(Pill(si ? "SI" : "Story", si ? SiPill : StoryPill));
            if (why != null) row.Add(Text(why + ".", 11, Quiet));
            return row;
        }
        static Label Pill(string t, Color bg)
        {
            Color fill = bg, ink = Ink;
            if (Same(bg, GreenPill)) { fill = WinBg; ink = WinFg; }
            else if (Same(bg, AmberPill) || Same(bg, SiPill)) { fill = OrangeDark; ink = OrangePale; }
            else if (Same(bg, RedPill)) { fill = LossBg; ink = LossFg; }
            else if (Same(bg, StoryPill)) { fill = IncBg; ink = Ink; }
            var l = new Label(t); l.style.fontSize = 12; l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.color = ink; l.style.backgroundColor = fill;
            l.style.paddingLeft = 9; l.style.paddingRight = 9; l.style.paddingTop = 3; l.style.paddingBottom = 3; l.style.marginRight = 6;
            Radius(l, 10);
            return l;
        }

        static void DealsSection()
        {
            var box = Box();
            box.Add(Text("IN THE PAPERS", 11, Quiet));
            box.Add(Text("Deals the agents and the press are talking about. Pressure rises each time one comes out, and eases when nothing happens.", 11, Quiet));
            var list = new List<Deals.Deal>(Deals.All.Values);
            list.Sort((a, b) => b.Heat.CompareTo(a.Heat));
            int shown = 0, conf = PressRoom.Record.Conf;
            foreach (var d in list)
            {
                if (shown >= 6) break;
                if (d.Heat == 0 && conf - d.LastConf > 2) continue;
                if (d.Signals.Contains("off") && conf - d.LastConf > 0) continue;
                shown++;
                var row = Item();
                string side = d.Signals.Contains("done") ? (d.Side == "renewing" ? "Signed a new contract" : "Done deal") : d.Side == "selling" ? "Sale" : d.Side == "buying" ? "Signing" : d.Side == "renewing" ? "New contract" : "Talks";
                row.Add(Text(d.Player + "   " + side + (d.Agent.Length > 0 ? ", agent " + d.Agent : ""), 12.5f, Ink));
                var bar = new VisualElement(); bar.style.height = 6; bar.style.width = 200; bar.style.backgroundColor = Grey; bar.style.marginTop = 2; bar.style.marginBottom = 3;
                var fill = new VisualElement(); fill.style.height = 6; fill.style.width = 20 * Math.Max(0, Math.Min(10, d.Heat)); fill.style.backgroundColor = d.Heat >= 7 ? BanRed : Gold; bar.Add(fill);
                row.Add(bar);
                var known = new List<string>();
                foreach (var s in d.Signals) { var t = Deals.Describe(s); if (t != null) known.Add(t); }
                if (d.Cost.Length > 0) known.Add(d.Cost + " out of the transfer budget");
                if (d.Stance.Length > 0) known.Add("your last stance: " + d.Stance);
                if (known.Count > 0) { var k = string.Join("; ", known); row.Add(Text(char.ToUpperInvariant(k[0]) + k.Substring(1) + ".", 11, Quiet)); }
                if (d.LastLine.Length > 0) row.Add(Text("In the papers: \"" + d.LastLine + "\"", 11, Ink));
                { var acts = DealActs(d); if (acts != null) row.Add(acts); }
                box.Add(row);
            }
            if (shown == 0) box.Add(Text("Nothing yet. Deals appear here once you have talked to an agent about a player.", 11.5f, Quiet));
            if (Deals.AnyOpenTransfer())
            {
                var r = Row();
                r.Add(Btn("Window's shut: drop the transfer talk", () => { Deals.ShutWindow("by hand"); Refresh(); }, Grey));
                box.Add(Text("Once the transfer window shuts, deals still open are off for now and the press stop asking. This happens by itself when the news carries the window's round-up.", 11, Quiet));
                box.Add(r);
            }
            var club = new List<string>();
            if (Deals.WagesConf >= conf - 2) club.Add("over the wage budget");
            if (Deals.SkintConf >= conf - 2) club.Add("short of transfer budget for the deals in hand");
            if (club.Count > 0) box.Add(Text("Your staff say the club is " + string.Join(" and ", club) + "." + (Mode == Approach.Gatekeeper ? " Your Gatekeeper keeps that in the building." : " That can reach the papers."), 11, Gold));
            Body.Add(box);
        }

        // what the supporters' group thinks, as SI's supporter screens last showed it, and what has reached the press
        static void TerracesSection()
        {
            FanPress.Fresh();
            var box = Box();
            box.Add(Text("THE TERRACES", 11, Quiet));
            if (FanPress.Group.Length == 0 && FanPress.Spokes.Length == 0)
            {
                box.Add(Text("Nothing yet. Open Club > Supporters (and its Club Vision and Recruitment pages) once, and the fans' posts and the press will start to use what the supporters think.", 11.5f, Quiet));
                Body.Add(box); return;
            }
            var who = FanPress.Group.Length > 0 ? FanPress.Group : FanPress.Club + " supporters";
            box.Add(Text(who + (FanPress.Spokes.Length > 0 ? ", spokesperson " + FanPress.Spokes : "") + (FanPress.Influence.Length > 0 ? ". Influence on the board: " + FanPress.Influence + "." : "."), 12.5f, Ink));
            if (FanPress.Overall.Length > 0) box.Add(Text(FanPress.Overall, 11, Quiet));
            if (FanPress.Segments.Count > 0)
            {
                var parts = new List<string>(); foreach (var kv in FanPress.Segments) parts.Add(kv.Value + "% " + kv.Key);
                box.Add(Text("Who posts under the stories: " + string.Join(", ", parts) + ".", 11, Quiet));
            }
            int n = 0;
            foreach (var c in FanPress.Crits) { if (n++ >= 3) break; box.Add(Text("✖ " + c, 11, RedText)); }
            n = 0;
            foreach (var h in FanPress.Highs) { if (n++ >= 3) break; box.Add(Text("✔ " + h, 11, Ink)); }
            var chance = FanPress.Influence == "Very High" ? 60 : FanPress.Influence == "High" ? 45 : FanPress.Influence == "Medium" ? 30 : 15;
            box.Add(Text("The press bring the supporters' view into about " + chance + "% of conferences, each grievance once.", 11, Quiet));
            Body.Add(box);
        }

        static void WhispersSection()
        {
            var box = Box();
            box.Add(Text("WHISPERS", 11, Quiet));
            box.Add(Text("What agents, your director of football and players have heard from you. One may reach the press at your next conference.", 11, Quiet));
            int shown = 0;
            int now = PressRoom.Record.Conf;
            for (int i = Word.All.Count - 1; i >= 0 && shown < 8; i--)
            {
                var w = Word.All[i];
                // old news drops off; anything still live stays for a few conferences
                if (w.State == "stale" && now - w.Conf > 0) continue;
                if (now - w.Conf > 3 && w.State != "planted") continue;
                shown++;
                var row = Item();
                row.Add(Text(Word.Describe(w), 12.5f, Ink));
                string state = w.State == "out" ? "Out" + (w.Outlet.Length > 0 ? ": " + w.Outlet : "") : w.State == "planted" ? "Planted with " + w.Outlet : w.State == "hushed" ? "Kept quiet" : w.State == "stale" ? "Old news: the deal is done or off" : "Not out yet";
                row.Add(Text((w.With.Length > 0 ? "Said to " + w.With + " · " : "") + state, 11, Quiet));
                { var acts = WhisperActs(w); if (acts != null) row.Add(acts); }
                box.Add(row);
            }
            if (shown == 0) box.Add(Text("Nothing new. Talk to an agent, your director of football or a player about a move.", 11.5f, Quiet));
            Body.Add(box);
        }

        // 0.9.0: discontent the news has reported about your players (Incidents.cs)
        static void DressingRoomSection()
        {
            if (!Incidents.Enabled) { var off = Box(); off.Add(Text("The dressing-room watch is switched off in the config.", 11.5f, Quiet)); Body.Add(off); return; }
            var box = Box();
            box.Add(Text("IN THE DRESSING ROOM", 11, Quiet));
            box.Add(Text("What the papers have reported about your players: wanting out, falling-outs, team-mates unhappy, training-ground bust-ups. The press will raise it until the news says it's settled.", 11, Quiet));
            int shown = 0, now = PressRoom.Record.Conf;
            for (int i = Incidents.All.Count - 1; i >= 0 && shown < 8; i--)
            {
                var x = Incidents.All[i];
                if (x.State == "stale") continue;
                if ((x.State == "settled" || x.State == "closed") && now - x.LastConf > 2) continue;
                shown++;
                var row = Item();
                row.Add(Text(Incidents.Describe(x), 12.5f, Ink));
                row.Add(Text(Incidents.StateLine(x, now), 11, Quiet));
                { var acts = IncidentActs(x); if (acts != null) row.Add(acts); }
                box.Add(row);
            }
            if (shown == 0) box.Add(Text("Nothing reported. When the news carries one of your players wanting out, a falling-out or a training-ground bust-up, it appears here.", 11.5f, Quiet));
            Body.Add(box);
            // 0.13.9: how they took what you said (team meetings and chats), newest first
            var took = Box();
            took.Add(Text("HOW THEY TOOK IT", 11, Quiet));
            took.Add(Text("Your team meetings and chats: the line you chose, the reaction the room showed and the reply. Kept per save.", 11, Quiet));
            int n = 0;
            foreach (var r in Reactions.Latest(8))
            {
                n++;
                var row = Item();
                row.Add(Text(Reactions.Heading(r), 12.5f, Ink));
                row.Add(Text(Reactions.Detail(r), 11, Quiet));
                var room = Reactions.RoomLine(r);
                if (room.Length > 0) row.Add(Text(room, 11, Ink));
                else if (r.Who.Length > 0 && r.Who.Contains(";") && r.Who.Split(';').Length <= 3) row.Add(Text(r.Who, 11, Quiet));
                took.Add(row);
            }
            if (n == 0) took.Add(Text("Nothing yet. After your next team meeting or chat, the room's reaction appears here.", 11.5f, Quiet));
            Body.Add(took);
        }

        // 0.15.0: the dressing-room story's actions, shared by its tab and the This week page
        static VisualElement IncidentActs(Incidents.I x)
        {
            if (!(Mode != Approach.None && (x.State == "open" || x.State == "inhouse"))) return null;
            var row = new VisualElement();
            var xx = x;
            var who = string.IsNullOrEmpty(x.Player) ? "incident " + x.Id : x.Player;
            if (x.State == "open")
            {
                var risk = Desk.RiskOf("closeranks", "");
                row.Add(Act("Close ranks", "closeranks", who, risk, false, Grey, () =>
                {
                    if (Desk.Misfires("closeranks", who, risk))
                    {
                        LastNews = "Too late to close ranks: the press already know about " + Incidents.Describe(xx) + ".";
                        Desk.Charge(Desk.Cost("closeranks"), "tried to close ranks on", who, "the press already knew");
                    }
                    else { Incidents.SetState(xx, "inhouse"); Desk.Charge(Desk.Cost("closeranks"), "closed ranks on", who, ""); }
                }));
            }
            else row.Add(Act("Let them ask", "letask", who, Desk.Risk.None, false, Navy, () => { Incidents.SetState(xx, "open"); }));
            return row;
        }

        // 0.15.0: the whisper's actions, shared by its tab and the This week page
        static VisualElement WhisperActs(Word.Whisper w)
        {
            if (!(w.State == "new" && Mode != Approach.None)) return null;
            var row = new VisualElement();
            var ww = w; var target = PlantTarget();
            if (target != null)
            {
                var risk = Desk.RiskOf("plant", "");
                row.Add(Act("Plant with " + target, "plant", target, risk, false, Navy, () =>
                {
                    ww.State = "planted"; ww.Outlet = target; Word.Save(); PressRoom.W("whisper", "planted", target + " | " + Word.Describe(ww));
                    string outcome = target + " will run it";
                    if (Desk.Misfires("plant", target, risk))
                    {
                        // the rest of the room works out where it came from
                        if (Pack.On) foreach (var j in new List<Pack.J>(Pack.All())) if (j.Outlet != target) Pack.Add(j.Name, -1, "jealous:" + target);
                        outcome += "; the room has worked out it came from you";
                        LastNews = "The other outlets have worked out that " + target + "'s story came from you. They're cooler with you for now.";
                    }
                    Desk.Charge(Desk.Cost("plant"), "planted a story with", target, outcome);
                }));
            }
            var hr = Desk.RiskOf("hush", w.With);
            row.Add(Act("Keep it quiet", "hush", w.Player, hr, false, Grey, () =>
            {
                if (Desk.Misfires("hush", ww.Player, hr))
                {
                    LastNews = "Couldn't keep it quiet: " + Word.Describe(ww) + ". It may come out at your next press conference.";
                    Desk.Charge(Desk.Cost("hush"), "tried to keep quiet", ww.Player, "it got out anyway");
                    PressRoom.W("whisper", "hush failed", Word.Describe(ww));
                }
                else
                {
                    ww.State = "hushed"; Word.Save(); PressRoom.W("whisper", "hushed", Word.Describe(ww));
                    Desk.Charge(Desk.Cost("hush"), "kept quiet", ww.Player, "");
                }
            }));
            return row;
        }

        // 0.15.0: the deal's actions, shared by its tab and the This week page
        static VisualElement DealActs(Deals.Deal d)
        {
            if (!(Mode != Approach.None && d.Heat >= 2 && !d.Signals.Contains("done"))) return null;
            var row = new VisualElement();
            var dd = d;
            if (Mode == Approach.Gatekeeper)
            {
                if (!d.Quiet) row.Add(Act("Say nothing", "quiet", d.Player, Desk.Risk.None, false, Grey, () => { Deals.BriefBack(dd); Desk.Charge(0, "said nothing about", dd.Player, ""); }));
                else row.Add(Text("The club is saying nothing about it.", 11, Quiet));
            }
            else if (d.Brief == "pending") row.Add(Text("Your briefing goes out at the next press conference.", 11, Quiet));
            else
            {
                var risk = Desk.RiskOf("brief", "");
                row.Add(Act("Brief back", "brief", d.Player, risk, false, Navy, () =>
                {
                    Deals.BriefBack(dd);
                    if (Desk.Misfires("brief", dd.Player, risk)) { Deals.AddHeat(dd.Player, 2); LastNews = "The agent answered your briefing on " + dd.Player + ": the deal is hotter in the papers."; Desk.Charge(Desk.Cost("brief"), "briefed back on", dd.Player, "the agent answered back"); }
                    else Desk.Charge(Desk.Cost("brief"), "briefed back on", dd.Player, "your line goes out at the next press conference");
                }));
            }
            return row;
        }

        // 0.15.0: a desk job's buttons, shared by the desk and the This week page
        static VisualElement MissionActs(Mission m)
        {
            var box = new VisualElement();
            var mm = m;
            if (m.State == "offered")
            {
                var r = Row();
                bool freeze = m.Kind == "freeze";
                var block = freeze && Desk.Count("banned") >= Desk.MaxBanned ? "No room for another ban" : null;
                if (block == null)
                    r.Add(Act("Take it on", "mission", Title(m), Desk.Risk.None, freeze, Green, () =>
                    {
                        mm.State = "active"; mm.Since = PressRoom.Record.Conf;
                        if (mm.Kind == "freeze") { Status[mm.Outlet] = "banned"; Desk.BanSince[mm.Outlet] = PressRoom.Record.Conf; }
                        Save(); PressRoom.W("mission", "taken", Title(mm));
                        Desk.Charge(Desk.Cost("mission"), "took on: " + Title(mm), Title(mm), "");
                    }));
                else r.Add(Text(block + ".", 11, Quiet));
                r.Add(Btn("Not now", () => { mm.State = "dropped"; Save(); Refresh(); }, Grey));
                box.Add(r);
            }
            else { var r = Row(); r.Add(Btn("Drop it", () => { mm.State = "dropped"; Save(); Refresh(); }, Grey)); box.Add(r); }
            return box;
        }
        static void SetStatus(string outlet, string s)
        {
            var old = StatusOf(outlet);
            Status[outlet] = s;
            if (s == "banned" && old != "banned") Desk.BanSince[outlet] = PressRoom.Record.Conf;
            if (s != "banned") Desk.BanSince.Remove(outlet);
            if (s == "favoured" || s == "banned") Desk.LastStatusWeek = Desk.Week;
            if (old == "banned" && s != "banned" && BanFired.ContainsKey(outlet))   // only after a ban actually cut a question
            {
                Returned.Add(outlet);
                foreach (var m in Missions)
                    if (m.Kind == "freeze" && m.Outlet == outlet && m.State == "done")
                        foreach (var o in Outlets()) if (o.Outlet == outlet) foreach (var j in o.Journalists) { if (Pack.On) Pack.Add(j, 2, "chastened"); else Adjust[j] = (Adjust.TryGetValue(j, out var a) ? a : 0) + 4; }
            }
            if (Pack.On && s == "favoured" && old != "favoured") Pack.Favoured(outlet, 1, "favoured");
            Save(); PressRoom.W("office", "status", outlet + " " + s);
        }

        // ---------- the page ----------
        static VisualElement Host, Tab, Page, Body;
        static DateTime NextCheck = DateTime.MinValue;
        static string TabNow = "week";
        // 0.13.1: black and orange (Nik: "I'm from Sheffield so we have to go black and orange")
        // 0.15.0: the Treatment Room's base (near-black cards, light text) with the office's orange
        static readonly Color Bg = new Color(0.082f, 0.082f, 0.090f, 0.985f), Card = new Color(0.110f, 0.110f, 0.122f, 1f), Ink = new Color(0.957f, 0.949f, 0.937f, 1f),
            Quiet = new Color(0.741f, 0.722f, 0.694f, 1f),
            Gold = new Color(1.00f, 0.60f, 0.18f, 1f),        // orange text: titles, news, warnings
            RedC = new Color(0.93f, 0.47f, 0.09f, 1f),        // the MEDIA tab and Ban buttons: orange
            RedText = new Color(1.00f, 0.50f, 0.42f, 1f),
            Green = new Color(0.74f, 0.35f, 0.05f, 1f),       // Appoint, Take it on: orange
            BanRed = new Color(0.60f, 0.13f, 0.10f, 1f),      // Ban, and a deal running hot
            Grey = new Color(0.24f, 0.24f, 0.24f, 1f),
            Navy = new Color(0.70f, 0.33f, 0.04f, 1f),        // the selected tab and main actions: burnt orange
            Dim = new Color(0.17f, 0.17f, 0.17f, 1f),
            GreenPill = new Color(0.22f, 0.40f, 0.26f, 1f), AmberPill = new Color(0.58f, 0.46f, 0.10f, 1f), RedPill = new Color(0.58f, 0.14f, 0.12f, 1f),
            SiPill = new Color(0.70f, 0.33f, 0.04f, 1f), StoryPill = new Color(0.30f, 0.30f, 0.30f, 1f);

        // 0.11.0: whose voice your answers are in, and whether journalists ask in their own voices
        static void VoicesSection()
        {
            if (!Voices.On) return;
            var box = Box();
            box.Add(Text("YOUR VOICE: " + Voices.Label(Voices.Persona), 13, Gold));
            box.Add(Text("How your press answers are worded, where a line has a version for this voice. What each answer does is unchanged.", 11, Quiet));
            var r = Row();
            foreach (var p in Voices.Personas)
            {
                var pp = p; int n = p == "default" ? 0 : VoiceCore.Count("persona:" + p);
                var label = Voices.Label(p).Split(' ')[0] == "Your" ? "Your own" : Voices.Label(p);
                r.Add(Btn(label + (p == "default" ? "" : " (" + n + ")"), () => { Voices.SetPersona(pp); Refresh(); }, p == Voices.Persona ? Navy : Grey));
            }
            box.Add(r);
            box.Add(Text($"Journalists' own voices: {(Voices.JournoOn ? "on" : "off")} ({VoiceCore.Count("journo:hard")} sharper, {VoiceCore.Count("journo:warm")} warmer questions). Marked lines seen so far: {Voices.Shown}, re-voiced: {Voices.Swapped}.", 11, Quiet));
            Body.Add(box);
        }

        // 0.15.0: the old sections in the new look: text a size up, all-caps captions as headings, buttons and cards as the Treatment Room's
        static Label Text(string t, float size, Color c)
        {
            var l = new Label(t); l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 3; l.style.marginTop = 0;
            bool caption = size <= 11.01f && t.Length > 3 && t.Length < 60 && t == t.ToUpperInvariant() && HasLetter(t) && !t.Contains(".");
            if (caption) { l.style.fontSize = 12; l.style.color = Quiet; l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.letterSpacing = 1.3f; l.style.marginBottom = 6; return l; }
            l.style.fontSize = size + 1; l.style.color = c;
            return l;
        }
        static Button Btn(string t, System.Action act, Color bg)
        {
            var b = new Button(); b.text = t;
            Color fill = bg, ink = Ink; bool edge = false;
            if (Same(bg, Navy) || Same(bg, Green) || Same(bg, RedC)) { fill = Orange; ink = OnOrange; }
            else if (Same(bg, Grey)) { fill = BtnBg; edge = true; }
            else if (Same(bg, Dim)) { fill = Hex("222226"); ink = Quiet; edge = true; }
            else if (Same(bg, BanRed)) { fill = Hex("8A2C35"); ink = White1; }
            b.style.backgroundColor = fill; b.style.color = ink; b.style.fontSize = 13;
            if (Same(fill, Orange)) b.style.unityFontStyleAndWeight = FontStyle.Bold;
            Border(b, edge ? 1 : 0, BtnEdge); Radius(b, 8);
            b.style.paddingLeft = 12; b.style.paddingRight = 12; b.style.paddingTop = 5; b.style.paddingBottom = 5;
            b.style.marginLeft = 0; b.style.marginRight = 6; b.style.marginTop = 2; b.style.marginBottom = 2;
            System.Action safe = () => { try { act(); } catch (Exception e) { PressRoom.Fail("office click", e); } };
            b.clickable.add_clicked(safe);
            return b;
        }
        static VisualElement Box()
        {
            var v = Panel(Card, Edge, 12); Pad(v, 14, 18); v.style.marginBottom = 14;
            return v;
        }
        static VisualElement Item()
        {
            var row = new VisualElement(); row.style.marginTop = 6; row.style.paddingBottom = 9;
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = RowRule;
            return row;
        }
        static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; r.style.alignItems = Align.Center; return r; }

        // the tab at the right edge of the screen; checked every couple of seconds from any text the game sets
        internal static void EnsureTab(VisualElement any)
        {
            if (!Enabled || DateTime.Now < NextCheck) return;
            NextCheck = DateTime.Now.AddSeconds(2);
            try
            {
                if (Desk.RescanDate && Host != null) Desk.ScanForDate(Host);   // 0.13.2: the top bar after a day change
                if (Tab != null && Tab.panel != null) { PrPopout.Update(); return; }   // 0.13.8: the popout's status, pill and notes
                VisualElement host = null;
                for (var e = any; e != null; e = e.parent) if (e.name == "MainUIDocument-container") { host = e; break; }
                if (host == null) return;
                Host = host;
                // a slim vertical tab in the strip SI leaves empty at the right edge (0.5 sat on the right-hand panels)
                Tab = Btn("M\nE\nD\nI\nA", Toggle, RedC);
                Tab.name = "FM27MM-MediaOfficeTab"; Tab.tooltip = "Media Office"; Tab.style.color = new Color(0.05f, 0.05f, 0.05f, 1f);
                Tab.style.position = Position.Absolute; Tab.style.right = 0; Tab.style.top = TabTop;
                Tab.style.width = 22; Tab.style.fontSize = 11; Tab.style.whiteSpace = WhiteSpace.Normal;
                Tab.style.paddingLeft = 2; Tab.style.paddingRight = 2; Tab.style.paddingTop = 6; Tab.style.paddingBottom = 6; Tab.style.marginRight = 0;
                Tab.style.borderTopLeftRadius = 6; Tab.style.borderBottomLeftRadius = 6; Tab.style.borderTopRightRadius = 0; Tab.style.borderBottomRightRadius = 0;
                host.Add(Tab);
                PrPopout.Attach(Tab, Tabs);   // 0.13.8: Core's popout reads these (hidden) children
                PressRoom.W("office", "tab added", "");
                Desk.RescanDate = true;
            }
            catch (Exception e) { PressRoom.Fail("office tab", e); }
        }

        static VisualElement Strip, TabsRow;
        // 0.13.8: open the office at one of its tabs (from Core's popout)
        internal static void OpenAt(string key)
        {
            TabNow = key;
            if (Page == null || Page.panel == null) Toggle(); else Refresh();
        }
        static void Toggle()
        {
            if (Page != null && Page.panel != null) { Page.RemoveFromHierarchy(); Page = null; return; }
            if (Host == null) return;
            try { Build(); } catch (Exception e) { PressRoom.Fail("office build", e); }
            Refresh();
        }
        // 0.15.0: the Press Office's new shell, as the Treatment Room's: the office's mark, the page's name, the date and the next match,
        // who runs the office, pill tabs, a scrolling body
        static Label TitleL, SubL, HeadL;
        static void Build()
        {
            Desk.ScanForDate(Host);
            FindFont();
            float hw = 1600f, hh = 900f;
            try { var b = Host.worldBound; if (!float.IsNaN(b.width) && b.width > 400) { hw = b.width; hh = b.height; } } catch { }
            Page = new VisualElement(); Page.name = "FM27MM-MediaOffice";
            Page.style.position = Position.Absolute; Page.style.right = 40; Page.style.top = 50;
            Page.style.width = Math.Min(1180f, hw - 120f);
            Page.style.maxHeight = hh - 90f;
            Page.style.backgroundColor = PageBg; Border(Page, 1, Edge); Radius(Page, 14); Page.style.overflow = Overflow.Hidden;
            var head = Flex(true, true); head.style.justifyContent = Justify.SpaceBetween; Pad(head, 16, 22); head.style.borderBottomWidth = 1; head.style.borderBottomColor = Edge;
            var left = Flex(true);
            var mark = Disp(L("P", 22, OnOrange, true)); mark.style.width = 40; mark.style.height = 40; mark.style.backgroundColor = Orange; Radius(mark, 10);
            mark.style.unityTextAlign = TextAnchor.MiddleCenter; mark.style.marginRight = 12; left.Add(mark);
            var titles = Flex(false);
            var office = L("PRESS OFFICE", 12, OrangeLight, true, 1); office.style.letterSpacing = 2f; titles.Add(office);
            TitleL = Disp(L("This week", 20, Text1, true)); titles.Add(TitleL);
            SubL = L("", 13, Text2); SubL.style.marginTop = 2; titles.Add(SubL);
            left.Add(titles); head.Add(left);
            var right = Flex(true);
            HeadL = L("", 13, Text2); HeadL.style.unityTextAlign = TextAnchor.MiddleRight; HeadL.style.marginRight = 14; right.Add(HeadL);
            right.Add(Btn("Close", Toggle, Grey));
            head.Add(right);
            Page.Add(head);
            Strip = null;   // 0.15.0: the old strip's facts are in the header and on This week
            TabsRow = Flex(true, true); Pad(TabsRow, 10, 22); TabsRow.style.borderBottomWidth = 1; TabsRow.style.borderBottomColor = Edge; Page.Add(TabsRow);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; scroll.style.maxHeight = Math.Max(240f, hh - 250f);
            try { scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; } catch { }
            Body = new VisualElement(); Pad(Body, 18, 22); scroll.Add(Body);
            Page.Add(scroll);
            Host.Add(Page);
            PressRoom.W("office", "opened", $"width {Math.Min(1180f, hw - 120f):0} of {hw:0}" + ((object)Display != null ? ", game display font" : ""));
        }
        static readonly Dictionary<string, string> TabTitles = new Dictionary<string, string> { { "week", "This week" }, { "press", "The press" }, { "transfers", "Transfers" },
            { "room", "The dressing room" }, { "terraces", "The terraces" }, { "cuttings", "Cuttings" }, { "settings", "Settings" } };
        static string Sub()
        {
            var parts = new List<string>();
            if (Desk.DateKnown) { try { parts.Add(Desk.Today.ToString("ddd d/M/yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"))); } catch { parts.Add(Desk.WeekLabel()); } }
            else parts.Add(Desk.WeekLabel());
            var opp = GameFacts.Get("next_opponent"); if (opp.Length > 0) parts.Add("Next: " + opp);
            if (Mode != Approach.None) parts.Add(Desk.Left + " of " + Desk.Hours + " hours left");
            return string.Join("  ·  ", parts);
        }
        static string HeadLine()
        {
            if (Mode == Approach.None) return "No head of media yet";
            Candidate c = null; foreach (var x in Candidates) if (x.A == Mode) c = x;
            return "Head of media: " + HeadName + " (" + (c?.Title ?? Mode.ToString()) + ")";
        }

        static readonly (string key, string label)[] Tabs =
        {
            ("week", "This week"), ("press", "The press"), ("transfers", "Transfers"), ("room", "Dressing room"), ("terraces", "Terraces"), ("cuttings", "Cuttings"), ("settings", "Settings"),
        };

        internal static void Refresh()
        {
            if (Body == null) return;
            Body.Clear();
            try
            {
                // the strip: the week, the time left, the pressure on you, who runs the office
                if (Strip != null)
                {
                    Strip.Clear();
                    var parts = new List<string> { Desk.WeekLabel(), Desk.Left + " of " + Desk.Hours + " hours left" };
                    if (Pack.On) parts.Add("Pressure: " + Pack.PressureBand(Pack.PressureAt(PressRoom.Record.Conf, true, out _)).ToLowerInvariant());
                    parts.Add(Mode == Approach.None ? "No head of media yet" : "Head of media: " + HeadName);
                    Strip.Add(Text(string.Join("  ·  ", parts), 12, Ink));
                }
                try
                {
                    if (TitleL != null) TitleL.text = TabTitles.TryGetValue(TabNow, out var tt) ? tt : "Press Office";
                    if (SubL != null) SubL.text = Sub();
                    if (HeadL != null) HeadL.text = HeadLine();
                }
                catch (Exception e) { PressRoom.Fail("office header", e); }
                if (TabsRow != null)
                {
                    TabsRow.Clear();
                    foreach (var (key, label) in Tabs) { var k = key; TabsRow.Add(TabPill(label, () => { TabNow = k; Refresh(); }, TabNow == k)); }
                }
                MemorySection(false);
                if (!string.IsNullOrEmpty(LastNews) && TabNow != "settings") { var n = Panel(AlertBg, AlertEdge, 10); Pad(n, 12, 16); n.style.marginBottom = 14; n.Add(L(LastNews, 14, OrangePale)); Body.Add(n); }
                switch (TabNow)
                {
                    case "week": WeekView(); break;   // 0.15.0: the front page (OfficeWeek.cs)
                    case "press": PressListSection(); break;
                    case "transfers": WhispersSection(); DealsSection(); break;
                    case "room": DressingRoomSection(); break;
                    case "terraces": TerracesSection(); break;
                    case "cuttings": CuttingsSection(); break;
                    case "settings": SettingsSection(); VoicesSection(); MemorySection(true); break;
                }
                if (TabNow == "press") Body.Add(Text("Changes apply from the next question. Bans are enforced by your press officer with a \"no comment\", which goes on the record as yours.", 11, Quiet));
            }
            catch (Exception e) { PressRoom.Fail("office refresh", e); }
        }

        static bool Choosing;
        static void HeadSection()
        {
            var box = Box();
            if (Mode != Approach.None && !Choosing)
            {
                Candidate c = null; foreach (var x in Candidates) if (x.A == Mode) c = x;
                box.Add(Text("HEAD OF MEDIA", 11, Quiet));
                box.Add(Text(HeadName + " · " + (c?.Title ?? Mode.ToString()), 14, Ink));
                if (c != null) box.Add(Text(c.Bio, 11.5f, Quiet));
                if (Desk.HeadLocked) box.Add(Text(Desk.HeadLockLine(), 11, Quiet));
                else { var r = Row(); r.Add(Btn("Replace", () => { Choosing = true; Refresh(); }, Grey)); box.Add(r); }
                Body.Add(box);
                return;
            }
            box.Add(Text(Mode == Approach.None ? "APPOINT A HEAD OF MEDIA" : "REPLACE YOUR HEAD OF MEDIA", 11, Quiet));
            box.Add(Text("They run the press office: who gets in, how the room is handled, the jobs they put on your desk and what each action costs.", 11.5f, Quiet));
            if (Mode != Approach.None) box.Add(Text("A change costs the rest of this week's time, and the next change can't come for 4 weeks.", 11, Gold));
            Body.Add(box);
            foreach (var c in Candidates)
            {
                var cb = Box();
                cb.Add(Text(c.Name + " · " + c.Title, 13.5f, Ink));
                cb.Add(Text(c.Bio, 11.5f, Quiet));
                cb.Add(Text(c.A == Approach.OpenDoor ? "Bans cost 2 hours; keeping a whisper quiet or closing ranks is harder." : c.A == Approach.Gatekeeper ? "Bans cost 1 hour and up to 2 outlets can be banned; keeping quiet is free; planting costs 2 hours." : "Planting is safer; keeping things quiet is harder.", 11, Quiet));
                var cc = c;
                var r = Row();
                if (Mode == cc.A) r.Add(Btn("Keep", () => { Choosing = false; Refresh(); }, Green));
                else r.Add(Btn("Appoint", () =>
                {
                    bool first = Mode == Approach.None;
                    HeadName = cc.Name; Mode = cc.A; HeadSince = PressRoom.Record.Conf; Choosing = false;
                    foreach (var m in Missions) if (m.State == "offered") m.State = "withdrawn";
                    LastNews = cc.Name + " starts as head of media.";
                    Desk.OnHeadChanged(first);
                    Save(); PressRoom.W("office", "appointed", cc.Name + " " + cc.A); Refresh();
                }, Green));
                if (Mode != Approach.None) r.Add(Btn("Cancel", () => { Choosing = false; Refresh(); }, Grey));
                cb.Add(r);
                Body.Add(cb);
            }
        }

        static void DeskSection()
        {
            var box = Box();
            box.Add(Text("ON YOUR DESK", 11, Quiet));
            int shown = 0;
            foreach (var m in Missions)
            {
                if (m.State != "offered" && m.State != "active") continue;
                shown++;
                var mb = new VisualElement(); mb.style.marginTop = 4; mb.style.marginBottom = 4;
                mb.Add(Text(Title(m) + (m.State == "active" ? "  (" + m.Progress + "/" + m.Goal + ")" : ""), 13, Ink));
                mb.Add(Text(Brief(m), 11.5f, Quiet));
                mb.Add(MissionActs(m));
                box.Add(mb);
            }
            if (shown == 0) box.Add(Text("Nothing for now. Your head of media will bring you jobs as the season goes.", 11.5f, Quiet));
            Body.Add(box);
        }

        // what this week's time went on
        // 0.13.3: press conferences you sent your assistant to
        static void NoShowSection()
        {
            var line = NoShow.Summary(); if (line == null) return;
            var box = Box();
            box.Add(Text("PRESS CONFERENCES MISSED", 11, Quiet));
            box.Add(Text(line, 11.5f, NoShow.Recent() >= 3 ? RedText : NoShow.Recent() == 2 ? Gold : Ink));
            if (NoShow.Owed) box.Add(Text("Someone who was stood up will mention it at your next press conference.", 11, Quiet));
            Body.Add(box);
        }

        static void SpendSection()
        {
            var box = Box();
            box.Add(Text("THIS WEEK'S TIME", 11, Quiet));
            box.Add(Text("Your press office has " + Desk.Hours + " hours a week. Looking is free, and ordinary chats and press conferences cost nothing here. Unused time is lost at the end of the week." + (Desk.DateKnown ? "" : " (The office hasn't read the date yet, so it counts the days as they pass.)"), 11, Quiet));
            int w = Desk.Week, n = 0;
            foreach (var s in Desk.Spent)
            {
                if (s.Week != w) continue;
                n++;
                box.Add(Text("• " + char.ToUpperInvariant(s.Action[0]) + s.Action.Substring(1) + (s.Target.Length > 0 && !s.Action.EndsWith(s.Target) ? " " + s.Target : "") + " (" + s.Hours + "h)" + (s.Outcome.Length > 0 ? ": " + s.Outcome : ""), 11.5f, Ink));
            }
            if (n == 0) box.Add(Text("Nothing spent yet this week. Actions live on the Press, Transfers and Dressing room tabs.", 11.5f, Quiet));
            Body.Add(box);
        }

        static string Reason(Pack.J j)
        {
            if (j.Grievance.Length == 0 || j.GrievanceConf < PressRoom.Record.Conf - 6) return "";
            if (j.Grievance.StartsWith("jealous:")) return " (jealous of " + j.Grievance.Substring(8) + ")";
            switch (j.Grievance)
            {
                case "bored": return " (bored of the stock answers)";
                case "evasive": return " (wants straighter answers)";
                case "slight": return " (you had a go at their club)";
                case "nocomment": return " (you gave them no comment)";
                case "storm": return " (you walked out on them)";
                case "noshow": return " (you sent your assistant)";
                case "cutshort": return " (you cut their question short)";
                case "ban": return " (you shut them out)";
                case "uturn": return " (they saw your U-turn)";
                case "rebuffed": return " (turned down your olive branch)";
            }
            return "";
        }
        static void PressListSection()
        {
            var box = Box();
            box.Add(Text("PRESS LIST", 11, Quiet));
            if (Pack.On)
            {
                int p = Pack.PressureAt(PressRoom.Record.Conf, true, out var why);
                box.Add(Text("Pressure on you: " + Pack.PressureBand(p) + (why.Length > 0 ? " (" + why + ")" : "") + ".", 12, p >= 4 ? RedText : p >= 2 ? Gold : Ink));
                box.Add(Text("Each journalist has a character and wants something. How you get on depends on what you say, your results and what you've done, and slides back to their normal over time.", 11, Quiet));
            }
            if (Mode == Approach.None) box.Add(Text("Appoint a head of media to set who is welcome, favoured or banned.", 11.5f, Quiet));
            else box.Add(Text("Favoured " + Desk.Count("favoured") + " of " + Desk.MaxFavoured + "  ·  banned " + Desk.Count("banned") + " of " + Desk.MaxBanned + "  ·  " + (Desk.LastStatusWeek == Desk.Week ? "this week's status change is used" : "one status change available this week") + ". A ban lasts at least 2 press conferences.", 11, Ink));
            var outlets = Outlets();
            if (outlets.Count == 0) box.Add(Text("No press conferences on the record yet.", 11.5f, Quiet));
            int n = 0;
            foreach (var o in outlets)
            {
                if (n++ >= 16) break;
                int r = OutletRel(o);
                var row = Item();
                var top = Row(); top.style.justifyContent = Justify.SpaceBetween;
                var name = Text(o.Outlet, 13, Ink); name.style.flexGrow = 1; name.style.flexShrink = 1; top.Add(name);
                var band = Text(Band(r), 12, BandColour(r)); band.style.marginRight = 6; top.Add(band);
                var s = StatusOf(o.Outlet); var oo = o.Outlet;
                if (s != "welcome") top.Add(Pill(s.ToUpperInvariant(), s == "banned" ? RedPill : SiPill));
                row.Add(top);
                var who = o.Journalists.Count == 0 ? "" : string.Join(", ", o.Journalists);
                var line = (who.Length > 0 ? who + " · " : "") + o.Answers + (o.Answers == 1 ? " answer" : " answers");
                if (o.NoComments > 0) line += " · " + o.NoComments + " no comment" + (o.NoComments == 1 ? "" : "s");
                if (o.Storms > 0) line += " · " + o.Storms + " walk-out" + (o.Storms == 1 ? "" : "s");
                if (o.Bans > 0) line += " · cut off " + o.Bans + (o.Bans == 1 ? " time" : " times");
                row.Add(Text(line, 11, Quiet));
                if (Pack.On)
                    foreach (var jn in o.Journalists)
                    {
                        var pj = Pack.Get(jn); if (pj == null) continue;
                        int jr = Rel(jn);
                        var jl = jn + ": " + Pack.TraitName(pj.T).ToLowerInvariant() + ", wants " + Pack.Wants(pj) + ". " + Pack.Band(jr) + (jr <= -6 ? ", has it in for you" : "") + Reason(pj) + ".";
                        row.Add(Text(jl, 11, BandColour(jr)));
                    }
                if (Mode != Approach.None)
                {
                    var acts = Row();
                    if (s == "welcome")
                    {
                        acts.Add(Act("Favour", "favour", oo, Desk.Risk.None, false, Navy, () => { SetStatus(oo, "favoured"); Desk.Charge(Desk.Cost("favour"), "favoured", oo, ""); }));
                        acts.Add(Act("Ban", "ban", oo, Desk.Risk.None, true, BanRed, () => { SetStatus(oo, "banned"); Desk.Charge(Desk.Cost("ban"), "banned", oo, "their questions get \"no comment\" from your press officer"); }));
                    }
                    else if (s == "favoured") acts.Add(Act("Back to welcome", "lift", oo, Desk.Risk.None, false, Grey, () => { SetStatus(oo, "welcome"); Desk.Charge(0, "stopped favouring", oo, ""); }));
                    else acts.Add(Act("Lift the ban", "lift", oo, Desk.Risk.None, false, Grey, () => { SetStatus(oo, "welcome"); Desk.Charge(0, "lifted the ban on", oo, ""); }));
                    row.Add(acts);
                }
                box.Add(row);
            }
            Body.Add(box);
        }

        // ---------- the cuttings (0.13): everything on the record, newest first ----------
        // 0.13.2: filters are buttons, not a text box: FM's own hotkeys take every key typed into a field.
        static string CutWho = "", CutKind = "all";
        static void CuttingsSection()
        {
            var box = Box();
            box.Add(Text("THE CUTTINGS", 11, Quiet));
            box.Add(Text(Desk.Totals(), 12, Ink));
            var kinds = Row(); kinds.style.marginTop = 4;
            foreach (var (k, label) in new[] { ("all", "Everything"), ("said", "Your answers"), ("refused", "No comments, walk-outs and no-shows"), ("office", "Office actions"), ("leaks", "Leaks and plants") })
            { var kk = k; kinds.Add(Btn(label, () => { CutKind = kk; Refresh(); }, CutKind == k ? Navy : Grey)); }
            box.Add(kinds);
            var outs = Desk.TopNames(false, 8); var plys = Desk.TopNames(true, 8);
            if (outs.Count > 0)
            {
                box.Add(Text("Outlets", 11, Quiet));
                var r = Row(); foreach (var o in outs) { var oo = o; r.Add(Btn(o, () => { CutWho = CutWho == oo ? "" : oo; Refresh(); }, CutWho == o ? Navy : Grey)); } box.Add(r);
            }
            if (plys.Count > 0)
            {
                box.Add(Text("People named", 11, Quiet));
                var r = Row(); foreach (var p in plys) { var pp = p; r.Add(Btn(p, () => { CutWho = CutWho == pp ? "" : pp; Refresh(); }, CutWho == p ? Navy : Grey)); } box.Add(r);
            }
            if (CutWho.Length > 0) { var r = Row(); r.Add(Btn("Clear: " + CutWho, () => { CutWho = ""; Refresh(); }, Grey)); box.Add(r); }
            box.Add(Text("The newest 40 that match are shown.", 11, Quiet));
            Body.Add(box);
            var list = Desk.Cuttings(CutWho, CutKind, 40);
            var lb = Box();
            if (list.Count == 0) lb.Add(Text("Nothing on the record matches.", 11.5f, Quiet));
            foreach (var c in list)
            {
                var it = Item();
                it.Add(Text(c.Kind + (c.Who.Length > 0 ? "  \u00B7  " + c.Who : "") + "  \u00B7  conference " + c.Conf + (c.When.Length > 0 ? ", " + c.When : ""), 11, Quiet));
                it.Add(Text(c.Text, 11.5f, Ink));
                lb.Add(it);
            }
            Body.Add(lb);
        }

        // ---------- settings (0.13) ----------
        static void SettingsSection()
        {
            var box = Box();
            box.Add(Text("SETTINGS", 11, Quiet));
            var r = Row();
            r.Add(Btn("Risk bands: " + (Desk.RiskBands ? "shown" : "hidden"), () => { Desk.RiskBands = !Desk.RiskBands; PressRoom.SaveOfficeSettings(); Refresh(); }, Grey));
            r.Add(Btn("SI / Story badges: " + (Desk.Badges ? "shown" : "hidden"), () => { Desk.Badges = !Desk.Badges; PressRoom.SaveOfficeSettings(); Refresh(); }, Grey));
            box.Add(r);
            box.Add(Text("SI means the game itself acts (a ban's \"no comment\", for example). Story means the mod's own world reacts: journalists, the papers, the fans. The mod never changes the game's numbers.", 11, Quiet));
            box.Add(Text("Weekly time, caps and the rest are in BepInEx\\config\\uk.nik.fm27mediamod.pressroom.cfg under [Media Office].", 11, Quiet));
            box.Add(Text("Press Room " + PressRoom.Version + ". " + (Desk.DateKnown ? "Date read: " + Desk.Today.ToString("ddd d MMM yyyy") + "." : "The date hasn't been read yet; weeks are counted from the days as they pass."), 11, Quiet));
            Body.Add(box);
        }
    }
}
