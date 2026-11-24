// FM27 Media Mod - Press Room 0.13.9: how the dressing room took it.
// 0.13.10: team meetings show one tile per player (tiles-players-meetings/player: name, personality, TextColourSetter/reaction),
// so the room is counted ("16 Downcast, 11 Unimpressed") and each row keeps the room before the reply when it's the same meeting.
// After a reply in a chat or a team meeting, the reaction SI shows ("Downcast", "Encouraged"...), SI's reply bubble and
// the line you chose go on file per save (dressing room.tsv, in Memory.Files) and show in the Media Office's Dressing room tab.
// Two ways in: the text hook (a reaction word set on an element under TextColourSetter, as the probe saw on 16-17 Nov)
// and a scan of the chat screen 0.7 s and 2.2 s after the reply (plain labels that never pass the hook).
// [Dressing room] MapChatScreen (off by default) logs the chat screen's texts and tooltips once per kind of chat,
// so the team meeting's layout can be mapped from Nik's own play.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine.UIElements;

namespace FM27MediaMod
{
    internal static class Reactions
    {
        internal const string FileName = "dressing room.tsv";
        internal static bool MapScreen;
        internal sealed class Row
        {
            public string Date = "", When = "", With = "", Group = "", Line = "", Reply = "", Reaction = "", Who = "", Present = "", Room = "", Before = "";
        }
        internal static readonly List<Row> All = new List<Row>();
        static string Dir => Path.Combine(Paths.BepInExRootPath, "FM27MediaMod");

        static Row Pending; static DateTime PendingAt = DateTime.MinValue; static int Seq;
        static VisualElement Card;                     // the chat screen the last click came from
        static readonly Dictionary<string, string> Words = new Dictionary<string, string>();     // name (or "") -> reaction word
        static readonly Dictionary<string, string> Others = new Dictionary<string, string>();    // spectators (an agent in the room)
        static readonly HashSet<string> Mapped = new HashSet<string>(); static int Maps;

        internal static void Reset() { All.Clear(); Pending = null; Words.Clear(); Others.Clear(); }

        // the button you clicked sits on the chat screen: remember the screen's card for the scans
        internal static void Clicked(VisualElement b)
        {
            try
            {
                VisualElement top = null, e = b;
                for (int i = 0; i < 80 && e != null; i++, e = e.parent)
                {
                    var n = e.name ?? "";
                    if (n == "TeamSpeakToDynamicCard") { top = e; break; }
                    if (n == "AllContainerFocusser" || n == "SpeakToShortcutElement") top = e;
                    if (n == "MainUIDocument-container") break;
                }
                Card = top ?? b.panel?.visualTree;
            }
            catch (Exception ex) { PressRoom.Fail("reaction card", ex); }
        }

        // SI's ConversationReplySelected, with the words you clicked
        internal static void OnReply(string chosen, string group)
        {
            try
            {
                if (Pending != null) Commit();
                var with = ((Word.ChatRole.Length > 0 ? Word.ChatRole + " " : "") + Word.ChatWith).Trim();
                Pending = new Row
                {
                    Date = Desk.Today == DateTime.MinValue ? "" : Desk.Today.Day + "/" + Desk.Today.Month + "/" + Desk.Today.Year,
                    When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), With = with, Group = group ?? "", Line = PressRoom.Visible(chosen ?? "").Trim()
                };
                PendingAt = DateTime.Now; Words.Clear(); Others.Clear();
                int s = ++Seq; var card = Card;
                if (card != null && card.panel != null)
                {
                    card.schedule.Execute((Action)(() => Scan(s, card, false))).ExecuteLater(700);
                    card.schedule.Execute((Action)(() => Scan(s, card, true))).ExecuteLater(2200);
                }
            }
            catch (Exception e) { PressRoom.Fail("reaction reply", e); }
        }

        // the text hook: cheap first (a short text on an element whose parent is TextColourSetter, soon after a reply)
        internal static void OnText(VisualElement el, string txt)
        {
            if (Pending == null || txt.Length > 32 || el == null) return;
            var p = el.parent; if (p == null || p.name != "TextColourSetter") return;
            if ((DateTime.Now - PendingAt).TotalSeconds > 3) return;
            try { Note(el, PressRoom.Visible(txt).Trim()); } catch (Exception e) { PressRoom.Fail("reaction text", e); }
        }

