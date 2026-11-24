// FM27 Media Mod - Treatment Room 0.5.8: the department's note on SI's treatment choice (pure C#, tested offline).
// From SI's medical inbox message (who, the injury, the options it offers) and the ledger (the player's injuries on file,
// minutes and susceptibility at the last risk assessment). Names only, no pronouns.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FM27MediaMod
{
    internal static class OfferNotes
    {
        static int Day(string d) => GameDate.Day(d);
        static string Bare(string s) => PersonLinks.Plain(s ?? "").Trim();
        static readonly System.Text.RegularExpressions.Regex InjuryRx = new System.Text.RegularExpressions.Regex(
            @"(?:has sustained|has suffered|has picked up|is suffering from|is carrying)\s+(an? [a-z][^.,]*?)(?=\s+(?:after|in|during|while|at|from|that|which)\b|[.,]|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex MinutesRx = new System.Text.RegularExpressions.Regex(
            @"(\d+)\s+match(?:es)?\s*\((\d+)\s*/\s*(\d+)\s*minutes\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // who the message is about ("Name [key]" from the ledger)
        internal static (string Name, string Id) Who(string who)
        {
            who = who ?? ""; int b = who.LastIndexOf(" [");
            if (b > 0 && who.EndsWith("]")) return (who.Substring(0, b), who.Substring(b + 2, who.Length - b - 3));
            return (who, "");
        }
        // the choices SI's message offers, in its own words
        internal static string Options(string plain)
        {
            var lt = (plain ?? "").ToLowerInvariant(); var o = new List<string>();
            if (lt.Contains("physio")) o.Add("the physios");
            if (lt.Contains("pain management")) o.Add("pain management");
            if (lt.Contains("specialist")) o.Add("a specialist");
            if (lt.Contains("see a doctor")) o.Add("a doctor");
            if (lt.Contains("protective equipment")) o.Add("protective equipment");
            return o.Count == 0 ? "" : o.Count == 1 ? o[0] : string.Join(", ", o.Take(o.Count - 1)) + " or " + o.Last();
        }

        // the department's note on SI's treatment choice: who, what, the options, the record, the minutes, a view
        internal static string Make(string who, string plain, int now)
        {
            var (name, id) = Who(who);
            if (name.Length == 0) return "";
            var t = plain ?? ""; var lt = t.ToLowerInvariant();
            var im = InjuryRx.Match(t); var injury = im.Success ? im.Groups[1].Value.Trim().ToLowerInvariant() : "";
            bool pain = lt.Contains("pain management"), specialist = lt.Contains("specialist");
            var sb = new System.Text.StringBuilder(name);
            if (injury.Length > 0) sb.Append(", " + injury + (lt.Contains("training") ? " in training" : ""));
            sb.Append(". ");
            var opts = Options(t); if (opts.Length > 0) sb.Append("Options: " + opts + ". ");
            // the record (this injury may not be on file yet)
            if (id.Length > 0)
            {
                var before = Ledger.Injuries.Values.Where(i => i.Who == id && i.Ours != "n" && !Summary.Personal(i.Injury) && Day(i.Date) >= 0 && (now < 0 || Day(i.Date) < now))
                    .OrderByDescending(i => Day(i.Date)).ToList();
                if (before.Count == 1) sb.Append("On file: " + Bare(before[0].Injury).ToLowerInvariant() + " (" + before[0].Date + "). ");
                else if (before.Count > 1) sb.Append("On file: " + before.Count + " injuries, the last " + Bare(before[0].Injury).ToLowerInvariant() + " (" + before[0].Date + "). ");
                else sb.Append("Nothing on file before this. ");
            }
            // the minutes and susceptibility at the last risk assessment
            Ledger.Rk k = null;
            if (id.Length > 0) k = Ledger.Risk.Values.Where(r => r.Who == id).OrderByDescending(r => Day(r.Date)).FirstOrDefault();
            int matches = -1, minutes = -1;
            if (k != null) { var mm = MinutesRx.Match(k.MatchLine ?? ""); if (mm.Success) { matches = int.Parse(mm.Groups[1].Value); minutes = int.Parse(mm.Groups[2].Value); } }
            if (pain && minutes == 0) sb.Append("No minutes in the 14 days to " + k.Date + ", so no match to rush back for: the physios are the safer call. ");
            else if (pain && matches >= 2) sb.Append(minutes + " minutes in the 14 days to " + k.Date + ": pain management keeps " + name + " available, but the injury is still there. ");
            else if (specialist) sb.Append("The Treatment Room will record how a specialist's verdict compares with the physios'. ");
            if (k != null && (k.Suscept.Equals("High", StringComparison.OrdinalIgnoreCase) || k.Suscept.Equals("Very High", StringComparison.OrdinalIgnoreCase) || k.Suscept.Equals("Above Average", StringComparison.OrdinalIgnoreCase)))
                sb.Append("Injury susceptibility " + k.Suscept.ToLowerInvariant() + ".");
            return sb.ToString().Trim();
        }
    }
}
