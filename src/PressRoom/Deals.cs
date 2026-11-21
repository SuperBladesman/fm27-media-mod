// FM27 Media Mod - Agents speak (Press Room 0.7). Personal single-player use only.
// Every deal you are working on (a signing, a sale, a contract, a player you are pushing out) builds up a file:
//   - what the agent has told you in private, in SI's own words: other clubs are interested, talks have gone on too long,
//     no discount on wages, the deal is close;
//   - what your own staff have told you: the director of football says the deal needs budget you haven't got, the
//     assistant says you are over the wage budget, the fee would take a sum out of the transfer budget;
//   - your own stance in the chats (Desperate, Keen, Contest, Stall, Threaten...);
//   - the player's personality and reputation as the game shows them.
// From that, the agent goes public: at a press conference a journalist opens with what the agent is saying. Each time it
// comes out, the pressure on that deal rises; it eases when nothing happens. The agent brings the papers up the next
// time you talk, and your head of media can brief back (a Fixer or an Open door) or keep the club silent (a Gatekeeper).
// Nothing here changes SI's numbers: fees, demands and interest are the game's own. The words only repeat what the game
// has already said, louder and in public.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Deals
    {
        internal class Deal
        {
            public string Player = "", Agent = "", Side = "", Stance = "", Cost = "", Brief = "", LastLine = "";
            public HashSet<string> Signals = new HashSet<string>(), Said = new HashSet<string>();
            public int Heat, LastConf = -1, PublicConf = -1, ChatConf = -1, BriefConf = -1;
            public bool Quiet;
        }
        internal static readonly Dictionary<string, Deal> All = new Dictionary<string, Deal>();
        internal static int SkintConf = -100, WagesConf = -100;
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "deals.tsv");
        static int Conf => PressRoom.Record.Conf;
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // ---------- what the game has said, in private ----------
        static readonly (string sig, Regex re)[] Said =
        {
            ("elsewhere", new Regex(@"interest in (?:his|her|their) services from elsewhere|rumoured interest being shown in|interest from other clubs", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("toolong",   new Regex(@"gone on for far too long", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("hardball",  new Regex(@"isn't going to significantly water down|diminish (?:his|her|their) level of interest|won't affect just how interested", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("close",     new Regex(@"very close to concluding|almost reached a deal|satisfied with these terms", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("jeopardy",  new Regex(@"could jeopardi[sz]e the deal", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("pricey",    new Regex(@"looking to pay a lot less", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("skint",     new Regex(@"would have to generate extra transfer budget", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("wages",     new Regex(@"spending over its wage budget", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("overpaid",  new Regex(@"I feel we've overpaid", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("done",      new Regex(@"We've got a deal in place to sign", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        };
        static readonly Regex CostRe = new Regex(@"deduction of (£[\d.,]+[MK]?) from our transfer budget|(£[\d.,]+[MK]?) would be deducted from our", RegexOptions.Compiled);
        static readonly HashSet<string> Strong = new HashSet<string> { "elsewhere", "toolong", "hardball", "jeopardy" };

        // a press conference is on: its questions are not private chat
        static bool InPress() => MediaOffice.Session != null && (DateTime.Now - MediaOffice.SessionAt).TotalMinutes < 5;

        // signals with no player yet wait a moment for a bubble that names one (SI sets them in the same millisecond)
        static readonly List<(string sig, string cost, DateTime at)> Waiting = new List<(string, string, DateTime)>();
        static string NearPlayer = ""; static DateTime NearAt = DateTime.MinValue;

        // which deal a line of text is about: a linked person, a known name (in full, or just a first name or surname of
        // a player already being talked about), or the player this chat is about
        static string PlayerIn(string raw)
        {
            var vis = PressRoom.Visible(raw ?? "");
            var linked = Word.PersonFull(raw, out var shown);
            if (linked != null) return Word.DisplayFor(linked);
            foreach (var d in All.Values)
            {
                var parts = d.Player.Split(' ');
                foreach (var p in parts) if (p.Length > 2 && Regex.IsMatch(vis, @"\b" + Regex.Escape(p) + @"\b")) return d.Player;
            }
            var cs = Word.ChatSubject;
            if (cs.Length > 0) foreach (var p in cs.Split(' ')) if (p.Length > 2 && Regex.IsMatch(vis, @"\b" + Regex.Escape(p) + @"\b")) return cs;
            return null;
        }

        internal static void OnBubble(VisualElement el, string txt, bool npc)
        {
            try
            {
                if (InPress() || IsOurs(txt)) return;
                if (Word.ManagerName.Length > 0 && Word.PendingWith == Word.ManagerName) return;   // you are the one in the header: a player chat or the press
                var vis = PressRoom.Visible(txt);
                var who = PlayerIn(txt);
                if (who != null) { NearPlayer = who; NearAt = DateTime.Now; FlushWaiting(who); }
                var cm = CostRe.Match(vis);
                string cost = cm.Success ? (cm.Groups[1].Success ? cm.Groups[1].Value : cm.Groups[2].Value) : null;
                foreach (var (sig, re) in Said)
                {
                    if (!re.IsMatch(vis)) continue;
                    if (sig == "skint") SkintConf = Conf;
                    if (sig == "wages") { WagesConf = Conf; continue; }   // the whole club, not one deal
                    Attach(who, sig, null);
                }
                if (cost != null) Attach(who, "cost", cost);
                if (npc) { LastBubble = el; LastBubbleText = txt; LastBubbleAt = DateTime.Now; TryAgentLine(); }
            }
            catch (Exception e) { PressRoom.Fail("deal bubble", e); }
        }

        static void Attach(string who, string sig, string cost)
        {
            if (who == null && NearPlayer.Length > 0 && (DateTime.Now - NearAt).TotalSeconds < 1.5) who = NearPlayer;
            if (who == null && Word.ChatSubject.Length > 0 && Word.ChatRole == "Agent") who = Word.ChatSubject;
            if (who == null) { Waiting.Add((sig, cost, DateTime.Now)); if (Waiting.Count > 20) Waiting.RemoveAt(0); return; }
            var d = Get(who);
            bool fresh = d.Signals.Add(sig);
            if (cost != null) d.Cost = cost;
            if (fresh && Strong.Contains(sig)) d.Heat = Math.Min(10, d.Heat + 1);
            if (sig == "done") Word.Stale(who, "deal agreed");
            d.LastConf = Conf;
            Save();
            PressRoom.W("deal", sig + (cost != null ? " " + cost : ""), who);
        }
        static void FlushWaiting(string who)
        {
            for (int i = Waiting.Count - 1; i >= 0; i--)
            {
                var w = Waiting[i]; Waiting.RemoveAt(i);
                if ((DateTime.Now - w.at).TotalSeconds < 1.5) Attach(who, w.sig, w.cost);
            }
        }

        static Deal Get(string who)
        {
            if (!All.TryGetValue(who, out var d)) { d = new Deal { Player = who, LastConf = Conf }; All[who] = d; }
            return d;
        }

        // your choice in a chat (from Word): the stance, which side of the deal you are on, and who the agent is
        internal static void Touch(string who, string kind, string stance)
        {
            try
            {
                if (string.IsNullOrEmpty(who)) return;
                var d = Get(who);
                d.Signals.Remove("off");     // talking again
                if (Word.ChatRole == "Agent" && Word.ChatWith.Length > 0 && Word.ChatWith != who) d.Agent = Word.ChatWith;
                if (kind == "selling" || kind == "pushout") d.Side = "selling";
                else if ((kind == "interest" || kind == "haggle") && d.Side != "selling") d.Side = "buying";
                if (!string.IsNullOrEmpty(stance)) d.Stance = stance;
                if (stance == "Desperate" || stance == "Contest" || stance == "Stall" || stance == "Threaten") d.Heat = Math.Min(10, d.Heat + 1);
                if (stance == "Agree") d.Signals.Add("close");
                d.LastConf = Conf;
                Save();
            }
            catch (Exception e) { PressRoom.Fail("deal touch", e); }
        }
        internal static void AddHeat(string who, int n) { if (who != null && All.TryGetValue(who, out var d)) { d.Heat = Math.Max(0, Math.Min(10, d.Heat + n)); Save(); } }

        // ---------- overtaken by events ----------
        // SI's own press questions talk about a player as yours once the deal is done ("the loan capture of", "bringing
        // ... to", "new signing"); headlines say when a player has signed or joined anyone. Either way the rumours are old.
        static readonly Regex OursNow = new Regex(@"\b(captur\w*|signing of|new signing|latest signing|bringing|brought in|since joining|has joined|having joined|arrival|addition of|acquisition|on loan from|loan move|recruit\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Moved = new Regex(@"\b(signs?|signed|completes?|completed|seals?|sealed|lands?|landed|captures?|captured|snaps? up|joins?|joined|loan move|moves to|swoop|deal done)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static bool Mentions(string vis, string player)
        {
            if (string.IsNullOrEmpty(player)) return false;
            if (vis.Contains(player)) return true;
            var parts = player.Split(' '); var sur = parts[parts.Length - 1];
            return sur.Length > 2 && Regex.IsMatch(vis, @"\b" + Regex.Escape(sur) + @"\b");
        }
        static IEnumerable<string> Players()
        {
            var seen = new HashSet<string>(All.Keys);
            foreach (var w in Word.All) if (w.State != "stale" && w.State != "out") seen.Add(w.Player);
            return seen;
        }
        internal static void SeeQuestion(string q)
        {
            try
            {
                var v = PressRoom.Visible(q ?? ""); if (!OursNow.IsMatch(v)) return;
                foreach (var p in new List<string>(Players()))
                {
                    if (!Mentions(v, p)) continue;
                    // only for players you were bringing in: "bringing Rothwell back into the side" is not a sale completed
                    if (All.TryGetValue(p, out var dd) && dd.Side == "selling") continue;
                    bool buying = false, selling = false;
                    foreach (var w in Word.All) if (w.Player == p) { if (w.Kind == "interest" || w.Kind == "haggle") buying = true; else selling = true; }
                    if (selling && !buying) continue;
                    MarkDone(p, "question");
                }
            }
            catch (Exception e) { PressRoom.Fail("deal question", e); }
        }
        // SI's own "window roundup" / "deadline day" stories mean the window has shut
        static readonly Regex WindowShut = new Regex(@"transfer window (round-?up|review|closes|has closed|is closed|shuts|has shut|slams shut)|deadline day (round-?up|review|recap|done and dusted)|window (has )?(closed|shut)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        internal static int WindowShutConf = -100;
        // transfer talk goes quiet: deals still open are off for now, and whispers about them old news
        internal static int ShutWindow(string why)
        {
            int n = 0;
            foreach (var d in All.Values)
            {
                if (d.Signals.Contains("done") || d.Signals.Contains("off")) continue;
                if (d.Side == "selling" && d.Stance == "Convince") continue;     // a player pushing to play more, not a transfer
                d.Signals.Add("off"); d.Heat = 0; d.LastConf = Conf; n++;
            }
            int w = 0;
            foreach (var x in Word.All)
                if ((x.Kind == "interest" || x.Kind == "haggle" || x.Kind == "selling") && (x.State == "new" || x.State == "hushed" || x.State == "planted")) { x.State = "stale"; w++; }
            WindowShutConf = Conf;
            Save(); if (w > 0) Word.Save();
            PressRoom.W("deal", "window shut (" + why + ")", n + " deals off, " + w + " whispers old news");
            return n + w;
        }
        internal static bool AnyOpenTransfer()
        {
            foreach (var d in All.Values) if (!d.Signals.Contains("done") && !d.Signals.Contains("off") && !(d.Side == "selling" && d.Stance == "Convince")) return true;
            foreach (var x in Word.All) if ((x.Kind == "interest" || x.Kind == "haggle" || x.Kind == "selling") && (x.State == "new" || x.State == "hushed" || x.State == "planted")) return true;
            return false;
        }

        internal static void SeeHeadline(string txt)
        {
            try
            {
                if (All.Count == 0 && Word.All.Count == 0) return;
                var v = PressRoom.Visible(txt ?? "");
                if (WindowShut.IsMatch(v)) { if (WindowShutConf != Conf && AnyOpenTransfer()) ShutWindow("headline: " + v); return; }
                if (!Moved.IsMatch(v)) return;
                foreach (var p in new List<string>(Players()))
                    if (Mentions(v, p))
                    {
                        if (All.TryGetValue(p, out var d) && !d.Signals.Contains("done") && d.Signals.Add("closed")) { Save(); PressRoom.W("deal", "closed (headline)", p + " | " + v); }
                        Word.Stale(p, "headline");
                    }
            }
            catch (Exception e) { PressRoom.Fail("deal headline", e); }
        }
        static void MarkDone(string p, string how)
        {
            var d = Get(p);
            if (d.Signals.Add("done")) { d.LastConf = Conf; Save(); PressRoom.W("deal", "done (" + how + ")", p); }
            Word.Stale(p, how);
        }
        static bool Over(Deal d) => d.Signals.Contains("off") || (d.Signals.Contains("closed") && !d.Signals.Contains("done"));

        // ---------- the agent goes public ----------
        static string Pos(string name) => name.EndsWith("s") ? name + "'s" : name + "'s";
        static string AgentRef(Deal d) => d.Agent.Length > 0 ? d.Agent + ", " + Pos(d.Player) + " agent," : Pos(d.Player) + " agent";
        static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static int RepBonus(string p)
        {
            if (!Word.Reputation.TryGetValue(p, out var r)) foreach (var kv in Word.Reputation) if (Word.DisplayFor(kv.Key) == p) { r = kv.Value; break; }
            switch (r ?? "")
            {
                case "Worldwide": return 20; case "Continental": return 15; case "National": return 5;
                case "Regional": return -5; case "Local": return -15; default: return 0;
            }
        }
        static string PersonalityOf(string p)
        {
            if (Word.Personality.TryGetValue(p, out var x)) return x;
            foreach (var kv in Word.Personality) if (Word.DisplayFor(kv.Key) == p) return kv.Value;
            return "";
        }
        static bool Loud(string pers) => (pers.Contains("Ambitious") && !pers.Contains("Unambitious")) || pers.Contains("Mercenary") || pers.Contains("Fickle");
        static bool Steady(string pers) => pers.Contains("Loyal") || pers.Contains("Devoted") || pers.Contains("Professional");
        static bool OurSideCanLeak(Deal d) => !d.Quiet && MediaOffice.Mode != MediaOffice.Approach.Gatekeeper;

        static int Chance(Deal d)
        {
            int c = 35, strong = 0;
            foreach (var s in d.Signals) if (Strong.Contains(s)) strong++;
            c += Math.Min(2, strong) * 15;
            if (d.Stance == "Desperate" || d.Stance == "Threaten") c += 10;
            c += RepBonus(d.Player);
            var pers = PersonalityOf(d.Player);
            if (Loud(pers)) c += 10; else if (Steady(pers)) c -= 10;
            if (MediaOffice.Mode == MediaOffice.Approach.OpenDoor) c += 10;
            else if (MediaOffice.Mode == MediaOffice.Approach.Gatekeeper) c -= 10;
            c += d.Heat * 3;
            return Math.Max(5, Math.Min(90, c));
        }

        // the next thing to say about this deal, not said before; null if nothing new
        static string Line(Deal d, out string key)
        {
            var A = AgentRef(d); var p = d.Player; var pers = PersonalityOf(p);
            var parts = p.Split(' '); var S = parts[parts.Length - 1];   // second mention: the surname
            var options = new List<(string key, string line)>();
            if (d.Side == "selling")
            {
                if (d.Stance == "Threaten") options.Add(("frozen", Cap(A) + " says " + S + " is being frozen out."));
                if (d.Stance == "Convince") options.Add(("told", Cap(A) + " says " + S + " has been told to find a new club."));
                if (d.Stance == "Desperate") options.Add(("needsale", Cap(A) + " says " + S + " is open to a move, and knows you need the sale."));
                if (d.Stance == "Keen" || d.Stance == "Curious" || d.Stance == "Interested") options.Add(("listen", Cap(A) + " says " + S + " would listen to offers."));
                if (d.Stance == "Back Down") options.Add(("staying", Cap(A) + " says " + S + " is staying put."));
                if (Steady(pers)) options.Add(("loyal", Cap(A) + " says " + S + " has no wish to leave."));
                if (OurSideCanLeak(d) && SkintConf >= Conf - 2) options.Add(("books", "Is selling " + p + " about balancing the books?"));
            }
            else if (d.Signals.Contains("done"))
            {
                if (d.Heat >= 3) options.Add(("done", p + " is finally through the door. Was it worth all the fuss?"));
                if (OurSideCanLeak(d) && d.Signals.Contains("overpaid")) options.Add(("overpaid", "Word from inside the club is that you paid over the odds for " + p + "."));
            }
            else
            {
                if (d.Signals.Contains("toolong")) options.Add(("toolong", Pos(p) + " camp say talks have gone on far too long."));
                if (d.Signals.Contains("elsewhere")) options.Add(("elsewhere", Cap(A) + " is telling anyone who'll listen that other clubs want " + S + "."));
                if (d.Signals.Contains("hardball")) options.Add(("hardball", Cap(A) + " says there'll be no discount on " + Pos(S) + " wages."));
                if (d.Stance == "Desperate") options.Add(("desperate", Cap(A) + " knows how badly you want " + S + ", and is making the most of it."));
                if (d.Stance == "Contest" || d.Stance == "Stall") options.Add(("lowball", Cap(A) + " says your offer doesn't do " + S + " justice."));
                if (d.Signals.Contains("close")) options.Add(("close", Cap(A) + " says a deal for " + S + " is very close."));
                if (Loud(pers) && d.Signals.Contains("elsewhere")) options.Add(("ambition", "Word is " + p + " will go wherever the ambition, or the money, is."));
                if (OurSideCanLeak(d))
                {
                    if (d.Signals.Contains("skint") || SkintConf >= Conf - 1) options.Add(("skint", "There are questions over whether you can afford " + p + " without selling first."));
                    if (WagesConf >= Conf - 2) options.Add(("wages", "We understand the wage bill is already over budget. Where does that leave " + p + "?"));
                    if (d.Signals.Contains("jeopardy")) options.Add(("jeopardy", "We hear the " + p + " deal is in the balance."));
                    if (d.Signals.Contains("pricey")) options.Add(("valuation", "We hear you think the asking price for " + p + " is too high."));
                    if (d.Cost.Length > 0) options.Add(("cost", "We're told " + p + " would take " + d.Cost + " out of your transfer budget."));
                }
            }
            foreach (var o in options) if (!d.Said.Contains(o.key)) { key = o.key; return o.line; }
            key = null; return null;
        }

        static int TalkConf = -1, Talked, BriefedConf = -1;
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }
        static bool About(Deal d, string q)
        {
            var v = PressRoom.Visible(q ?? "");
            foreach (var part in d.Player.Split(' ')) if (part.Length > 2 && v.Contains(part)) return true;
            return Word.Transfer.IsMatch(v);
        }

        internal static string Talk(string q, string outlet, string journalist, PressRoom.Kind k)
        {
            try
            {
                int conf = Conf, qno = Word.QNo;
                if (TalkConf != conf) { TalkConf = conf; Talked = 0; }
                if (MediaOffice.IsBanned(outlet)) return null;
                // your own briefing goes first, from the second question on
                if (BriefedConf != conf && qno >= 2)
                    foreach (var d in All.Values)
                        if (d.Brief == "pending")
                        {
                            d.Brief = "done"; d.BriefConf = conf; d.Heat = Math.Max(0, d.Heat - 3); BriefedConf = conf; d.LastLine = "";
                            var line = d.Side == "selling" ? "Club sources say there's no pressure to sell " + d.Player + "." : "Club sources say there's no rush over " + d.Player + ".";
                            d.LastLine = line; Save();
                            PressRoom.W("brief", outlet + " / " + journalist, line);
                            return line;
                        }
                int hottest = 0; foreach (var d in All.Values) hottest = Math.Max(hottest, d.Heat);
                if (Talked >= (hottest >= 7 ? 2 : 1)) return null;
                Deal best = null; string bestLine = null, bestKey = null; int bestC = -1;
                foreach (var d in All.Values)
                {
                    if (d.PublicConf == conf || Over(d)) continue;
                    if (conf - d.LastConf > 2 && d.Heat < 5) continue;
                    if (qno < 3 && !About(d, q)) continue;
                    var line = Line(d, out var key); if (line == null) continue;
                    int c = Chance(d);
                    if (Hash(d.Player + conf) % 100 >= (uint)c) continue;
                    if (c > bestC) { best = d; bestLine = line; bestKey = key; bestC = c; }
                }
                if (best == null) return null;
                best.Said.Add(bestKey); best.PublicConf = conf; best.Heat = Math.Min(10, best.Heat + 3); best.LastLine = bestLine; Talked++;
                Save();
                PressRoom.W("agent", bestKey + " " + bestC + "% " + outlet + " / " + journalist, bestLine);
                return bestLine;
            }
            catch (Exception e) { PressRoom.Fail("agent talk", e); }
            return null;
        }

        // ---------- the agent brings it up in your next chat ----------
        static VisualElement LastBubble; static string LastBubbleText = ""; static DateTime LastBubbleAt = DateTime.MinValue;
        static string HeaderWho = ""; static DateTime HeaderAt = DateTime.MinValue;
        internal static readonly Dictionary<string, string> BubbleMap = new Dictionary<string, string>();
        internal static string BubbleMapFor = ""; internal static DateTime BubbleMapAt = DateTime.MinValue;
        static readonly HashSet<string> Ours = new HashSet<string>();
        static bool IsOurs(string t) { foreach (var o in Ours) if (t.StartsWith(o)) return true; return false; }

        internal static void OnHeader(string who) { HeaderWho = who ?? ""; HeaderAt = DateTime.Now; TryAgentLine(); }

        static string ChatLine(Deal d)
        {
            if (d.BriefConf >= Conf - 1) return "I saw your club's line in the papers. Message received.";
            if (d.Side == "selling") return "My client has seen what's being said in the papers. We'd like this handled properly.";
            if (d.Signals.Contains("toolong") || d.Signals.Contains("jeopardy") || d.Signals.Contains("hardball") || d.Stance == "Contest" || d.Stance == "Stall")
                return "All this in the papers isn't helping anyone. Let's get it settled.";
            if (d.Signals.Contains("close") || d.Stance == "Agree") return "Everyone's reading that we're close. Let's not keep them waiting.";
            return "You'll have seen the papers. We're not short of options.";
        }

        // the agent's first bubble and the header naming them arrive within a moment of each other, in either order
        static void TryAgentLine()
        {
            try
            {
                if (LastBubble == null || HeaderWho.Length == 0 || InPress()) return;
                if (Math.Abs((LastBubbleAt - HeaderAt).TotalSeconds) > 1.5) return;
                if (BubbleMap.ContainsValue(LastBubbleText) || IsOurs(LastBubbleText)) return;
                if (BubbleMapFor != HeaderWho) { BubbleMap.Clear(); BubbleMapFor = HeaderWho; }
                Deal best = null;
                foreach (var d in All.Values) if (d.Agent == HeaderWho && d.Heat >= 4 && d.ChatConf != Conf && !d.Signals.Contains("done") && !Over(d) && (best == null || d.Heat > best.Heat)) best = d;
                if (best == null) return;
                var line = ChatLine(best); Ours.Add(line);
                var res = line + " " + LastBubbleText;
                BubbleMap[LastBubbleText] = res; BubbleMapAt = DateTime.Now;
                best.ChatConf = Conf; Save();
                PressRoom.W("agent-chat", HeaderWho + " / " + best.Player, line);
                var el = LastBubble; LastBubble = null;
                try { PressRoom.Applying = true; el.TryCast<SI.Bindable.SIText>()?.SetText(res); } finally { PressRoom.Applying = false; }
            }
            catch (Exception e) { PressRoom.Fail("agent chat", e); }
        }
        // the same bubble shown again keeps the line (prefix)
        internal static string Reshow(string txt)
        {
            if (BubbleMap.Count == 0 || (DateTime.Now - BubbleMapAt).TotalMinutes > 10 || Word.PendingWith != BubbleMapFor) return null;
            return BubbleMap.TryGetValue(txt, out var m) ? m : null;
        }

        // ---------- the press office ----------
        internal static void BriefBack(Deal d)
        {
            if (MediaOffice.Mode == MediaOffice.Approach.Gatekeeper) { d.Quiet = true; d.Heat = Math.Max(0, d.Heat - 1); PressRoom.W("deal", "say nothing", d.Player); }
            else { d.Brief = "pending"; PressRoom.W("deal", "brief back", d.Player); }
            Save();
        }

        internal static string Describe(string sig)
        {
            switch (sig)
            {
                case "elsewhere": return "the agent says other clubs are interested";
                case "toolong": return "the agent says talks have gone on too long";
                case "hardball": return "the agent won't budge on wages";
                case "close": return "close to agreement";
                case "jeopardy": return "your director of football says more demands could sink it";
                case "pricey": return "your director of football thinks the price is too high";
                case "skint": return "you'd need to sell or move budget first";
                case "done": return "deal agreed";
                case "closed": return "signed somewhere, or off";
                case "off": return "off for now: the transfer window has shut";
                case "overpaid": return "your director of football thinks you overpaid";
                default: return null;
            }
        }

        internal static void OnConference(int conf)
        {
            try
            {
                var gone = new List<string>();
                foreach (var d in All.Values)
                {
                    if (d.LastConf < conf - 1 && d.PublicConf < conf - 1) d.Heat = Math.Max(0, d.Heat - 2);
                    if (d.Heat == 0 && d.LastConf < conf - 4) gone.Add(d.Player);
                }
                foreach (var g in gone) All.Remove(g);
                Save();
            }
            catch (Exception e) { PressRoom.Fail("deal conf", e); }
        }

        internal static void Reset()
        {
            All.Clear(); SkintConf = -100; WagesConf = -100; BubbleMap.Clear(); Waiting.Clear(); TalkConf = -1; BriefedConf = -1; Talked = 0;
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 3 && p[0] == "#club") { int.TryParse(p[1], out SkintConf); int.TryParse(p[2], out WagesConf); continue; }
                    if (p.Length < 15 || p[0] == "player") continue;
                    var d = new Deal { Player = p[0], Agent = p[1], Side = p[2], Stance = p[3], Cost = p[5], Brief = p[11], LastLine = p[13] };
                    foreach (var s in p[4].Split(',')) if (s.Length > 0) d.Signals.Add(s);
                    foreach (var s in p[14].Split(',')) if (s.Length > 0) d.Said.Add(s);
                    int.TryParse(p[6], out d.Heat); int.TryParse(p[7], out d.LastConf); int.TryParse(p[8], out d.PublicConf);
                    int.TryParse(p[9], out d.ChatConf); int.TryParse(p[10], out d.BriefConf); d.Quiet = p[12] == "1";
                    All[d.Player] = d;
                }
                // misread push-outs (no agent, no signals, a Convince / Threaten / Back Down stance): not deals
                var misread = new List<string>();
                foreach (var d in All.Values)
                    if (d.Side == "selling" && d.Agent.Length == 0 && d.Signals.Count == 0 && (d.Stance == "Convince" || d.Stance == "Threaten" || d.Stance == "Back Down")
                        && !Word.All.Exists(w => w.Player == d.Player && w.Kind == "pushout")) misread.Add(d.Player);
                foreach (var m in misread) All.Remove(m);
                if (misread.Count > 0) { Save(); PressRoom.W("deal", "misread push-outs dropped", string.Join(", ", misread)); }
                foreach (var d in All.Values) if (d.Signals.Contains("done") || d.Signals.Contains("closed")) Word.Stale(d.Player, null);
            }
            catch (Exception e) { PressRoom.Fail("deal load", e); }
        }
        internal static void Save()
        {
            try
            {
                var sb = new StringBuilder("player\tagent\tside\tstance\tsignals\tcost\theat\tlastconf\tpublicconf\tchatconf\tbriefconf\tbrief\tquiet\tlastline\tsaid\n");
                sb.Append("#club\t").Append(SkintConf).Append('\t').Append(WagesConf).Append('\n');
                foreach (var d in All.Values)
                    sb.Append(Clean(d.Player)).Append('\t').Append(Clean(d.Agent)).Append('\t').Append(d.Side).Append('\t').Append(Clean(d.Stance)).Append('\t')
                      .Append(string.Join(",", d.Signals)).Append('\t').Append(Clean(d.Cost)).Append('\t').Append(d.Heat).Append('\t').Append(d.LastConf).Append('\t')
                      .Append(d.PublicConf).Append('\t').Append(d.ChatConf).Append('\t').Append(d.BriefConf).Append('\t').Append(d.Brief).Append('\t')
                      .Append(d.Quiet ? "1" : "0").Append('\t').Append(Clean(d.LastLine)).Append('\t').Append(string.Join(",", d.Said)).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("deal save", e); }
        }
    }
}
