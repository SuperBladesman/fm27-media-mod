// FM27 Media Mod - Word gets out (Press Room 0.6). MIT licence (see LICENSE).
// What you say in agent, director-of-football and player chats is overheard: the stance the game labels your choice
// with (Desperate, Keen, Curious; Agree, Contest, Stall; Convince, Threaten, Back Down) and the player it is about.
// Each becomes a whisper. At the next press conference one journalist may open with it ("Word is you're desperate to
// sell Tyrese Campbell."), unless you kept it quiet, or you planted it with an outlet of your choosing, who then owe you.
// Only the journalist's opening words are ours; the chat itself and everything it does are SI's.
// Also kept: the personality the game shows for players (profile and chat screens), for players who answer like themselves.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Word
    {
        internal class Whisper { public int Conf; public string When, Kind, Level, Player, With, Text, State, Outlet; }
        internal static readonly List<Whisper> All = new List<Whisper>();
        internal static readonly Dictionary<string, string> Personality = new Dictionary<string, string>();   // full name -> personality
        internal static readonly Dictionary<string, string> Reputation = new Dictionary<string, string>();    // full name -> reputation (Local ... Worldwide)
        static readonly Dictionary<string, string> Known = new Dictionary<string, string>();                 // full name -> name as shown
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "whispers.tsv");
        static string PeoplePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "people.tsv");
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // ---------- what the chat screen tells us ----------
        // From the probe (16 Nov): the chat header names the person (Name, then Job; in player chats it shows you, the Manager);
        // a player chat's summary names the player (Interactees/PlayerInfo/Name) and shows Personality just BEFORE the name;
        // a profile page shows PlayerPersonalityText just BEFORE PlayerName. So a personality is paired with the name that
        // follows it within a second, never with the one before.
        internal static string ChatWith = "", ChatRole = "", ChatPlayer = "", ChatSubject = "";
        internal static string LastProfile = ""; internal static DateTime LastProfileAt = DateTime.MinValue;
        internal static string PendingWith = "", ManagerName = "";
        static string PendPers = "", PendPersSrc = ""; static DateTime PendPersAt = DateTime.MinValue;
        static string PendName = "", PendNameSrc = ""; static DateTime PendNameAt = DateTime.MinValue;
        static string PendRep = "", PendRepSrc = ""; static DateTime PendRepAt = DateTime.MinValue;
        static readonly Regex ProfileOf = new Regex(@"Click to view (.+?)(?:'s|') profile", RegexOptions.Compiled);

        // the full name behind a link (from its tooltip) and the name as shown
        static string PersonIn(string raw, out string shown)
        {
            shown = null;
            foreach (Match m in PressRoom.Link.Matches(raw ?? ""))
            {
                string tip; try { tip = Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value)); } catch { continue; }
                var pm = ProfileOf.Match(tip); if (!pm.Success) continue;
                shown = PressRoom.Visible(m.Groups[2].Value).Trim();
                return pm.Groups[1].Value.Trim();
            }
            return null;
        }
        internal static string PersonFull(string raw, out string shown) => PersonIn(raw, out shown);
        // the name to use for a person: the deal already open under their first and last name, else the name as shown
        internal static string DisplayFor(string full)
        {
            if (string.IsNullOrEmpty(full)) return full;
            var parts = full.Split(' ');
            var fl = parts.Length >= 2 ? parts[0] + " " + parts[parts.Length - 1] : full;
            Known.TryGetValue(full, out var known);
            foreach (var k in Deals.All.Keys) if (k == full || k == fl || k == known) return k;
            if (Known.TryGetValue(full, out var v) && v.Contains(" ") && !v.Contains(".")) return v;
            return full;
        }

        // the short name the game uses: "Á. Núñez" on a profile with full name "Álvaro Núñez Cobo" gives "Álvaro Núñez"
        static string ShortName(string full, string shown)
        {
            shown = (shown ?? "").Trim();
            if (shown.Contains(" ") && !shown.Contains(".")) return shown;
            int dot = shown.IndexOf(". ");
            if (dot > 0)
            {
                var sur = shown.Substring(dot + 2).Trim();
                var parts = full.Split(' ');
                if (sur.Length > 1 && (" " + full + " ").Contains(" " + sur + " ") && parts[0] != sur) return parts[0] + " " + sur;
            }
            return full;
        }
        static bool Better(string v) => v.Contains(" ") && !v.Contains(".");
        static void Remember(string full, string disp)
        {
            if (string.IsNullOrEmpty(full) || Known.Count >= 5000) return;
            if (!Known.TryGetValue(full, out var old) || (!Better(old) && Better(disp)) || (Better(disp) && disp.Length < old.Length && old == full))
            {
                Known[full] = disp;
                if (Better(disp) && (Personality.ContainsKey(full) || Reputation.ContainsKey(full))) SavePeople();   // keep the short name for next session
            }
        }

        static void Learn(string raw)
        {
            foreach (Match m in PressRoom.Link.Matches(raw ?? ""))
            {
                string tip; try { tip = Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value)); } catch { continue; }
                var pm = ProfileOf.Match(tip);
                if (pm.Success) { var full = pm.Groups[1].Value.Trim(); Remember(full, PressRoom.Visible(m.Groups[2].Value).Trim()); }
            }
        }

        static bool Under(VisualElement e, string name, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent) if (e.name == name) return true;
            return false;
        }

        // called for every text the game sets: only a handful of element names matter, so that is checked first
        internal static void OnText(VisualElement el, string txt)
        {
            try
            {
                if (string.IsNullOrEmpty(txt)) return;
                var n = el.name;
                if (n == "MessageHeadline" || n == "newsHeadlineText" || n == "Headline") { Deals.SeeHeadline(txt); return; }
                if (n != "Name" && n != "Job" && n != "PlayerName" && n != "PlayerPersonalityText" && n != "Personality" && n != "text" && n != "Advice" && n != "PlayerReputationText") return;
                bool hasLink = txt.IndexOf('\u0001') >= 0;
                if (n == "text")
                {
                    // what the other side says can link the player ("Do you want to offer Campbell to other clubs?")
                    if (Under(el, "BubbleBackground", 3))
                    {
                        if (hasLink) Learn(txt);
                        Deals.OnBubble(el, txt, Under(el, "NPCBubbleFrame", 6));
                    }
                    return;
                }
                if (n == "Advice")
                {
                    if (Under(el, "SpeakToResponseActionButton", 4)) Deals.OnBubble(el, txt, false);
                    return;
                }
                if (n == "PlayerReputationText")
                {
                    if (Under(el, "PlayerReputationTextFrame", 3)) RepSeen(PressRoom.Visible(txt).Trim(), "profile");
                    return;
                }
                if (n == "Name" && Under(el, "PrimarySender", 6))
                {
                    if (hasLink) { var who = PersonIn(txt, out _); if (who != null) { PendingWith = who; Deals.OnHeader(who); } }
                    return;
                }
                if (n == "Job" && Under(el, "PrimarySender", 6))
                {
                    var job = PressRoom.Visible(txt).Trim();
                    if (job == "Manager") { ManagerName = PendingWith; return; }   // a player chat or a press conference: the header is you
                    if (PendingWith.Length == 0) return;
                    if (PendingWith != ChatWith) { ChatWith = PendingWith; ChatSubject = ""; }
                    ChatRole = job.Length < 40 ? job : "";
                    return;
                }
                if ((n == "Name" || n == "Personality") && Under(el, "PlayerInfo", 6) && Under(el, "Interactees", 10))
                {
                    if (n == "Name")
                    {
                        if (!hasLink) return;
                        var full = PersonIn(txt, out var shown); if (full == null) return;
                        var disp = !string.IsNullOrEmpty(shown) && shown.Contains(" ") ? shown : full;
                        Remember(full, disp);
                        if (disp != ChatPlayer) { ChatPlayer = disp; ChatSubject = disp; }
                        ChatWith = disp; ChatRole = "Player";
                        NameSeen(full, "chat");
                    }
                    else PersSeen(PressRoom.Visible(txt).Trim(), "chat");
                    return;
                }
                if (n == "PlayerName" && hasLink && Under(el, "ViewedEntityTypeSwitch", 6))
                {
                    var full = PersonIn(txt, out var pshown); if (full == null) return;
                    Remember(full, ShortName(full, pshown));
                    LastProfile = full; LastProfileAt = DateTime.Now;
                    NameSeen(full, "profile");
                    return;
                }
                if (n == "PlayerPersonalityText" && Under(el, "PersonalityVisibleForActualPlayer", 6)) PersSeen(PressRoom.Visible(txt).Trim(), "profile");
            }
            catch (Exception e) { PressRoom.Fail("word text", e); }
        }

        static void SetPersonality(string who, string p)
        {
            if (string.IsNullOrEmpty(who) || string.IsNullOrEmpty(p) || p.Length > 40) return;
            if (Personality.TryGetValue(who, out var old) && old == p) return;
            Personality[who] = p; SavePeople();
            PressRoom.W("person", who, p);
        }

        static void PersSeen(string p, string src)
        {
            if (p.Length < 3 || p == "Personality") return;
            if (PendNameSrc == src && (DateTime.Now - PendNameAt).TotalSeconds < 1.0) { SetPersonality(PendName, p); PendNameSrc = ""; return; }
            PendPers = p; PendPersSrc = src; PendPersAt = DateTime.Now;
        }
        static void RepSeen(string r, string src)
        {
            if (r.Length < 3 || r == "Reputation") return;
            if (PendNameSrc == src && (DateTime.Now - PendNameAt).TotalSeconds < 1.0) { SetReputation(PendName, r); return; }
            PendRep = r; PendRepSrc = src; PendRepAt = DateTime.Now;
        }
        static void SetReputation(string who, string r)
        {
            if (string.IsNullOrEmpty(who) || r.Length > 30) return;
            if (Reputation.TryGetValue(who, out var old) && old == r) return;
            Reputation[who] = r; SavePeople();
            PressRoom.W("reputation", who, r);
        }
        static void NameSeen(string full, string src)
        {
            bool used = false;
            if (PendPersSrc == src && (DateTime.Now - PendPersAt).TotalSeconds < 1.0) { SetPersonality(full, PendPers); PendPersSrc = ""; used = true; }
            if (PendRepSrc == src && (DateTime.Now - PendRepAt).TotalSeconds < 1.0) { SetReputation(full, PendRep); PendRepSrc = ""; used = true; }
            if (!used) { PendName = full; PendNameSrc = src; PendNameAt = DateTime.Now; }
        }

        // ---------- the choice you made ----------
        static readonly HashSet<string> Buy = new HashSet<string> { "Desperate", "Keen", "Curious", "Interested" };
        static readonly HashSet<string> Haggle = new HashSet<string> { "Agree", "Contest", "Stall" };
        static readonly HashSet<string> Push = new HashSet<string> { "Convince", "Threaten", "Back Down" };
        // Convince / Threaten / Back Down head many player chats (asking a player to talk to a teammate, a loan club's complaint...):
        // only a line about the player leaving is a push-out
        internal static readonly Regex Leave = new Regex(@"\b(new club|another club|find a club|leave|leaving|move on|moving on|look elsewhere|elsewhere|no future|not (in|part of) (my|our|the) plans|surplus|sell you|sold|go out on loan|loan(ed)? (you )?out|transfer[- ]list\w*|available for transfer|offers? for you|exit)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SellWords = new Regex(@"\b(sell|selling|offers?|move \w+ on|leave|available|market)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex TwoNames = new Regex(@"(?<![.!?]\s)(?<!^)\b(\p{Lu}[\p{L}'\-]+(?: \p{Lu}[\p{L}'\-]+)?) (\p{Lu}[\p{L}'\-]+)\b", RegexOptions.Compiled);
        static readonly HashSet<string> NotNames = new HashSet<string> { "I", "We", "The", "You", "He", "She", "They", "It", "Look", "Okay", "Right", "Fair", "Thanks", "Sheffield", "United", "City", "Town", "Football", "Club",
            "Premier", "Champions", "Europa", "Conference", "League", "Cup", "Lane", "Road", "Park", "Stadium", "Ground", "Bramall", "Sky", "Bet", "Championship", "England", "Mr", "Mrs", "Ms", "Your", "My", "Our", "This", "That", "Director", "Board", "Academy", "Under", "Rovers", "Wednesday", "Albion", "Athletic", "County", "Villa", "Forest", "Wanderers" };

        // who the choice is about: a player linked or named in the line itself, the player you are talking to,
        // a name in plain text, the player this chat has been about, or the profile you have just come from
        static string Subject(string chosen, out bool inLine)
        {
            inLine = true;
            var vis = PressRoom.Visible(chosen ?? "");
            var linked = PersonIn(chosen, out var shown); if (linked != null) return !string.IsNullOrEmpty(shown) && Better(shown) ? shown : DisplayFor(linked);
            foreach (var kv in Known)
            {
                var parts = kv.Key.Split(' '); if (parts.Length < 2) continue;
                var fl = parts[0] + " " + parts[parts.Length - 1];
                if (vis.Contains(kv.Key)) return kv.Key;
                if (vis.Contains(fl)) return fl;
                if (kv.Value.Contains(" ") && !kv.Value.Contains(".") && vis.Contains(kv.Value)) return kv.Value;
            }
            inLine = false;
            if (ChatRole == "Player" && ChatWith == ChatPlayer && ChatPlayer.Length > 0) return ChatPlayer;
            foreach (Match m in TwoNames.Matches(vis))
            {
                var a = m.Groups[1].Value; var b = m.Groups[2].Value;
                if (b.EndsWith("'s")) b = b.Substring(0, b.Length - 2);
                if (NotNames.Contains(a.Split(' ')[0]) || NotNames.Contains(b)) continue;
                inLine = true; return a + " " + b;
            }
            if (ChatSubject.Length > 0) return ChatSubject;
            if (LastProfile.Length > 0 && (DateTime.Now - LastProfileAt).TotalMinutes < 3) return DisplayFor(LastProfile);
            return null;
        }

        internal static void OnChatReply(string chosen, string group)
        {
            try
            {
                if (string.IsNullOrEmpty(chosen)) return;
                // any line naming a player sets what this chat is about, so later lines ("Kyle's demands") know who
                var who = Subject(chosen, out var inLine);
                if (who != null && inLine) ChatSubject = who;
                PressRoom.W("chat", (group ?? "no group") + " | " + (ChatRole.Length > 0 ? ChatRole + " " : "") + ChatWith, (who ?? "no player") + " | " + PressRoom.Visible(chosen));
                if (string.IsNullOrEmpty(group)) { if (who != null && inLine && ChatRole == "Agent") Deals.Touch(DisplayFor(who), null, null); return; }
                string kind = null;
                if (Buy.Contains(group)) kind = SellWords.IsMatch(PressRoom.Visible(chosen)) ? "selling" : "interest";
                else if (Haggle.Contains(group)) kind = "haggle";
                else if (Push.Contains(group) && Leave.IsMatch(PressRoom.Visible(chosen))) kind = "pushout";
                if (kind == null) { if (who != null && inLine && ChatRole == "Agent") Deals.Touch(DisplayFor(who), null, null); return; }
                if (who == null) { PressRoom.W("whisper", "no player found", group + " | " + PressRoom.Visible(chosen)); return; }
                Deals.Touch(DisplayFor(who), kind, group);
                // one whisper per player and kind per conference: the latest stance wins
                foreach (var old in All)
                    if (old.Player == who && old.Kind == kind && old.Conf == PressRoom.Record.Conf && old.State == "new") { old.Level = group; old.Text = PressRoom.Visible(chosen); Save(); PressRoom.W("whisper", kind + " " + group, who); return; }
                All.Add(new Whisper { Conf = PressRoom.Record.Conf, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Kind = kind, Level = group, Player = who,
                    With = (ChatRole.Length > 0 ? ChatRole + " " : "") + ChatWith, Text = PressRoom.Visible(chosen), State = "new", Outlet = "" });
                while (All.Count > 400) All.RemoveAt(0);
                Save();
                PressRoom.W("whisper", kind + " " + group, who + " | " + PressRoom.Visible(chosen));
            }
            catch (Exception e) { PressRoom.Fail("whisper", e); }
        }

        // ---------- the press hear about it ----------
        internal static string Fact(Whisper w)
        {
            var p = w.Player;
            switch (w.Kind + "/" + w.Level)
            {
                case "selling/Desperate": return "you're desperate to sell " + p;
                case "selling/Keen": return p + " is available if the price is right";
                case "selling/Curious": return "you've been asking what " + p + " would fetch";
                case "interest/Desperate": return "you're desperate to land " + p;
                case "interest/Keen": return "you're keen on " + p;
                case "interest/Curious": return "you've been asking about " + p;
                case "interest/Interested": return "you've made enquiries about " + p;
                case "selling/Interested": return p + " could be on the way out";
                case "haggle/Agree": return "the " + p + " deal is close";
                case "haggle/Contest": return "the " + p + " talks have turned awkward";
                case "haggle/Stall": return "you're playing hardball over " + p;
                case "pushout/Threaten": return "you've told " + p + " there's no future here";
                case "pushout/Convince": return "you've told " + p + " to find a new club";
                case "pushout/Back Down": return p + " talked you round";
            }
            return null;
        }
        internal static string Describe(Whisper w)
        {
            var f = Fact(w); if (f == null) return w.Kind + " " + w.Player;
            return char.ToUpperInvariant(f[0]) + f.Substring(1);
        }

        static int LeakedConf = -1;
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        // at most one whisper per press conference: a planted one with its outlet first, otherwise a fresh one may slip out
        internal static int QNo; static int QConf = -1;
        internal static void NextQuestion() { if (QConf != PressRoom.Record.Conf) { QConf = PressRoom.Record.Conf; QNo = 0; } QNo++; }
        internal static readonly Regex Transfer = new Regex(@"\b(transfers?|sign|signs|signed|signing|signings|window|deals?|bids?|offers?|sell|sells|selling|sold|move|leave|leaves|leaving|future|targets?|interest|interested|replace|replaced|replacement|contracts?|wages?|depart\w*|exit|business|loan|loans|capture|arrival|arrivals|recruit\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static string Surname(string p) { var parts = (p ?? "").Split(' '); return parts[parts.Length - 1]; }

        // a whisper lands only on a question it fits: transfers or futures, or the player by name
        static bool Fits(Whisper w, string q, int asked) =>
            Transfer.IsMatch(PressRoom.Visible(q ?? "")) || PressRoom.Visible(q ?? "").Contains(Surname(w.Player));

        internal static string Leak(string q, string outlet, string journalist, PressRoom.Kind k)
        {
            try
            {
                int conf = PressRoom.Record.Conf;
                int Asked = QNo;
                if (LeakedConf == conf || string.IsNullOrEmpty(outlet)) return null;
                for (int i = All.Count - 1; i >= 0; i--)
                {
                    var w = All[i];
                    if (w.State == "planted" && w.Outlet == outlet && Fact(w) != null && Fits(w, q, Asked + 1))
                    {
                        w.State = "out"; LeakedConf = conf; Save();
                        MediaOffice.Owe(outlet, 2); Deals.AddHeat(w.Player, 2);
                        PressRoom.W("leak", "planted " + outlet + " / " + journalist, Describe(w));
                        return "Our sources tell us " + Fact(w) + ".";
                    }
                }
                if (k == PressRoom.Kind.Broadcast) return null;   // the rumour mill, not the cameras
                int chance = MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? 35 : MediaOffice.Mode == MediaOffice.Approach.Fixer ? 80 : MediaOffice.Mode == MediaOffice.Approach.OpenDoor ? 65 : 55;
                for (int i = All.Count - 1; i >= 0; i--)
                {
                    var w = All[i];
                    if ((w.State != "new" && w.State != "hushed") || conf - w.Conf > 2 || Fact(w) == null || !Fits(w, q, Asked)) continue;
                    int c = w.State == "hushed" ? (MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? 0 : chance / 2) : chance;
                    // 0.10: someone who has it in for you hears what you wanted kept quiet
                    if (w.State == "hushed" && Pack.On && Pack.IsFeud(journalist)) c = chance;
                    if (Hash(w.Player + w.Kind + conf) % 100 >= (uint)c) { continue; }
                    w.State = "out"; w.Outlet = outlet; LeakedConf = conf; Save(); Deals.AddHeat(w.Player, 2);
                    PressRoom.W("leak", outlet + " / " + journalist, Describe(w));
                    var lead = w.Kind == "pushout" ? "Word from the training ground is " : (Hash(journalist + conf) % 2 == 0 ? "Word is " : "We hear ");
                    return lead + Fact(w) + ".";
                }
            }
            catch (Exception e) { PressRoom.Fail("leak", e); }
            return null;
        }

        // a deal done or gone: what was said about it is old news
        internal static void Stale(string player, string why)
        {
            bool any = false;
            foreach (var w in All)
                if (w.Player == player && (w.State == "new" || w.State == "hushed" || w.State == "planted")) { w.State = "stale"; any = true; }
            if (any) { Save(); if (why != null) PressRoom.W("whisper", "old news (" + why + ")", player); }
        }

        // ---------- state ----------
        internal static void Reset()
        {
            All.Clear(); Personality.Clear(); Reputation.Clear(); Known.Clear();
            ChatWith = ""; ChatRole = ""; ChatPlayer = ""; ChatSubject = ""; LastProfile = ""; PendingWith = "";
            LeakedConf = -1; QConf = -1; QNo = 0;
        }

        internal static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var p = line.Split('\t'); if (p.Length < 9 || p[0] == "conf") continue;
                        All.Add(new Whisper { Conf = int.TryParse(p[0], out var c) ? c : 0, When = p[1], Kind = p[2], Level = p[3], Player = p[4], With = p[5], Text = p[6], State = p[7], Outlet = p[8] });
                    }
                // earlier versions filed any Convince / Threaten / Back Down line as a push-out: those not about leaving are dropped
                int misread = All.RemoveAll(w => w.Kind == "pushout" && !Leave.IsMatch(w.Text ?? ""));
                if (misread > 0) { Save(); PressRoom.W("whisper", "misread push-outs dropped", misread.ToString()); }
                if (File.Exists(PeoplePath))
                    foreach (var line in File.ReadAllLines(PeoplePath))
                    {
                        var p = line.Split('\t'); if (p.Length < 2 || p[0] == "name") continue;
                        if (p[1].Length > 0) Personality[p[0]] = p[1];
                        if (p.Length >= 3 && p[2].Length > 0) Reputation[p[0]] = p[2];
                        if (p.Length >= 4 && p[3].Length > 0) Known[p[0]] = p[3];
                    }
            }
            catch (Exception e) { PressRoom.Fail("word load", e); }
        }
        internal static void Save()
        {
            try
            {
                var sb = new StringBuilder("conf\twhen\tkind\tlevel\tplayer\twith\ttext\tstate\toutlet\n");
                foreach (var w in All) sb.Append(w.Conf).Append('\t').Append(Clean(w.When)).Append('\t').Append(w.Kind).Append('\t').Append(Clean(w.Level)).Append('\t').Append(Clean(w.Player)).Append('\t')
                    .Append(Clean(w.With)).Append('\t').Append(Clean(w.Text)).Append('\t').Append(w.State).Append('\t').Append(Clean(w.Outlet)).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("word save", e); }
        }
        static void SavePeople()
        {
            try
            {
                var sb = new StringBuilder("name\tpersonality\treputation\tshown\n");
                var names = new HashSet<string>(Personality.Keys); names.UnionWith(Reputation.Keys);
                foreach (var nm in names)
                    sb.Append(Clean(nm)).Append('\t').Append(Personality.TryGetValue(nm, out var pv) ? Clean(pv) : "").Append('\t').Append(Reputation.TryGetValue(nm, out var rv) ? Clean(rv) : "").Append('\t').Append(Known.TryGetValue(nm, out var kv) && Better(kv) ? Clean(kv) : "").Append('\n');
                File.WriteAllText(PeoplePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("people save", e); }
        }
    }
}
