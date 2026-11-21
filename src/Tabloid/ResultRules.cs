// FM27 Media Mod - Tabloid: whose result is it? (0.6.6, pure .NET so it replays offline)
// SI's match stories carry the result in a fixed form: "Middlesbrough's convincing 4-1 ... victory over Sheffield United",
// "Sheffield United's 4-2 ... defeat against Wrexham", "QPR lost ground ... after 3-1 defeat to Sheffield United",
// "... claim a draw against Sheffield United". The result word is read against the club after it: a victory over us is
// our defeat, a defeat to us is our win; a victory over someone else, in a sentence that names us, is our win.
// Headlines are only trusted when they name us ("Sheffield United turn dominance into victory against Lincoln").
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class ResultRules
    {
        // the result word, then the preposition, then the club (a capitalised name; "his former club" does not count)
        static readonly Regex Formula = new Regex(
            @"\b(?<w>(?i:victory|victories|win|wins|triumph|defeat|defeats|loss|draw|stalemate))\s+(?<p>(?i:over|against|to|with|at the hands of|at|for|by))\s+(?<t>[A-Z][^,.;:!?]*)",
            RegexOptions.Compiled);
        // "after 3-1 defeat to X" / "after 2-0 victory over X" are caught by Formula; these catch the verb forms
        static readonly Regex Verb = new Regex(
            @"(?<a>[A-Z][\w'’.\- ]*?)\s+(?<v>(?i:beat|beats|thrashed|thrash|hammered|hammer|stunned|stun|edged|edge past|saw off|sink|sank|overcame|overcome|dispatched|swept aside))\s+(?<t>[A-Z][^,.;:!?]*)",
            RegexOptions.Compiled);
        static readonly Regex Sentence = new Regex(@"[^.!?]+[.!?]?", RegexOptions.Compiled);

        // our result ("win", "defeat", "draw") or null when no sentence ties a result to us
        internal static string Ours(string head, string body, IList<string> ourNames, out string why)
        {
            why = "";
            if (ourNames == null || ourNames.Count == 0) return null;
            foreach (var (text, where) in new[] { (body ?? "", "body"), (head ?? "", "head") })   // the story's own text first: the headline may be a tile's
            {
                if (text.Length == 0) continue;
                // headlines in capitals: read them in title case so the club test works
                var t = where == "head" && text == text.ToUpperInvariant() ? Title(text) : text;
                foreach (Match sm in Sentence.Matches(t))
                {
                    var s = sm.Value;
                    bool namesUs = Names(s, ourNames);
                    if (!namesUs) continue;
                    foreach (Match m in Formula.Matches(s))
                    {
                        var w = m.Groups["w"].Value.ToLowerInvariant();
                        var team = m.Groups["t"].Value;
                        // "a defeat for Sheffield United could see them fall to 3rd" is a preview, not a result
                        if (Regex.IsMatch(team, @"\b(could|would|might)\b")) continue;
                        bool teamIsUs = StartsWithUs(team, ourNames);
                        string r = null;
                        if (m.Groups["p"].Value.ToLowerInvariant() == "for")
                        {
                            // "win for the Blades", "defeat for Sheffield United": the club after "for" owns the result
                            if (!teamIsUs) continue;
                            r = w.StartsWith("draw") || w == "stalemate" ? "draw" : w.StartsWith("defeat") || w == "loss" ? "defeat" : "win";
                            why = where + ": " + Short(m.Value);
                            return r;
                        }
                        if (w.StartsWith("draw") || w == "stalemate") r = "draw";
                        else if (m.Groups["p"].Value.ToLowerInvariant() == "by" && (w.StartsWith("victor") || w.StartsWith("win") || w == "triumph")) r = teamIsUs ? "win" : "defeat";   // "a 2-1 victory by X"
                        else if (w.StartsWith("victor") || w.StartsWith("win") || w == "triumph") r = teamIsUs ? "defeat" : "win";
                        else if (w.StartsWith("defeat") || w == "loss") r = teamIsUs ? "win" : "defeat";
                        if (r == null) continue;
                        // "X's 2-1 victory over Y" where neither X nor Y is us but we are named elsewhere in the sentence:
                        // only trust it when the words before the result name us, or the club after it is us
                        if (!teamIsUs && !Names(s.Substring(0, m.Index), ourNames)) continue;
                        why = where + ": " + Short(m.Value);
                        return r;
                    }
                    foreach (Match m in Verb.Matches(s))
                    {
                        bool aUs = EndsWithUs(m.Groups["a"].Value, ourNames), tUs = StartsWithUs(m.Groups["t"].Value, ourNames);
                        if (aUs == tUs) continue;
                        why = where + ": " + Short(m.Value);
                        return aUs ? "win" : "defeat";
                    }
                    // "Sheffield United denied by late goal", "held to a draw"
                    if (where == "head" && Regex.IsMatch(s, @"\b(draw|drew|stalemate|held to|held by|share the spoils|goalless|all square|denied by (a )?late)\b", RegexOptions.IgnoreCase))
                    { why = where + ": " + Short(s); return "draw"; }
                }
            }
            return null;
        }

        // a headline result word is ours only when we are named before it ("Sheffield United lose top spot", not
        // "Middlesbrough ease to victory" or "Wrexham win at Bramall Lane against Sheffield United")
        internal static bool HeadIsOurs(string head, IList<string> ours, Regex resultWord)
        {
            if (string.IsNullOrEmpty(head)) return false;
            var m = resultWord.Match(head); if (!m.Success) return false;
            int first = int.MaxValue;
            foreach (var n in ours) { if (n.Length <= 3) continue; int i = head.IndexOf(n, StringComparison.OrdinalIgnoreCase); if (i >= 0 && i < first) first = i; }
            return first < m.Index;
        }

        internal static bool Names(string s, IList<string> ours)
        {
            foreach (var n in ours) if (n.Length > 3 && s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        static bool StartsWithUs(string t, IList<string> ours)
        {
            t = t.Trim();
            foreach (var n in ours) if (n.Length > 3 && t.StartsWith(n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static bool EndsWithUs(string a, IList<string> ours)
        {
            a = a.Trim();
            foreach (var n in ours) if (n.Length > 3 && a.EndsWith(n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        static string Title(string s) => Regex.Replace(s.ToLowerInvariant(), @"(^|[\s'’(\-])(\p{L})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
        static string Short(string s) { s = s.Trim(); return s.Length > 90 ? s.Substring(0, 90) + "..." : s; }
    }
}
