// FM27 Media Mod - Press Room 0.15.0: the Press Office's front page, "This week" (the approved sketch: Nik, 23 Nov, "This is the way").
// RIGHT NOW: the pressure on you, the next press conference, the office's time and the jobs on your desk.
// FROM YOUR HEAD OF MEDIA: the stories that could come up, the room, the fans and the dressing room, each with what's out there,
// what the head of media would do (the same actions, costs and risks as the other tabs) and why. Then where the time went.
// Every card is drawn on its own, so one that fails can't blank the page (the Treatment Room's lesson from 0.6.0).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static partial class MediaOffice
    {
        static void Part(System.Action build, string what)
        {
            try { build(); }
            catch (Exception e) { PressRoom.Fail("office " + what, e); Body.Add(L("This part could not be drawn (" + e.GetType().Name + "). The Press Room log has the details.", 13, Text2, false, 12)); }
        }

        static void WeekView()
        {
            if (Choosing) { HeadSection(); return; }
            if (Mode != Approach.None) Part(OfferMissions, "missions");
            Part(NoShowCard, "missed conferences");
            Part(RightNow, "right now");
            if (Mode == Approach.None) { Part(HeadSection, "head of media"); return; }
            Part(() =>
            {
                Body.Add(Caps("FROM YOUR HEAD OF MEDIA", Text2, 4));
                Body.Add(L("What could come up at the next press conference, who is in what mood, and what " + First(HeadName) + " would do about it. Each action says what it costs from this week's hours"
                    + (Desk.RiskBands ? " and how likely it is to go wrong." : "."), 13, Text2, false, 14));
            }, "intro");
            Part(StoryCards, "stories");
            Part(RoomCard, "the room");
            Part(FansCard, "the fans");
            Part(DressingRoomCard, "the dressing room");
            Part(TimeCard, "the time");
            Part(HeadSection, "head of media");
        }

        static string First(string name) { var n = (name ?? "").Trim(); int i = n.IndexOf(' '); return i > 0 ? n.Substring(0, i) : n.Length > 0 ? n : "your head of media"; }
        static string Ordinal(int n) { int t = n % 100; string s = t >= 11 && t <= 13 ? "th" : (n % 10) == 1 ? "st" : (n % 10) == 2 ? "nd" : (n % 10) == 3 ? "rd" : "th"; return n + s; }
        static string Clip1(string s, int n) { s = (s ?? "").Trim(); return s.Length <= n ? s : s.Substring(0, n - 1).TrimEnd() + "…"; }
        static VisualElement Tile(float grow = 1f, bool last = false)
        {
            var t = Panel(CardBg, Edge, 12); Pad(t, 14, 16); t.style.flexGrow = grow; t.style.flexShrink = 1; t.style.flexBasis = 0;
            if (!last) t.style.marginRight = 12;
            return t;
        }
        static VisualElement Sub2(string caps, string text)
        {
            var v = new VisualElement(); v.style.marginBottom = 12;
            v.Add(Caps(caps, Text2, 4));
            if (text.Length > 0) v.Add(L(text, 14, Text1));
            return v;
        }

        // ---------- RIGHT NOW ----------
        static void RightNow()
        {
            Body.Add(Caps("RIGHT NOW", Text2, 8));
            var row = Flex(true); row.style.alignItems = Align.Stretch; row.style.marginBottom = 22;
            row.Add(PressureTile());
            row.Add(NextTile());
            row.Add(TimeTile(Mode == Approach.None));
            if (Mode != Approach.None) row.Add(DeskTile());
            Body.Add(row);
        }

        static VisualElement PressureTile()
        {
            var t = Tile();
            t.Add(Caps("PRESSURE ON YOU", Text2, 8));
            if (!Pack.On) { t.Add(Disp(L("Not tracked", 22, Text1, true, 6))); t.Add(L("The press pack is switched off in the config.", 13, Text2)); return t; }
            int p = Pack.PressureAt(PressRoom.Record.Conf, true, out var why);
            var band = Pack.PressureBand(p);
            t.Add(Disp(L(band, 24, band == "Intense" ? RedLight : band == "High" ? Orange : band == "Rising" ? OrangeLight : Text1, true, 8)));
            var last = new List<Pack.Result>();
            for (int i = Pack.Results.Count - 1; i >= 0 && last.Count < 5; i--) last.Add(Pack.Results[i]);
            last.Reverse();
            if (last.Count > 0)
            {
                var f = Flex(true); f.style.marginBottom = 8;
                foreach (var r in last)
                {
                    var c = r.R == 'W' ? Chip("W", WinBg, WinFg) : r.R == 'D' ? Chip("D", IncBg, Text1) : Chip("L", LossBg, LossFg);
                    c.style.marginRight = 4; c.style.width = 28; c.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(c, 3, 0);
                    f.Add(c);
                }
                t.Add(f);
            }
            var bits = new List<string>();
            int pos = GameFacts.Int("position");
            if (pos > 0) bits.Add(pos == 1 ? "top of the league" : Ordinal(pos) + " in the league");
            if (!string.IsNullOrEmpty(why)) bits.Add(why);
            var line = bits.Count > 0 ? Cap(string.Join("; ", bits)) + "." : "";
            if (last.Count > 0)
            {
                var lr = last[last.Count - 1];
                line += (line.Length > 0 ? " " : "") + "Latest: " + lr.For + "-" + lr.Against + (lr.Opponent.Length > 0 ? " v " + lr.Opponent : "") + ".";
            }
            if (line.Length > 0) t.Add(L(line, 13, Text2));
            return t;
        }

        // the journalist to watch: the coolest towards you, or the one with a fresh grievance
        static Pack.J WatchPick()
        {
            if (!Pack.On) return null;
            int conf = PressRoom.Record.Conf;
            Pack.J best = null; int low = int.MaxValue;
            foreach (var j in Pack.All())
            {
                if (string.IsNullOrEmpty(j.Name) || StatusOf(j.Outlet) == "banned") continue;
                int r = Rel(j.Name);
                bool sore = j.Grievance.Length > 0 && j.GrievanceConf >= conf - 6;
                if (r > -3 && !sore) continue;
                int score = r - (sore ? 2 : 0);
                if (score < low) { low = score; best = j; }
            }
            return best;
        }

        static VisualElement NextTile()
        {
            var t = Tile();
            t.Add(Caps("NEXT PRESS CONFERENCE", Text2, 8));
            var opp = GameFacts.Get("next_opponent");
            t.Add(Disp(L(opp.Length > 0 ? opp : "Not known yet", 22, Text1, true, 8)));
            var w = WatchPick();
            if (w != null)
            {
                var l = L("Watch: " + w.Outlet + ". " + w.Name + " wants " + Pack.Wants(w) + Reason(w) + ".", 13, Text2);
                t.Add(l);
            }
            else if (Pack.On) t.Add(L("Nobody in the room has it in for you.", 13, Text2));
            if (opp.Length == 0) t.Add(L("The next match hasn't been read yet.", 12.5f, Text2));
            return t;
        }

        static VisualElement TimeTile(bool none)
        {
            var t = Tile(1f, none);
            t.Add(Caps("YOUR OFFICE'S TIME", Text2, 8));
            if (none) { t.Add(Disp(L("No office yet", 22, Text1, true, 6))); t.Add(L("Appoint a head of media below: they run the press office and bring you jobs.", 13, Text2)); return t; }
            var big = Flex(true); big.style.alignItems = Align.FlexEnd; big.style.marginBottom = 8;
            big.Add(Disp(L(Desk.Left.ToString(), 28, Text1, true)));
            var of = L("  of " + Desk.Hours + " hours", 14, Text2); of.style.marginBottom = 4; big.Add(of);
            t.Add(big);
            var track = new VisualElement(); track.style.height = 8; track.style.backgroundColor = Track; Radius(track, 4); track.style.marginBottom = 10;
            var fill = new VisualElement(); fill.style.height = 8; Radius(fill, 4); fill.style.backgroundColor = Orange;
            fill.style.width = new Length(Desk.Hours > 0 ? Math.Max(0f, Math.Min(100f, 100f * Desk.Left / Desk.Hours)) : 0f, LengthUnit.Percent);
            track.Add(fill); t.Add(track);
            t.Add(L("Looking is free; actions cost hours. What's left is lost at the end of the week." + (Desk.DateKnown ? "" : " (The office hasn't read the date yet.)"), 13, Text2));
            return t;
        }

        static VisualElement DeskTile()
        {
            var t = Tile(1.4f, true);
            var open = Missions.Where(m => m.State == "offered" || m.State == "active").ToList();
            t.Add(Caps("ON YOUR DESK" + (open.Count > 0 ? " (" + open.Count + ")" : ""), Text2, 8));
            foreach (var m in open)
            {
                var mb = new VisualElement(); mb.style.marginBottom = 10;
                mb.Add(L(Title(m) + (m.State == "active" ? "  (" + m.Progress + "/" + m.Goal + ")" : ""), 14, Text1, true, 2));
                mb.Add(L(Brief(m), 13, Text2, false, 4));
                mb.Add(MissionActs(m));
                t.Add(mb);
            }
            if (open.Count == 0) t.Add(L("Nothing for now. " + First(HeadName) + " will bring you jobs as the season goes.", 13, Text2));
            return t;
        }

        static void NoShowCard()
        {
            var line = NoShow.Summary(); if (line == null) return;
            var n = Panel(AlertBg, AlertEdge, 10); Pad(n, 12, 16); n.style.marginBottom = 14;
            n.Add(Caps("PRESS CONFERENCES MISSED", OrangeLight, 4));
            n.Add(L(line + (NoShow.Owed ? " Someone who was stood up will mention it at your next press conference." : ""), 14, OrangePale));
            Body.Add(n);
        }

        // ---------- the stories that could come up ----------
        sealed class Story { public string Player = ""; public Deals.Deal D; public Word.Whisper W; public int Weight; }

        static List<Story> LiveStories()
        {
            int conf = PressRoom.Record.Conf;
            var by = new Dictionary<string, Story>(StringComparer.OrdinalIgnoreCase);
            Story Of(string p) { if (!by.TryGetValue(p, out var s)) { s = new Story { Player = p }; by[p] = s; } return s; }
            // deals, as the Transfers tab keeps them
            foreach (var d in Deals.All.Values)
            {
                if (string.IsNullOrEmpty(d.Player)) continue;
                if (d.Heat == 0 && conf - d.LastConf > 2) continue;
                if (d.Signals.Contains("off") && conf - d.LastConf > 0) continue;
                if (d.Signals.Contains("done") && conf - d.LastConf > 1) continue;
                var s = Of(d.Player); s.D = d; s.Weight += 2 * Math.Max(1, d.Heat);
            }
            // whispers not yet out, planted, or just out
            for (int i = Word.All.Count - 1; i >= 0; i--)
            {
                var w = Word.All[i];
                if (string.IsNullOrEmpty(w.Player)) continue;
                bool live = w.State == "planted" || ((w.State == "new" || w.State == "out") && conf - w.Conf <= 3);
                if (!live) continue;
                var s = Of(w.Player); if (s.W != null) continue;
                s.W = w; s.Weight += w.State == "planted" ? 8 : w.State == "new" ? 6 : 4;
            }
            return by.Values.OrderByDescending(x => x.Weight).Take(3).ToList();
        }

        static string StoryTitle(Story s)
        {
            if (s.D != null)
            {
                var d = s.D;
                if (d.Signals.Contains("done")) return d.Side == "renewing" ? s.Player + " signs a new contract" : s.Player + ": done deal";
                return d.Side == "renewing" ? s.Player + "'s new contract" : d.Side == "selling" ? "Selling " + s.Player : d.Side == "buying" ? "Signing " + s.Player : "Talks over " + s.Player;
            }
            switch (s.W.Kind)
            {
                case "renewal": return s.Player + "'s new contract";
                case "selling": case "pushout": return s.Player + "'s future";
                case "interest": return "Interest in " + s.Player;
                default: return "Talks over " + s.Player;
            }
        }

        static void StoryCards()
        {
            var list = LiveStories();
            if (list.Count == 0)
            {
                var (card, inner) = Shell("Transfer talk", "Quiet", IncBg, Text1, "", TabLink("Transfers", "transfers"));
                inner.Add(L("No deal or whisper is likely to come up at the next press conference. When you talk to an agent, your director of football or a player about a move, it appears here first.", 14, Text1));
                Body.Add(card);
                return;
            }
            foreach (var s in list) Part(() => StoryCard(s), "story " + s.Player);
        }

        static void StoryCard(Story s)
        {
            var d = s.D; var w = s.W;
            bool likely = (w != null && (w.State == "planted" || w.State == "out")) || (d != null && d.Heat >= 4);
            bool quiet = d != null && d.Quiet;
            var chip = quiet ? "Kept quiet" : likely ? "Likely to come up" : "Could come up";
            var (card, inner) = Shell(StoryTitle(s), chip, likely ? OrangeDark : IncBg, likely ? OrangePale : Text1, "", TabLink("Transfers", "transfers"));

            // what's out there
            var out1 = new List<string>();
            if (w != null)
            {
                var f = Word.Describe(w);
                string where = w.State == "planted" ? " Planted with " + w.Outlet + "." : w.State == "out" ? " It's out" + (w.Outlet.Length > 0 ? ": " + w.Outlet + " ran it." : ".") : " Not out yet" + (w.With.Length > 0 ? ": said to " + w.With + "." : ".");
                out1.Add(f + "." + where);
            }
            if (d != null)
            {
                var known = new List<string>();
                foreach (var sg in d.Signals) { var tx = Deals.Describe(sg); if (tx != null) known.Add(tx); }
                if (d.Cost.Length > 0) known.Add(d.Cost + " out of the transfer budget");
                if (d.Agent.Length > 0 && known.Count == 0) known.Add("talks with " + d.Agent);
                if (known.Count > 0) out1.Add(Cap(string.Join("; ", known)) + ".");
                if (d.Brief == "pending") out1.Add("Your briefing goes out at the next press conference.");
                if (d.LastLine.Length > 0) out1.Add("In the papers: “" + d.LastLine + "”");
            }
            inner.Add(Sub2("WHAT'S OUT THERE", string.Join(" ", out1)));

            // who will ask
            var ask = new List<string>();
            if (w != null && w.State == "planted" && w.Outlet.Length > 0)
                ask.Add(w.Outlet + " will run it" + (StatusOf(w.Outlet) == "favoured" ? "; they're favoured and on side" : "") + ".");
            var watch = WatchPick();
            if (watch != null && (w == null || watch.Outlet != w.Outlet)) ask.Add(watch.Name + " (" + watch.Outlet + ") is the one most likely to push: wants " + Pack.Wants(watch) + ".");
            if (ask.Count == 0) ask.Add("Any outlet in the room could raise it.");
            inner.Add(Sub2("WHO WILL ASK", string.Join(" ", ask)));

            // what we'd do: advice that matches what the mod does, then the same actions as the Transfers tab
            var advice = new List<string>();
            if (w != null && w.State == "new") advice.Add("It isn't out yet. Plant it to have it told your way, or keep it quiet.");
            else if (w != null && w.State == "planted") advice.Add("It's with " + w.Outlet + ". Back up the line when they ask.");
            if (d != null && !d.Signals.Contains("done"))
            {
                if (d.Side == "selling") advice.Add("Don't say " + s.Player + " is staying unless you mean it: if the sale goes through, the press call it a U-turn.");
                else if (d.Side == "buying") advice.Add("Don't rule out a move for " + s.Player + " if you might make it: if the deal goes through, the press call it a U-turn.");
                else if (d.Side == "renewing") advice.Add("Say talks are ongoing and leave it there.");
            }
            if (d != null && d.Signals.Contains("done")) advice.Add("It's done: there's nothing to hide, and the press will want the story.");
            var wd = Caps("WHAT WE'D DO", Text2, 4);
            inner.Add(wd);
            if (advice.Count > 0) inner.Add(L(string.Join(" ", advice), 14, Text1, false, 6));
            var acts = new VisualElement();
            if (w != null) { var a = WhisperActs(w); if (a != null) acts.Add(a); }
            if (d != null) { var a = DealActs(d); if (a != null) acts.Add(a); }
            if (acts.childCount > 0) inner.Add(acts);
            else if (advice.Count == 0) inner.Add(L("Nothing to do for now.", 14, Text2));
            inner.Add(Foot(WhyLine()));
            Body.Add(card);
        }

        // why the head of media works this way: their own brief, and what each move costs under them
        static string WhyLine()
        {
            Candidate c = null; foreach (var x in Candidates) if (x.A == Mode) c = x;
            var costs = "Under " + First(HeadName) + ", planting a story costs " + Hours(Desk.Cost("plant")) + ", keeping one quiet " + Hours(Desk.Cost("hush")) + " and briefing back " + Hours(Desk.Cost("brief")) + ".";
            return (c != null ? "How " + First(HeadName) + " works: " + c.Bio + " " : "") + costs;
        }
        static string Hours(int h) => h <= 0 ? "nothing" : h == 1 ? "1 hour" : h + " hours";

        // ---------- the room ----------
        static string MoodWord(int r) => r >= 6 ? "Friendly" : r >= 3 ? "Warm" : r >= -2 ? "Neutral" : r >= -5 ? "Cool" : "Hostile";
        static void RoomCard()
        {
            if (!Pack.On) return;
            int conf = PressRoom.Record.Conf;
            var all = Pack.All().Where(j => !string.IsNullOrEmpty(j.Name)).ToList();
            var shown = all.Where(j => Rel(j.Name) != 0 || (j.Grievance.Length > 0 && j.GrievanceConf >= conf - 6)).OrderBy(j => Rel(j.Name)).Take(6).ToList();
            int cooler = all.Count(j => Rel(j.Name) <= -3), warmer = all.Count(j => Rel(j.Name) >= 3);
            var chip = cooler + warmer == 0 ? "All settled" : (cooler > 0 ? cooler + " cooler" : "") + (cooler > 0 && warmer > 0 ? ", " : "") + (warmer > 0 ? warmer + " warmer" : "");
            var (card, inner) = Shell("The room", chip, cooler > 0 ? LossBg : IncBg, cooler > 0 ? LossFg : Text1, "", TabLink("The press", "press"));
            if (shown.Count == 0) inner.Add(L("Nobody in the press pack is warmer or cooler than usual.", 14, Text1, false, 8));
            else
            {
                var head = Flex(true); head.style.paddingBottom = 6; head.style.borderBottomWidth = 1; head.style.borderBottomColor = RowRule;
                head.Add(Cell(Caps("JOURNALIST", Text2, 0), 1.2f)); head.Add(Cell(Caps("OUTLET", Text2, 0), 1.2f)); head.Add(Cell(Caps("MOOD", Text2, 0), 0.7f)); head.Add(Cell(Caps("WHY", Text2, 0), 2f));
                inner.Add(head);
                foreach (var j in shown)
                {
                    int r = Rel(j.Name);
                    var row = Flex(true); Pad(row, 8, 0); row.style.borderBottomWidth = 1; row.style.borderBottomColor = RowRule;
                    row.Add(Cell(L(j.Name, 14, Text1, true), 1.2f));
                    row.Add(Cell(L(j.Outlet, 13, Text2), 1.2f));
                    var mc = new VisualElement(); mc.Add(MoodChip(r)); row.Add(Cell(mc, 0.7f));
                    var why = Reason(j).Trim(); if (why.StartsWith("(") && why.EndsWith(")")) why = why.Substring(1, why.Length - 2);
                    row.Add(Cell(L(why.Length > 0 ? Cap(why) : "Wants " + Pack.Wants(j), 13, Text2), 2f));
                    inner.Add(row);
                }
                int rest = all.Count - shown.Count;
                if (rest > 0) inner.Add(L("The rest of the room (" + rest + "): as usual.", 13, Text2, false, 4));
            }
            inner.Add(Spacer(10));
            inner.Add(Caps("WHAT WE'D DO", Text2, 4));
            var w = WatchPick();
            if (w != null)
            {
                inner.Add(L("Give " + w.Name + " a straight answer early: " + w.Name + " wants " + Pack.Wants(w) + ".", 14, Text1, false, 6));
                var bridge = Missions.FirstOrDefault(m => m.Kind == "bridge" && m.Target == w.Name && (m.State == "offered" || m.State == "active"));
                if (bridge != null) inner.Add(L(bridge.State == "active" ? "The job on your desk to mend fences is under way." : "Or take on the job on your desk: " + Title(bridge) + ".", 13, Text2, false, 4));
                var o = w.Outlet;
                if (StatusOf(o) == "welcome")
                {
                    var acts = Row();
                    acts.Add(Act("Favour " + o, "favour", o, Desk.Risk.None, false, Navy, () => { SetStatus(o, "favoured"); Desk.Charge(Desk.Cost("favour"), "favoured", o, ""); }));
                    inner.Add(acts);
                }
                inner.Add(Foot("Favouring one outlet makes the insiders, the red tops and the phone-ins at the others jealous."));
            }
            else inner.Add(L("Nobody needs handling. Keep answering straight.", 14, Text1));
            Body.Add(card);
        }

        // ---------- the fans ----------
        static int FansMood(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("could not be happier") || s.Contains("ecstatic") || s.Contains("jubilant")) return 3;
            if (s.Contains("furious") || s.Contains("angry") || s.Contains("unhappy")) return -2;
            if (s.Contains("disappointed") || s.Contains("concerned")) return -1;
            if (s.Contains("delighted") || s.Contains("very pleased")) return 2;
            if (s.Contains("pleased") || s.Contains("happy")) return 1;
            return 0;
        }
        static void FansCard()
        {
            FanPress.Fresh();
            if (FanPress.Group.Length == 0 && FanPress.Spokes.Length == 0)
            {
                var (c0, i0) = Shell("The fans", "Not read yet", IncBg, Text1, "", TabLink("Terraces", "terraces"));
                i0.Add(L("Open Club > Supporters once, and the office reads what the supporters think.", 14, Text1));
                Body.Add(c0); return;
            }
            int m = FansMood(FanPress.Overall);
            var word = m >= 3 ? "Ecstatic" : m == 2 ? "Delighted" : m == 1 ? "Pleased" : m == -1 ? "Disappointed" : m <= -2 ? "Unhappy" : "Content";
            var (card, inner) = Shell("The fans", word, m >= 1 ? WinBg : m <= -1 ? LossBg : IncBg, m >= 1 ? WinFg : m <= -1 ? LossFg : Text1, "", TabLink("Terraces", "terraces"));
            var said = new List<string>();
            if (FanPress.Overall.Length > 0) said.Add(FanPress.Overall.TrimEnd('.') + ".");
            var crits = FanPress.Crits.Take(3).Select(c => c.TrimEnd('.')).ToList();
            if (crits.Count > 0) said.Add("Still sore about: " + string.Join("; ", crits) + ".");
            var highs = FanPress.Highs.Take(3).Select(h => h.TrimEnd('.')).ToList();
            if (highs.Count > 0) said.Add("Happy with: " + string.Join("; ", highs) + ".");
            inner.Add(L(string.Join(" ", said), 14, Text1, false, 10));
            inner.Add(Caps("WHAT WE'D DO", Text2, 4));
            var adv = m >= 2 ? "Thank them while it's going well." : m <= -1 ? "Own it when they come up: they know when things aren't right." : "Keep them on side.";
            if (crits.Count > 0) adv += " If the supporters' view comes up, it will be one of their grievances.";
            inner.Add(L(adv, 14, Text1));
            var chance = FanPress.Influence == "Very High" ? 60 : FanPress.Influence == "High" ? 45 : FanPress.Influence == "Medium" ? 30 : 15;
            inner.Add(Foot((FanPress.Influence.Length > 0 ? "The supporters' influence on the board is " + FanPress.Influence + ", so the" : "The") + " press bring their view into about " + chance + "% of press conferences, each grievance once."));
            Body.Add(card);
        }

        // ---------- the dressing room ----------
        static string Who(Reactions.Row r) { var w = (r.With ?? "").Trim(); if (w.StartsWith("Player ")) w = w.Substring(7); return w.Length > 0 ? w : "Team meeting"; }
        static void DressingRoomCard()
        {
            int now = PressRoom.Record.Conf;
            var promises = Reactions.All.Where(r => r.Group == "Promise").Reverse().Take(4).ToList();
            var open = Incidents.Enabled ? Incidents.All.Where(x => x.State == "open" || x.State == "inhouse").Reverse().Take(4).ToList() : new List<Incidents.I>();
            var took = Reactions.Latest(4).ToList();
            var chip = open.Count > 0 ? open.Count + (open.Count == 1 ? " open story" : " open stories") : promises.Count > 0 ? promises.Count + (promises.Count == 1 ? " promise on record" : " promises on record") : "Settled";
            var (card, inner) = Shell("The dressing room", chip, open.Count > 0 ? LossBg : IncBg, open.Count > 0 ? LossFg : Text1, "", TabLink("Dressing room", "room"));
            if (open.Count > 0)
            {
                inner.Add(Caps("IN THE PAPERS", Text2, 4));
                foreach (var x in open)
                {
                    inner.Add(L(Incidents.Describe(x), 14, Text1, true, 2));
                    inner.Add(L(Incidents.StateLine(x, now), 13, Text2, false, 4));
                    var a = IncidentActs(x); if (a != null) inner.Add(a);
                }
                inner.Add(Spacer(8));
            }
            if (promises.Count > 0)
            {
                inner.Add(Caps("PROMISES YOU'VE MADE", Text2, 4));
                foreach (var r in promises) inner.Add(L(Who(r) + (r.Date.Length > 0 ? " (" + r.Date + ")" : "") + ": “" + Clip1(r.Line, 130) + "”", 14, Text1, false, 4));
                inner.Add(Spacer(8));
            }
            inner.Add(Caps("HOW THEY TOOK IT", Text2, 6));
            if (took.Count == 0) inner.Add(L("Nothing yet. After your next team meeting or chat, the reaction appears here.", 14, Text2));
            foreach (var r in took)
            {
                var row = Flex(true); Pad(row, 6, 0); row.style.borderBottomWidth = 1; row.style.borderBottomColor = RowRule;
                row.Add(Cell(L(r.Date, 13, Text2), 0.6f));
                row.Add(Cell(L(Who(r) + (r.Group.Length > 0 ? " · " + r.Group : ""), 14, Text1), 1.6f));
                var mc = new VisualElement();
                if (r.Reaction.Length > 0) mc.Add(Chip(r.Reaction, IncBg, Text1));
                row.Add(Cell(mc, 0.8f));
                var room = r.Room.Length > 0 ? "The room: " + r.Room + (r.Before.Length > 0 ? " (before: " + r.Before + ")" : "") : "";
                row.Add(Cell(L(room, 13, Text2), 1.6f));
                inner.Add(row);
            }
            Body.Add(card);
        }

        // ---------- where the time went ----------
        static string SpendLine(Desk.Spend s) =>
            Cap(s.Action) + (s.Target.Length > 0 && !s.Action.EndsWith(s.Target) ? " " + s.Target : "") + " (" + s.Hours + "h" + (s.Outcome.Length > 0 ? ", " + s.Outcome : "") + ")";
        static void TimeCard()
        {
            int wk = Desk.Week;
            var now = Desk.Spent.Where(s => s.Week == wk).Select(SpendLine).ToList();
            var before = Desk.Spent.Where(s => s.Week == wk - 1).Select(SpendLine).ToList();
            var box = new VisualElement(); box.style.marginTop = 6; box.style.marginBottom = 16;
            box.Add(Caps("WHERE THE TIME WENT", Text2, 6));
            box.Add(L("This week: " + (now.Count == 0 ? "nothing spent yet." : string.Join(" · ", now) + "."), 14, Text1, false, 4));
            if (before.Count > 0) box.Add(L("Last week: " + string.Join(" · ", before) + ".", 13, Text2));
            Body.Add(box);
        }
    }
}