        // a reaction word, filed under the person it belongs to (PlayerInfo / NonPlayerInfo, Interactees / Spectators)
        static void Note(VisualElement el, string word)
        {
            if (word.Length == 0 || word.Length > 32 || word.Any(char.IsDigit)) return;
            string name = ""; bool spectator = false; VisualElement e = el.parent;
            for (int i = 0; i < 12 && e != null; i++, e = e.parent)
            {
                var n = e.name ?? "";
                if (name.Length == 0 && (n == "PlayerInfo" || n == "NonPlayerInfo" || n.EndsWith("Info")))
                {
                    var t = Named(e, "Name", 0); if (t != null) name = PressRoom.Visible(t.text ?? "").Trim();
                }
                if (name.Length == 0 && n == "player") name = TileName(e);   // 0.13.10: a team meeting's tile
                if (n == "Spectators") { spectator = true; break; }
                if (n == "Interactees") break;
            }
            if (spectator) Others[name] = word; else Words[name] = word;
        }

        static TextElement Named(VisualElement e, string name, int depth)
        {
            if (e == null || depth > 4) return null;
            for (int i = 0; i < e.childCount; i++)
            {
                var c = e.ElementAt(i);
                if (c.name == name) { var t = c.TryCast<TextElement>(); if (t != null) return t; var inner = First(c, 0); if (inner != null) return inner; }
                var r = Named(c, name, depth + 1); if (r != null) return r;
            }
            return null;
        }
        // the first text on a meeting tile that isn't the reaction (the name comes before the personality)
        static string TileName(VisualElement tile)
        {
            var found = new List<string>(); Texts(tile, found, 0);
            return found.Count > 0 ? found[0] : "";
        }
        static void Texts(VisualElement e, List<string> into, int depth)
        {
            if (e == null || depth > 4 || into.Count > 2) return;
            if (e.name == "TextColourSetter") return;
            var t = e.TryCast<TextElement>(); if (t != null && !string.IsNullOrEmpty(t.text)) { var v = PressRoom.Visible(t.text).Trim(); if (v.Length > 0) into.Add(v); }
            for (int i = 0; i < e.childCount; i++) Texts(e.ElementAt(i), into, depth + 1);
        }
        static TextElement First(VisualElement e, int depth)
        {
            if (e == null || depth > 4) return null;
            var t = e.TryCast<TextElement>(); if (t != null && !string.IsNullOrEmpty(t.text)) return t;
            for (int i = 0; i < e.childCount; i++) { var r = First(e.ElementAt(i), depth + 1); if (r != null) return r; }
            return null;
        }

        // the scan: reaction words that never passed the hook, and SI's reply bubble
        static void Scan(int s, VisualElement card, bool last)
        {
            try
            {
                if (s != Seq || Pending == null || card == null || card.panel == null) return;
                int seen = 0; string bubble = "";
                var map = MapScreen && !last && Maps < 4 && Mapped.Add(Pending.With.Length == 0 ? "(no sender)" : Pending.With) ? new List<string>() : null;
                if (map != null) Maps++;
                Walk(card, 0, ref seen, ref bubble, map, "");
                if (bubble.Length > 0) Pending.Reply = bubble;
                if (map != null)
                {
                    PressRoom.W("chat map", Pending.With.Length == 0 ? "(no sender)" : Pending.With, map.Count + " texts after: " + Clip(Pending.Line, 80));
                    foreach (var m in map) PressRoom.W("chat map", "", m);
                }
                if (last) Commit();
            }
            catch (Exception e) { PressRoom.Fail("reaction scan", e); if (last) Commit(); }
        }

