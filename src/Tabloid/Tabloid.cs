// FM27 Media Mod - Tabloid Layer 0.1. MIT licence (see LICENSE).
// Every story is given an outlet type: red top (tabloid) or blue top (broadsheet). Red tops get rewritten headlines
// (the same facts, louder verbs, capitals) and a red masthead band; blue tops keep the sober wording on a navy band.
// Names and links inside a headline are kept exactly (FM marks them \x01<link data>\x02<name>\x01).
// A stable share of stories (by headline) gets the treatment, so the same story always reads the same way.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Tabloid Layer", "0.6.7")]
    public class Tabloid : BasePlugin
    {
        public const string GUID = "uk.nik.fm27mediamod.tabloid";
        internal static BepInEx.Logging.ManualLogSource L;
        internal static int Share, Fans; internal static bool Caps, Log2, Kick;
        internal static readonly Dictionary<string, string> Handles = new Dictionary<string, string>();
        internal static int Errors;
        internal static readonly Regex Link = new Regex("\u0001([A-Za-z0-9+/=]+)\u0002(.*?)\u0001", RegexOptions.Compiled);
        internal struct Res { public string Text; public bool Red; public bool Hit; }
        internal static readonly Dictionary<string, Res> Cache = new Dictionary<string, Res>();
        internal static System.IO.StreamWriter Out;

        public override void Load()
        {
            L = Log;
            Share = Config.Bind("Tabloid", "Share", 75, "Percent of stories that get the tabloid treatment (0 = off, 100 = all). Stable per story.").Value;
            Caps = Config.Bind("Tabloid", "Capitals", true, "Red-top capitals on rewritten headlines.").Value;
            Kick = Config.Bind("Tabloid", "Kickers", true, "Red-top labels in front (WAR OF WORDS:, BOMBSHELL:, CRISIS: ...).").Value;
            Log2 = Config.Bind("Tabloid", "LogRewrites", true, "Write every rewrite to BepInEx\\FM27MediaMod\\tabloid_<time>.tsv.").Value;
            if (Log2)
            {
                var dir = System.IO.Path.Combine(Paths.BepInExRootPath, "FM27MediaMod"); System.IO.Directory.CreateDirectory(dir);
                Out = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "tabloid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            }
            Terraces.Emoji = Config.Bind("Fans", "Emoji", true, "Let the fans use emoji, drawn from Windows' own emoji font (Segoe UI Emoji). Set to false to leave them out.").Value;
            Terraces.On = Config.Bind("Fans", "TheTerraces", true, "Write the fan posts under each story (SI's own Top Reactions slots) from the club's supporter profile, mood, expectations and memories, as SI's supporter screens show them. When on, the extra posts below are not added.").Value;
            Fans = Config.Bind("Fans", "ExtraReactions", 2, "Fan posts of our own added once under each story's Top Reactions, after SI's (0 = none, at most 2).").Value;
            if (Fans > 2) Fans = 2;
            foreach (var (key, def, who) in new[] {
                ("ITK", "@{club}ITK", "the know-it-all account"), ("Fanzine", "@{club}Fanzine", "the fanzine"), ("Pod", "@The{club}Pod", "the fan podcast"),
                ("Rival", "auto", "the rival fan"), ("Dad", "auto", "the long-suffering fan"), ("Tactics", "auto", "the tactics bore"),
                ("Loyalist", "auto", "the loyalist"), ("Neutral", "auto", "the neutral"), ("Defender", "auto", "the fan who defends the manager") })
                Handles[key] = Config.Bind("Fans", "Voice_" + key, def, $"Name shown for {who}. 'auto' invents a fan's name (like SI's own fan posts), the same one for the same story. {{club}} becomes the club's hashtag name.").Value;
            try { new Harmony(GUID).PatchAll(typeof(HeadlinePatch)); } catch (Exception e) { Log.LogError("Tabloid layer not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(FanPatch)); } catch (Exception e) { Log.LogError("Fan reactions not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(HeadlinePatch_Apply)); new Harmony(GUID).PatchAll(typeof(FanPatch_Apply)); } catch (Exception e) { Log.LogWarning("Tabloid: ApplyText hooks not active (fine before the 17 Nov patch): " + e.Message); }
            Log.LogInfo($"FM27 Media Mod Tabloid Layer 0.6.7 loaded (share {Share}%, capitals {Caps}, the terraces {Terraces.On}, extra fan posts {(Terraces.On ? 0 : Fans)}).");
        }

        internal static void Fail(string where, Exception e) { if (Errors++ < 20) L.LogWarning($"[{where}] {e.GetType().Name}: {e.Message}"); }

        static readonly string[] HeadlineNames = { "MessageHeadline", "newsHeadlineText", "Headline", "NewsStoryHeadline", "HeadlineText" };

        internal static bool IsHeadline(VisualElement e)
        {
            for (int i = 0; i < 3 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                foreach (var h in HeadlineNames) if (n == h) return true;
            }
            return false;
        }

        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        internal static Res Rewrite(string txt)
        {
            if (Cache.TryGetValue(txt, out var done)) return done;
            var links = new List<Match>();
            var masked = Link.Replace(txt, m => { links.Add(m); return "\u0003" + (links.Count - 1) + "\u0004"; });
            var visible = Link.Replace(txt, m => m.Groups[2].Value);
            var res = new Res { Text = txt, Red = Share > 0 && Hash(visible) % 100 < (uint)Share, Hit = false };
            if (res.Red)
            {
                var s = HeadRules.Apply(masked, Kick, out bool hit);
                res.Hit = hit;
                res.Text = Restore(s, links, Caps);
                if (hit) try { Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\tred\t{visible}\t{Link.Replace(res.Text, m => m.Groups[2].Value)}"); } catch { }
            }
            if (Cache.Count > 5000) Cache.Clear();
            Cache[txt] = res;
            return res;
        }

        // put the links back; capitals on visible text only, never on the link data
        internal static string Restore(string s, List<Match> links, bool caps)
        {
            var sb = new StringBuilder(); int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '\u0003')
                {
                    int j = s.IndexOf('\u0004', i); int k = int.Parse(s.Substring(i + 1, j - i - 1)); var m = links[k];
                    sb.Append('\u0001').Append(m.Groups[1].Value).Append('\u0002').Append(caps ? m.Groups[2].Value.ToUpperInvariant() : m.Groups[2].Value).Append('\u0001');
                    i = j + 1;
                }
                else { sb.Append(caps ? char.ToUpperInvariant(s[i]) : s[i]); i++; }
            }
            return sb.ToString();
        }

        // ---------- mastheads ----------
        internal static readonly UnityEngine.Color RedTop = new UnityEngine.Color(0.80f, 0.04f, 0.10f, 1f);
        internal static readonly UnityEngine.Color BlueTop = new UnityEngine.Color(0.02f, 0.16f, 0.38f, 1f);
        internal static readonly UnityEngine.Color White = new UnityEngine.Color(1f, 1f, 1f, 1f);
        const string Mark = "fm27mm-top";

        // kind: 0 not a headline, 1 news card (always a story), 2 inbox list row, 3 message page
        internal static int Kind(VisualElement e)
        {
            for (int i = 0; i < 3 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                if (n == "newsHeadlineText" || n == "NewsStoryHeadline") return 1;
                if (n == "MessageHeadline") return 2;
                if (n == "Headline" || n == "HeadlineText") return 3;
            }
            return 0;
        }

        internal static string Plain(string t) => t.IndexOf('\u0001') < 0 ? t : Link.Replace(t, m => m.Groups[2].Value);

        internal static void Band(VisualElement e, UnityEngine.Color c)
        {
            e.AddToClassList(Mark);
            e.style.backgroundColor = c; e.style.color = White;
            e.style.paddingLeft = 8; e.style.paddingRight = 8; e.style.paddingTop = 3; e.style.paddingBottom = 3;
            e.style.borderTopLeftRadius = 3; e.style.borderTopRightRadius = 3; e.style.borderBottomLeftRadius = 3; e.style.borderBottomRightRadius = 3;
        }

        internal static void Bar(VisualElement e, UnityEngine.Color c)
        {
            e.AddToClassList(Mark);
            e.style.borderLeftWidth = 4; e.style.borderLeftColor = c; e.style.paddingLeft = 6;
        }

        internal static void Clear(VisualElement e)
        {
            if (!e.ClassListContains(Mark)) return;
            e.RemoveFromClassList(Mark);
            e.style.backgroundColor = new StyleColor(StyleKeyword.Null); e.style.color = new StyleColor(StyleKeyword.Null);
            e.style.paddingLeft = new StyleLength(StyleKeyword.Null); e.style.paddingRight = new StyleLength(StyleKeyword.Null);
            e.style.paddingTop = new StyleLength(StyleKeyword.Null); e.style.paddingBottom = new StyleLength(StyleKeyword.Null);
            e.style.borderTopLeftRadius = new StyleLength(StyleKeyword.Null); e.style.borderTopRightRadius = new StyleLength(StyleKeyword.Null);
            e.style.borderBottomLeftRadius = new StyleLength(StyleKeyword.Null); e.style.borderBottomRightRadius = new StyleLength(StyleKeyword.Null);
            e.style.borderLeftWidth = new StyleFloat(StyleKeyword.Null); e.style.borderLeftColor = new StyleColor(StyleKeyword.Null);
        }


    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class HeadlinePatch
    {
        // SI's 17 Nov patch: bound text now goes through ApplyText, so the same code runs there too (see HeadlinePatch_Apply);
        // a call that comes from inside SetText on the same element is left to the SetText hook.
        [System.ThreadStatic] internal static System.IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { InSet = __instance.Pointer; Pre(__instance, ref txt); }
        static void Postfix() { InSet = System.IntPtr.Zero; }

        internal static void Pre(SI.Bindable.SIText __instance, ref string txt)
        {
            try
            {
                if (Tabloid.Share < 0) return;
                int kind = Tabloid.Kind(__instance);
                if (kind == 0) return;
                // judge length by the words shown, not the hidden link data (two names add ~250 characters)
                if (string.IsNullOrEmpty(txt)) { Tabloid.Clear(__instance); return; }
                var shown = txt.IndexOf('\u0001') >= 0 ? Tabloid.Link.Replace(txt, m => m.Groups[2].Value) : txt;
                if (shown.Length < 8 || shown.Length > 220) { Tabloid.Clear(__instance); return; }
                if (kind == 1)
                {
                    var r = Tabloid.Rewrite(txt);
                    // a news card is always a story: every one gets a masthead
                    if (r.Red)
                    {
                        txt = (!r.Hit && Tabloid.Caps) ? txt.ToUpperInvariantKeepLinks() : r.Text;
                        txt = Tabloid.Plain(txt);   // your club's name is drawn in orange: unreadable on red, so print it plain
                        Tabloid.Band(__instance, Tabloid.RedTop);
                    }
                    else { txt = Tabloid.Plain(txt); Tabloid.Band(__instance, Tabloid.BlueTop); }
                    return;
                }
                // inbox rows and message headers are the sender's own subject line (press officer, coach, scout):
                // they keep SI's wording. Only the article itself (a news card) speaks in its paper's voice.
                Tabloid.Clear(__instance);
            }
            catch (Exception e) { Tabloid.Fail("headline", e); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class HeadlinePatch_Apply
    {
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { if (__instance.Pointer == HeadlinePatch.InSet) return; HeadlinePatch.Pre(__instance, ref txt); }
    }

    static class Ext
    {
        // capitals for visible text only: link data (between \x01 and \x02) keeps its case
        public static string ToUpperInvariantKeepLinks(this string s)
        {
            // states: 0 plain text, 1 link data, 2 link name
            var sb = new StringBuilder(s.Length); int st = 0;
            foreach (var c in s)
            {
                if (c == '\u0001') { st = st == 0 ? 1 : 0; sb.Append(c); continue; }
                if (c == '\u0002' && st == 1) { st = 2; sb.Append(c); continue; }
                sb.Append(st == 1 ? c : char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }

    // ---------------- fan reactions ----------------
    // Story pages carry "Top Reactions": SI's fan posts, chosen by the engine to match the story. We keep those and add
    // our own regulars underneath: the know-it-all, the rival, the long-suffering dad, the tactics bore, the neutral.
    // Flavour only: they change nothing in the game.
    static class FanDesk
    {
        internal static string LastBody = "", LastHead = "";
        internal static readonly HashSet<IntPtr> Pending = new HashSet<IntPtr>();
        internal static readonly Regex Tag = new Regex("\\|#(?:<[^>]*>|\u0001[A-Za-z0-9+/=]*\u0002)*([^\u0001<|]+)", RegexOptions.Compiled);   // 0.6.5: also the read-back form, |#<color..><link..><style..>Name
        internal static readonly UnityEngine.Color Handle = new UnityEngine.Color(0.47f, 0.60f, 0.88f, 1f);
        internal static readonly UnityEngine.Color Body = new UnityEngine.Color(0.90f, 0.90f, 0.93f, 1f);

        // (kind, keywords) checked in order against the story's headline and text
        static readonly (string kind, Regex rx)[] Kinds =
        {
            ("storm", new Regex(@"storm(ed|ing)? out|blows top|loses cool|lost (his|her|their) cool|walked out of|refus(ed|es|ing) to (answer|comment)|declined to (answer|comment)|no comment", RegexOptions.IgnoreCase)),
            ("unrest", new Regex(@"irreconcilable|wants out|transfer request|unhappy|ramps up (his|her|their) efforts to leave|complain|moaning|unsettled", RegexOptions.IgnoreCase)),
            ("crisis", new Regex(@"pressure|heat (building|mounts)|under fire|sack|position is|crisis|serious questions|all is not well|below expectations", RegexOptions.IgnoreCase)),
            ("injury", new Regex(@"injur|ruled out|sidelined|virus|knock|scan", RegexOptions.IgnoreCase)),
            ("award", new Regex(@"of the month|of the week|award|scoops|accolade", RegexOptions.IgnoreCase)),
            ("signing", new Regex(@"signing|signed|joins|completed the|debut|unveil|loan", RegexOptions.IgnoreCase)),
            ("defeat", new Regex(@"defeat|beaten|lost|losing|suffered", RegexOptions.IgnoreCase)),
            ("win", new Regex(@"\bwin\b|victory|won\b|comeback|beat\b|three points", RegexOptions.IgnoreCase)),
        };

        // {club} = the club's nickname from SI's own hashtags, {who} = the story's first subject
        static readonly Dictionary<string, (string who, string text)[]> Posts = new Dictionary<string, (string, string)[]>
        {
            ["storm"] = new[] {
                ("Fanzine", "Next issue's cover just wrote itself. #{club}"),
                ("Pod", "Emergency pod tonight. We need to talk about that press conference."),
                ("Loyalist", "Walking out of a presser over one question? Get a grip. #{club}"),
                ("Neutral", "Best entertainment in the league isn't on the pitch any more #{club}"),
                ("ITK", "Said yesterday something was brewing behind the scenes. More to come on this."),
                ("Rival", "Imagine being that rattled by a journalist. Fragile. #{club}"),
                ("Dad", "Good. About time someone told that lot where to go. #{club}"),
            },
            ["crisis"] = new[] {
                ("Pod", "Emergency pod tonight. Bring tissues. #{club}"),
                ("Fanzine", "We'll say it: this needs sorting before it gets ugly. #{club}"),
                ("Dad", "Seen this film before. Board need to act before it's too late. #{club}"),
                ("Defender", "Not having this. Give the gaffer time, look where we were two years ago #{club}"),
                ("Rival", "Bookies have stopped taking bets on this one #{who}"),
                ("ITK", "Board meeting on Monday. That's all I'm saying."),
            },
            ["unrest"] = new[] {
                ("Fanzine", "Whatever has gone on, airing it in public helps nobody. #{club}"),
                ("Pod", "Full breakdown of the {who} situation on tonight's pod."),
                ("Loyalist", "Nobody is bigger than the club. Sell and move on. #{club}"),
                ("ITK", "Hearing the dressing room is split down the middle on this. Watch this space."),
                ("Tactics", "Strange way to treat one of your best players. Doesn't add up. #{who}"),
                ("Rival", "Lovely to see it. Pass the popcorn. #{club}"),
            },
            ["defeat"] = new[] {
                ("Pod", "Pod's up. Forty minutes of us shouting. Enjoy. #{club}"),
                ("Fanzine", "Letters page is going to be lively this month. #{club}"),
                ("Dad", "Paid good money to watch that. Never again. (See you next week.) #{club}"),
                ("Rival", "Bottlers. Every single season. #{club}"),
                ("Tactics", "No plan B. Same problem every week. #{club}"),
            },
            ["win"] = new[] {
                ("Fanzine", "Print it. Frame it. #{club}"),
                ("Pod", "Celebration pod tonight, all welcome. #{club}"),
                ("Rival", "Enjoy it while it lasts. Back to normal by March. #{club}"),
                ("Loyalist", "Get in!!! Scenes. #{club}"),
                ("Neutral", "Grudging respect. That was proper football. #{club}"),
            },
            ["signing"] = new[] {
                ("Pod", "New signing special tonight: who is {who}? #{club}"),
                ("Fanzine", "Welcome to the club, {who}. Don't believe the hype. Or do. #{club}"),
                ("ITK", "Been saying this for weeks. You heard it here first. #{who}"),
                ("Dad", "Who? Had to look this one up. Hope the scouts know something I don't. #{club}"),
                ("Rival", "Panic buy. #{club}"),
                ("Tactics", "Interesting one. Fits the system if the gaffer sticks with three at the back. #{who}"),
            },
            ["injury"] = new[] {
                ("Pod", "We've done the physio maths on the pod. It's not good. #{club}"),
                ("Dad", "Of course. Of course it is. #{club}"),
                ("Tactics", "Another soft-tissue one. Somebody look at the training load. #{club}"),
                ("Rival", "Made of glass, that squad #{club}"),
            },
            ["award"] = new[] {
                ("Fanzine", "Somebody check the trophy cabinet still shuts. #{club}"),
                ("Rival", "Monthly award curse incoming #{club}"),
                ("Loyalist", "Deserved. Stick that in your pipe. #{club}"),
                ("Neutral", "Fair play. Didn't see that coming in August."),
            },
            ["other"] = new[] {
                ("Fanzine", "Never a quiet week. #{club}"),
                ("Pod", "New episode out now. We cover this, and more. #{club}"),
                ("Neutral", "Never a dull day at this club #{club}"),
                ("ITK", "Not everything is being reported here. Trust me."),
                ("Dad", "I've supported this club for forty years and I still don't understand it. #{club}"),
            },
        };

        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        // invented fans, in the style of SI's own fan posts: first name and surname
        static readonly string[] First = { "Jess", "Joe", "Kallum", "Liam", "Megan", "Ryan", "Sophie", "Dan", "Chloe", "Tom", "Abbie", "Callum",
            "Ellie", "Jordan", "Hannah", "Kyle", "Lauren", "Scott", "Amy", "Craig", "Becky", "Lewis", "Gemma", "Nathan", "Holly", "Jamie",
            "Stacey", "Aaron", "Leanne", "Mark", "Danielle", "Gary", "Kirsty", "Ash", "Nicola", "Dean", "Rachel", "Wayne", "Zoe", "Paul",
            "Priya", "Imran", "Aisha", "Harvey", "Faye", "Tariq", "Jade", "Owen", "Bethany", "Rhys" };
        static readonly string[] Last = { "Tomlin", "Hallam", "Grivosti", "Kaloczi", "Ashworth", "Brammer", "Crookes", "Dyson", "Ellis", "Fenwick",
            "Garside", "Hepworth", "Ibbotson", "Jessop", "Kirkby", "Lockwood", "Marsden", "Naylor", "Oldroyd", "Pickering", "Quarmby", "Rowbotham",
            "Senior", "Turton", "Unwin", "Varley", "Wragg", "Youle", "Atkin", "Beever", "Cawthorne", "Denton", "Eyre", "Froggatt", "Gill", "Hague",
            "Ingle", "Jagger", "Kenworthy", "Lister", "Mellor", "Nuttall", "Oxley", "Platts", "Ramsden", "Staniforth", "Tingle", "Walsh", "Hussain", "Begum" };

        static string Invent(string voice, string story)
        {
            uint h = Hash(voice + "|" + story);
            return First[h % (uint)First.Length] + " " + Last[(h / 97) % (uint)Last.Length];
        }

        internal static void Note(VisualElement e, string txt)
        {
            var n = e.name ?? "";
            if (n == "BodyText" || (e.parent != null && e.parent.name == "NewsStoryBodyText")) LastBody = Tabloid.Plain(txt);
            else if (n == "Headline" || n == "newsHeadlineText") LastHead = Tabloid.Plain(txt);
        }

        // our posts go in a block of our own just after SI's list, never inside the game's own (pooled) list
        internal static void Augment(VisualElement list, List<string> tags)
        {
            try
            {
                if (Tabloid.Fans <= 0) return;
                var host = list.parent; if (host == null) return;
                var story = LastHead + " " + LastBody;
                string key = Hash(story).ToString();
                VisualElement block = null;
                for (int i = 0; i < host.childCount; i++) if (host.ElementAt(i).name == "FM27MM-Fans") block = host.ElementAt(i);
                if (block != null && block.tooltip == key) return;
                if (block == null)
                {
                    block = new VisualElement(); block.name = "FM27MM-Fans";
                    int at = host.IndexOf(list); host.Insert(at < 0 ? host.childCount : at + 1, block);
                }
                block.Clear(); block.tooltip = key;
                string kind = "other";
                foreach (var (k, rx) in Kinds) if (rx.IsMatch(story)) { kind = k; break; }
                var pool = Posts[kind];
                string club = tags.Count > 0 ? tags[tags.Count - 1] : "";
                string who = tags.Count > 1 ? tags[0] : club;
                uint h = Hash(story);
                int n = Math.Min(Tabloid.Fans, pool.Length);
                for (int i = 0; i < n; i++)
                {
                    var (handle, text) = pool[(int)((h + (uint)i) % (uint)pool.Length)];   // consecutive: never the same post twice
                    string clubTag = club.Replace(" ", "").Replace("-", "");
                    var voice = handle;
                    if (!Tabloid.Handles.TryGetValue(voice, out handle) || string.IsNullOrWhiteSpace(handle)) handle = "auto";
                    if (handle.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)) handle = Invent(voice, story);
                    handle = handle.Replace("{club}", clubTag.Length > 0 ? clubTag : "Terrace");
                    text = text.Replace("#{club}", clubTag.Length > 0 ? "<color=#7693dbff>#" + clubTag + "</color>" : "")
                               .Replace("#{who}", who.Length > 0 ? "<color=#7693dbff>#" + who.Replace(" ", "") + "</color>" : "")
                               .Replace("{who}", who).Replace("{club}", club);
                    var row = new VisualElement(); row.name = "FM27MM-Fan";
                    row.style.marginTop = 8; row.style.paddingLeft = 2;
                    var a = new Label(handle); a.style.color = Handle; a.style.fontSize = 12;
                    var t = new Label(text); t.style.color = Body; t.style.fontSize = 13; t.style.whiteSpace = WhiteSpace.Normal;
                    row.Add(t); row.Add(a);
                    block.Add(row);
                }
                try { Tabloid.Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\tfans\t{kind}\t{LastHead}"); } catch { }
            }
            catch (Exception e) { Tabloid.Fail("fans", e); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class FanPatch
    {
        // SI's 17 Nov patch: bound text now goes through ApplyText, so the same code runs there too (see FanPatch_Apply);
        // a call that comes from inside SetText on the same element is left to the SetText hook.
        [System.ThreadStatic] internal static System.IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { InSet = __instance.Pointer; Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { InSet = System.IntPtr.Zero; Post(__instance, txt); }

        // a fan post (or its author) SI shows again in the same story keeps our words
        internal static void Pre(SI.Bindable.SIText __instance, ref string txt)
        {
            try
            {
                if (Terraces.Applying || !Terraces.On || Terraces.Replaced.Count == 0 || string.IsNullOrEmpty(txt)) return;
                var m = Terraces.Map(txt); if (m == null) return;
                for (VisualElement e = __instance; e != null; e = e.parent) if (e.name == "ReactionsList") { txt = m; return; }
            }
            catch (Exception e) { Tabloid.Fail("terraces pre", e); }
        }

        internal static void Post(SI.Bindable.SIText __instance, string txt)
        {
            try
            {
                if (string.IsNullOrEmpty(txt) || Terraces.Applying) return;
                FanDesk.Note(__instance, txt);
                FanBook.OnText(__instance, txt);
                if (__instance.name != "Description") return;
                if (Terraces.On)
                {
                    // the club's fans, in SI's own post slots
                    for (VisualElement e = __instance.parent; e != null; e = e.parent)
                    {
                        var nm = e.name ?? "";
                        if (nm == "ReactionsList") { Terraces.Schedule(e); return; }
                        if (nm == "Body" || nm == "SocialReactionsBlock") return;
                    }
                    return;
                }
                if (Tabloid.Fans <= 0) return;
                // add ours once per story, after SI's whole list (0.4.x added them inside every SI post: a story with three posts got six of ours)
                VisualElement view = null;
                for (var e = __instance.parent; e != null; e = e.parent)
                {
                    var nm = e.name ?? "";
                    if (nm == "ReactionsList") { view = e; break; }
                    if (nm == "Body" || nm == "SocialReactionsBlock") break;
                }
                if (view == null) return;
                var tags = new List<string>();
                foreach (Match m in FanDesk.Tag.Matches(txt)) { var t = m.Groups[1].Value.Trim(); if (t.Length > 0 && !tags.Contains(t)) tags.Add(t); }
                var p = view.Pointer;
                if (!FanDesk.Pending.Add(p)) return;
                var keep = view;
                System.Action run = () => { FanDesk.Pending.Remove(p); FanDesk.Augment(keep, tags); };
                view.schedule.Execute(run).ExecuteLater(50);
            }
            catch (Exception e) { Tabloid.Fail("fanpatch", e); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class FanPatch_Apply
    {
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { if (__instance.Pointer == FanPatch.InSet) return; FanPatch.Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { if (__instance.Pointer == FanPatch.InSet) return; FanPatch.Post(__instance, txt); }
    }
}
