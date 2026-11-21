// FM27 Media Mod - shared decoder for SI's rich-text links. Personal single-player use only.
// A link is \u0001<base64 record>\u0002<visible name>\u0001. Decoded, a person's record reads
// "Click to view <Full Name>'s profile"; clubs, nations and competitions read "Click to view <Name>" (no "profile").
// The 4 bytes after the "ferd" marker are the object's id, stable for the save (top byte = type: 0x01 person, 0x18 club).
// Verified 19 Nov 2026 on Nik's logs (Witbooi / Emile Witbooi -> 0x01029dda; Sheffield United -> 0x1800024c).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod
{
    internal sealed class LinkInfo
    {
        public string Visible = "", Full = "";
        public uint Id;
        public bool IsPerson;
        public string Key => Id != 0 ? Id.ToString("x8") : Full;
    }

    internal static class PersonLinks
    {
        internal static readonly Regex Link = new Regex("\u0001([A-Za-z0-9+/=]+)\u0002(.*?)\u0001", RegexOptions.Compiled);
        static readonly byte[] ClickTo = Encoding.ASCII.GetBytes("Click to view ");
        static readonly byte[] Profile = Encoding.ASCII.GetBytes("'s profile");
        static readonly byte[] Ferd = Encoding.ASCII.GetBytes("ferd");

        internal static string Plain(string t) => (t == null || t.IndexOf('\u0001') < 0) ? t : Link.Replace(t, m => m.Groups[2].Value);

        static int Find(byte[] hay, byte[] needle, int from)
        {
            for (int i = Math.Max(0, from); i <= hay.Length - needle.Length; i++)
            {
                int j = 0; while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        internal static LinkInfo Decode(string b64, string visible)
        {
            var li = new LinkInfo { Visible = (visible ?? "").Trim() };
            try
            {
                var s = b64 ?? ""; int rem = s.Length % 4;
                if (rem == 2) s += "=="; else if (rem == 3) s += "=";
                var d = Convert.FromBase64String(s);
                int f = Find(d, Ferd, 0);
                if (f >= 0 && f + 8 <= d.Length) li.Id = BitConverter.ToUInt32(d, f + 4);
                int c = Find(d, ClickTo, 0);
                if (c >= 0)
                {
                    int start = c + ClickTo.Length;
                    int p = Find(d, Profile, start);
                    if (p >= 0) { li.IsPerson = true; li.Full = Encoding.UTF8.GetString(d, start, p - start); }
                    else { int end = start; while (end < d.Length && d[end] >= 0x20) end++; li.Full = Encoding.UTF8.GetString(d, start, end - start); }
                }
                if (!li.IsPerson && (li.Id >> 24) == 0x01) li.IsPerson = true;   // type byte says person even if the tooltip is missing
            }
            catch { }
            if (li.Full.Length == 0) li.Full = li.Visible;
            return li;
        }

        // every distinct link in a text, in order of first appearance; Visible = the longest form seen
        internal static List<LinkInfo> All(string txt)
        {
            var list = new List<LinkInfo>(); var byKey = new Dictionary<string, LinkInfo>();
            if (string.IsNullOrEmpty(txt) || txt.IndexOf('\u0001') < 0) return list;
            foreach (Match m in Link.Matches(txt))
            {
                var li = Decode(m.Groups[1].Value, m.Groups[2].Value);
                if (byKey.TryGetValue(li.Key, out var have)) { if (li.Visible.Length > have.Visible.Length) have.Visible = li.Visible; continue; }
                byKey[li.Key] = li; list.Add(li);
            }
            return list;
        }

        internal static List<LinkInfo> People(string txt)
        {
            var r = new List<LinkInfo>();
            foreach (var l in All(txt)) if (l.IsPerson) r.Add(l);
            return r;
        }
    }
}