        static void Walk(VisualElement e, int depth, ref int seen, ref string bubble, List<string> map, string path)
        {
            if (e == null || depth > 70 || seen > 9000) return;
            seen++;
            try { if (e.resolvedStyle.display == DisplayStyle.None) return; } catch { }
            var n = e.name ?? "";
            var here = n.Length > 0 ? (path.Length > 0 ? path + "/" + n : n) : path;
            var t = e.TryCast<TextElement>();
            if (t != null && !string.IsNullOrEmpty(t.text))
            {
                var v = PressRoom.Visible(t.text).Trim();
                if (v.Length > 0)
                {
                    if (e.parent != null && e.parent.name == "TextColourSetter") Note(e, v);
                    if (here.Contains("NPCBubbleFrame")) bubble = v;   // the newest bubble is the last one drawn
                    if (map != null && map.Count < 300) map.Add(Tail(here, 6) + "\t" + Clip(v, 120));
                }
            }
            if (map != null && map.Count < 300) { string tip = ""; try { tip = e.tooltip ?? ""; } catch { } if (tip.Length > 0) map.Add(Tail(here, 6) + "\ttooltip: " + Clip(PressRoom.Visible(tip), 120)); }
            for (int i = 0; i < e.childCount; i++) Walk(e.ElementAt(i), depth + 1, ref seen, ref bubble, map, here);
        }
        static string Tail(string path, int n) { var p = path.Split('/'); return string.Join("/", p.Skip(Math.Max(0, p.Length - n))); }
        static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 3) + "...";

        static void Commit()
        {
            try
            {
                var r = Pending; Pending = null; if (r == null) return;
                // the main reaction: the word most of the room shows (a one-to-one chat has one)
                // 0.13.11: the hook can file a word under "" before the scan finds the name: one person, one word
                if (Words.ContainsKey("") && Words.Keys.Any(k => k.Length > 0)) Words.Remove("");
                var words = Words.Values.Where(w => w.Length > 0).ToList();
                var groups = words.GroupBy(w => w).OrderByDescending(g => g.Count()).ToList();
                r.Reaction = groups.Count == 0 ? "" : groups[0].Key;
                r.Room = Words.Keys.Count(k => k.Length > 0) > 2 ? string.Join(", ", groups.Select(g => g.Count() + " " + g.Key)) : "";   // a room, not a one-to-one
                // the room before this reply: the last row of the same meeting or chat, a few minutes earlier
                if (All.Count > 0)
                {
                    var prev = All[All.Count - 1];
                    if (prev.With == r.With && prev.Date == r.Date && DateTime.TryParse(prev.When, out var pw) && (DateTime.Now - pw).TotalMinutes < 15)
                        r.Before = prev.Room.Length > 0 ? prev.Room : prev.Reaction;
                }
                r.Who = string.Join("; ", Words.Where(kv => kv.Key.Length > 0).Select(kv => kv.Key + ": " + kv.Value));
                r.Present = string.Join("; ", Others.Where(kv => kv.Key.Length > 0).Select(kv => kv.Key + ": " + kv.Value));
                if (r.Reaction.Length == 0 && r.Reply.Length == 0) { PressRoom.W("reaction", "nothing read", (r.With.Length > 0 ? r.With : "(no sender)") + " | " + Clip(r.Line, 90)); return; }
                All.Add(r);
                while (All.Count > 300) All.RemoveAt(0);
                Save();
                PressRoom.W("reaction", (r.Reaction.Length > 0 ? r.Reaction : "no reaction word") + " | " + (r.With.Length > 0 ? r.With : "(no sender)") + " | " + r.Group,
                    Clip(r.Line, 90) + " => " + Clip(r.Reply, 90) + (r.Room.Length > 0 ? " | room: " + r.Room : "") + (r.Before.Length > 0 ? " | before: " + r.Before : "") + (r.Who.Length > 0 ? " | " + r.Who : "") + (r.Present.Length > 0 ? " | present: " + r.Present : ""));
                MediaOffice.Refresh();
            }
            catch (Exception e) { PressRoom.Fail("reaction commit", e); }
        }

        static string C(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        internal static void Load()
        {
            All.Clear();
            try
            {
                var p = Path.Combine(Dir, FileName); if (!File.Exists(p)) return;
                foreach (var line in File.ReadAllLines(p))
                {
                    var a = line.Split('\t'); if (a.Length < 9) continue;
                    All.Add(new Row { Date = a[0], When = a[1], With = a[2], Group = a[3], Line = a[4], Reply = a[5], Reaction = a[6], Who = a[7], Present = a[8],
                        Room = a.Length > 9 ? a[9] : "", Before = a.Length > 10 ? a[10] : "" });
                }
            }
            catch (Exception e) { PressRoom.Fail("reactions load", e); }
        }
        static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var r in All) sb.Append(string.Join("\t", new[] { r.Date, r.When, r.With, r.Group, r.Line, r.Reply, r.Reaction, r.Who, r.Present, r.Room, r.Before }.Select(C))).Append('\n');
                File.WriteAllText(Path.Combine(Dir, FileName), sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { PressRoom.Fail("reactions save", e); }
        }

        // the Media Office's lines: newest first
        internal static IEnumerable<Row> Latest(int n) { for (int i = All.Count - 1, k = 0; i >= 0 && k < n; i--, k++) yield return All[i]; }
        internal static string Heading(Row r) =>
            (r.Reaction.Length > 0 ? r.Reaction : "Reaction not read") + " · " + (r.With.Length > 0 ? r.With : "Team meeting") + (r.Group.Length > 0 ? " · " + r.Group : "");
        // the room in numbers, with what it was before this reply
        internal static string RoomLine(Row r) =>
            r.Room.Length == 0 ? "" : "The room: " + r.Room + (r.Before.Length > 0 ? " (before: " + r.Before + ")" : "");
        internal static string Detail(Row r) =>
            (r.Date.Length > 0 ? r.Date + ": " : "") + "“" + Clip(r.Line, 140) + "”" + (r.Reply.Length > 0 ? "  Reply: “" + Clip(r.Reply, 120) + "”" : "");
    }
}
