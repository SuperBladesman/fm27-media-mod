// FM27 Media Mod - The dressing room (Press Room 0.9.0). MIT licence (see LICENSE).
// Chain B of the project doc "Design - real-trigger misbehaviour": the discontent stories SI's engine writes about
// YOUR players (a player forcing a move, a relationship with the manager breaking down, team-mates unhappy with how it
// has been handled, a training-ground bust-up, a split dressing room) are read off the news as the game shows them,
// kept per save (incidents.tsv), listed in the MEDIA OFFICE, and raised by the press at the next conferences, sharper
// the longer they run, until the game itself reports them settled. Every incident is one SI's engine rolled and
// reported; only the journalist's opening words are ours, and they only restate what the news said.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Incidents
    {
        internal class I
        {
            public int Id, FirstConf, LastConf, Seen = 1, Raised, RaisedConf = -1;
            public string When = "", Chain = "", Player = "", PlayerId = "", Shown = "", Other = "", Flags = "", State = "open", Text = "", Stories = "";
        }
        internal static readonly List<I> All = new List<I>();
        internal static bool Enabled = true;
        internal static int PerConference = 1, Expire = 10;
        internal static string ClubSetting = "", ManagerSetting = "";
        static string SavedManager = "";
        static int NextId = 1;
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "incidents.tsv");
        static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }
        const RegexOptions RX = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        // ---------- reading the news ----------
        static readonly (string chain, Regex rx)[] Sig =
        {
            ("bustup", new Regex(@"bust[- ]?up|training ground (incident|bust)|came to blows|had to be pulled apart", RX)),
            ("feud",   new Regex(@"\bfeud\b|fallen out with|dislike between|issues between|relationship between .*? (irreconcilable|broken|strained|deteriorat)", RX)),
            ("unrest", new Regex(@"dressing[- ]?room (unrest|split|rift)|player power|divided dressing", RX)),
            ("strop",  new Regex(@"force a move|forcing a move|(looking|looks) to force|transfer request|downed tools|refus\w* to (train|play)", RX)),
        };
        static readonly Regex Settled = new Regex(@"put (his|her|their) issues behind|removed from the transfer list|willingness to play for the club again|decided to report for training|cleared the air|patched (things )?up|buried the hatchet|back in the fold", RX);
        static readonly Regex Mates = new Regex(@"team-?mates.{0,90}(unhappy|angry|annoyed|frustrat|unimpressed|headache)|handful of players.{0,60}unhappy|(players|squad) (are |is )?(reported to be )?unhappy with", RX);
        static readonly Regex Injured = new Regex(@"sidelines|out for (about|around|up to)|ruled out|faces up to", RX);
        static readonly Regex Irrecon = new Regex(@"irreconcilable", RX);
        static readonly HashSet<string> NewsEls = new HashSet<string> { "newsBodyText", "BodyText", "newsHeadlineText", "NewsStoryHeadline", "Headline", "MessageHeadline" };
        static readonly HashSet<uint> Done = new HashSet<uint>(); static readonly Queue<uint> DoneQ = new Queue<uint>();

        internal static string OwnClubName() { try { return OwnClub() ?? ""; } catch { return ""; } }
        internal static string OwnManagerName() { try { return OwnManager() ?? ""; } catch { return ""; } }
        static string OwnClub()
        {
            if (ClubSetting.Length > 0) return ClubSetting;
            try { FanPress.Fresh(); } catch { }
            return FanPress.Club ?? "";
        }
        static string OwnManager()
        {
            if (ManagerSetting.Length > 0) return ManagerSetting;
            var w = Word.ManagerName ?? "";
            if (w.Length > 0 && w != SavedManager) { SavedManager = w; Save(); }
            return SavedManager;
        }
        static bool IsManager(LinkInfo l, string mgr) => mgr.Length > 0 && l.IsPerson && (l.Full == mgr || l.Visible == mgr || (" " + l.Full + " ").Contains(" " + Last(mgr) + " ") && l.Full.StartsWith(First(mgr)));
        static string First(string n) { var p = (n ?? "").Trim().Split(' '); return p[0]; }
        static string Last(string n) { var p = (n ?? "").Trim().Split(' '); return p[p.Length - 1]; }

        // is this story about our club? our manager named; or the first club in it is ours; or "away from <our club>";
        // or the assistant's own report ("It looks like ..."), which only ever concerns your squad
        static bool Ours(List<LinkInfo> links, string plain)
        {
            var club = OwnClub(); var mgr = OwnManager();
            if (plain.StartsWith("It looks like ")) return true;
            LinkInfo firstClub = null;
            foreach (var l in links)
            {
                if (IsManager(l, mgr)) return true;
                if (!l.IsPerson && firstClub == null) firstClub = l;
            }
            if (club.Length == 0) return false;
            if (firstClub != null && (firstClub.Full == club || firstClub.Visible == club)) return true;
            foreach (var l in links)
                if (!l.IsPerson && (l.Full == club || l.Visible == club) && plain.IndexOf("away from " + l.Visible, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        internal static void OnText(VisualElement el, string txt)
        {
            try
            {
                if (!Enabled || string.IsNullOrEmpty(txt) || txt.Length < 30 || txt.IndexOf('\u0001') < 0) return;
                if (!NewsEls.Contains(el.name ?? "")) return;
                var plain = PressRoom.Visible(txt);
                // one fingerprint per story, from its visible words: re-renders and re-opened stories are the same story
                var norm = PressRoom.Record.Norm(plain);
                uint h = Hash(norm.Length > 200 ? norm.Substring(0, 200) : norm);
                if (Done.Contains(h)) return;
                Done.Add(h); DoneQ.Enqueue(h); while (DoneQ.Count > 500) Done.Remove(DoneQ.Dequeue());
                var sh = h.ToString("x8");
                bool settled = Settled.IsMatch(plain);
                string chain = null;
                if (!settled) foreach (var s in Sig) if (s.rx.IsMatch(plain)) { chain = s.chain; break; }
                if (chain == null && !settled) return;
                var links = PersonLinks.All(txt);
                bool ours = Ours(links, plain);
                PressRoom.W("dressing room", (ours ? "ours " : "elsewhere ") + (chain ?? "settled") + " @ " + el.name, Short(plain, 160));
                if (!ours) return;
                var mgr = OwnManager();
                LinkInfo player = null, other = null; bool mgrNamed = false;
                foreach (var l in links)
                {
                    if (!l.IsPerson) continue;
                    if (IsManager(l, mgr)) { mgrNamed = true; continue; }
                    if (player == null) player = l; else if (other == null) other = l;
                }
                int conf = PressRoom.Record.Conf;
                if (settled)
                {
                    if (player == null) return;
                    bool any = false;
                    foreach (var x in All) if (x.PlayerId == player.Key && (x.State == "open" || x.State == "inhouse")) { x.State = "settled"; x.LastConf = conf; any = true; }
                    if (any) { Save(); PressRoom.W("dressing room", "settled", player.Full); MediaOffice.Refresh(); }
                    return;
                }
                if (player == null && chain != "unrest") return;
                var flags = new List<string>();
                if (Mates.IsMatch(plain)) flags.Add("mates");
                if (chain == "bustup" && Injured.IsMatch(plain)) flags.Add("injured");
                if (chain == "feud" && Irrecon.IsMatch(plain)) flags.Add("irreconcilable");
                if (chain == "feud" && mgrNamed && other == null) flags.Add("you");
                // a feud with you is about the player wanting out as much as the row: one thread per player
                string pid = player != null ? player.Key : "squad";
                I hit = null;
                foreach (var x in All)
                    if (x.PlayerId == pid && (x.State == "open" || x.State == "inhouse") && (x.Chain == chain || (Thread(x.Chain) && Thread(chain)))) { hit = x; break; }
                if (hit != null)
                {
                    if ((" " + hit.Stories + " ").Contains(" " + sh + " ")) return;   // a story already counted (e.g. after a restart)
                    var st = new List<string>(hit.Stories.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); st.Add(sh); while (st.Count > 12) st.RemoveAt(0);
                    hit.Stories = string.Join(" ", st);
                    hit.Seen++; hit.LastConf = conf; hit.Text = Short(plain, 300);
                    if (chain == "feud" && hit.Chain == "strop") hit.Chain = "feud";
                    foreach (var f in flags) if (!(" " + hit.Flags + " ").Contains(" " + f + " ")) hit.Flags = (hit.Flags + " " + f).Trim();
                    if (other != null && hit.Other.Length == 0) hit.Other = other.Visible;
                    PressRoom.W("dressing room", "again " + hit.Chain + " x" + hit.Seen, (player != null ? player.Full : "squad"));
                }
                else
                {
                    All.Add(new I { Id = NextId++, FirstConf = conf, LastConf = conf, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Chain = chain,
                        Player = player != null ? player.Full : "", PlayerId = pid, Shown = player != null ? player.Visible : "",
                        Other = other != null ? other.Visible : (mgrNamed ? "you" : ""), Flags = string.Join(" ", flags), State = "open", Text = Short(plain, 300), Stories = sh });
                    while (All.Count > 200) All.RemoveAt(0);
                    PressRoom.W("dressing room", "new " + chain + (flags.Count > 0 ? " [" + string.Join(" ", flags) + "]" : ""), (player != null ? player.Full : "squad") + (other != null ? " / " + other.Full : ""));
                }
                Save();
                MediaOffice.Refresh();
            }
            catch (Exception e) { PressRoom.Fail("dressing room read", e); }
        }
        static bool Thread(string c) => c == "strop" || c == "feud";
        static string Short(string s, int n) { s = (s ?? "").Trim(); return s.Length > n ? s.Substring(0, n) : s; }

        // ---------- the press raise it ----------
        static readonly Regex Topic = new Regex(@"\b(squad|dressing[- ]room|morale|happy|unhappy|happiness|harmony|atmosphere|spirit|camp|unity|together|mood|discipline|attitude|relationship|player power|future|leave|leaving|transfer|move|contract|commit\w*|unsettled|frustrat\w*|playing time|first[- ]team|selection|dropped|bench|training|bust)\b", RX);
        static int TalkConf = -1, TalkThis;

        static string Pick(string[] pool, uint h) => pool[h % (uint)pool.Length];
        static string Fill(string t, I x) => t.Replace("{P}", x.Shown.Length > 0 ? x.Shown : x.Player).Replace("{S}", Last(x.Shown.Length > 0 ? x.Shown : x.Player)).Replace("{O}", x.Other == "you" ? "you" : x.Other);
        static bool Has(I x, string f) => (" " + x.Flags + " ").Contains(" " + f + " ");

        static readonly Dictionary<PressRoom.Kind, string[]> StropNew = new Dictionary<PressRoom.Kind, string[]>
        {
            [PressRoom.Kind.RedTop] = new[] { "The papers are full of it: {P} wants out.", "{P} is pushing for a move, according to the papers." },
            [PressRoom.Kind.PhoneIn] = new[] { "Our callers have all seen the reports that {P} is trying to force a move.", "The phones haven't stopped since the reports about {P} wanting out." },
            [PressRoom.Kind.Local] = new[] { "Supporters have read the reports that {P} wants away.", "Fans here have been reading about {P} wanting away." },
            [PressRoom.Kind.Broadcast] = new[] { "There are reports that {P} is looking to force a move.", "For the record: the reports say {P} wants to leave." },
            [PressRoom.Kind.Broadsheet] = new[] { "Reports suggest {P} is trying to engineer a move away.", "The reports say {P} is agitating for a move." },
            [PressRoom.Kind.Digital] = new[] { "The reports about {P} wanting a move are everywhere online.", "Our readers have seen the stories that {P} wants out." },
        };
        static readonly string[] StropOld = { "It's been a while now and the reports say {P} still wants out.", "There's still no end to the {S} situation, it seems.", "The {S} saga is still running, by all accounts." };
        static readonly string[] MatesLine = { "{P} wants out, and the reports say some of the squad aren't happy with how it's been handled.", "The reports say {P}'s push for a move has rubbed some team-mates up the wrong way." };
        static readonly string[] FeudYouNew = { "The reports say your relationship with {P} has broken down.", "Word is things have soured between you and {P}.", "The papers say things have gone from bad to worse between you and {P}." };
        static readonly string[] FeudYouIrrecon = { "The reports describe your relationship with {P} as irreconcilable.", "One report calls things between you and {P} irreconcilable." };
        static readonly string[] FeudYouOld = { "There's still no sign of you and {P} clearing the air.", "The reports of a rift between you and {P} haven't gone away." };
        static readonly string[] FeudTwo = { "The reports of a falling-out between {P} and {O} haven't gone unnoticed.", "Word is {P} and {O} have fallen out." };
        static readonly string[] BustNew = { "The reports of a training-ground bust-up between {P} and {O} are doing the rounds.", "Word is {P} and {O} clashed in training." };
        static readonly string[] BustHurt = { "{P} is out after a training-ground bust-up with {O}, the reports say.", "The reports say {P} has been sidelined after a bust-up with {O} in training." };
        static readonly string[] BustOld = { "The fallout from {P} and {O}'s bust-up is still being talked about.", "People are still talking about {P} and {O}'s scrap in training." };
        static readonly string[] UnrestNew = { "The reports of a split in your dressing room are everywhere.", "Word is your dressing room is divided." };
        static readonly string[] UnrestOld = { "The talk of a divided dressing room hasn't gone away." };
        static readonly string[] SettledLine = { "The reports say things are settled with {P} now.", "It seems the {S} situation is behind you, by the latest reports." };

        internal static string Describe(I x)
        {
            var p = x.Shown.Length > 0 ? x.Shown : x.Player;
            string d;
            switch (x.Chain)
            {
                case "strop": d = p + " wants a move"; break;
                case "feud": d = x.Other == "you" || x.Other.Length == 0 ? p + "'s relationship with you has broken down" : "Falling-out between " + p + " and " + x.Other; break;
                case "bustup": d = "Training-ground bust-up: " + p + (x.Other.Length > 0 && x.Other != "you" ? " and " + x.Other : "") + (Has(x, "injured") ? " (injured)" : ""); break;
                case "unrest": d = "A split in the dressing room"; break;
                default: d = p; break;
            }
            if (Has(x, "mates")) d += "; team-mates unhappy";
            return d;
        }
        internal static string StateLine(I x, int now)
        {
            if (x.State == "settled" || x.State == "closed") return "Settled, by the latest reports";
            int ago = now - x.FirstConf;
            var first = ago <= 0 ? "In the news since your last press conference" : "First reported " + ago + " press conference" + (ago == 1 ? "" : "s") + " ago";
            var seen = x.Seen > 1 ? " · in the news " + x.Seen + " times" : "";
            var asked = x.Raised > 0 ? " · the press have raised it " + x.Raised + (x.Raised == 1 ? " time" : " times") : "";
            var inh = x.State == "inhouse" ? " · closing ranks" : "";
            return first + seen + asked + inh;
        }

        // the opening line for this question, or null; subject = the player, so your answer is kept on the record about them
        internal static string Talk(string q, string outlet, string journalist, PressRoom.Kind k, out string subject)
        {
            subject = null;
            try
            {
                if (!Enabled || PerConference <= 0 || All.Count == 0) return null;
                int conf = PressRoom.Record.Conf;
                if (TalkConf != conf) { TalkConf = conf; TalkThis = 0; }
                if (TalkThis >= PerConference) return null;
                var vq = PressRoom.Visible(q ?? "");
                bool changed = false; I best = null;
                for (int i = All.Count - 1; i >= 0 && best == null; i--)
                {
                    var x = All[i];
                    if (x.State == "stale" || x.State == "closed") continue;
                    if ((x.State == "open" || x.State == "inhouse") && conf - x.LastConf > Expire) { x.State = "stale"; changed = true; continue; }
                    if (x.RaisedConf == conf) continue;
                    var name = x.Shown.Length > 0 ? x.Shown : x.Player;
                    bool named = name.Length > 0 && vq.IndexOf(Last(name), StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!named && !Topic.IsMatch(vq)) continue;
                    bool fresh = x.Raised == 0 || x.LastConf > x.RaisedConf;
                    int chance = x.State == "settled" ? 60 : fresh ? 85 : 40;
                    if (x.State == "inhouse") chance = MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? 0 : chance / 3;
                    if (k == PressRoom.Kind.Broadsheet && !named) chance /= 2;
                    if (named && x.State != "inhouse") chance = Math.Max(chance, 75);
                    if (Hash(x.Id + "|" + journalist + "|" + conf) % 100 >= (uint)chance) continue;
                    best = x;
                }
                if (changed) Save();
                if (best == null) return null;
                var hh = Hash(best.Id + outlet + conf);
                bool isNew = best.Raised == 0;
                string t;
                if (best.State == "settled") t = Pick(SettledLine, hh);
                else switch (best.Chain)
                {
                    case "strop": t = isNew ? (Has(best, "mates") ? Pick(MatesLine, hh) : Pick(StropNew.TryGetValue(k, out var sp) ? sp : StropNew[PressRoom.Kind.Digital], hh)) : Pick(StropOld, hh); break;
                    case "feud":
                        if (best.Other == "you" || best.Other.Length == 0) t = isNew ? (Has(best, "irreconcilable") ? Pick(FeudYouIrrecon, hh) : (Has(best, "mates") ? Pick(MatesLine, hh) : Pick(FeudYouNew, hh))) : Pick(FeudYouOld, hh);
                        else t = Pick(FeudTwo, hh);
                        break;
                    case "bustup":
                        if (best.Other.Length == 0 || best.Other == "you") return null;   // need both names
                        t = isNew ? (Has(best, "injured") ? Pick(BustHurt, hh) : Pick(BustNew, hh)) : Pick(BustOld, hh); break;
                    case "unrest": t = isNew ? Pick(UnrestNew, hh) : Pick(UnrestOld, hh); break;
                    default: return null;
                }
                var line = Fill(t, best);
                best.Raised++; best.RaisedConf = conf; if (best.State == "settled") best.State = "closed";
                TalkThis++; Save();
                subject = best.Player.Length > 0 ? best.Player : null;
                PressRoom.W("dressing room", "raised " + best.Chain + " " + k + " " + outlet + " / " + journalist, line);
                return line;
            }
            catch (Exception e) { PressRoom.Fail("dressing room talk", e); return null; }
        }

        // ---------- the Media Office buttons ----------
        internal static void SetState(I x, string s) { x.State = s; Save(); PressRoom.W("dressing room", "office " + s, Describe(x)); }

        // ---------- state ----------
        internal static void Reset() { All.Clear(); NextId = 1; TalkConf = -1; TalkThis = 0; SavedManager = ""; Done.Clear(); DoneQ.Clear(); }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 2 && p[0] == "#own") { SavedManager = p.Length > 2 ? p[2] : ""; continue; }
                    if (p.Length < 15 || p[0] == "id") continue;
                    var x = new I();
                    int.TryParse(p[0], out x.Id); int.TryParse(p[1], out x.FirstConf); int.TryParse(p[2], out x.LastConf); int.TryParse(p[3], out x.Seen);
                    int.TryParse(p[4], out x.Raised); int.TryParse(p[5], out x.RaisedConf);
                    x.When = p[6]; x.Chain = p[7]; x.Player = p[8]; x.PlayerId = p[9]; x.Shown = p[10]; x.Other = p[11]; x.Flags = p[12]; x.State = p[13]; x.Text = p[14]; if (p.Length >= 16) x.Stories = p[15];
                    All.Add(x); if (x.Id >= NextId) NextId = x.Id + 1;
                }
            }
            catch (Exception e) { PressRoom.Fail("dressing room load", e); }
        }

        internal static void Save()
        {
            try
            {
                var sb = new StringBuilder("id\tfirstconf\tlastconf\tseen\traised\traisedconf\twhen\tchain\tplayer\tplayerid\tshown\tother\tflags\tstate\ttext\tstories\n");
                sb.Append("#own\t").Append(Clean(OwnClubSafe())).Append('\t').Append(Clean(SavedManager)).Append('\n');
                foreach (var x in All)
                    sb.Append(x.Id).Append('\t').Append(x.FirstConf).Append('\t').Append(x.LastConf).Append('\t').Append(x.Seen).Append('\t').Append(x.Raised).Append('\t').Append(x.RaisedConf).Append('\t')
                      .Append(Clean(x.When)).Append('\t').Append(x.Chain).Append('\t').Append(Clean(x.Player)).Append('\t').Append(x.PlayerId).Append('\t').Append(Clean(x.Shown)).Append('\t')
                      .Append(Clean(x.Other)).Append('\t').Append(Clean(x.Flags)).Append('\t').Append(x.State).Append('\t').Append(Clean(x.Text)).Append('\t').Append(x.Stories).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("dressing room save", e); }
        }
        static string OwnClubSafe() { try { return ClubSetting.Length > 0 ? ClubSetting : (FanPress.Club ?? ""); } catch { return ""; } }
    }
}
