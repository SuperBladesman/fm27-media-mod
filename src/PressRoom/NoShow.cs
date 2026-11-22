// FM27 Media Mod - Press Room 0.13.3: sending the assistant. MIT licence (see LICENSE).
// SI lets the manager send the assistant to a press conference from its invite (21 Nov 2026: SI's reply on the invite,
// EventFmxNewsResponseAction, response 1; attending fires EventFmxStartPressConference instead). The mod remembers it:
// one in six weeks is nothing, two and the room notices, three and it becomes the story. A journalist who was stood up
// says so at the next press conference the manager attends. Kept in media office.tsv, so it is per save.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class NoShow
    {
        internal class N { public int Days, Conf; public string When = "", How = "assistant", Outlets = "", Names = "", Watch = ""; }
        internal static readonly List<N> All = new List<N>();
        internal const int WindowDays = 42;                       // six weeks

        // owed at the next press conference attended: a remark from someone who was stood up
        internal static bool Owed; internal static int OwedCount; internal static string OwedNames = "";

        // the invite on screen, read by the press officer's briefing
        static VisualElement Invite; static DateTime InviteAt = DateTime.MinValue, LastSentAt = DateTime.MinValue;
        static DateTime StartedAt = DateTime.MinValue;            // the last press conference you went to (not tunnel interviews)
        static List<string> InviteOutlets = new List<string>(), InviteNames = new List<string>(), InviteWatch = new List<string>();

        internal static void Reset() { All.Clear(); Owed = false; OwedCount = 0; OwedNames = ""; Invite = null; }

        internal static void LoadLine(string[] p)
        {
            if (p[0] == "noshow" && p.Length >= 8)
                All.Add(new N { Days = int.TryParse(p[1], out var d) ? d : 0, Conf = int.TryParse(p[2], out var c) ? c : 0, When = p[3], How = p[4], Outlets = p[5], Names = p[6], Watch = p[7] });
            else if (p[0] == "noshowowed" && p.Length >= 3) { Owed = true; OwedCount = int.TryParse(p[1], out var n) ? n : 1; OwedNames = p[2]; }
        }
        internal static void SaveLines(StringBuilder sb, Func<string, string> clean)
        {
            foreach (var n in All) sb.Append("noshow\t").Append(n.Days).Append('\t').Append(n.Conf).Append('\t').Append(clean(n.When)).Append('\t').Append(clean(n.How)).Append('\t')
                .Append(clean(n.Outlets)).Append('\t').Append(clean(n.Names)).Append('\t').Append(clean(n.Watch)).Append('\n');
            if (Owed) sb.Append("noshowowed\t").Append(OwedCount).Append('\t').Append(clean(OwedNames)).Append('\n');
        }

        // how many in the last six weeks (the clock is the game's own day count, which never jumps)
        internal static int Recent()
        {
            int n = 0; foreach (var x in All) if (Desk.Days - x.Days <= WindowDays) n++;
            return n;
        }
        static string Ordinal(int n) => n == 1 ? "once" : n == 2 ? "twice" : n + " times";

        internal static void SawInvite(VisualElement block, List<string> outlets, List<string> names, List<string> watch)
        {
            VisualElement inv = null; var e = block;
            for (int i = 0; i < 10 && e != null; i++, e = e.parent) if (e.name == "PressConferenceInvite") { inv = e; break; }
            if (inv == null) return;
            Invite = inv; InviteAt = DateTime.Now;
            InviteOutlets = new List<string>(outlets); InviteNames = new List<string>(names); InviteWatch = new List<string>(watch);
        }
        static bool InviteOnScreen()
        {
            try { return Invite != null && Invite.panel != null && (DateTime.Now - InviteAt).TotalMinutes < 30; } catch { return false; }
        }

        // SI's own handler for the buttons on a news item (the invite's "send assistant" is one of them)
        internal static void OnSpeakTo(FM.UI.SpeakToActionEvent e)
        {
            try
            {
                int ev = (int)e.Event;
                if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxStartPressConference) { Invite = null; StartedAt = DateTime.Now; return; }
                if (ev != (int)FM.GamePlugin.InteropEventIDs.EventFmxNewsResponseAction) return;
                if (!InviteOnScreen()) return;
                string npc = ""; try { npc = e.NPC == null ? "" : e.NPC.GetPersistentIdentity(); } catch { }
                int response = Convert.ToInt32(e.ResponseId);
                PressRoom.W("noshow", "reply on the invite", "response " + response + " npc " + npc);
                Sent(response);
            }
            catch (Exception x) { PressRoom.Fail("no-show", x); }
        }

        static string OutletOf(string name, int i)
        {
            var j = Pack.On ? Pack.Get(name) : null;
            if (j != null && !string.IsNullOrEmpty(j.Outlet)) return j.Outlet;
            return InviteNames.Count == InviteOutlets.Count && i < InviteOutlets.Count ? InviteOutlets[i] : "";
        }
        static void Hit(string who, int d, Dictionary<string, int> tally)
        {
            if (string.IsNullOrEmpty(who) || d == 0) return;
            tally[who] = (tally.TryGetValue(who, out var t) ? t : 0) + d;
        }

        static void Sent(int response)
        {
            if ((DateTime.Now - LastSentAt).TotalSeconds < 20) return;   // one click, one no-show
            LastSentAt = DateTime.Now;
            var names = new List<string>(InviteNames);
            var n = new N { Days = Desk.Days, Conf = PressRoom.Record.Conf, When = Desk.DateKnown ? Desk.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "day " + Desk.Days,
                How = response == 1 ? "assistant" : "skipped " + response, Outlets = string.Join("|", InviteOutlets), Names = string.Join("|", names), Watch = string.Join("|", InviteWatch) };
            // who was stood up before, inside the window
            var before = new HashSet<string>();
            foreach (var x in All) if (Desk.Days - x.Days <= WindowDays) foreach (var s in x.Names.Split('|')) if (s.Length > 0) before.Add(s);
            All.Add(n);
            Invite = null;
            int count = Recent();
            // the head of media's way: an open door takes it worse, a gatekeeper shrugs one more off
            int level = count + (MediaOffice.Mode == MediaOffice.Approach.OpenDoor ? 1 : MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? -1 : 0);
            var watch = new HashSet<string>(InviteWatch);
            var tally = new Dictionary<string, int>();
            for (int i = 0; i < names.Count; i++)
            {
                var who = names[i]; var outlet = OutletOf(who, i).Trim();
                bool hunter = watch.Contains(outlet), favoured = MediaOffice.IsFavoured(outlet);
                if (level >= 3) Hit(who, hunter ? -2 : -1, tally);
                else if (level == 2 && (hunter || favoured || before.Contains(who))) Hit(who, -1, tally);
            }
            foreach (var kv in tally)
            {
                if (Pack.On) Pack.Add(kv.Key, kv.Value, "noshow");
                else MediaOffice.Adjust[kv.Key] = (MediaOffice.Adjust.TryGetValue(kv.Key, out var a) ? a : 0) + kv.Value;
            }
            Owed = true; OwedCount = count; OwedNames = n.Names;
            string hurt = tally.Count == 0 ? "" : " (" + tally.Count + (tally.Count == 1 ? " journalist" : " journalists") + " put out)";
            string news = count == 1 ? "Your assistant took the press conference." : count == 2 ? "Your assistant took it again: twice in six weeks." : "That's " + count + " press conferences in six weeks without you.";
            news += level <= 1 ? (count == 1 ? " One missed is nothing; the press officer has made a note." : " Your head of media shrugs it off, for now.")
                : level == 2 ? (count == 1 ? " Your head of media isn't pleased, and the story-chasers noticed" + hurt + "." : " The room has noticed" + hurt + ".")
                : " It's becoming the story, and the whole room is put out.";
            MediaOffice.LastNews = news;
            MediaOffice.Save();
            PressRoom.W("noshow", n.How + ", " + count + " in six weeks, level " + level, names.Count + " in the room; " + tally.Count + " put out" + (watch.Count > 0 ? "; watch " + string.Join(", ", watch) : ""));
        }

        // ---------- the next press conference you attend ----------
        static readonly string[] Once = { "Good to have you back. We had your assistant last time.", "Nice to see you in the chair again.", "We missed you last time, so let's make up for it." };
        static readonly string[] Twice = { "We've had your assistant twice in a few weeks, so it's good to see you.", "We were starting to think you'd gone off us.", "Twice we've had your assistant lately. Glad you could make it." };
        static readonly string[] Often = { "We've seen more of your assistant than of you lately.", "Glad you could fit us in this time.", "Your assistant's had plenty of practice in that chair lately." };

        internal static string Talk(string journalist, uint h)
        {
            if (!Owed) return null;
            if ((DateTime.Now - StartedAt).TotalMinutes > 45) return null;   // a press conference you chose to attend, not a tunnel interview
            bool wasThere = ("|" + OwedNames + "|").Contains("|" + journalist + "|");
            if (!wasThere && Word.QNo < 3) return null;          // wait for someone who was stood up, but not past the third question
            Owed = false; MediaOffice.Save();
            var pool = OwedCount >= 3 ? Often : OwedCount == 2 ? Twice : Once;
            var line = pool[h % (uint)pool.Length];
            PressRoom.W("noshow", "remembered by " + journalist, line);
            return line;
        }

        // ---------- the office ----------
        internal static string Summary()
        {
            int c = Recent(); if (c == 0) return null;
            string num = c == 1 ? "one" : c == 2 ? "two" : c.ToString(CultureInfo.InvariantCulture);
            return "Your assistant has taken " + num + " of your press conferences in the last six weeks. "
                + (c == 1 ? "One is nothing." : c == 2 ? "Two and the room notices: the story-chasers and anyone stood up twice are put out." : "Three or more and it becomes the story: the whole room is put out.")
                + (MediaOffice.Mode == MediaOffice.Approach.OpenDoor ? " Your head of media keeps an open door and takes absences harder." : MediaOffice.Mode == MediaOffice.Approach.Gatekeeper ? " Your head of media guards your time and lets one more go." : "");
        }

        internal static void AddCuttings(List<Desk.Cutting> list, Func<string[], bool> match)
        {
            foreach (var n in All)
            {
                if (!match(new[] { n.Outlets, n.Names, "assistant" })) continue;
                int people = n.Names.Length == 0 ? 0 : n.Names.Split('|').Length;
                list.Add(new Desk.Cutting { Conf = n.Conf, When = n.When, Kind = n.How == "assistant" ? "Sent the assistant" : "Skipped", Who = "",
                    Text = "Your assistant took the press conference" + (people > 0 ? " (" + people + " journalists in the room)" : "") + "." });
            }
        }
    }
}
