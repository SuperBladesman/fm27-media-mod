// FM27 Media Mod - The Terraces: who says what under a story. MIT licence (see LICENSE).
// The lines are real fans' posts (from published fan-reaction round-ups, Reddit and forum threads), with real names swapped
// for tokens and gendered words taken out, plus a few written in the same register. Each invented fan has a "hand":
// some type in lower case, some shout when it goes well, some add emoji or the club's sign-off. Club words (nicknames,
// the ground, the home end, the sign-off, rivals' nicknames) come from "fan words.tsv", which you can edit.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace FM27MediaMod
{
    internal class Pack
    {
        public string Club = "", Nickname = "", Fan = "", Short = "", Home = "", End = "", Signoff = "", Emoji = "", Dialect = "";
        public List<string> Aka = new List<string>(), Derby = new List<string>();
        public List<(string rival, string[] names)> Rivals = new List<(string, string[])>();
    }

    internal static class FanWords
    {
        static string FilePath => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod", "fan words.tsv");
        static DateTime Stamp = DateTime.MinValue;
        static readonly List<Pack> Packs = new List<Pack>();
        static string Norm(string s) { var sb = new StringBuilder(); foreach (var c in (s ?? "").ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c); return sb.ToString(); }
        static bool Same(string a, string b) => a.Length > 0 && b.Length > 0 && (Norm(a) == Norm(b) || Story.Mentions(a, b) || Story.Mentions(b, a));

        internal static Pack For(string club)
        {
            Fresh();
            if (string.IsNullOrEmpty(club)) return null;
            foreach (var p in Packs) { if (Same(p.Club, club)) return p; foreach (var a in p.Aka) if (Same(a, club)) return p; }
            return null;
        }
        // the names this club's fans use for a rival: rude ones first, the plain one last
        internal static string[] RivalNames(Pack p, string rival)
        {
            if (p == null || string.IsNullOrEmpty(rival)) return null;
            foreach (var r in p.Rivals) if (Same(r.rival, rival)) return r.names;
            return null;
        }
        static void Fresh()
        {
            try
            {
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Default, new UTF8Encoding(false));
                var t = File.GetLastWriteTimeUtc(FilePath); if (t == Stamp) return; Stamp = t;
                Packs.Clear(); var byClub = new Dictionary<string, Pack>();
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.TrimEnd(); if (line.Length == 0 || line.StartsWith("#")) continue;
                    var f = line.Split('\t'); if (f.Length < 3) continue;
                    var club = f[0].Trim(); var key = f[1].Trim().ToLowerInvariant(); var val = f[2].Trim();
                    if (!byClub.TryGetValue(club, out var p)) { p = new Pack { Club = club }; byClub[club] = p; Packs.Add(p); }
                    switch (key)
                    {
                        case "nickname": p.Nickname = val; break; case "fan": p.Fan = val; break; case "short": p.Short = val; break;
                        case "home": p.Home = val; break; case "end": p.End = val; break; case "signoff": p.Signoff = val; break;
                        case "emoji": p.Emoji = val; break; case "dialect": p.Dialect = val.ToLowerInvariant(); break;
                        case "aka": p.Aka.AddRange(val.Split('|')); break;
                        case "derby": p.Derby.Add(val); break;
                        case "rival": if (f.Length >= 4) p.Rivals.Add((val, f[3].Split('|'))); break;
                    }
                }
            }
            catch (Exception e) { Tabloid.Fail("fan words", e); }
        }

        const string Default =
