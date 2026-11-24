// FM27 Media Mod - Treatment Room 0.5.6: what the Treatment Room tells its popout (Core 0.2.0's conventions).
// 0.5.8: a note as soon as SI's medical message offers a treatment choice (physios, pain management, a specialist,
// protective equipment): the player's record and minutes, and the department's view, from what the ledger holds.
// Children of the MEDICAL tab, all display:none (so nothing changes when Core is not installed):
//   FM27MM-Status  "N injured · N at high risk|Latest: <player> (<injury>)"
//   FM27MM-Badge   injuries that arrived since the Treatment Room was last opened (this session)
//   FM27MM-Nudge   "id|text": a new injury, or a risk assessment with someone at very high risk (once each)
//   FM27MM-Sub:<section>  one button per section of the page, opening it there
// Words: players by name only, no pronouns; numbers and wording from the ledger (SI's own screens).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Popout
    {
        static Label Status, Badge, Nudge;
        static VisualElement On;
        static HashSet<string> SeenAtOpen;        // forecast keys known when the page was last opened
        static HashSet<string> Known;             // forecast keys already nudged about (or there at the start)
        static int KnownRiskDay = int.MinValue;
        static HashSet<string> KnownInj;          // injury keys on record already (no note for them)
        static string KeyOf(Ledger.Inj i) => i.Who + "|" + i.Date + "|" + (i.Injury ?? "").ToLowerInvariant();
        static DateTime Next = DateTime.MinValue;
        static int Day(string d) => GameDate.Day(d);

        internal static void Attach(VisualElement tab, (string Key, string Label)[] views)
        {
            try
            {
                On = tab;
                Status = Hidden(new Label(""), "FM27MM-Status"); tab.Add(Status);
                Badge = Hidden(new Label(""), "FM27MM-Badge"); tab.Add(Badge);
                Nudge = Hidden(new Label(""), "FM27MM-Nudge"); tab.Add(Nudge);
                foreach (var v in views)
                {
                    var key = v.Key; var b = new Button(); b.text = v.Label;
                    System.Action open = () => { try { Room.Open(key, tab); Seen(); } catch (Exception e) { TreatmentRoom.Fail("popout open " + key, e); } };
                    b.clickable.add_clicked(open);
                    tab.Add(Hidden(b, "FM27MM-Sub:" + v.Label));
                }
                Next = DateTime.MinValue; Update();
                TreatmentRoom.W("popout", "attached", views.Length + " sections");
            }
            catch (Exception e) { TreatmentRoom.Fail("popout attach", e); }
        }
        static T Hidden<T>(T e, string name) where T : VisualElement { e.name = name; e.style.display = DisplayStyle.None; e.pickingMode = PickingMode.Ignore; return e; }

        static List<Ledger.Fc> OutNow(int now)
        {
            var squad = new HashSet<string>(Ledger.Squad.Keys);
            return Ledger.Forecasts.Values.Where(f => f.Ours != "n" && f.Returned.Length == 0 && (squad.Count == 0 || squad.Contains(f.Who))
                && Day(f.Date) >= 0 && (now < 0 || now - Day(f.Date) <= 120) && !Summary.Personal(f.Injury))
                .OrderByDescending(f => Day(f.Date)).ToList();
        }
        static string K(Ledger.Fc f) => f.Who + "|" + f.Date + "|" + (f.Injury ?? "").ToLowerInvariant();
        static string Bare(string s) => PersonLinks.Plain(s ?? "").Trim();

        // the page was opened: the pill starts again from here
        internal static void Seen()
        {
            try { SeenAtOpen = new HashSet<string>(Ledger.Forecasts.Values.Select(K)); if (Badge != null) Badge.text = ""; } catch { }
        }

        static DateTime HeldUntil = DateTime.MinValue, WaitAt = DateTime.MinValue; static string WaitName = "", WaitOptions = "", WaitDate = "";
        // 0.6.0: the Guidance page's "decision waiting" (the same as the popout's second line)
        internal static string Waiting() => WaitName.Length > 0 ? WaitName + (WaitOptions.Length > 0 ? " (" + WaitOptions + ")" : "") : "";
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s ?? "") { h ^= c; h *= 16777619; } return h; }
        internal static void Update()
        {
            if (On == null || Status == null || DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(3);
            try
            {
                int now = Day(GameDate.Text);
                var outNow = OutNow(now);
                if (SeenAtOpen == null) SeenAtOpen = new HashSet<string>(Ledger.Forecasts.Values.Select(K));
                if (Known == null) { Known = new HashSet<string>(Ledger.Forecasts.Values.Select(K)); KnownRiskDay = Files.LatestRiskDay(); }
                // the status line
                int last = Files.LatestRiskDay();
                var latest = last < 0 ? new List<Ledger.Rk>() : Ledger.Risk.Values.Where(k => Day(k.Date) == last).ToList();
                int high = latest.Count(k => k.Risk.Equals("High", StringComparison.OrdinalIgnoreCase) || k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase));
                var record = Ledger.Injuries.Values.Where(i => i.Ours != "n" && Day(i.Date) >= 0 && !Summary.Personal(i.Injury)).OrderByDescending(i => Day(i.Date)).ToList();
                string first, second;
                if (outNow.Count > 0) { first = outNow.Count + " injured"; second = "Latest: " + Files.Name(outNow[0].Who) + " (" + Bare(outNow[0].Injury) + ")"; }
                else
                {
                    first = "No current injuries on file";
                    second = record.Count > 0 ? "Last on record: " + Files.Name(record[0].Who) + ", " + Bare(record[0].Injury).ToLowerInvariant() + " (" + record[0].Date + ")" : "";
                }
                if (last >= 0) first += " · " + high + " at high risk";
                else if (second.Length == 0) second = "No risk assessment read yet";
                Status.text = first + "|" + second;
                // the pill: injuries since the page was last opened
                int fresh = outNow.Count(f => !SeenAtOpen.Contains(K(f)));
                Badge.text = fresh > 0 ? fresh.ToString() : "";
                // 0.5.8: SI's treatment choice, as the message arrives (first: it is the moment a decision is waiting)
                if (Ledger.NewOffers.Count > 0)
                {
                    (string Who, string Plain) o = ("", "");
                    while (Ledger.NewOffers.Count > 0) o = Ledger.NewOffers.Dequeue();
                    var note = OfferNotes.Make(o.Who, o.Plain, now);
                    if (note.Length > 0)
                    {
                        Nudge.text = "offer:" + Hash(o.Plain) + "|" + note; HeldUntil = DateTime.Now.AddMinutes(3);
                        WaitName = OfferNotes.Who(o.Who).Name; WaitOptions = OfferNotes.Options(o.Plain); WaitDate = GameDate.Text; WaitAt = DateTime.Now;
                        TreatmentRoom.W("popout", "offer note", note);
                    }
                }
                // the decision stays on the status line for the rest of that game day, or until a response is seen
                if (WaitName.Length > 0 && (GameDate.Text != WaitDate || Ledger.AnsweredAt > WaitAt)) WaitName = "";
                if (WaitName.Length > 0) Status.text = first + "|Decision waiting: " + WaitName + (WaitOptions.Length > 0 ? " (" + WaitOptions + ")" : "");
                // a note just put out is held for 3 minutes, so a note about the same injury can't replace it before it is shown
                if (DateTime.Now < HeldUntil) return;
                // a note: a new injury (a forecast, or a recent injury on record), else a new assessment with very high risk
                var newInj = outNow.Where(f => !Known.Contains(K(f))).ToList();
                if (KnownInj == null) KnownInj = new HashSet<string>(Ledger.Injuries.Keys);
                var newRec = record.Where(i => !KnownInj.Contains(KeyOf(i)) && (now < 0 || now - Day(i.Date) <= 7)).ToList();
                foreach (var i in Ledger.Injuries) KnownInj.Add(i.Key);
                if (newInj.Count > 0)
                {
                    var f = newInj[0];
                    foreach (var x in newInj) Known.Add(K(x));
                    var exp = Bare(f.Expected);
                    // the note's id has no "|" (Core splits id and text at the first one)
                    Nudge.text = "inj:" + K(f).Replace('|', ':') + "|" + Files.Name(f.Who) + " is injured: " + Bare(f.Injury).ToLowerInvariant() + (exp.Length > 0 ? ". Expected back: " + exp : "") + ". The Treatment Room has the forecast and the file.";
                }
                else if (newRec.Count > 0)
                {
                    var i = newRec[0];
                    var outFor = Bare(i.OutFor);
                    Nudge.text = "rec:" + KeyOf(i).Replace('|', ':') + "|" + Files.Name(i.Who) + ": " + Bare(i.Injury).ToLowerInvariant() + (outFor.Length > 0 ? ", out for " + outFor : "") + " (" + i.Date + "). It's on file in the Treatment Room.";
                }
                else if (last >= 0 && last != KnownRiskDay)
                {
                    KnownRiskDay = last;
                    var vh = latest.Where(k => k.Risk.Equals("Very High", StringComparison.OrdinalIgnoreCase)).Select(k => k.Person).Where(p => p.Length > 0).OrderBy(p => p).ToList();
                    if (vh.Count > 0) Nudge.text = "risk:" + last + "|" + "New risk assessment: " + string.Join(", ", vh) + (vh.Count == 1 ? " is" : " are") + " at very high risk of injury.";
                }
            }
            catch (Exception e) { TreatmentRoom.Fail("popout update", e); }
        }
    }
}
