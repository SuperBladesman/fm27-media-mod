// FM27 Media Mod - Partnerships 0.2.2: what Partnerships tells its popout (Core 0.2.0's conventions).
// Children of the PAIRS tab, all display:none (nothing changes when Core is not installed):
//   FM27MM-Status  "N partnerships on file|Strongest: A and B (word)"
//   FM27MM-Badge   stories that arrived since the page was last opened (this session)
//   FM27MM-Nudge   "id|text": the top new story (split, breaking down, back together, telepathic, clicking, newly recognised)
// Names are SI's pitch names; no pronouns.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class PtPopout
    {
        static Label Status, Badge, Nudge;
        static VisualElement On;
        static HashSet<string> SeenAtOpen, Known;
        static DateTime Next = DateTime.MinValue;

        internal static void Attach(VisualElement tab)
        {
            try
            {
                On = tab;
                Status = Hidden(new Label(""), "FM27MM-Status"); tab.Add(Status);
                Badge = Hidden(new Label(""), "FM27MM-Badge"); tab.Add(Badge);
                Nudge = Hidden(new Label(""), "FM27MM-Nudge"); tab.Add(Nudge);
                Next = DateTime.MinValue; Update();
                Partnerships.W("popout", "attached", "");
            }
            catch (Exception e) { Partnerships.Fail("popout attach", e); }
        }
        static T Hidden<T>(T e, string name) where T : VisualElement { e.name = name; e.style.display = DisplayStyle.None; e.pickingMode = PickingMode.Ignore; return e; }

        internal static void Seen()
        {
            try { if (PtDate.Text.Length > 0) SeenAtOpen = new HashSet<string>(PtLedger.Stories(PtDate.Text).Select(x => x.Id)); if (Badge != null) Badge.text = ""; } catch { }
        }

        static string Say(PressItem x)
        {
            switch (x.Kind)
            {
                case "split": return x.A + " and " + x.B + " didn't start together " + x.When + ", after " + x.Starts + " starts as a pair.";
                case "breaking": return x.A + " and " + x.B + ": the partnership looks to be breaking down.";
                case "reunited": return x.A + " and " + x.B + " were back together " + x.When + ".";
                case "telepathic": return x.A + " and " + x.B + " have an almost telepathic understanding now.";
                case "clicking": return x.A + " and " + x.B + " are clicking: their partnership has stepped up.";
                case "new": return x.A + " and " + x.B + " are now a recognised partnership.";
                default: return x.A + " and " + x.B + ".";
            }
        }

        internal static void Update()
        {
            if (On == null || Status == null || DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(3);
            try
            {
                var today = PtDate.Text; if (today.Length == 0) return;
                var views = PtLedger.Pairs.Values.Select(p => PtLedger.View(p, today)).ToList();
                var known = views.Where(v => v.Step >= 1).OrderByDescending(v => v.Step).ThenByDescending(v => v.Starts).ToList();
                var best = known.FirstOrDefault();
                Status.text = (known.Count == 1 ? "1 partnership" : known.Count + " partnerships") + " on file"
                    + "|" + (best != null ? "Strongest: " + best.P.A + " and " + best.P.B + " (" + best.Word.ToLowerInvariant() + ")" : "Open Tactics to start the record");
                var stories = PtLedger.Stories(today);
                if (SeenAtOpen == null) SeenAtOpen = new HashSet<string>(stories.Select(x => x.Id));
                if (Known == null) Known = new HashSet<string>(stories.Select(x => x.Id));
                int fresh = stories.Count(x => !SeenAtOpen.Contains(x.Id));
                Badge.text = fresh > 0 ? fresh.ToString() : "";
                var n = stories.FirstOrDefault(x => !Known.Contains(x.Id));
                foreach (var x in stories) Known.Add(x.Id);
                if (n != null) Nudge.text = "pair:" + n.Id.Replace('|', ':') + "|" + Say(n);
            }
            catch (Exception e) { Partnerships.Fail("popout update", e); }
        }
    }
}