"# FM27 Media Mod - fan words. One entry per line: club <tab> key <tab> value. Edit freely; the game picks it up next story.\n" +
"# club     the club as the game names it (other spellings under aka, separated by |)\n" +
"# keys     nickname (the Blades), fan (one fan: Blade), short (United), home (the Lane), end (the home end: the Kop),\n" +
"#          signoff (added to some posts: UTB), emoji (the club's own), dialect (yorkshire adds a few local words),\n" +
"#          derby (a line for beating the main rival), and rival <tab> <rival as the game names it> <tab> what the fans call them,\n" +
"#          rude names first and the plain name last, separated by |. Hardcore and fair-weather fans use the rude ones.\n" +
"# Banter only: nothing about tragedies, and nothing aimed at race, religion, sexuality or disability.\n" +
"Sheffield United\taka\tSheff Utd|SUFC|Blades\n" +
"Sheffield United\tnickname\tthe Blades\n" +
"Sheffield United\tfan\tBlade\n" +
"Sheffield United\tshort\tUnited\n" +
"Sheffield United\thome\tthe Lane\n" +
"Sheffield United\tend\tthe Kop\n" +
"Sheffield United\tsignoff\tUTB\n" +
"Sheffield United\temoji\t⚔️\n" +
"Sheffield United\tdialect\tyorkshire\n" +
"Sheffield United\tderby\tThe city is red ⚔️\n" +
"Sheffield United\tderby\tPorkers beaten. Best feeling in the world.\n" +
"Sheffield United\trival\tSheff Wed\tthe pigs|the porkers|Wendy|Wednesday\n" +
"Sheffield United\trival\tLeeds\tdirty Leeds|Leeds\n" +
"Sheffield United\trival\tNott'm Forest\tthe Tricky Trees|the scabs|Forest\n" +
"Sheffield United\trival\tBarnsley\tthe dingles|Barnsley\n" +
"Sheffield Wednesday\taka\tSheff Wed|SWFC|Owls\n" +
"Sheffield Wednesday\tnickname\tthe Owls\n" +
"Sheffield Wednesday\tfan\tOwl\n" +
"Sheffield Wednesday\tshort\tWednesday\n" +
"Sheffield Wednesday\thome\tHillsborough\n" +
"Sheffield Wednesday\tend\tthe Kop\n" +
"Sheffield Wednesday\tsignoff\tWAWAW\n" +
"Sheffield Wednesday\temoji\t\U0001F989\n" +
"Sheffield Wednesday\tdialect\tyorkshire\n" +
"Sheffield Wednesday\trival\tSheff Utd\tthe Blunts|the pigs|United\n" +
"Leeds United\taka\tLeeds|LUFC\n" +
"Leeds United\tshort\tLeeds\n" +
"Leeds United\thome\tElland Road\n" +
"Leeds United\tsignoff\tMOT\n" +
"Leeds United\tdialect\tyorkshire\n" +
"Nottingham Forest\taka\tNott'm Forest|Forest|NFFC|Tricky Trees\n" +
"Nottingham Forest\tnickname\tthe Reds\n" +
"Nottingham Forest\tshort\tForest\n" +
"Nottingham Forest\thome\tthe City Ground\n" +
"Nottingham Forest\tend\tthe Trent End\n" +
"Nottingham Forest\tsignoff\tCOYR\n" +
"Nottingham Forest\temoji\t\U0001F333\n";
    }

    internal static class Voice
    {
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }
        static readonly Dictionary<string, int> Default = new Dictionary<string, int>
        { ["Core"] = 35, ["Casual"] = 20, ["Fair Weather"] = 15, ["Hardcore"] = 15, ["Family"] = 10, ["Corporate"] = 5 };
        static readonly Regex Emo = new Regex("[\uD83C-\uDBFF][\uDC00-\uDFFF]|[☀-➿]️?|️|‍", RegexOptions.Compiled);
        internal static string NoEmoji(string t) => Regex.Replace(Emo.Replace(t ?? "", ""), @"\s{2,}", " ").Trim();

        // what fills the tokens in a line
        internal class Ctx
        {
            public Story S; public Pack P; public string Seg = ""; public uint H; public string Rival = ""; public HashSet<string> Said;
        }

        internal static (string text, string name, string seg) Compose(Story s, int slot, HashSet<string> used)
        {
            uint h = Hash(s.Head + "|" + s.Body + "|" + slot);
            var ours = FanWords.For(FanBook.Club);
            // a story about one of our rivals going badly: one of ours turns up under it
            if (s.AboutRival && s.Negative && slot == 1 && FanBook.Club.Length > 0)
            {
                var gseg = h % 2 == 0 ? "Hardcore" : "Fair Weather";
                var gx = new Ctx { S = s, P = ours, Seg = gseg, H = h, Rival = s.Rival, Said = used };
                var g = Pick(Lines.Gloat, gx);
                if (g != null) return (Hand(g, gx, Hash("gloat" + s.Head), true), Name("gloat" + s.Head), gseg + " (ours, gloating)");
            }
            var seg = PickSegment(s, h, used);
            used.Add(seg);
            var pack = s.Ours ? ours : FanWords.For(s.ClubTag);
            var x = new Ctx { S = s, P = pack, Seg = seg, H = h, Rival = s.Rival, Said = used };
            string text = null;
            // the club's own pre-match fan line (SI's), now and then
            if (s.Ours && s.Kind == "preview" && seg == "Core" && FanBook.Voice.Count > 0 && h % 3 == 0) text = FanBook.Voice[(int)(h / 3 % (uint)FanBook.Voice.Count)];
            // a derby, or a story involving the rivals
            if (text == null && s.Rival.Length > 0 && !s.AboutRival && (seg == "Hardcore" || seg == "Core" || seg == "Fair Weather"))
            {
                if (s.Kind == "win" && seg == "Hardcore" && pack != null && pack.Derby.Count > 0 && h % 2 == 0) text = pack.Derby[(int)(h / 2 % (uint)pack.Derby.Count)];
                if (text == null)
                {
                    var key = s.Kind == "win" || s.Kind == "defeat" || s.Kind == "draw" || s.Kind == "preview" ? s.Kind : "other";
                    if (Lines.Rival.TryGetValue(seg + "/" + key, out var rp)) text = Pick(rp, x);
                }
            }
            if (text == null && s.Kind == "signing" && s.Loan) text = Line(seg, "loan", x);
            if (text == null) text = Line(seg, s.Kind, x);
            if (text == null) return (null, null, seg);
            if (s.Ours) text = AddHook(text, x);
            var name = Name(seg + s.Head + slot);
            text = Hand(text, x, Hash(name), false);
            if (text.Length > 230) { var cut = text.LastIndexOf(' ', 229); text = text.Substring(0, cut > 150 ? cut : 229); }
            return (text, name, seg);
        }

        static string PickSegment(Story s, uint h, HashSet<string> used)
        {
            var w = new Dictionary<string, double>();
            var src = s.Ours && FanBook.Segments.Count >= 3 ? FanBook.Segments : Default;
            foreach (var kv in src) w[kv.Key] = Math.Max(1, kv.Value);
            int mood = s.Ours ? FanBook.Mood(FanBook.Overall) : 0;
            if (s.Negative || mood <= -1) { Mul(w, "Hardcore", 1.6); Mul(w, "Fair Weather", 1.4); Mul(w, "Family", 0.7); Mul(w, "Corporate", 0.8); }
            else if (s.Positive && mood >= 2) { Mul(w, "Fair Weather", 1.3); Mul(w, "Casual", 1.2); }
            foreach (var u in used) Mul(w, u, 0.25);   // spread the voices across a story's posts
            double total = 0; foreach (var v in w.Values) total += v;
            double r = (h % 10000) / 10000.0 * total;
            foreach (var kv in w) { r -= kv.Value; if (r <= 0) return kv.Key; }
            return "Core";
        }
        static void Mul(Dictionary<string, double> w, string k, double f) { if (w.ContainsKey(k)) w[k] *= f; }

        static string Line(string seg, string kind, Ctx x)
        {
            if (!Lines.By.TryGetValue(seg, out var kinds)) kinds = Lines.By["Core"];
            if (!kinds.TryGetValue(kind, out var pool)) { if (kind == "loan") return null; kinds.TryGetValue("other", out pool); }
            return pool == null ? null : Pick(pool, x);
        }
        internal static string Pick(string[] pool, Ctx x)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                var raw = pool[(int)((x.H / 7 + (uint)i) % (uint)pool.Length)];
                if (x.Said != null && x.Said.Contains(raw)) continue;     // no line twice under one story
                var t = Fill(raw, x);
                if (t != null) { x.Said?.Add(raw); return t; }
            }
            return null;
        }
        static readonly Regex Token = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);
        // null when the line needs something this story or club doesn't have
        static string Fill(string t, Ctx x)
        {
            bool ok = true;
            var p = x.P; var s = x.S;
            string clubName = s.Ours ? FanBook.Club : s.ClubTag;
            string nick = p?.Nickname ?? "";
            var res = Token.Replace(t, m =>
            {
                string v = null;
                switch (m.Groups[1].Value)
                {
                    case "player": v = s.Subject; break;
                    case "manager": v = s.Manager; break;
                    case "nickname": v = nick; break;
                    case "Nickname": v = nick.Length > 0 ? char.ToUpperInvariant(nick[0]) + nick.Substring(1) : ""; break;
                    case "nickshort": v = Regex.Replace(nick, @"^the\s+", "", RegexOptions.IgnoreCase).ToLowerInvariant(); break;
                    case "NICK": v = Regex.Replace(nick, @"^the\s+", "", RegexOptions.IgnoreCase).ToUpperInvariant(); break;
                    case "fan": v = p != null && p.Fan.Length > 0 ? p.Fan : "fan"; break;
                    case "short": v = p != null && p.Short.Length > 0 ? p.Short : clubName; break;
                    case "home": v = p?.Home; break;
                    case "end": v = p?.End; break;
                    case "reyt": v = p != null && p.Dialect == "yorkshire" ? "reyt" : null; break;
                    case "rival": { var n = FanWords.RivalNames(p, x.Rival); v = n != null ? n[n.Length - 1] : x.Rival; break; }
                    case "rivalnick":
                    {
                        var n = FanWords.RivalNames(p, x.Rival);
                        bool rude = x.Seg == "Hardcore" || x.Seg == "Fair Weather" || x.Seg == "Casual";
                        if (n == null) v = x.Rival;
                        else if (rude && n.Length > 1) v = n[(int)((x.H / 11) % (uint)(n.Length - 1))];
                        else v = n[n.Length - 1];
                        break;
                    }
                    default: v = m.Value; break;
                }
                if (string.IsNullOrEmpty(v)) { ok = false; return ""; }
                return v;
            });
            return ok ? res : null;
        }

        // each invented fan types the same way every time: some lower case, some shouting, some emoji, some sign off
        static readonly Dictionary<string, string[]> Emoji = new Dictionary<string, string[]>
        {
            ["win"] = new[] { "\U0001F525", "\U0001F44F", "\U0001F64C", "\U0001F60D", "\U0001F4AA" },
            ["signing"] = new[] { "\U0001F525", "\U0001F44F", "\U0001F64C", "\u270D\uFE0F" },
            ["award"] = new[] { "\U0001F44F", "\U0001F410", "\U0001F525" },
            ["preview"] = new[] { "\U0001F91E", "\U0001F4AA", "\U0001F525" },
            ["other"] = new[] { "\U0001F44D", "\U0001F64C" },
            ["defeat"] = new[] { "\U0001F62D", "\U0001F926", "\U0001F644", "\U0001F62C", "\U0001F610", "\U0001F612" },
            ["draw"] = new[] { "\U0001F610", "\U0001F937", "\U0001F971" },
            ["sale"] = new[] { "\U0001F494", "\U0001F937", "\U0001F610", "\U0001F612" },
            ["injury"] = new[] { "\U0001F64F", "\U0001F622", "\U0001F494" },
            ["unrest"] = new[] { "\U0001F37F", "\U0001F644", "\U0001F440" },
            ["storm"] = new[] { "\U0001F37F", "\U0001F602", "\U0001F440", "\U0001F525" },
            ["crisis"] = new[] { "\U0001F62C", "\U0001F644", "\U0001F926", "\U0001F612" },
            ["gloat"] = new[] { "\U0001F602", "\U0001F37F", "\U0001F921" },
        };
        static string Hand(string text, Ctx x, uint nh, bool gloat)
        {
            var s = x.S; var seg = x.Seg; var p = gloat ? FanWords.For(FanBook.Club) : x.P;
            uint r1 = nh % 100, r2 = (nh / 100) % 100, r3 = (nh / 10000) % 100, r4 = (nh / 1000000) % 100;
            bool good = gloat || s.Positive || s.Kind == "preview";
            bool bad = !gloat && s.Negative;
            int lowP = seg == "Casual" ? 70 : seg == "Fair Weather" ? 30 : seg == "Hardcore" ? 15 : seg == "Core" ? 8 : 0;
            int emoP = seg == "Casual" ? 55 : seg == "Fair Weather" ? 45 : seg == "Hardcore" ? 35 : seg == "Family" ? 30 : seg == "Core" ? 15 : 0;
            bool hasEmo = Emo.IsMatch(text);
            if (r1 < lowP) text = text.ToLowerInvariant().TrimEnd('.');
            else if (good && !hasEmo && ((seg == "Hardcore" && r3 < 35) || (seg == "Fair Weather" && r3 < 25)))
            {
                // shout the first sentence
                var m = Regex.Match(text, @"^[^.!?]+[.!?]*");
                var first = m.Value.TrimEnd('.', '!', '?').ToUpperInvariant();
                text = first + "!!" + text.Substring(m.Length);
            }
            if (p != null && p.Signoff.Length > 0 && (seg == "Hardcore" || seg == "Core" || gloat) && r4 < 30) text = text.TrimEnd() + " " + p.Signoff;
            if (!hasEmo && r2 < emoP)
            {
                if (!Emoji.TryGetValue(gloat ? "gloat" : s.Kind, out var set)) set = Emoji["other"];
                var e = set[(int)((nh / 7) % (uint)set.Length)];
                if (!gloat && (s.Kind == "win" || s.Kind == "signing" || s.Kind == "preview") && p != null && p.Emoji.Length > 0 && r3 % 3 == 0) e = p.Emoji;
                if (seg == "Fair Weather" && bad && r3 % 4 == 0) e = "\U0001F921";
                if (seg == "Casual" && r3 % 3 == 1) e += e;
                text = text.TrimEnd() + " " + e;
            }
            return text;
        }

        // one thing the club's fans actually remember or expect, added to some posts
        static string AddHook(string text, Ctx x)
        {
            var s = x.S; var seg = x.Seg; uint h = x.H;
            uint r = (h / 31) % 100;
            // SI's own verdict on a signing, in the fan's words
            if (s.Kind == "signing" && s.Subject.Length > 0 && (seg == "Core" || seg == "Fair Weather"))
                foreach (var kv in FanBook.Signings)
                    if (kv.Key.EndsWith(s.Subject, StringComparison.OrdinalIgnoreCase))
                    {
                        var v = kv.Value;
                        if (Regex.IsMatch(v, @"^(Not sure|Unsure|Disappointed|Unhappy)", RegexOptions.IgnoreCase)) return Pick(Lines.SignDoubt, x) ?? text;
                        if (Regex.IsMatch(v, @"^(Very pleased|Very happy|Delighted)", RegexOptions.IgnoreCase)) return Pick(Lines.SignLove, x) ?? text;
                        break;
                    }
            if (r < 30 && (seg == "Core" || seg == "Hardcore" || seg == "Fair Weather"))
            {
                bool sore = s.Kind == "defeat" || s.Kind == "draw" || s.Kind == "crisis" || s.Kind == "sale", glad = s.Kind == "win" || s.Kind == "signing" || s.Kind == "award";
                var mem = sore ? Memory(FanBook.Crits, false, h) : (glad ? Memory(FanBook.Highs, true, h) : null);
                if (mem != null) return Join(text, mem);
            }
            if (r >= 30 && r < 45 && seg == "Core")
            {
                var style = FanBook.Culture.Find(c => c.Type == "Playing style");
                if (style != null)
                {
                    var what = Regex.Replace(style.Name, @"^Play\s+", "", RegexOptions.IgnoreCase).ToLowerInvariant();
                    int m = FanBook.Mood(style.Reaction);
                    if (s.Kind == "win" && m >= 0) return Join(text, "That's the " + what + " we were promised.");
                    if (s.Kind == "defeat" && m < 0) return Join(text, "Where's the " + what + " we were promised?");
                }
            }
            if (r >= 45 && r < 58 && s.Kind == "win" && (seg == "Hardcore" || seg == "Fair Weather") && s.Rival.Length == 0)
            {
                var main = MainRival(); if (main.Length > 0) { var y = new Ctx { S = s, P = x.P, Seg = seg, H = h, Rival = main, Said = x.Said }; var d = Pick(Lines.Dig, y); if (d != null) return Join(text, d); }
            }
            if (r >= 58 && r < 70 && seg == "Family" && FanBook.Culture.Exists(c => c.Type == "Development"))
                return Join(text, "Would love to see one of the academy kids get a go, too.");
            if (r >= 70 && r < 85 && seg == "Core" && s.Negative && FanBook.Spokes.Length > 0 && (FanBook.Influence == "High" || FanBook.Influence == "Very High"))
                return Join(text, "Hope " + FanBook.Spokes + " raises this with the board. They do listen to us.");
            return text;
        }
        static string Join(string a, string b)
        {
            a = a.TrimEnd(); if (a.Length > 0 && ".!?\u2026".IndexOf(a[a.Length - 1]) < 0) a += ".";
            return a + " " + b;
        }
        // the rival the supporters care most about (Required before Preferred, then the first listed)
        static string MainRival()
        {
            string best = ""; int rank = 9;
            foreach (var c in FanBook.Culture)
            {
                if (c.Type != "Rivalry") continue;
                var n = Story.RivalName(c.Name); if (n.Length == 0) continue;
                int k = c.Importance == "Required" ? 0 : c.Importance == "Preferred" ? 1 : c.Importance == "Desired" ? 2 : 3;
                if (k < rank) { rank = k; best = n; }
            }
            return best;
        }
        // "Disappointed with the sale of Femi Seriki." -> "Still not over selling Femi Seriki, mind."
        static string Memory(List<string> items, bool good, uint h)
        {
            if (items.Count == 0) return null;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[(int)((h / 13 + (uint)i) % (uint)items.Count)];
                string x = null;
                Match m;
                if ((m = Regex.Match(it, @"sale of (.+?)\.?$")).Success) x = "selling " + m.Groups[1].Value;
                else if ((m = Regex.Match(it, @"the (?:\p{L}+ )?(\d+-\d+)(?: [\p{L} ]+?)? (defeat against|draw with|win against|win over|defeat to) (.+?)\.?$")).Success) x = "that " + m.Groups[1].Value + (m.Groups[2].Value.StartsWith("draw") ? " with " : " against ") + m.Groups[3].Value;
                else if ((m = Regex.Match(it, @"decision to bring (.+?) in on loan")).Success) x = "getting " + m.Groups[1].Value + " in on loan";
                else if ((m = Regex.Match(it, @"signing of (.+?)\.?$")).Success) x = "signing " + m.Groups[1].Value;
                if (x == null) continue;
                var pool = good ? (x.StartsWith("that ") ? Lines.Fond : Lines.FondSigning) : Lines.Grudge;
                return pool[(int)((h / 17) % (uint)pool.Length)].Replace("{x}", x);
            }
            return null;
        }

        static readonly string[] First = { "Jess", "Joe", "Kal", "Liam", "Megan", "Ryan", "Sophie", "Dan", "Chloe", "Tom", "Abbie", "Callum", "Priya", "Sam", "Jordan", "Alex",
            "Hannah", "Kyle", "Zara", "Lewis", "Nadia", "Ben", "Ellie", "Mo", "Rachel", "Gaz", "Katie", "Jamal", "Lucy", "Marc", "Bethany", "Owen", "Aisha", "Rob", "Tasha", "Sean",
            "Leanne", "Imran", "Gemma", "Chris", "Kerry", "Darren", "Amy", "Nathan", "Becky", "Kieran", "Sian", "Ade", "Jade", "Pete" };
        static readonly string[] Last = { "Tomlin", "Hallam", "Ashworth", "Brammer", "Crookes", "Dyson", "Ellis", "Fenwick", "Gill", "Hirst", "Ingle", "Jagger", "Kenworthy",
            "Lister", "Mellor", "Nuttall", "Oxley", "Platts", "Ramsden", "Staniforth", "Tingle", "Walsh", "Hussain", "Begum", "Okafor", "Mensah", "Clarke", "Shaw", "Booth",
            "Barker", "Whitehead", "Holmes", "Stones", "Wragg", "Marsden", "Patel", "Kaur", "Nowak", "Doyle", "Byrne", "Hartley", "Senior", "Turton", "Cawthorne", "Fox" };
        static string Name(string seed) { uint h = Hash(seed); return First[h % (uint)First.Length] + " " + Last[(h / 97) % (uint)Last.Length]; }
    }

    // The words. Many are real fans' posts with names swapped for tokens; gender-neutral throughout.
    // Tokens: {player} {manager} {nickname} {Nickname} {nickshort} {NICK} {fan} {short} {home} {end} {reyt} {rival} {rivalnick}.
    // A line is skipped when the story or the club's fan words can't fill it.
    internal static class Lines
    {
        internal static readonly string[] Grudge = { "Still not over {x}, mind.", "Not forgotten {x} either.", "And no, I haven't forgiven {x}." };
        internal static readonly string[] Fond = { "More like {x}, please.", "Still buzzing about {x}.", "Remember {x}? That's the standard." };
        internal static readonly string[] FondSigning = { "Still buzzing about {x}.", "Credit where it's due for {x}." };
        internal static readonly string[] SignDoubt = { "Hoping to be proved wrong, but not that excited about this one", "Not convinced {player} is what we needed. Prove me wrong.", "Bit of a gamble, {player}. Hope I'm wrong." };
        internal static readonly string[] SignLove = { "Exactly the kind of signing we needed. Very happy with this.", "This is an absolute statement of intent.", "Proper signing. {player} will make us better." };
        internal static readonly string[] Dig = { "{rivalnick} will be fuming.", "Meanwhile, {rivalnick}...", "Bet {rivalnick} loved that." };
        internal static readonly string[] Gloat = { "Couldn't happen to a nicer club \U0001F602", "{rivalnick} in meltdown again. Pass the popcorn \U0001F37F", "Love to see it \U0001F602",
            "Sounds about right for {rivalnick}", "Mind the gap \U0001F602", "Not laughing. Ok I'm laughing \U0001F602", "Never change, {rivalnick}. Never change." };

        internal static readonly Dictionary<string, string[]> Rival = new Dictionary<string, string[]>
        {
            ["Hardcore/win"] = new[] { "Bragging rights. Get in!!", "Nothing better than beating {rivalnick}. Nothing.", "That one's for everyone who works with {rivalnick} fans. Enjoy Monday.", "Mind the gap." },
            ["Fair Weather/win"] = new[] { "Beat {rivalnick}. Season made.", "Mind the gap \U0001F602" },
            ["Core/win"] = new[] { "Rivalry matters here. Get it right against {rival} and the rest takes care of itself.", "Proper derby performance. Enjoy this one." },
            ["Hardcore/defeat"] = new[] { "Losing to {rivalnick}. Worst feeling in football.", "Can't go to work tomorrow. Thanks for that." },
            ["Fair Weather/defeat"] = new[] { "Losing to {rivalnick}. I'm done.", "Beaten by {rivalnick}. Embarrassing." },
            ["Core/defeat"] = new[] { "Of all the games to lose. Not good enough against {rival}." },
            ["Hardcore/draw"] = new[] { "A point against {rivalnick}. Should've had them." },
            ["Core/draw"] = new[] { "Derbies are never pretty. Point's fine, but we were the better side." },
            ["Hardcore/preview"] = new[] { "Anything against {rivalnick}, I don't care how. We win. Simple.", "Nothing matters more than beating {rivalnick}. Nothing." },
            ["Fair Weather/preview"] = new[] { "Lose to {rivalnick} and I'm out." },
            ["Core/preview"] = new[] { "Whatever else happens this season, get this one right against {rival}." },
            ["Hardcore/other"] = new[] { "Anything involving {rivalnick}, not interested \U0001F602" },
            ["Core/other"] = new[] { "Twenty-odd years in the same seat and {rival} still matter more than anyone." },
        };

        internal static readonly Dictionary<string, Dictionary<string, string[]>> By = new Dictionary<string, Dictionary<string, string[]>>
        {
            ["Hardcore"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "GET IN!!", "Never in doubt.", "Think we deserved that!! Get in {nickshort}, {reyt} 3 points that", "That's more like it. Loud from the first whistle and the team fed off it.",
                                  "Away end bouncing. This is why I never miss one.", "Absolute belter of a Saturday!", "Proper performance, proper result. Same again next week." },
                ["defeat"] = new[] { "Not good enough. Not even close.", "Paid for the ticket, paid for the coach, and got that. Embarrassing.", "We sing for ninety minutes and get nothing back.",
                                     "Lose, fine. Lose without a fight? Not at this club.", "Bullied all over the pitch. Again.", "Got bullied in the middle of the park. We were a body light." },
                ["draw"] = new[] { "Should've buried them. Too nice, too often.", "Point's a point. Want more fight though.", "Not good enough at home. Simple as." },
                ["signing"] = new[] { "Run through a wall for the shirt and you'll be a hero here, {player}.", "Welcome to {nickname} {player}. Graft first, everything else second.",
                                      "Can't wait to see {player} in front of {end}.", "Give everything and we'll sing your name, {player}." },
                ["loan"] = new[] { "Loan or not, give everything for the shirt and we'll sing your name, {player}." },
                ["sale"] = new[] { "Sold to the highest bidder. Again.", "Keep {player}. Sort it out. Get promoted.", "Poor decision.", "Players come and go. The badge stays. Still feels wrong." },
                ["unrest"] = new[] { "Nobody's bigger than this club. Play or go.", "Want out? Door's there.", "Moaning in the papers. Not having that." },
                ["injury"] = new[] { "Gutted for {player}. Come back stronger.", "Next one up. Go again.", "Massive blow for {nickname} and for a player who was just finding best form!" },
                ["award"] = new[] { "Deserved. {player} plays like one of us.", "Plays like they've stood on {end} their whole life, {player}." },
                ["crisis"] = new[] { "Stick together. That's what this club does.", "I'll back the manager as long as the effort's there. Right now it isn't.", "Get behind them. Moaning never won anything." },
                ["storm"] = new[] { "Good. Someone sticking up for the club for once.", "Bit of fire. About time." },
                ["preview"] = new[] { "Big one. Loud from the first whistle.", "Coach booked, voice ready. Come on!", "CMON {NICK}" },
                ["other"] = new[] { "Home, away, wherever. Same as always.", "Doesn't matter what's going on. We'll be there." },
            },
            ["Core"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "Big 3 points. Made hard work of it, but for what mattered.", "Need to win those sorts of tricky games at this stage, because they can be the difference come the end of the season.",
                                  "Thoroughly deserved. Best I've seen us play in a while.", "Season ticket's paying for itself this year.",
                                  "Big 3 points when we didn't deserve it . Got away with one at the end . We need to learn game management", "Work in progress, but you can see what the manager's building." },
                ["defeat"] = new[] { "Reading this you'd think we were meant to walk this league. {short} don't do easy, never have and never will.", "One bad day doesn't make a bad season. Game management needs sorting though.",
                                     "Too lightweight in the middle of the park. Same problem as last time.", "Ref cost us but we have ourselves to blame.", "Fundamentally this is much more than bad luck.",
                                     "Imagine if we made a substitution before conceding. What a day that would be", "Didn't think that was too bad a performance in all honesty." },
                ["draw"] = new[] { "Don't understand the negativity. Point's a point.", "Need to sort out this system. Too predictable.", "Not every week's a thrashing. Keep ticking over.", "Fair result. Need to start turning these into wins though." },
                ["signing"] = new[] { "Very happy with this.", "Very happy with this! Welcome to {nickname} {player}", "Nice to see we're making sensible signings again.", "Good signing all round this.",
                                      "Very solid at this level. Deal that makes sense all round imo.", "Time prove the doubters wrong {player}", "Sensible. Fills a gap we've needed filling for a while." },
                ["loan"] = new[] { "Loan deal, hardly building for the future is it?", "Sensible loan. Fills a gap, no big risk.", "Welcome {player}. Make it hard for us to send you back." },
                ["sale"] = new[] { "Poor decision. Hope there's a plan to replace {player}.", "I'm not usually a panic stations {fan} but I really don't like selling players mid-season",
                                   "Think it's time the board started answering q's about where the money's going.", "{player}'s under contract. If they really don't want to go they don't have to.",
                                   "Depends what we do with the money. Not panicking yet.", "Annoyingly low fee for a player in their prime. Wish {player} all the best." },
                ["unrest"] = new[] { "Keep it in-house. This needs sorting quietly, not in the papers.", "Club before individuals. Always has been.", "Pay them or play them. Simple." },
                ["injury"] = new[] { "One step forward a million steps back it feels with injuries this season.", "Feels like there's a blow before every single game at the moment. Fingers crossed.",
                                     "{player} just getting back into the swing of it as well...", "Squad depth about to get tested. That's what it's there for." },
                ["award"] = new[] { "Well earned. Good to see {player} getting the credit.", "Consistent all season. Fully deserved.", "{player} is an absolute colossus btw" },
                ["crisis"] = new[] { "Nothing is going to change if you sack the manager now.", "Dealt a bad hand. When the dust settles they'll rightfully get credit for the job they've done.",
                                     "Don't understand the panic. Look at where we were a year ago.", "Long season. Bit of perspective please.", "In {manager} we trust, plain and simple" },
                ["storm"] = new[] { "Not ideal in front of the cameras, but I get the frustration.", "Let the football do the talking." },
                ["preview"] = new[] { "Should win this on current form. Never easy in this league though.", "Big week. Want us on the front foot from the start.", "Tricky one. Need to be patient." },
                ["other"] = new[] { "Steady as we go.", "Plenty to be positive about at the moment." },
            },
            ["Fair Weather"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "Best team in the league. Always said so.", "Back on the bandwagon and loving it.", "This manager is a genius. New contract, now.", "BEST. SEASON. EVER.", "Buying tickets for the next one. Don't let me down." },
                ["defeat"] = new[] { "Can season just end now?", "This is why I don't buy a season ticket.", "Knew it was too good to last.", "Embarrassing. Shambolic. Woeful. Take your pick.",
                                     "I expected nothing and we got nothing.", "Could probably just retweet one of several tweets from this season. Huffed and puffed again.", "Big clear out needed in summer. So much dead wood" },
                ["draw"] = new[] { "Draw. Wake me up when we're winning again.", "Boring. Next." },
                ["signing"] = new[] { "Never heard of {player} but if they score I'm in.", "Who? Fine, go on then.", "Statement of intent, this. We're going up.", "Now announce a striker and it's a perfect window" },
                ["loan"] = new[] { "Another loan. Wow. Ambition." },
                ["sale"] = new[] { "Sell everyone, why not.", "Could be a long season...", "I hate transfer windows, I've decided." },
                ["unrest"] = new[] { "More drama. Sort it out and win some games.", "Pay them or sell them. Boring." },
                ["injury"] = new[] { "Of course. Of course they're injured.", "The pain never ends", "This keeps getting worse and worse" },
                ["award"] = new[] { "Knew {player} was class from day one. Honestly.", "Saw the highlights. {player} looks decent!" },
                ["crisis"] = new[] { "Not surprised.", "Club's a mess.", "Damage is done.", "Results fix this. Or they don't." },
                ["storm"] = new[] { "Ha. Best press conference of the season.", "Entertaining, at least." },
                ["preview"] = new[] { "Win this and we're going up. Lose it and I'm out.", "If we win this I'll come to the next one. Promise." },
                ["other"] = new[] { "Only here for the wins, if I'm honest." },
            },
            ["Casual"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "absolute scenes", "only saw the highlights but what a goal", "we're actually good this year??", "Didn't see it but sounds like a belter" },
                ["defeat"] = new[] { "missed it. sounds like i didn't miss much", "checked the score at half time and regretted it", "Don't know why so many people are annoyed. Played well to be honest. Individual errors once again" },
                ["draw"] = new[] { "draw? fair enough", "could be worse i suppose" },
                ["signing"] = new[] { "looks a decent signing tbf", "Good signing! Certainly be more up for it than some we've had" },
                ["loan"] = new[] { "loan? fair enough, decent cover" },
                ["sale"] = new[] { "wait, {player}'s gone? when did that happen", "shame, liked {player}" },
                ["unrest"] = new[] { "bit of drama never hurt anyone", "grabbing the popcorn" },
                ["injury"] = new[] { "unlucky {player}", "that's a shame. get well soon {player}" },
                ["award"] = new[] { "{player} to the Prem soon surely", "{player} is class tbf" },
                ["crisis"] = new[] { "is it bad? it sounds bad" },
                ["storm"] = new[] { "the clip of that press conference is everywhere lol" },
                ["preview"] = new[] { "who are we playing again? win pls", "might actually watch this one" },
                ["other"] = new[] { "following from afar. keep it up", "decent times to be a fan tbf" },
            },
            ["Family"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "Kids sang all the way home. Days like this are why we come.", "Youngest is already asking for a new shirt." },
                ["defeat"] = new[] { "Long drive home with two very quiet kids. Next time.", "Tough one to explain at bedtime." },
                ["draw"] = new[] { "Decent day out even without the win." },
                ["signing"] = new[] { "The kids will want {player} on the back of their shirts by Saturday.", "Hope {player} does the meet and greet at the open day." },
                ["loan"] = new[] { "Welcome {player}! The kids are excited already." },
                ["sale"] = new[] { "Now explaining to my youngest why the name on their shirt has gone." },
                ["unrest"] = new[] { "Kids look up to these players. Sort it out like grown-ups." },
                ["injury"] = new[] { "Get well soon {player}. The little ones will miss you." },
                ["award"] = new[] { "Great role model for the kids, {player}." },
                ["crisis"] = new[] { "Whatever happens we'll still be there on Saturday. Family tradition." },
                ["storm"] = new[] { "Bit much in front of the cameras." },
                ["preview"] = new[] { "Tickets sorted for the whole family. Please be a good one!" },
                ["other"] = new[] { "Matchday's still the best day of the week in our house." },
            },
            ["Corporate"] = new Dictionary<string, string[]>
            {
                ["win"] = new[] { "Superb result. The atmosphere in the lounge was electric.", "Results like this are good for the whole club, on and off the pitch." },
                ["defeat"] = new[] { "Not the result our guests were hoping for. Still well placed." },
                ["draw"] = new[] { "Steady. Table position is what counts." },
                ["signing"] = new[] { "Sensible business, if the numbers stack up.", "Good to see the club investing." },
                ["loan"] = new[] { "Low-risk business. Sensible." },
                ["sale"] = new[] { "Good business from a financial point of view, if it's reinvested." },
                ["unrest"] = new[] { "Public disputes don't help the club's image." },
                ["injury"] = new[] { "Frustrating. Hopefully managed carefully." },
                ["award"] = new[] { "Great exposure for the club." },
                ["crisis"] = new[] { "Stability matters. Knee-jerk decisions cost money." },
                ["storm"] = new[] { "Not the headlines the commercial team will want." },
                ["preview"] = new[] { "Strong crowd expected. Good for everyone." },
                ["other"] = new[] { "Encouraging times on and off the pitch." },
            },
        };
    }
}
