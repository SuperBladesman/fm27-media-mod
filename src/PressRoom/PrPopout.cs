// FM27 Media Mod - Press Room 0.13.8: what the Media Office tells its popout (Core 0.2.0's conventions).
// Children of the MEDIA tab, all display:none (nothing changes when Core is not installed):
//   FM27MM-Status  "Pressure: <band> · N things said in private|Head of media: <name> · <week>"
//   FM27MM-Badge   things said in private that could still reach the press (whispers in state "new")
//   FM27MM-Nudge   "id|text": the office's latest news, or something said in private that could get out (once each);
//                  0.13.10: a whisper's id ends "@Transfers", so Core 0.2.2 opens the Transfers page from the note
//   FM27MM-Sub:<tab>  one button per Media Office tab, opening the office there
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class PrPopout
    {
        static Label Status, Badge, Nudge;
        static VisualElement On;
        static string LastNews = null;
        static HashSet<string> KnownWhispers;
        static DateTime Next = DateTime.MinValue;

        internal static void Attach(VisualElement tab, (string key, string label)[] tabs)
        {
            try
            {
                On = tab;
                Status = Hidden(new Label(""), "FM27MM-Status"); tab.Add(Status);
                Badge = Hidden(new Label(""), "FM27MM-Badge"); tab.Add(Badge);
                Nudge = Hidden(new Label(""), "FM27MM-Nudge"); tab.Add(Nudge);
                foreach (var (key, label) in tabs)
                {
                    var k = key; var b = new Button(); b.text = label;
                    System.Action open = () => { try { MediaOffice.OpenAt(k); } catch (Exception e) { PressRoom.Fail("popout open " + k, e); } };
                    b.clickable.add_clicked(open);
                    tab.Add(Hidden(b, "FM27MM-Sub:" + label));
                }
                Next = DateTime.MinValue; Update();
                PressRoom.W("popout", "attached", tabs.Length + " tabs");
            }
            catch (Exception e) { PressRoom.Fail("popout attach", e); }
        }
        static T Hidden<T>(T e, string name) where T : VisualElement { e.name = name; e.style.display = DisplayStyle.None; e.pickingMode = PickingMode.Ignore; return e; }
        static string WKey(Word.Whisper w) => w.When + "|" + w.Player + "|" + w.Kind + "|" + w.Conf;
        static uint Hash(string s) { uint h = 2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return h; }

        internal static void Update()
        {
            if (On == null || Status == null || DateTime.Now < Next) return;
            Next = DateTime.Now.AddSeconds(3);
            try
            {
                var open = Word.All.Where(w => w.State == "new").ToList();
                var first = new List<string>();
                if (Pack.On) first.Add("Pressure: " + Pack.PressureBand(Pack.PressureAt(PressRoom.Record.Conf, true, out _)).ToLowerInvariant());
                if (open.Count > 0) first.Add(open.Count == 1 ? "1 thing said in private" : open.Count + " things said in private");
                if (first.Count == 0) first.Add("The press room is quiet");
                var second = (MediaOffice.Mode == MediaOffice.Approach.None ? "No head of media yet" : "Head of media: " + MediaOffice.HeadName) + " · " + Desk.WeekLabel();
                Status.text = string.Join(" · ", first) + "|" + second;
                Badge.text = open.Count > 0 ? open.Count.ToString() : "";
                // notes: the office's latest news first, then something said in private that could get out
                if (KnownWhispers == null) KnownWhispers = new HashSet<string>(Word.All.Select(WKey));
                if (LastNews == null) LastNews = MediaOffice.LastNews ?? "";
                var news = MediaOffice.LastNews ?? "";
                if (news.Length > 0 && news != LastNews) { LastNews = news; Nudge.text = "news:" + Hash(news) + "|" + news; return; }
                var fresh = open.Where(w => !KnownWhispers.Contains(WKey(w))).ToList();
                foreach (var w in Word.All) KnownWhispers.Add(WKey(w));
                if (fresh.Count > 0)
                {
                    var w = fresh[fresh.Count - 1];
                    Nudge.text = "word:" + Hash(WKey(w)) + "@Transfers|" + "Said in private: " + Word.Describe(w).TrimEnd('.') + ". It could reach the press; the Media Office can try to keep it quiet.";
                }
            }
            catch (Exception e) { PressRoom.Fail("popout update", e); }
        }
    }
}
