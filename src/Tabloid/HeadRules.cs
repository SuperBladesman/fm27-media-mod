// FM27 Media Mod - Tabloid Layer: the headline rules, kept apart from the game hooks so they can be tested offline
// against every headline the game has shown (0.6.5). Pure .NET: no Unity, no BepInEx.
// Links inside a headline are masked as \u0003<n>\u0004 before these run, and put back afterwards.
using System;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class HeadRules
    {
        // a name, a club or a link straight after the word (case-sensitive, whatever the rule's own case setting)
        const string Named = @"(?= (?-i:[\u0003A-ZÀ-Þ]))";

        // things that are not news stories: never rewritten
        internal static readonly Regex Skip = new Regex(@"^\s*(Scouting|Scout)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // plain-text swaps, applied in order to the headline with links masked out
        internal static readonly (Regex rx, string to)[] Rules = Build(new[]
        {
            (@"\bcompleted? the signing of\b", "land"),
            (@"\bsigning confirmed\b", "deal done"),
            (@"\bsigns (a )?new (long-term )?(deal|contract)\b", "pens bumper new deal"),
            (@"\bsign (a )?new (long-term )?(deal|contract)\b", "pen bumper new deal"),
            (@"\bsigns (a )?(contract|deal) extension\b", "pens bumper new deal"),
            (@"\bsign (a )?(contract|deal) extension\b", "pen bumper new deal"),
            (@"\bsigns (for|with)\b", "seals move to"),
            (@"\bsign (for|with)\b", "seal move to"),
            (@"\bsigns on\b", "seals move"),
            (@"\bsign on\b", "seal move"),
            (@"\bsign\b(?= \u0003)", "snap up"),
            // 0.6.5: not "joins the race for", "joins the queue", ...
            (@"\bjoins\b(?! (the )?(race|chase|queue|hunt|list|calls|chorus|fight|battle|scramble)\b)", "seals move to"),
            (@"\bpleased to hear praise from\b", "laps up praise from"),
            (@"\bhappy with\b", "delighted with"),
            (@"\bpleased with\b", "thrilled with"),
            (@"\bpleased as\b", "purring as"),
            (@"\bpleased to\b", "thrilled to"),
            (@"\bpleased\b", "purring"),
            (@"\bdisappointed with\b", "gutted by"),
            (@"\bdisappointed\b", "gutted"),
            (@"\bunhappy with\b", "fuming at"),
            (@"\bunhappy\b", "fuming"),
            (@"\bfrustrated\b", "seething"),
            (@"\bcriticises\b", "slams"),
            (@"\bcriticizes\b", "slams"),
            // 0.6.5: only the verb, with someone after it ("X questions Y"); never "Questions grow over ..."
            (@"(?<=\S )questions" + Named, "rips into"),
            // 0.6.5: not "sings X's praises", and not at the end
            (@"(?<!'s )\bpraises\b(?= \S)", "hails"),
            (@"\bbelieves\b", "insists"),
            // 0.6.5: "bullish of" is not English
            (@"\bconfident (of|about)\b", "bullish about"),
            (@"\bconfident\b", "bullish"),
            (@"\bresponds to\b", "hits back at"),
            (@"\brejects\b(?! (claims|talk|rumours|reports|suggestions|accusations)\b)", "snubs"),
            (@"\bturns down\b", "snubs"),
            (@"\binterested in\b", "circling"),
            // 0.6.5: "Dos Santos linked with switch to Portland" is a player linked with a move, not a club hunting
            (@"\blinked with\b", "in the frame for"),
            (@"\bmonitoring\b", "keeping tabs on"),
            (@"(?<=\S )scouting" + Named, "spying on"),
            (@"\bfined for\b", "hammered with fine for"),
            (@"\bfined\b", "hammered with fine"),
            (@"\bsacked\b", "axed"),
            (@"\bdismissed\b(?! (as|claims|talk|rumours|reports|concerns|fears)\b)", "axed"),
            (@"\bappointed\b", "handed the hot seat"),
            (@"\bresigns\b", "walks out"),
            (@"\bsuffers injury\b", "crocked"),
            (@"\binjured\b", "crocked"),
            (@"\bruled out\b", "sidelined"),
            (@"\bwants (a )?move\b", "wants out"),
            (@"\bwants to leave\b", "wants out"),
            (@"\btransfer request\b", "transfer bombshell"),
            (@"\brescues\b", "bails out"),
            (@"\bshines\b", "dazzles"),
            // 0.6.5: "warns of ..." is not "fires warning to of ..."
            (@"\bwarns\b" + Named, "fires warning to"),
            (@"\bthrash\b", "hammer"),
            // 0.6.5: "beat Leeds", "beat rivals"; not "beat competition to land", "beat the drop"
            (@"\bbeat\b(?= (?-i:[\u0003A-ZÀ-Þ])| rivals\b)", "sink"),
            (@"\bdraw with\b", "held by"),
            (@"\blose to\b", "stunned by"),
            (@"\bloses to\b", "stunned by"),
            (@"\bupset(?= (win|victory|defeat|loss)\b|$)", "shock"),
            (@"\bclinch\b", "seal"),
            (@"\bpromoted\b", "going up"),
            (@"\brelegated to\b", "dumped into"),
            (@"\brelegated\b", "down and out"),
            // 0.6.5: "face late pressure to finalise" and "as pressure eases" no longer read as a crisis
            (@"\blate pressure to\b", "a race against the clock to"),
            (@"\bpressure\b(?! (eases|eased|easing|lifted|lifts|off)\b)", "heat"),
            (@"\bunder fire\b", "in the firing line"),
            (@"\breveals\b", "lifts the lid on"),
            (@"\bdenies\b", "slaps down"),
            (@"\badmits\b", "confesses"),
        });

        // whole-headline red-top rewrites first ({L} = a name or club, kept as a link); $1, $2 = those names
        internal static readonly (Regex rx, string to)[] Full = BuildFull(new[]
        {
            (@"^{L} sign {L}$", "$2 IN! $1 land their man"),
            (@"^{L} signing confirmed$", "Done deal: $1 puts pen to paper"),
            (@"^{L} happy with draw$", "$1 settles for a point"),
            (@"^{L} disappointed with defeat$", "Gutted $1 left licking wounds"),
            (@"^{L} makes {L} debut$", "$1 bows in for $2"),
            (@"^{L} in offer for {L}$", "$1 swoop for $2"),
            (@"^{L} wins (.+) Manager of the Month$", "$1 scoops gong: $2 boss of the month"),
            (@"^{L}'s {L} scoops (.+)$", "$2 bags $3"),
            (@"^Pressure grows on {L} ahead of {L} clash$", "Last chance saloon: $1 on the brink before $2 showdown"),
            (@"^Run can continue - {L}$", "$1 vows: the run goes on"),
            (@"^{L} believes (.+)$", "$1 insists $2"),
            (@"^{L} criticises {L}$", "War of words: $1 lays into $2"),
            (@"^{L} responds to {L}$", "$1 bites back at $2"),
            (@"^{L} questions {L}$", "$1 turns on $2"),
            (@"^{L} sacked$", "Axed! $1 shown the door"),
            (@"^{L} appointed (.+)$", "$1 handed the hot seat at $2"),
            (@"^{L} loses cool$", "Meltdown! $1 storms out of presser"),
            (@"^{L} blows top$", "$1 sees red and walks out on the press"),
            (@"^{L} storms out(.*)$", "Meltdown! $1 storms out$2"),
            (@"^{L} refuses to (comment|answer)(.*)$", "$1 stonewalls the press$3"),
            (@"^Pressure building on {L} in {L} drama$", "Crisis: heat building on $1 as $2 saga rumbles on"),
            (@"^{L} slaps transfer request in$", "Bombshell: $1 wants out"),
            (@"^{L} face late pressure to finalise {L} transfer$", "Race against the clock: $1 scramble to get $2 deal done"),
            (@"^{L} hands in transfer request$", "Bombshell: $1 wants out"),
            // 0.6.5: with or without links
            (@"^Questions grow over (.+?)'s position at (.+)$", "Crisis: $1's job on the line at $2"),
            (@"^Questions grow over (.+?)'s future at (.+)$", "Crisis: $1's future on the line at $2"),
            (@"^(.+?) sings (.+?)'s praises$", "Love-in: $1 hails $2"),
            (@"^(.+?) back (.+?) as pressure eases$", "$1 stand by $2 as the heat comes off"),
        });

        // kickers: a red-top label in front, picked by the strongest word in the rewritten headline
        internal static readonly (Regex rx, string kick)[] Kickers =
        {
            (new Regex(@"\b(slams|rips into|hits back at|lays into|bites back|turns on|war of words)\b", RegexOptions.IgnoreCase), "WAR OF WORDS: "),
            (new Regex(@"\b(wants out|bombshell)\b", RegexOptions.IgnoreCase), "BOMBSHELL: "),
            (new Regex(@"\b(axed|walks out|shown the door)\b", RegexOptions.IgnoreCase), "THE CHOP: "),
            (new Regex(@"\b(fuming|seething|gutted|licking wounds)\b", RegexOptions.IgnoreCase), "FURY: "),
            (new Regex(@"\b(snubs)\b", RegexOptions.IgnoreCase), "SNUBBED: "),
            (new Regex(@"\b(heat|firing line|on the brink|last chance|on the line)\b(?! comes off)", RegexOptions.IgnoreCase), "CRISIS: "),
            (new Regex(@"\b(laps up|hails|purring)\b", RegexOptions.IgnoreCase), "LOVE-IN: "),
            (new Regex(@"\b(crocked|sidelined)\b", RegexOptions.IgnoreCase), "INJURY BLOW: "),
            (new Regex(@"\b(swoop|circling|in the hunt for|in the frame for|spying on|keeping tabs on)\b", RegexOptions.IgnoreCase), "TRANSFER TALK: "),
        };

        static (Regex, string)[] Build((string, string)[] src)
        {
            var o = new (Regex, string)[src.Length];
            for (int i = 0; i < src.Length; i++) o[i] = (new Regex(src[i].Item1, RegexOptions.IgnoreCase | RegexOptions.Compiled), src[i].Item2);
            return o;
        }
        static (Regex, string)[] BuildFull((string, string)[] src)
        {
            var o = new (Regex, string)[src.Length];
            for (int i = 0; i < src.Length; i++)
                o[i] = (new Regex(src[i].Item1.Replace("{L}", "(\u0003\\d+\u0004)"), RegexOptions.IgnoreCase | RegexOptions.Compiled), src[i].Item2);
            return o;
        }
        static string MatchCase(string from, string to)
        {
            if (from.Length > 0 && char.IsUpper(from[0])) return char.ToUpperInvariant(to[0]) + to.Substring(1);
            return to;
        }

        // the whole rewrite of one masked headline; hit = anything changed
        internal static string Apply(string s, bool kick, out bool hit)
        {
            hit = false; bool full = false;
            if (Skip.IsMatch(s)) return s;
            foreach (var (rx, to) in Full)
                if (rx.IsMatch(s)) { s = rx.Replace(s, to); hit = full = true; break; }
            if (!full)
                foreach (var (rx, to) in Rules)
                {
                    bool h = false;
                    s = rx.Replace(s, m => { h = true; return MatchCase(m.Value, to); });
                    if (h) hit = true;
                }
            if (hit && kick && !full)
                foreach (var (rx, k) in Kickers)
                    if (rx.IsMatch(s) && !s.StartsWith(k.Substring(0, 4), StringComparison.OrdinalIgnoreCase)) { s = k + s; break; }
            return s;
        }
    }
}
