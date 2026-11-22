// FM27 Media Mod - the supporters in the press room (Press Room 0.8.3). MIT licence (see LICENSE).
// The Tabloid layer reads the club's supporter screens into fans.tsv (who they are, their spokesperson, how much say they
// have, what pleases and disappoints them, in SI's own words). Here a journalist now and then opens with what the
// supporters' group is saying: once a conference at most, more often the more influence the supporters have on the board,
// and each grievance or highlight only once. The MEDIA OFFICE shows the same picture under THE TERRACES.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace FM27MediaMod
{
    internal static class FanPress
    {
        internal static string Club = "", Group = "", Spokes = "", Influence = "", Overall = "";
        internal static int Followers, Attendance;
        internal static readonly List<string> Crits = new List<string>(), Highs = new List<string>();
        internal static readonly List<KeyValuePair<string, int>> Segments = new List<KeyValuePair<string, int>>();
        internal static readonly Dictionary<string, string> Areas = new Dictionary<string, string>();
        static readonly HashSet<string> Used = new HashSet<string>();
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "fans.tsv");
        static string UsedPath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "fan press.tsv");
        static DateTime Stamp = DateTime.MinValue, UsedStamp = DateTime.MinValue;
        static int TalkConf = -1;
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        internal static void Fresh()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var t = File.GetLastWriteTimeUtc(FilePath);
                    if (t != Stamp)
                    {
                        Stamp = t; Club = Group = Spokes = Influence = Overall = ""; Followers = Attendance = 0;
                        Crits.Clear(); Highs.Clear(); Segments.Clear(); Areas.Clear();
                        foreach (var line in File.ReadAllLines(FilePath))
                        {
                            var p = line.Split('\t'); if (p.Length < 2) continue;
                            switch (p[0])
                            {
                                case "club": Club = p[1]; break; case "group": Group = p[1]; break; case "spokes": Spokes = p[1]; break;
                                case "influence": Influence = p[1]; break; case "overall": Overall = p[1]; break;
                                case "followers": int.TryParse(p[1], out Followers); break; case "attendance": int.TryParse(p[1], out Attendance); break;
                                case "crit": Crits.Add(p[1]); break; case "high": Highs.Add(p[1]); break;
                                case "seg": if (p.Length >= 3 && int.TryParse(p[2], out var n)) Segments.Add(new KeyValuePair<string, int>(p[1], n)); break;
                                case "area": if (p.Length >= 3) Areas[p[1]] = p[2]; break;
                            }
                        }
                        Segments.Sort((a, b) => b.Value.CompareTo(a.Value));
                    }
                }
                else if (Stamp != DateTime.MinValue) { Stamp = DateTime.MinValue; Club = Group = Spokes = Influence = Overall = ""; Crits.Clear(); Highs.Clear(); Segments.Clear(); Areas.Clear(); }
                // which lines the press have already used (per save, swapped by the memory)
                var u = File.Exists(UsedPath) ? File.GetLastWriteTimeUtc(UsedPath) : DateTime.MinValue;
                if (u != UsedStamp) { UsedStamp = u; Used.Clear(); if (File.Exists(UsedPath)) foreach (var l in File.ReadAllLines(UsedPath)) if (l.Length > 0) Used.Add(l); }
            }
            catch (Exception e) { PressRoom.Fail("fan press load", e); }
        }

        static int Mood(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            if (s.Contains("could not be happier") || s.Contains("ecstatic")) return 3;
            if (s.Contains("delighted") || s.Contains("very pleased")) return 2;
            if (s.Contains("unhappy") || s.Contains("furious") || s.Contains("angry")) return -2;
            if (s.Contains("disappointed")) return -1;
            if (s.Contains("pleased") || s.Contains("happy")) return 1;
            return 0;
        }

        // SI's sentence, as the supporters' group puts it to the press
        internal static string Say(string item, uint h)
        {
            var first = Regex.Split(item ?? "", @"(?<=\.)\s+")[0].Trim().TrimEnd('.');
            if (first.Length < 10) return null;
            string clause;
            if (Regex.IsMatch(first, @"^(The supporters|The fans) ")) clause = "t" + first.Substring(1);
            else if (Regex.IsMatch(first, @"^(Very |Extremely )?(Disappointed|Unhappy|Angry|Concerned|Frustrated|Pleased|Delighted|Happy|Thrilled)\b")) clause = "supporters are " + char.ToLowerInvariant(first[0]) + first.Substring(1);
            else return null;
            var group = Group.Length > 0 ? Group : (Club.Length > 0 ? Club + " supporters' group" : "the supporters' group");
            if (Spokes.Length == 0) return "The " + group + " says " + clause + ".";
            switch (h % 3)
            {
                case 0: return Spokes + ", of the " + group + ", says " + clause + ".";
                case 1: return "We've spoken to " + Spokes + " of the " + group + ": " + clause + ".";
                default: return "The " + group + " has been in touch. " + Spokes + " says " + clause + ".";
            }
        }

        static readonly Regex FansQ = new Regex(@"\b(supporters?|fans?|fanbase|crowd|stands|season ticket)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex FormQ = new Regex(@"\b(form|results|recent (games|matches|weeks|results)|lately|unbeaten|winless|streak|consistency|inconsistent|slump|bad patch|momentum|dropped points|points dropped|good run|bad run|this run|run of)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SeasonQ = new Regex(@"\b(season|promotion|promoted|play-?offs?|title|table|top of|league position|expectations?|ambitions?|targets?|the board)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Names = new Regex(@"\p{Lu}[\p{L}'\-]+(?: \p{Lu}[\p{L}'\-]+)*", RegexOptions.Compiled);
        // a supporters' view goes in front of a question only when it is about the same thing
        internal static bool Fits(string item, string q)
        {
            var v = PressRoom.Visible(q ?? "");
            if (FansQ.IsMatch(v)) return true;
            foreach (Match m in Names.Matches(item))
            {
                if (m.Index == 0) continue;     // the sentence's first word
                var n = m.Value; if (n.Length < 4 || Regex.IsMatch(n, "Championship|League|Cup|Sky Bet|EFL|Premier|Trophy|Supporters|The ")) continue;
                if (v.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                var last = n.Split(' '); if (last.Length > 1 && last[last.Length - 1].Length > 3 && v.IndexOf(last[last.Length - 1], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            if (Regex.IsMatch(item, @"\b(sale|sell|signing|sign|bring|loan|deal|finances|transfer)", RegexOptions.IgnoreCase)) return Word.Transfer.IsMatch(v);
            // an old result only when the question is about that opponent again (handled by the names above)
            if (Regex.IsMatch(item, @"\b(defeat|draw|win|victory|loss)\b", RegexOptions.IgnoreCase)) return false;
            return SeasonQ.IsMatch(v) || FormQ.IsMatch(v);
        }

        internal static string Talk(string q, string outlet, string journalist, PressRoom.Kind k)
        {
            try
            {
                Fresh();
                if (Group.Length == 0 && Spokes.Length == 0) return null;
                int conf = PressRoom.Record.Conf, qno = Word.QNo;
                if (TalkConf == conf || qno < 2 || MediaOffice.IsBanned(outlet)) return null;
                int chance = Influence == "Very High" ? 60 : Influence == "High" ? 45 : Influence == "Medium" ? 30 : 15;
                uint h = Hash("fans" + conf + Club);
                if (h % 100 >= (uint)chance) { TalkConf = conf; PressRoom.W("fans", "quiet this time", chance + "%"); return null; }
                // mostly the grievances; when the mood is very good, sometimes the praise
                bool praise = Mood(Overall) >= 2 && (h / 100) % 100 < 35;
                string pick = null;
                foreach (var list in praise ? new[] { Highs, Crits } : new[] { Crits, Highs })
                {
                    foreach (var it in list) if (!Used.Contains(it) && Say(it, h) != null && Fits(it, q)) { pick = it; break; }
                    if (pick != null) break;
                }
                if (pick == null) return null;
                var line = Say(pick, h / 7);
                Used.Add(pick); TalkConf = conf;
                try { File.AppendAllText(UsedPath, pick.Replace('\n', ' ') + "\n", new UTF8Encoding(false)); UsedStamp = File.GetLastWriteTimeUtc(UsedPath); } catch { }
                PressRoom.W("fans", outlet + " / " + journalist, line);
                return line;
            }
            catch (Exception e) { PressRoom.Fail("fan press", e); }
            return null;
        }
    }
}
