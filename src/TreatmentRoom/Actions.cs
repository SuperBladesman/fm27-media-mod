// FM27 Media Mod - Treatment Room: medical actions the manager takes (read-only: we only watch them).
// Two channels (Probe 0.0.10, 22 Nov): a person's menu sends EventFmxDoAction with the ActionID in field 'acid' of the
// target; a screen's own Actions menu (Risk Assessment) sends it through the conversation event's ActionId.
// News responses (the inbox's buttons) are recorded when a treatment offer was on screen shortly before.
using System;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal static class Actions
    {
        static readonly Regex Acid = new Regex(@"1633904996:(\d+)", RegexOptions.Compiled);   // 'acid'
        static readonly string[] Medical = { "Injury", "Physio", "Rest", "Intensity", "Medical", "TrainingReport", "Fitness" };

        static string Name(long id) { try { return ((FM.GamePlugin.ActionID)(int)id).ToString(); } catch { return id.ToString(); } }
        static bool IsMedical(string n) { foreach (var k in Medical) if (n.IndexOf(k, StringComparison.Ordinal) >= 0) return true; return false; }

        // who the action was probably for: the profile open in the last 3 minutes, else the last person on a medical table
        static string Who()
        {
            if (Screens.OwnerId.Length > 0 && (DateTime.Now - Screens.OwnerAt).TotalMinutes < 3) return Screens.OwnerName + " [" + Screens.OwnerId + "] (profile)";
            if (Screens.LastPerson.Length > 0 && (DateTime.Now - Screens.LastPersonAt).TotalMinutes < 3) return Screens.LastPerson + " (last on a medical screen)";
            return "";
        }

        internal static void OnEvent(int ev, string target, string value)
        {
            if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxDoAction)
            {
                var m = Acid.Match(target ?? "");
                if (!m.Success) return;   // a screen's Actions menu: OnSpeakTo has the id
                if (!long.TryParse(m.Groups[1].Value, out var id)) return;
                var n = Name(id);
                TreatmentRoom.W("action", n + " (" + id + ")", target + " | " + value);
                if (IsMedical(n)) Ledger.Action("menu", n, id.ToString(), Who(), target);
            }
            else if (ev == (int)FM.GamePlugin.InteropEventIDs.EventFmxNewsResponseAction)
            {
                TreatmentRoom.W("news-response", target, value);
                var offer = Ledger.RecentOffer(10);
                if (offer.Length > 0) { Ledger.AnsweredAt = DateTime.Now; Ledger.Action("news", "NewsResponse after a treatment offer", "", Who(), offer + " | " + target + " | " + value); }
            }
        }

        internal static void OnSpeakTo(FM.UI.SpeakToActionEvent e)
        {
            if (e == null) return;
            long id; try { id = Convert.ToInt64(e.ActionId); } catch { return; }
            if (id <= 0) return;
            string ev = ""; try { ev = ((FM.GamePlugin.InteropEventIDs)(int)e.Event).ToString(); } catch { }
            var n = Name(id);
            if (!IsMedical(n)) return;
            TreatmentRoom.W("speakto action", n + " (" + id + ")", ev);
            Ledger.Action("screen", n, id.ToString(), Who(), ev);
        }
    }
}
