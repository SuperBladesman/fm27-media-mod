// FM27 Media Mod - Press Room 0.4 "On the record". MIT licence (see LICENSE).
// 0.9.0: the dressing room (Incidents.cs): discontent the news reports about your players is kept per save, shown in the
// MEDIA OFFICE and raised by the press; and when a journalist quotes your earlier answer and that same answer is offered
// again, the option reads "I still feel the same" instead of repeating itself (what you said is still kept in SI's words).
// 0.4: every press-conference answer you give is kept (BepInEx\FM27MediaMod\on the record.tsv). When a later
// question is the same one again, or names a player you were asked about before, the journalist opens by quoting
// your own words back to you. The question and its answers are untouched: only the journalist's opening line is ours.
// Journalists with the personality of their outlet: in press conferences some questions get a lead-in in the
// outlet's voice (red top, phone-in, broadsheet, broadcaster, local paper, digital), outlets are marked red (red tops
// and phone-ins) or navy (broadsheets), and the press-conference invite gets a one-line briefing on who is in the room.
// Text and colour only: the question, its answers and their effects are SI's.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    [BepInPlugin(GUID, "FM27 Media Mod Press Room", Version)]
    public class PressRoom : BasePlugin
    {
        internal const string Version = "0.13.4";
        public const string GUID = "uk.nik.fm27mediamod.pressroom";
        internal static BepInEx.Logging.ManualLogSource L;
        internal static int LeadShare; internal static bool Pills, Briefing;
        internal static int Errors;
        internal static System.IO.StreamWriter Out;

        public override void Load()
        {
            L = Log;
            MediaOffice.TabTop = Config.Bind("Media Office", "TabTop", 120, "How far down the screen the MEDIA tab sits on the right edge (in screen units; another mod's tab sits around 500).").Value;
            GenericPerConference = Config.Bind("Press room", "GenericLeadInsPerConference", 2, "At most this many plain lead-ins (\"For the viewers at home.\") per conference; memories, whispers, agents and supporters' views come on top.").Value;
            LeadShare = Config.Bind("Press room", "LeadInShare", 60, "Percent of press-conference questions that get a lead-in in the outlet's voice (0 = none).").Value;
            Pills = Config.Bind("Press room", "OutletColours", true, "Mark red tops and phone-ins red, broadsheets navy.").Value;
            Briefing = Config.Bind("Press room", "Briefing", true, "One-line briefing on who is in the room, on the press-conference invite.").Value;
            Record.PerConference = Config.Bind("On the record", "CallbacksPerConference", 2, "Most questions per press conference that open by quoting your own earlier answer back to you (0 = off).").Value;
            Record.FileName = Config.Bind("On the record", "File", "on the record.tsv", "File in BepInEx\\FM27MediaMod that keeps your answers. Start a new save? Change the name (or move the file aside) so old quotes don't follow you.").Value;
            EchoRewrite = Config.Bind("On the record", "RewordRepeatedAnswer", true, "When a journalist quotes your earlier answer and that same answer is among the options, show it as standing by it (\"I still feel the same\") instead of repeating it word for word. What you said is still kept in SI's words; the answer and its effect are unchanged.").Value;
            Pack.On = Config.Bind("Press pack", "Enabled", true, "Journalists with their own characters and agendas: how you get on depends on what you say, your results and what you've done, and drifts back to normal over time. Off = everyone warms up with every answer, as before 0.10.").Value;
            var harsh = Config.Bind("Press pack", "Harshness", "realistic", "gentle, realistic or brutal: how hard the press take a slight, and how long they hold it.").Value.Trim().ToLowerInvariant();
            Pack.Harsh = harsh.StartsWith("g") ? 0 : harsh.StartsWith("b") ? 2 : 1;
            Pack.PerConference = Config.Bind("Press pack", "OpenersPerConference", 2, "Most questions per press conference that open with a journalist's own edge (a U-turn thrown back, a feud, the pressure on you).").Value;
            Incidents.Enabled = Config.Bind("Dressing room", "Enabled", true, "Discontent the news reports about your players (wanting out, falling-outs with you or team-mates, team-mates unhappy, training-ground bust-ups) is kept per save, listed in the MEDIA OFFICE and raised by the press until the game reports it settled.").Value;
            Incidents.PerConference = Config.Bind("Dressing room", "PerConference", 1, "Most questions per press conference that open with a dressing-room story (0 = never).").Value;
            Incidents.Expire = Config.Bind("Dressing room", "FadeAfterConferences", 10, "A story not seen in the news again for this many press conferences is dropped.").Value;
            Incidents.ClubSetting = Config.Bind("Dressing room", "OwnClub", "", "Your club as the news names it. Empty = read from the supporters screen (fans.tsv).").Value;
            Incidents.ManagerSetting = Config.Bind("Dressing room", "ManagerName", "", "Your manager's name as the news writes it. Empty = learned from chat screens.").Value;
            var dir = System.IO.Path.Combine(Paths.BepInExRootPath, "FM27MediaMod"); System.IO.Directory.CreateDirectory(dir);
            Out = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "pressroom_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            try { new Harmony(GUID).PatchAll(typeof(PressTextPatch)); } catch (Exception e) { Log.LogError("Press Room not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(PressTextPatch_Apply)); } catch (Exception e) { Log.LogWarning("Press Room: ApplyText hook not active (fine before the 17 Nov patch): " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(PressEventPatch)); } catch (Exception e) { Log.LogWarning("Journalist memory not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(SpeakToPatch)); } catch (Exception e) { Log.LogWarning("Press Room: sending the assistant not noticed: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(PickPatch)); } catch (Exception e) { Log.LogWarning("Answer capture (button) not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(SubmitPatch)); } catch (Exception e) { Log.LogWarning("Answer capture (keys) not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(AnswerPatch)); } catch (Exception e) { Log.LogWarning("On the record not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(MemoryReceiverPatch)); } catch (Exception e) { Log.LogWarning("Memory per save not active: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(MemoryLoadPatch)); } catch (Exception e) { Log.LogWarning("Memory: load hook not active, will guess the save: " + e.Message); }
            try { new Harmony(GUID).PatchAll(typeof(MemorySavePatch)); } catch (Exception e) { Log.LogWarning("Memory: save hook not active, will use the newest save file: " + e.Message); }
            Grudges.Load();
            Record.Load();
            Desk.Hours = Math.Max(1, Math.Min(8, Config.Bind("Media Office", "HoursPerWeek", 3, "0.13: hours of press-office time a week for the office's own actions (bans, favours, planting, keeping quiet, briefing back, closing ranks, missions). Unused time is lost.").Value));
            BadgesCfg = Config.Bind("Media Office", "Badges", true, "0.13: show SI (the game itself acts) or Story (the mod's world reacts) beside each action.");
            RiskCfg = Config.Bind("Media Office", "RiskBands", true, "0.13: show each action's risk as low, medium or high.");
            Desk.Badges = BadgesCfg.Value; Desk.RiskBands = RiskCfg.Value;
            MediaOffice.Enabled = Config.Bind("Media Office", "Enabled", true, "The MEDIA OFFICE tab: head of media, press list (welcome, favoured, banned) and missions.").Value;
            MediaOffice.Load();
            Word.Load();
            Deals.Load();
            Incidents.Load();
            Pack.Load();
            Voices.On = Config.Bind("Voices", "Enabled", true, "Re-voice marked press lines: the persona you pick in the MEDIA OFFICE for your answers, and the asking journalist's own way of putting marked questions. Off = SI's wording (the invisible markers are still removed).").Value;
            Voices.JournoOn = Config.Bind("Voices", "JournalistVoices", true, "Attack dogs and contrarians ask marked questions more sharply, cheerleaders more warmly.").Value;
            Voices.Init();
            Log.LogInfo($"Press Room {Version}: voices {(Voices.On ? "on" : "off")}, {VoiceCore.E.Count} marked lines with variants ({VoiceCore.Rows} rows, {VoiceCore.Bad} bad), persona {Voices.Persona}, journalist voices {Voices.JournoOn}.");
            Log.LogInfo($"FM27 Media Mod Press Room {Version} loaded (press pack {(Pack.On ? new[] { "gentle", "realistic", "brutal" }[Pack.Harsh] : "off")},  (lead-ins {LeadShare}%, colours {Pills}, briefing {Briefing}; on the record: {Record.All.Count} answers kept, up to {Record.PerConference} callbacks per conference; media office {(MediaOffice.Enabled ? (MediaOffice.Mode == MediaOffice.Approach.None ? "on, no head of media yet" : "on, " + MediaOffice.HeadName + " (" + MediaOffice.Mode + ")") : "off")}).");
            Log.LogInfo($"Press Room {Version}: dressing room {(Incidents.Enabled ? "on" : "off")} ({Incidents.All.Count} kept, up to {Incidents.PerConference} per conference); repeated answers reworded: {EchoRewrite}.");
        }

        static BepInEx.Configuration.ConfigEntry<bool> BadgesCfg, RiskCfg;
        internal static void SaveOfficeSettings()
        {
            try { if (BadgesCfg != null) BadgesCfg.Value = Desk.Badges; if (RiskCfg != null) RiskCfg.Value = Desk.RiskBands; }
            catch (Exception e) { Fail("office settings", e); }
        }
        internal static void Fail(string where, Exception e) { if (Errors++ < 20) L.LogWarning($"[{where}] {e.GetType().Name}: {e.Message}"); }
        internal static void W(string a, string b, string c) { try { Out?.WriteLine($"{DateTime.Now:HH:mm:ss}\t{a}\t{b}\t{c}"); } catch { } }

        internal static readonly Regex Link = new Regex("\u0001([A-Za-z0-9+/=]+)\u0002(.*?)\u0001", RegexOptions.Compiled);
        internal static string Plain(string t) => string.IsNullOrEmpty(t) || t.IndexOf('\u0001') < 0 ? (t ?? "") : Link.Replace(t, m => m.Groups[2].Value);
        // what a reader sees: no link data and no rich-text tags (<color>, <link>, <style>)
        static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        internal static string Visible(string t) { var p = Plain(t); return p.IndexOf('<') < 0 ? p : Tags.Replace(p, ""); }

        // the chain of elements above a text, unnamed ones shown as "?", for the log (first few of each kind only)
        static readonly Dictionary<string, int> Shown = new Dictionary<string, int>();
        internal static void Chain(string what, VisualElement e)
        {
            try
            {
                Shown.TryGetValue(what, out var n); if (n >= 2) return; Shown[what] = n + 1;
                var sb = new StringBuilder();
                for (int i = 0; i < 10 && e != null; i++, e = e.parent) sb.Insert(0, (string.IsNullOrEmpty(e.name) ? "?" : e.name) + "/");
                W("struct", what, sb.ToString());
            }
            catch { }
        }
        internal static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        // ---------- outlets ----------
        internal enum Kind { RedTop, PhoneIn, Broadsheet, Broadcast, Local, Digital }
        static readonly Regex RedTopRx = new Regex(@"^(the )?(sun|sun on sunday|daily mirror|sunday mirror|mirror|daily star|daily star sunday|daily express|sunday express|daily mail|mail on sunday|the people|sunday people|metro|daily record|sunday mail)$", RegexOptions.IgnoreCase);
        static readonly Regex BroadsheetRx = new Regex(@"^(the )?(times|sunday times|guardian|observer|daily telegraph|telegraph|sunday telegraph|independent|athletic|financial times|i|i paper|the i paper|scotsman|herald|irish times|new york times|wall street journal|l'equipe|marca|as|gazzetta dello sport|kicker|le monde|el pais)$", RegexOptions.IgnoreCase);
        static readonly Regex PhoneInRx = new Regex(@"talk ?sport|phone-?in", RegexOptions.IgnoreCase);
        static readonly Regex BroadcastRx = new Regex(@"sky sports|bbc sport|bbc radio 5|5 live|\bitv\b|tnt sports|bt sport|channel 4|channel 5|amazon|match of the day|bbc one|bbc two|espn|cbs|dazn|bein", RegexOptions.IgnoreCase);
        static readonly Regex LocalRx = new Regex(@"bbc radio|\b(echo|post|gazette|chronicle|examiner|herald|evening|news|argus|mercury|journal|advertiser|star|telegraph|live|press|courier|sentinel|times)$", RegexOptions.IgnoreCase);

        internal static Kind Classify(string outlet)
        {
            var o = Visible(outlet).Trim();
            if (RedTopRx.IsMatch(o)) return Kind.RedTop;
            if (BroadsheetRx.IsMatch(o)) return Kind.Broadsheet;
            if (PhoneInRx.IsMatch(o)) return Kind.PhoneIn;
            if (BroadcastRx.IsMatch(o)) return Kind.Broadcast;
            if (LocalRx.IsMatch(o)) return Kind.Local;
            return Kind.Digital;
        }

        // lead-ins: a full sentence put before SI's question; it never changes what is being asked
        static readonly Dictionary<Kind, string[]> Leads = new Dictionary<Kind, string[]>
        {
            [Kind.RedTop] = new[] { "Straight answer, please.", "Our readers want to know.", "Let's not dance around it.", "Supporters deserve the truth on this one.", "Before you give us the usual line." },
            [Kind.PhoneIn] = new[] { "The phone lines have been red hot this week.", "Our listeners are split down the middle on this.", "Half our callers this morning wanted your head, the other half want a statue.", "Big debate on the station about this one." },
            [Kind.Broadsheet] = new[] { "A question about the bigger picture.", "Looking beyond the result for a moment.", "To take the longer view.", "Something our readers have been weighing up." },
            [Kind.Broadcast] = new[] { "For the viewers at home.", "Quick one for the cameras.", "For everyone watching.", "One for the highlights package.", "For those who couldn't get a ticket.", "Briefly, for the broadcast." },
            [Kind.Local] = new[] { "Supporters in the city have been asking about this.", "Plenty of fans in the pubs round the ground are talking about this.", "For the regulars who were there." },
            [Kind.Digital] = new[] { "Our readers have been debating this all week.", "This one's been all over our comments section.", "It's trending with our readers." },
        };

        static readonly string[] RadioLeads = { "For our listeners.", "For everyone listening at home.", "For those in the car on the way home.", "Quick one for the radio audience.", "For the listeners who couldn't make it." };
        static bool IsRadio(string outlet) => (outlet ?? "").IndexOf("Radio", StringComparison.OrdinalIgnoreCase) >= 0;
        // generic lead-ins: at most a couple per conference, and the same line not again for a while
        internal static int GenericPerConference = 2;
        static int GenericConf = -1, GenericThisConf;
        static readonly List<string> RecentGeneric = new List<string>();

        internal static readonly Color Red = new Color(0.80f, 0.04f, 0.10f, 1f);
        internal static readonly Color Navy = new Color(0.02f, 0.16f, 0.38f, 1f);
        internal static readonly Color White = new Color(1f, 1f, 1f, 1f);
        const string Mark = "fm27mm-outlet";

        internal static void Pill(VisualElement e, Kind k)
        {
            bool red = k == Kind.RedTop || k == Kind.PhoneIn, navy = k == Kind.Broadsheet;
            if (!Pills || (!red && !navy)) { Unpill(e); return; }
            e.AddToClassList(Mark);
            e.style.backgroundColor = red ? Red : Navy; e.style.color = White;
            e.style.paddingLeft = 5; e.style.paddingRight = 5;
            e.style.borderTopLeftRadius = 3; e.style.borderTopRightRadius = 3; e.style.borderBottomLeftRadius = 3; e.style.borderBottomRightRadius = 3;
        }

        internal static void Unpill(VisualElement e)
        {
            if (!e.ClassListContains(Mark)) return;
            e.RemoveFromClassList(Mark);
            e.style.backgroundColor = new StyleColor(StyleKeyword.Null); e.style.color = new StyleColor(StyleKeyword.Null);
            e.style.paddingLeft = new StyleLength(StyleKeyword.Null); e.style.paddingRight = new StyleLength(StyleKeyword.Null);
            e.style.borderTopLeftRadius = new StyleLength(StyleKeyword.Null); e.style.borderTopRightRadius = new StyleLength(StyleKeyword.Null);
            e.style.borderBottomLeftRadius = new StyleLength(StyleKeyword.Null); e.style.borderBottomRightRadius = new StyleLength(StyleKeyword.Null);
        }

        // ---------- journalists remember ----------
        internal static class Grudges
        {
            internal class G { public string Outlet; public string Why; public int Left; }
            internal static readonly Dictionary<string, G> All = new Dictionary<string, G>();
            static string FilePath => System.IO.Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "grudges.tsv");
            internal static void Load()
            {
                try
                {
                    if (!System.IO.File.Exists(FilePath)) return;
                    foreach (var line in System.IO.File.ReadAllLines(FilePath))
                    {
                        var p = line.Split('\t'); if (p.Length < 4) continue;
                        All[p[0]] = new G { Outlet = p[1], Why = p[2], Left = int.TryParse(p[3], out var n) ? n : 0 };
                    }
                }
                catch (Exception e) { Fail("grudges load", e); }
            }
            internal static void Save()
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var kv in All) if (kv.Value.Left > 0) sb.Append(kv.Key).Append('\t').Append(kv.Value.Outlet).Append('\t').Append(kv.Value.Why).Append('\t').Append(kv.Value.Left).Append('\n');
                    System.IO.File.WriteAllText(FilePath, sb.ToString());
                }
                catch (Exception e) { Fail("grudges save", e); }
            }
            internal static void Add(string journalist, string outlet, string why)
            {
                if (string.IsNullOrEmpty(journalist)) return;
                All[journalist] = new G { Outlet = outlet, Why = why, Left = MediaOffice.GrudgeLength(why) };
                Save(); W("grudge", journalist + " (" + outlet + ")", why);
            }
        }

        // ---------- wrapping it up (0.13.4) ----------
        // SI's early exit (Media Lab's WRAP IT UP) ends a conference or interview at once. After three answers it is
        // "that's all we've got time for"; earlier, the journalist whose question was cut feels it, and says so next time.
        internal static class WrapUp
        {
            static string Sid = ""; static int Count;
            internal const int Fair = 3;
            internal static void Answered(string sid) { if (sid != Sid) { Sid = sid; Count = 0; } Count++; }
            internal static int AnsweredIn(string sid) => sid == Sid ? Count : 0;
            internal static void Out(string sid)
            {
                try
                {
                    int n = AnsweredIn(sid);
                    Record.Add("wrapped", n, "");
                    if (n < Fair) Grudges.Add(CurJournalist, CurOutlet, "cutshort");
                    W("wrapped", n + " answered", n >= Fair ? "fair enough" : (CurJournalist + " (" + CurOutlet + ") cut short"));
                    Sid = ""; Count = 0;
                }
                catch (Exception e) { Fail("wrap up", e); }
            }
        }

        // ---------- on the record ----------
        internal static class Record
        {
            internal class E { public int Conf; public string When, Kind, Outlet, Journalist, Subjects, Question, Answer, Group = ""; public int Value; public bool Used; }
            internal static readonly List<E> All = new List<E>();
            internal static int Conf, ThisConf, PerConference = 2;
            internal static DateTime ConfAt = DateTime.MinValue;
            internal static string FileName = "on the record.tsv";
            static string FilePath => System.IO.Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", FileName);
            static string Clean(string t) => (t ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

            internal static void Load()
            {
                try
                {
                    if (!System.IO.File.Exists(FilePath)) return;
                    foreach (var line in System.IO.File.ReadAllLines(FilePath))
                    {
                        var p = line.Split('\t'); if (p.Length < 10 || p[0] == "conf") continue;
                        var e = new E { Conf = int.TryParse(p[0], out var c) ? c : 0, When = p[1], Kind = p[2], Value = int.TryParse(p[3], out var v) ? v : 0,
                            Outlet = p[4], Journalist = p[5], Subjects = p[6], Question = p[7], Answer = p[8], Used = p[9] == "1", Group = p.Length > 10 ? p[10] : "" };
                        All.Add(e); if (e.Conf > Conf) Conf = e.Conf;
                    }
                }
                catch (Exception e) { Fail("record load", e); }
            }

            internal static void Save()
            {
                try
                {
                    while (All.Count > 3000) All.RemoveAt(0);
                    var sb = new StringBuilder("conf\twhen\tkind\tvalue\toutlet\tjournalist\tsubjects\tquestion\tanswer\tused\tgroup\n");
                    foreach (var e in All)
                        sb.Append(e.Conf).Append('\t').Append(Clean(e.When)).Append('\t').Append(e.Kind).Append('\t').Append(e.Value).Append('\t')
                          .Append(Clean(e.Outlet)).Append('\t').Append(Clean(e.Journalist)).Append('\t').Append(Clean(e.Subjects)).Append('\t')
                          .Append(Clean(e.Question)).Append('\t').Append(Clean(e.Answer)).Append('\t').Append(e.Used ? "1" : "0").Append('\t').Append(Clean(e.Group)).Append('\n');
                    System.IO.File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
                }
                catch (Exception e) { Fail("record save", e); }
            }

            internal static void NewConference(string why)
            {
                // SI's own start event always counts; "Question 1 of" (tunnel interviews) only if no conference began in the last 10 minutes
                double since = (DateTime.Now - ConfAt).TotalSeconds;
                if (since < 60 || (why != "start" && since < 600)) return;
                Conf++; ConfAt = DateTime.Now; ThisConf = 0; Led.Clear(); LedText.Clear(); DecidedQ = null;
                W("conference", Conf.ToString(), why);
                MediaOffice.OnConference(Conf);
            }

            // people named in a question. A name is a link whose record carries its tooltip, "Click to view <full name>'s
            // profile" for a person (clubs and grounds have no "profile"). The full name is the key, so "Kumbedi" and
            // "Saël Kumbedi" are the same person; the name as written in the new question is what gets shown.
            static readonly Regex ProfileOf = new Regex(@"Click to view (.+?)(?:'s|') profile", RegexOptions.Compiled);
            internal static List<KeyValuePair<string, string>> People(string raw)
            {
                var o = new List<KeyValuePair<string, string>>();
                if (string.IsNullOrEmpty(raw)) return o;
                foreach (Match m in Link.Matches(raw))
                {
                    string tip = "";
                    try { tip = Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value)); } catch { continue; }
                    var pm = ProfileOf.Match(tip); if (!pm.Success) continue;
                    var key = pm.Groups[1].Value.Trim(); var shown = m.Groups[2].Value.Trim();
                    if (key.Length < 2 || shown == CurJournalist || key == CurJournalist) continue;
                    bool dup = false; foreach (var kv in o) if (kv.Key == key) dup = true;
                    if (!dup) o.Add(new KeyValuePair<string, string>(key, shown));
                }
                return o;
            }
            static string Keys(string raw) { var l = new List<string>(); foreach (var kv in People(raw)) l.Add(kv.Key); return string.Join("|", l); }
            // 0.9.0: a question opened with a dressing-room story is also on the record about that player
            static string WithExtra(string k) { var x = ExtraSubject; if (string.IsNullOrEmpty(x) || ("|" + k + "|").Contains("|" + x + "|")) return k; return k.Length == 0 ? x : k + "|" + x; }

            internal static string Norm(string t)
            {
                var sb = new StringBuilder();
                foreach (var c in (t ?? "").ToLowerInvariant()) if (char.IsLetterOrDigit(c) || c == ' ') sb.Append(c);
                return Regex.Replace(sb.ToString(), " +", " ").Trim();
            }

            internal static void Add(string kind, int value, string answer)
            {
                try
                {
                    if (string.IsNullOrEmpty(CurQRaw)) return;
                    if (kind == "answer" && string.IsNullOrWhiteSpace(answer)) return;
                    var e = new E { Conf = Conf, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Kind = kind, Value = value, Outlet = CurOutlet, Journalist = CurJournalist,
                        Subjects = WithExtra(Keys(CurQRaw)), Question = Visible(CurQRaw).Trim(), Answer = Visible(answer ?? "").Trim(),
                        Group = kind == "answer" && (DateTime.Now - PickedAt).TotalSeconds < 6 ? (PickedGroup ?? "") : "" };
                    All.Add(e); Save();
                    W("record", kind + " " + value, e.Question + " => " + e.Answer);
                    MediaOffice.OnRecord(e);
                }
                catch (Exception ex) { Fail("record add", ex); }
            }

            static string WhenSaid(int gap)
            {
                if (gap <= 1) return "at your last press conference";
                if (gap <= 3) return "a couple of press conferences ago";
                if (gap <= 10) return "a few weeks back";
                return "a while back";
            }
            static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

            // your earlier words, short enough to read in a question bubble
            internal static string Quote(string a)
            {
                a = (a ?? "").Trim();
                if (a.Length > 150)
                {
                    var m = Regex.Match(a, @"^.{20,150}?[.!?](?=\s|$)");
                    if (m.Success) a = m.Value;
                    else { int cut = a.LastIndexOf(' ', 140); a = a.Substring(0, cut > 40 ? cut : 140).TrimEnd(',', ';', ':', ' ') + "..."; }
                }
                if (a.Length > 0 && ".!?".IndexOf(a[a.Length - 1]) < 0 && !a.EndsWith("...")) a += ".";
                return "\u201C" + a + "\u201D";
            }

            // the opening line for this question, or null; marks the quoted answer used so it is never quoted twice
            internal static string Callback(string qRaw, Kind k)
            {
                if (PerConference <= 0 || ThisConf >= PerConference) return null;
                var qn = Norm(Visible(qRaw));
                E hit = null; string kindOfHit = null, who = null;
                for (int i = All.Count - 1; i >= 0 && hit == null; i--)
                {
                    var e = All[i];
                    if (e.Used || e.Conf >= Conf || e.Kind != "answer") continue;
                    if (Norm(e.Question) == qn) { hit = e; kindOfHit = "repeat"; }
                }
                if (hit == null)
                {
                    var people = People(qRaw);
                    for (int i = All.Count - 1; i >= 0 && hit == null; i--)
                    {
                        var e = All[i];
                        if (e.Used || e.Conf >= Conf || string.IsNullOrEmpty(e.Subjects)) continue;
                        if (e.Kind != "answer" && e.Kind != "nocomment" && e.Kind != "storm" && !(e.Kind == "wrapped" && e.Value < WrapUp.Fair)) continue;
                        foreach (var n in people)
                            if (("|" + e.Subjects + "|").Contains("|" + n.Key + "|")) { hit = e; kindOfHit = e.Kind; who = n.Value; break; }
                    }
                }
                if (hit == null) return null;
                var when = WhenSaid(Conf - hit.Conf);
                string line;
                if (kindOfHit == "repeat")
                {
                    var q = Quote(hit.Answer);
                    switch (k)
                    {
                        case Kind.RedTop: line = "You've had this one before. " + Cap(when) + ", you said: " + q; break;
                        case Kind.PhoneIn: line = "Our callers remember you being asked this " + when + ". You said: " + q; break;
                        case Kind.Local: line = "You were asked this " + when + ", and people here remember the answer: " + q; break;
                        case Kind.Broadcast: line = "You were asked this " + when + ". Your answer then: " + q; break;
                        default: line = "We put this to you " + when + ". You said: " + q; break;
                    }
                }
                else if (kindOfHit == "nocomment")
                    line = k == Kind.RedTop ? Cap(when) + " it was no comment on " + who + "." : Cap(when) + ", you wouldn't talk about " + who + ".";
                else if (kindOfHit == "storm")
                    line = Cap(when) + ", a question about " + who + " ended with you walking out.";
                else if (kindOfHit == "wrapped")
                    line = Cap(when) + ", you wrapped things up before we got to " + who + ".";
                else
                {
                    var q = Quote(hit.Answer);
                    switch (k)
                    {
                        case Kind.RedTop: line = "Your words on " + who + ", " + when + ": " + q; break;
                        case Kind.PhoneIn: line = "Our listeners keep replaying what you said about " + who + " " + when + ": " + q; break;
                        case Kind.Local: line = "Supporters haven't forgotten what you said about " + who + " " + when + ": " + q; break;
                        case Kind.Broadcast: line = "For the record, " + when + " you said this about " + who + ": " + q; break;
                        default: line = "Asked about " + who + " " + when + ", you said: " + q; break;
                    }
                }
                if (kindOfHit == "repeat" || kindOfHit == "answer") SetEcho(hit.Answer, kindOfHit);
                hit.Used = true; ThisConf++; Save();
                W("callback", kindOfHit + " " + k + " " + CurOutlet + " / " + CurJournalist + " | then conf " + hit.Conf + " now " + Conf, line);
                return line;
            }
        }

        // the question being answered, and its answers in screen order (top = most positive)
        internal static string CurQRaw; internal static DateTime CurQAt;
        internal static readonly List<KeyValuePair<IntPtr, string>> Answers = new List<KeyValuePair<IntPtr, string>>();
        internal static string Picked, PickedGroup; internal static DateTime PickedAt = DateTime.MinValue;
        internal static readonly HashSet<string> LedText = new HashSet<string>();   // our own versions, so they are never taken for new questions

        // ---------- 0.9.0: an answer you're quoted on, offered again word for word ----------
        // When a callback quotes your earlier answer and SI offers that same answer again, the option reads as standing by it
        // instead of repeating itself. The slot, and so what the answer does, is SI's and unchanged; what you said is still kept
        // on the record in SI's words. An option carrying a Promise tag is never reworded, so the wording always matches the wiring.
        internal static bool EchoRewrite = true;
        internal static string EchoNorm, EchoKind, ExtraSubject;
        internal static readonly Dictionary<string, string> EchoMap = new Dictionary<string, string>();   // our line -> SI's words
        internal static readonly Dictionary<IntPtr, VisualElement> AnswerEls = new Dictionary<IntPtr, VisualElement>();
        static readonly string[] EchoRepeat = { "Same question, same answer. Nothing has changed my mind.", "My answer hasn't changed. I said it then and I stand by it.",
            "I'll give the same answer as last time: I still feel exactly the same.", "Nothing has changed since then. I still feel the same way." };
        static readonly string[] EchoSubject = { "I stand by what I said. My view hasn't changed.", "I meant it then and I still mean it now.",
            "That still stands. I feel exactly the same.", "Yes, I said that, and I still feel the same way." };
        internal static void SetEcho(string answer, string kind)
        {
            if (!EchoRewrite) return;
            var v = Record.Norm(Visible(answer ?? ""));
            if (v.Length < 12) return;
            EchoNorm = v; EchoKind = kind;
        }
        internal static void ClearEcho() { EchoNorm = null; EchoKind = null; }
        static bool HasPromise(VisualElement e)
        {
            VisualElement b = e;
            for (int i = 0; i < 16 && b != null; i++, b = b.parent) if (b.name == "SpeakToMeetingAnswerActionButton") break;
            return b != null && b.name == "SpeakToMeetingAnswerActionButton" && FindText(b, "Promise", 0);
        }
        static bool FindText(VisualElement e, string want, int depth)
        {
            if (e == null || depth > 10) return false;
            var te = e.TryCast<TextElement>();
            if (te != null && Visible(te.text ?? "").Trim() == want) return true;
            for (int i = 0; i < e.childCount; i++) if (FindText(e.ElementAt(i), want, depth + 1)) return true;
            return false;
        }
        internal static string EchoFor(VisualElement el, string txt)
        {
            if (EchoNorm == null || string.IsNullOrEmpty(txt) || EchoMap.ContainsKey(txt)) return null;
            if (Record.Norm(Visible(txt)) != EchoNorm) return null;
            if (HasPromise(el)) { W("echo", "kept (promise tag)", Visible(txt)); return null; }
            var pool = EchoKind == "repeat" ? EchoRepeat : EchoSubject;
            var line = pool[Hash(EchoNorm + "|" + Record.Conf) % (uint)pool.Length];
            EchoMap[line] = txt;
            W("echo", EchoKind, Visible(txt) + " => " + line);
            return line;
        }
        // the options may already be on screen when the opener is decided: reword the matching one in place
        internal static void ApplyEcho()
        {
            if (EchoNorm == null) return;
            try
            {
                foreach (var kv in new List<KeyValuePair<IntPtr, string>>(Answers))
                {
                    if (!AnswerEls.TryGetValue(kv.Key, out var el) || el == null) continue;
                    var line = EchoFor(el, kv.Value);
                    if (line == null) continue;
                    try { Applying = true; el.TryCast<SI.Bindable.SIText>()?.SetText(line); } finally { Applying = false; }
                }
            }
            catch (Exception e) { Fail("echo apply", e); }
        }

        internal static void OnAnswerText(VisualElement e, string txt)
        {
            if (string.IsNullOrEmpty(CurQRaw) || (DateTime.Now - CurQAt).TotalSeconds > 5) return;
            if (EchoMap.TryGetValue(txt, out var origEcho)) txt = origEcho;   // keep SI's words, not our rewording
            AnswerEls[e.Pointer] = e;
            for (int i = 0; i < Answers.Count; i++) if (Answers[i].Key == e.Pointer) { Answers[i] = new KeyValuePair<IntPtr, string>(e.Pointer, txt); return; }
            Answers.Add(new KeyValuePair<IntPtr, string>(e.Pointer, txt));
        }

        internal static void OnAnswered(int value)
        {
            if (Picked != null && EchoMap.TryGetValue(Picked, out var po)) Picked = po;   // our rewording -> SI's words
            string ans = null, how = "none";
            if (Picked != null && (DateTime.Now - PickedAt).TotalSeconds < 3) { ans = Picked; how = "click"; }
            string mapped = Answers.Count == 5 && value >= 1 && value <= 5 ? Answers[5 - value].Value : null;
            if (ans == null && mapped != null) { ans = mapped; how = "slot"; }
            W("pick", how + " value " + value + " of " + Answers.Count + (mapped != null && Picked != null ? (Visible(mapped) == Visible(Picked) ? " (slot agrees)" : " (slot DIFFERS: " + Visible(mapped) + ")") : ""), Visible(ans ?? ""));
            Picked = null;
            Record.Add("answer", value, ans);
        }

        static readonly string[] AfterStorm = { "After you walked out on us last time, let's try this again.", "Hopefully you'll stay for this one.",
            "Last time you didn't let me finish.", "Shall we see if you make it to the end today?" };
        static readonly string[] AfterCut = { "No dashing off before my question this time, I hope.", "Last time you'd gone before I got my question in, so here it is.",
            "Hopefully we've got you a bit longer today." };
        static readonly string[] AfterNoComment = { "You ducked my last question, so let's try another.", "Maybe you'll answer this one.",
            "Last time it was no comment. Let's see." };

        internal static string CurOutlet = "", CurJournalist = "";

        // ---------- the current question ----------
        internal static DateTime Armed = DateTime.MinValue;         // "Question n of m" just shown
        internal static VisualElement LastQ; internal static string LastQText; internal static DateTime LastQAt;
        internal static readonly Dictionary<string, string> Led = new Dictionary<string, string>();   // SI question -> with lead-in
        internal static bool Applying;

        // 0.5.0: the outlet and the journalist's name arrive in either order (on question 1 the name comes first), so the
        // opener is decided once both are known for the new question. 0.4.0 decided on the name and could use the
        // previous question's outlet.
        internal static void OnOutlet(string outlet) { CurOutlet = Visible(outlet).Trim(); Decide(); }
        internal static void OnJournalist(string name) { CurJournalist = Visible(name).Trim(); Decide(); }
        internal static string DecidedQ;

        internal static void Decide()
        {
            if (LastQ == null || (DateTime.Now - LastQAt).TotalSeconds > 1) return;
            if (string.IsNullOrEmpty(CurOutlet) || string.IsNullOrEmpty(CurJournalist)) return;
            if (LastQText == DecidedQ) { LastQ = null; return; }
            var q = LastQText; var el = LastQ; LastQ = null; DecidedQ = q;
            Word.NextQuestion();
            Deals.SeeQuestion(q);
            var k = Classify(CurOutlet);
            uint h = Hash(Plain(q));
            string lead = null;
            if (MediaOffice.IsBanned(CurOutlet))
            {
                // the press officer cuts in; SI's own "no comment" follows, so the game records it
                lead = MediaOffice.BanLine(CurOutlet);
                Record.Add("ban", 0, "");
                MediaOffice.ScheduleNoComment(el, q, CurOutlet);
            }
            if (lead == null) lead = MediaOffice.OneOff(CurOutlet, CurJournalist);
            if (lead == null) lead = NoShow.Talk(CurJournalist, h);   // 0.13.3: back after sending the assistant
            if (lead == null && Grudges.All.TryGetValue(CurJournalist, out var g) && g.Left > 0)
            {
                var pool = g.Why == "storm" ? AfterStorm : g.Why == "cutshort" ? AfterCut : AfterNoComment;
                lead = pool[h % (uint)pool.Length];
                g.Left--; Grudges.Save();
            }
            if (lead == null) lead = Pack.Talk(q, CurOutlet, CurJournalist, k);
            if (lead == null) lead = Word.Leak(q, CurOutlet, CurJournalist, k);
            if (lead == null) lead = Deals.Talk(q, CurOutlet, CurJournalist, k);
            if (lead == null) lead = FanPress.Talk(q, CurOutlet, CurJournalist, k);
            if (lead == null) { lead = Incidents.Talk(q, CurOutlet, CurJournalist, k, out var incSubject); if (lead != null) ExtraSubject = incSubject; }
            if (lead == null) lead = Record.Callback(q, k);
            if (lead == null) lead = MediaOffice.Opener(CurOutlet, CurJournalist, h);
            if (GenericConf != Record.Conf) { GenericConf = Record.Conf; GenericThisConf = 0; }
            // 0.12.0: the journalist's own version is decided first; it is already in that outlet's voice, so no generic lead-in on top
            var voiced = Voices.ForQuestion(q, CurJournalist);
            if (lead == null && voiced == null && LeadShare > 0 && h % 100 < (uint)LeadShare && GenericThisConf < GenericPerConference)
            {
                var pool = k == Kind.Broadcast && IsRadio(CurOutlet) ? RadioLeads : Leads[k];
                for (int i = 0; i < pool.Length && lead == null; i++)
                {
                    var c = pool[(int)((h / 101 + (uint)i) % (uint)pool.Length)];
                    if (!RecentGeneric.Contains(c)) lead = c;
                }
                if (lead != null) { GenericThisConf++; RecentGeneric.Add(lead); while (RecentGeneric.Count > 10) RecentGeneric.RemoveAt(0); }
            }
            // 0.11.0: the asking journalist's own way of putting a marked question (attack dogs harder, cheerleaders warmer)
            var body = voiced ?? q;
            if (lead == null && body == q) { W("question", k + " " + CurOutlet + " / " + CurJournalist, Plain(q)); return; }
            var led = lead == null ? body : lead + " " + body;
            Led[q] = led; LedText.Add(led);
            W("lead-in", k + " " + CurOutlet + " / " + CurJournalist, Plain(led));
            try { Applying = true; el.TryCast<SI.Bindable.SIText>()?.SetText(led); } finally { Applying = false; }
            ApplyEcho();
        }

        // ---------- briefing on the invite ----------
        internal static readonly HashSet<IntPtr> Pending = new HashSet<IntPtr>();

        internal static void Brief(VisualElement block)
        {
            try
            {
                var outlets = new List<string>();
                Collect(block, outlets, 0);
                if (outlets.Count == 0) return;
                // 0.13.3: the room is remembered even with the briefing off, in case the assistant is sent
                {
                    var hunters = new List<string>(); foreach (var o in outlets) { var kk = Classify(o); if (kk == Kind.RedTop || kk == Kind.PhoneIn) hunters.Add(o.Trim()); }
                    var who = new List<string>(); CollectNames(block, who, 0);
                    NoShow.SawInvite(block, outlets.ConvertAll(x => x.Trim()), who, hunters);
                }
                if (!Briefing) return;
                int red = 0, phone = 0, broad = 0, cast = 0, local = 0, dig = 0;
                var reds = new List<string>();
                foreach (var o in outlets)
                    switch (Classify(o))
                    {
                        case Kind.RedTop: red++; reds.Add(o); break;
                        case Kind.PhoneIn: phone++; reds.Add(o); break;
                        case Kind.Broadsheet: broad++; break;
                        case Kind.Broadcast: cast++; break;
                        case Kind.Local: local++; break;
                        default: dig++; break;
                    }
                var parts = new List<string>();
                if (red > 0) parts.Add(red + (red == 1 ? " red top" : " red tops"));
                if (phone > 0) parts.Add(phone + " phone-in");
                if (broad > 0) parts.Add(broad + (broad == 1 ? " broadsheet" : " broadsheets"));
                if (cast > 0) parts.Add(cast + (cast == 1 ? " broadcaster" : " broadcasters"));
                if (local > 0) parts.Add(local + " local");
                if (dig > 0) parts.Add(dig + " online and magazines");
                string mood = red + phone >= 3 ? "Expect it to get personal." : red + phone >= 1 ? "Someone will be after a headline." : broad >= 2 ? "Expect a measured grilling." : "Should be a quiet one.";
                string text = "Press officer: " + string.Join(", ", parts) + " in the room. " + mood + (reds.Count > 0 ? "  Watch: " + string.Join(", ", reds.ConvertAll(x => x.Trim())) + "." : "");
                var names = new List<string>(); CollectNames(block, names, 0);
                var grudges = new List<string>();
                foreach (var n in names) if (Grudges.All.TryGetValue(n, out var g) && g.Left > 0) grudges.Add(n + " (" + g.Outlet + ")" + (g.Why == "storm" ? " hasn't forgotten the walk-out" : g.Why == "cutshort" ? " still has the question you cut short" : " still wants that answer"));
                if (grudges.Count > 0) text += "  " + string.Join("; ", grudges) + ".";
                if (NoShow.Recent() >= 1) text += "  Your assistant has had " + NoShow.Recent() + " of these in six weeks.";
                VisualElement lbl = null;
                for (int i = 0; i < block.childCount; i++) if (block.ElementAt(i).name == "FM27MM-Briefing") lbl = block.ElementAt(i);
                if (lbl == null)
                {
                    var l = new Label(text); l.name = "FM27MM-Briefing";
                    l.style.whiteSpace = WhiteSpace.Normal; l.style.color = new Color(1f, 0.85f, 0.45f, 1f); l.style.fontSize = 13;
                    l.style.marginTop = 4; l.style.marginBottom = 6; l.style.paddingLeft = 6; l.style.borderLeftWidth = 3; l.style.borderLeftColor = Red;
                    block.Insert(Math.Min(1, block.childCount), l);
                }
                else lbl.TryCast<Label>().text = text;
                W("briefing", outlets.Count.ToString(), text);
            }
            catch (Exception e) { Fail("briefing", e); }
        }

        // journalist names in the invite list (Vertical/text beside each publication)
        static void CollectNames(VisualElement e, List<string> into, int depth)
        {
            if (e == null || depth > 12) return;
            if ((e.name ?? "") == "text" && e.parent != null && e.parent.name == "Vertical")
            {
                var te = e.TryCast<TextElement>();
                if (te != null) { var t = Visible(te.text).Trim(); if (t.Length > 0) into.Add(t); }
            }
            for (int i = 0; i < e.childCount; i++) CollectNames(e.ElementAt(i), into, depth + 1);
        }

        static void Collect(VisualElement e, List<string> into, int depth)
        {
            if (e == null || depth > 12) return;
            var n = e.name ?? "";
            if (n == "publication-text" || n == "Publication")
            {
                var te = e.TryCast<TextElement>();
                if (te != null) { var t = Visible(te.text).Trim(); if (t.Length > 0) into.Add(t); }
            }
            for (int i = 0; i < e.childCount; i++) Collect(e.ElementAt(i), into, depth + 1);
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), nameof(SI.Bindable.SIText.SetText))]
    static class PressTextPatch
    {
        // SI's 17 Nov patch: bound text now goes through ApplyText, so the same code runs there too (see PressTextPatch_Apply);
        // a call that comes from inside SetText on the same element is left to the SetText hook.
        [System.ThreadStatic] internal static System.IntPtr InSet;
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { InSet = __instance.Pointer; Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { InSet = System.IntPtr.Zero; Post(__instance, txt); }

        // 1 = the outlet, 2 = the journalist, in the press-conference question header; 0 = anything else
        static int Header(VisualElement e)
        {
            bool frame = false; VisualElement x = e;
            for (int i = 0; i < 5 && x != null; i++, x = x.parent)
            {
                var nm = x.name ?? "";
                if (nm == "NameAndHierarhy") return Under(x, "IsJournalist", 12) ? (frame ? 2 : 1) : 0;
                if (nm == "Frame" && i > 0) frame = true;
            }
            return 0;
        }

        static string n0(VisualElement e) => e.name ?? "";
        static bool Under(VisualElement e, string name, int max)
        {
            for (int i = 0; i < max && e != null; i++, e = e.parent) if (e.name == name) return true;
            return false;
        }

        internal static void Pre(SI.Bindable.SIText __instance, ref string txt)
        {
            try
            {
                if (string.IsNullOrEmpty(txt)) return;
                // 0.11.0: the Voices tag channel. Tags are always stripped; a marked answer or question may be re-voiced.
                if (txt.IndexOf('\u2063') >= 0) txt = Voices.OnShown(__instance, txt);
                if (PressRoom.Applying) return;
                var n = __instance.name ?? "";
                // a question being re-shown (history, re-render) keeps its lead-in
                if (PressRoom.Led.Count > 0 && PressRoom.Led.TryGetValue(txt, out var led) && Under(__instance, "NPCBubbleFrame", 6)) txt = led;
                // an agent's bubble shown again keeps what they said about the papers
                else if (Deals.BubbleMap.Count > 0 && Under(__instance, "NPCBubbleFrame", 6)) { var m = Deals.Reshow(txt); if (m != null) txt = m; }
                // 0.9.0: the answer you are being quoted on, offered again: stand by it instead of repeating it
                else if (PressRoom.EchoNorm != null && Under(__instance, "SpeakToMeetingAnswerActionButton", 16) && !Under(__instance, "TagVisible", 6)) { var r = PressRoom.EchoFor(__instance, txt); if (r != null) txt = r; }
            }
            catch (Exception e) { PressRoom.Fail("prefix", e); }
        }

        internal static void Post(SI.Bindable.SIText __instance, string txt)
        {
            try
            {
                Memory.Pump();
                if (PressRoom.Applying || string.IsNullOrEmpty(txt)) return;
                MediaOffice.EnsureTab(__instance);
                if (txt.Length >= 8 && txt.Length <= 20) Desk.SeeDate(__instance, txt);  // 0.13: the date in the top bar, if it passes the hook
                if (n0(__instance) == "PlayerName") Desk.OnPlayerName(__instance, txt);  // 0.13: groundwork for tapping up
                Word.OnText(__instance, txt);
                Incidents.OnText(__instance, txt);
                Pack.OnText(__instance, txt);
                if (txt.Length <= 60) Pack.OnTile(__instance, txt);          // 0.13.4: today's result from the match report tile
                var n = __instance.name ?? "";
                // 0.2.0 checked each text's direct parent by name, but SI puts unnamed elements in between, so the
                // question, outlet and journalist were never recognised. Now: look up the ancestors instead.
                if (txt.StartsWith("Question ") && Under(__instance, "FinishedText", 3))
                {
                    PressRoom.Armed = DateTime.Now;
                    if (txt.StartsWith("Question 1 of")) PressRoom.Record.NewConference("question 1");
                    return;
                }
                if (Under(__instance, "BubbleBackground", 3) && Under(__instance, "NPCBubbleFrame", 6))
                {
                    var plain = PressRoom.Plain(txt);
                    bool isQuestion = plain.Contains("?") && plain.IndexOf("question from", StringComparison.OrdinalIgnoreCase) < 0;
                    if (isQuestion && !PressRoom.LedText.Contains(txt))
                    {
                        PressRoom.LastQ = __instance; PressRoom.LastQText = txt; PressRoom.LastQAt = DateTime.Now; PressRoom.Chain("question", __instance);
                        if (txt != PressRoom.CurQRaw) { PressRoom.CurQRaw = txt; PressRoom.Answers.Clear(); PressRoom.AnswerEls.Clear(); PressRoom.ExtraSubject = null; PressRoom.ClearEcho(); PressRoom.CurOutlet = ""; PressRoom.CurJournalist = ""; }
                        PressRoom.CurQAt = DateTime.Now;
                    }
                    return;
                }
                if (Under(__instance, "SpeakToMeetingAnswerActionButton", 16) && !Under(__instance, "TagVisible", 6))
                {
                    PressRoom.OnAnswerText(__instance, txt);
                    return;
                }
                // the question header: outlet in NameAndHierarhy, journalist in NameAndHierarhy/Frame
                int hdr = Header(__instance);
                if (hdr == 1)
                {
                    PressRoom.Chain("outlet", __instance);
                    var k = PressRoom.Classify(txt);
                    PressRoom.Pill(__instance, k);
                    PressRoom.OnOutlet(txt);
                    return;
                }
                if (hdr == 2)
                {
                    PressRoom.Chain("journalist", __instance);
                    PressRoom.OnJournalist(txt);
                    return;
                }
                // outlets in the press-room lists and the invite
                if (n == "publication-text" || n == "Publication")
                {
                    PressRoom.Pill(__instance, PressRoom.Classify(txt));
                    VisualElement block = __instance;
                    for (int i = 0; i < 12 && block != null; i++, block = block.parent) if (block.name == "JournalistsInAttendanceBlock") break;
                    if (block != null && block.name == "JournalistsInAttendanceBlock")
                    {
                        var ptr = block.Pointer;
                        if (PressRoom.Pending.Add(ptr))
                        {
                            var keep = block;
                            System.Action run = () => { PressRoom.Pending.Remove(ptr); PressRoom.Brief(keep); };
                            block.schedule.Execute(run).ExecuteLater(150);
                        }
                    }
                }
            }
            catch (Exception e) { PressRoom.Fail("postfix", e); }
        }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIText), "ApplyText")]
    static class PressTextPatch_Apply
    {
        static void Prefix(SI.Bindable.SIText __instance, ref string txt) { if (__instance.Pointer == PressTextPatch.InSet) return; PressTextPatch.Pre(__instance, ref txt); }
        static void Postfix(SI.Bindable.SIText __instance, string txt) { if (__instance.Pointer == PressTextPatch.InSet) return; PressTextPatch.Post(__instance, txt); }
    }

    // storm-outs and no-comments are remembered against the journalist who asked
    [HarmonyPatch(typeof(FM.GamePlugin.GameInteropSubsystem), nameof(FM.GamePlugin.GameInteropSubsystem.SendEvent))]
    static class PressEventPatch
    {
        static void Prefix(FM.GamePlugin.GameInteropSubsystem __instance, uint eventID, SI.Interop.InteropReference objectTo)
        {
            try
            {
                int ev = (int)eventID;
                MediaOffice.Interop = __instance;
                if (objectTo != null) { string uid = ""; try { uid = objectTo.GetPersistentIdentity() ?? ""; } catch { } Desk.OnUiEvent(ev, uid.Length > 90 ? uid.Substring(0, 90) : uid); }
                if (objectTo != null && !MediaOffice.Firing)
                {
                    string id = ""; try { id = objectTo.GetPersistentIdentity() ?? ""; } catch { }
                    if (id.StartsWith("825307168:102,") && id.Contains("1398040932:6,"))
                    {
                        if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxClearChanges) MediaOffice.Session = null;
                        else { MediaOffice.Session = objectTo; MediaOffice.SessionAt = DateTime.Now; }
                    }
                }
                if (MediaOffice.Firing) return;     // the press officer's "no comment" on a banned outlet is not yours
                string sid = ""; if (objectTo != null) { try { sid = objectTo.GetPersistentIdentity() ?? ""; } catch { } }
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceAnswerQuestion) PressRoom.WrapUp.Answered(sid);
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceEarlyExit) PressRoom.WrapUp.Out(sid);
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceStormOut) { PressRoom.Grudges.Add(PressRoom.CurJournalist, PressRoom.CurOutlet, "storm"); PressRoom.Record.Add("storm", 0, ""); }
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceNoComment) { PressRoom.Grudges.Add(PressRoom.CurJournalist, PressRoom.CurOutlet, "nocomment"); PressRoom.Record.Add("nocomment", 0, ""); }
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxStartPressConference) PressRoom.Record.NewConference("start");
                else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxNewGameCreated) Memory.OnNewGame();
            }
            catch (Exception e) { PressRoom.Fail("event", e); }
        }
    }

    // 0.13.3: SI's handler for the buttons on a news item; the invite's "send assistant" is one of them
    [HarmonyPatch(typeof(FM.UI.NewsActionHandlerModule), nameof(FM.UI.NewsActionHandlerModule.TriggerSpeakToEvent))]
    static class SpeakToPatch
    {
        static void Prefix(FM.UI.SpeakToActionEvent speakToActionEvent) { if (speakToActionEvent != null) NoShow.OnSpeakTo(speakToActionEvent); }
    }

    // the answer you clicked: SI's own button tells us, so we keep the exact words
    [HarmonyPatch(typeof(SI.Bindable.SIButton), "SIButton_Clicked")]
    static class PickPatch
    {
        internal static void Take(VisualElement b, string how)
        {
            try
            {
                VisualElement x = b; bool answer = false;
                for (int i = 0; i < 8 && x != null; i++, x = x.parent) if (x.name == "SpeakToMeetingAnswerActionButton") { answer = true; break; }
                if (!answer) return;
                string best = null; Longest(b, ref best, 0);
                if (best == null) return;
                PressRoom.Picked = best; PressRoom.PickedAt = DateTime.Now; PressRoom.PickedGroup = null;
                // the label of the group the option sits in (Desperate, Keen, Agree, Threaten...)
                string how2 = "nested";
                VisualElement g = b;
                for (int i = 0; i < 40 && g != null; i++, g = g.parent) if (g.name == "SpeakToOptionalActionButton") break;
                if (g != null && g.name == "SpeakToOptionalActionButton") { var t = FindNamed(g, "GroupTitle", 0); if (t != null) PressRoom.PickedGroup = PressRoom.Visible(t.text ?? "").Trim(); }
                if (string.IsNullOrEmpty(PressRoom.PickedGroup)) { how2 = "layout"; PressRoom.PickedGroup = GroupByLayout(b); }
                if (string.IsNullOrEmpty(PressRoom.PickedGroup))
                {
                    how2 = "none";
                    var chain = new System.Text.StringBuilder(); VisualElement x2 = b;
                    for (int i = 0; i < 40 && x2 != null; i++, x2 = x2.parent) chain.Append(string.IsNullOrEmpty(x2.name) ? "_" : x2.name).Append('<');
                    PressRoom.W("pick", "group not found", chain.ToString());
                }
                PressRoom.W("pick", how2 + " " + (PressRoom.PickedGroup ?? ""), PressRoom.Visible(best));
            }
            catch (Exception e) { PressRoom.Fail("pick " + how, e); }
        }
        // the group heading nearest above the option you clicked, inside the same answer panel
        static string GroupByLayout(VisualElement b)
        {
            VisualElement root = b;
            for (int i = 0; i < 60 && root != null; i++, root = root.parent) if (root.name == "NewsResponseOptionalButtons") break;
            if (root == null || root.name != "NewsResponseOptionalButtons") return null;
            var titles = new List<TextElement>(); CollectNamed(root, "GroupTitle", titles, 0);
            var bb = b.worldBound; float cy = bb.y + bb.height / 2f;
            if (float.IsNaN(cy) || bb.height <= 0) return null;
            TextElement best = null; float bestY = float.MinValue, bestDx = float.MaxValue;
            foreach (var t in titles)
            {
                var tb = t.worldBound;
                if (float.IsNaN(tb.y) || tb.height <= 0 || tb.y + tb.height > cy + 1f) continue;
                float dx = Math.Abs((tb.x + tb.width / 2f) - (bb.x + bb.width / 2f));
                bool overlaps = tb.x < bb.x + bb.width && bb.x < tb.x + tb.width;
                if (overlaps) dx = 0;
                if (tb.y > bestY + 2f || (Math.Abs(tb.y - bestY) <= 2f && dx < bestDx)) { best = t; bestY = tb.y; bestDx = dx; }
            }
            return best == null ? null : PressRoom.Visible(best.text ?? "").Trim();
        }
        static void CollectNamed(VisualElement e, string name, List<TextElement> into, int depth)
        {
            if (e == null || depth > 30 || into.Count > 40) return;
            if (e.name == name) { var te = e.TryCast<TextElement>(); if (te != null) { into.Add(te); return; } }
            for (int i = 0; i < e.childCount; i++) CollectNamed(e.ElementAt(i), name, into, depth + 1);
        }

        static TextElement FindNamed(VisualElement e, string name, int depth)
        {
            if (e == null || depth > 8) return null;
            if (e.name == name) { var te = e.TryCast<TextElement>(); if (te != null) return te; }
            for (int i = 0; i < e.childCount; i++) { var r = FindNamed(e.ElementAt(i), name, depth + 1); if (r != null) return r; }
            return null;
        }

        static void Longest(VisualElement e, ref string best, int depth)
        {
            if (e == null || depth > 10) return;
            var te = e.TryCast<TextElement>();
            if (te != null)
            {
                var t = te.text ?? "";
                var v = PressRoom.Visible(t).Trim();
                if (v.Length > 3 && v != "Advice" && v != "Promise" && (best == null || v.Length > PressRoom.Visible(best).Trim().Length)) best = t;
            }
            for (int i = 0; i < e.childCount; i++) Longest(e.ElementAt(i), ref best, depth + 1);
        }
        static void Prefix(SI.Bindable.SIButton __instance) { Take(__instance, "click"); }
    }

    [HarmonyPatch(typeof(SI.Bindable.SIButton), nameof(SI.Bindable.SIButton.Submit))]
    static class SubmitPatch
    {
        static void Prefix(SI.Bindable.SIButton __instance) { PickPatch.Take(__instance, "submit"); }
    }

    // the answer arrives with its slot (5 = top answer ... 1 = bottom)
    [HarmonyPatch(typeof(FM.UI.NewsActionHandlerModule), nameof(FM.UI.NewsActionHandlerModule.TriggerSpeakToEvent))]
    static class AnswerPatch
    {
        static void Prefix(FM.UI.SpeakToActionEvent speakToActionEvent)
        {
            try
            {
                var e = speakToActionEvent;
                if ((int)e.Event == (int)FM.GamePlugin.InteropEventIDs.EventFmxConversationReplySelected)
                {
                    if (PressRoom.Picked != null && (DateTime.Now - PressRoom.PickedAt).TotalSeconds < 3) Word.OnChatReply(PressRoom.Picked, PressRoom.PickedGroup);
                    return;
                }
                if ((int)e.Event != (int)FM.GamePlugin.InteropEventIDs.EventFmxPressConferenceAnswerQuestion) return;
                int v = 0; try { if (e.ResponseValue != null && !e.ResponseValue.IsNull) int.TryParse(e.ResponseValue.AsString(), out v); } catch { }
                PressRoom.OnAnswered(v);
            }
            catch (Exception ex) { PressRoom.Fail("answer", ex); }
        }
    }
}
