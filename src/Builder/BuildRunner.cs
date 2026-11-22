// FM27 Media Mod builder: decides what needs building at each launch and does it. Pure .NET (no BepInEx), so it can be
// tested offline. Rules:
//  - media.fmf / simatch.fmf: if the file in the game folder is the one we wrote last time, and the data is the same
//    version, nothing to do. Otherwise the file is taken as SI's (new install, SI patch, Steam verify), backed up, checked
//    for mod content (our 800000-range text ids must not be in it), patched and written back; the game's caches are
//    then moved aside so FM rebuilds them. If any change no longer fits SI's file, nothing is written.
//  - the language: rebuilt when missing, when SI's English or the template changes, or when the data version changes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FM27MediaMod.Builder
{
    internal sealed class BuildSettings
    {
        public string GameDir = "", DataDir = "", StateDir = "", LanguagesDir = "", CachesDir = "";
        public string ModName = "FM27 Media Mod";
        public bool Media = true, Language = true, MoveCaches = true;
    }

    internal static class BuildRunner
    {
        static readonly Regex ModIds = new Regex("translation_id=\"8\\d{5}\"", RegexOptions.CultureInvariant);

        public static void Run(BuildSettings s, Action<string> log)
        {
            Directory.CreateDirectory(s.StateDir);
            var statePath = Path.Combine(s.StateDir, "builder state.tsv");
            var st = ReadState(statePath);
            string dataVer = ReadDataVersion(s.DataDir);
            log($"data version {dataVer}");
            bool mediaChanged = false;
            if (s.Media)
            {
                try { mediaChanged = BuildMedia(s, st, dataVer, log); }
                catch (Exception e) { log("media: failed, game files left as they were: " + e.Message); }
                WriteState(statePath, st);
            }
            if (mediaChanged && s.MoveCaches) MoveCaches(s, log);
            if (s.Language)
            {
                try { BuildLanguage(s, st, dataVer, log); }
                catch (Exception e) { log("language: failed: " + e.Message); }
                WriteState(statePath, st);
            }
        }

        // ---------------- media ----------------
        static readonly string[] Archives = { "media.fmf", "simatch.fmf" };
        static string ArchivePath(BuildSettings s, string a) => a == "media.fmf"
            ? Path.Combine(s.GameDir, "data", "game_simulation", "media.fmf")
            : Path.Combine(s.GameDir, "shared", "data", "simatch.fmf");

        static bool BuildMedia(BuildSettings s, Dictionary<string, string> st, string dataVer, Action<string> log)
        {
            var patchFile = Path.Combine(s.DataDir, "media.patch");
            if (!File.Exists(patchFile)) { log("media: no media.patch in the data folder"); return false; }
            var cur = new Dictionary<string, byte[]>(); var curMd5 = new Dictionary<string, string>();
            bool todo = false;
            foreach (var a in Archives)
            {
                var p = ArchivePath(s, a);
                if (!File.Exists(p)) { log("media: " + p + " not found"); return false; }
                cur[a] = File.ReadAllBytes(p); curMd5[a] = Md5(cur[a]);
                if (!(Get(st, a + ".out") == curMd5[a] && Get(st, a + ".data") == dataVer)) todo = true;
            }
            if (!todo) { log("media: up to date"); return false; }

            var vanilla = new Dictionary<string, FmfArchive>();
            var vanBytes = new Dictionary<string, byte[]>();
            foreach (var a in Archives)
            {
                byte[] v;
                if (Get(st, a + ".out") == curMd5[a])
                {
                    var b = BackupPath(s, a, Get(st, a + ".vanilla"));
                    if (!File.Exists(b)) throw new IOException($"{a} is the mod's old build but SI's original backup is missing; use Steam's 'Verify integrity of game files' and start FM again");
                    v = File.ReadAllBytes(b);
                }
                else
                {
                    v = cur[a];
                    CheckClean(a, new FmfArchive(v), patchFile);   // before anything is labelled as SI's original
                    var b = BackupPath(s, a, curMd5[a]);
                    if (!File.Exists(b)) { Directory.CreateDirectory(Path.GetDirectoryName(b)); File.WriteAllBytes(b, v); log($"media: the game's {a} (SI's, or another mod's) backed up as {Path.GetFileName(b)}"); }
                }
                vanBytes[a] = v; vanilla[a] = new FmfArchive(v);
            }

            var patches = new MediaPatches(patchFile);
            var dic = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ar in vanilla.Values)
                foreach (var e in ar.Entries)
                    if (e.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        XmlCanon.ProseStrings(XmlCanon.Parse(Encoding.UTF8.GetString(ar.Read(e))), v => { var h = XmlCanon.Fnv64(v); if (patches.NeededHashes.Contains(h)) dic[h] = v; });

            var repl = new Dictionary<string, Dictionary<string, byte[]>>();
            foreach (var a in Archives) repl[a] = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            int missing = 0;
            foreach (var fp in patches.Files)
            {
                if (!vanilla.TryGetValue(fp.Archive, out var ar)) throw new InvalidDataException("unknown archive " + fp.Archive);
                if (!ar.Has(fp.Inner)) throw new InvalidDataException($"{fp.Inner} is not in SI's {fp.Archive} any more");
                var text = Encoding.UTF8.GetString(ar.Read(fp.Inner));
                if (ModIds.IsMatch(text)) throw new InvalidDataException($"{fp.Archive} already holds mod content ({fp.Inner}); use Steam's 'Verify integrity of game files' and start FM again");
                var real = XmlCanon.Lines(XmlCanon.Parse(text), 0);
                if (!MediaPatches.Apply(real, fp, dic, out var got, ref missing, out var why))
                    throw new InvalidDataException($"{fp.Inner}: {why} (an SI patch may have changed it; a mod update is needed)");
                repl[fp.Archive][fp.Inner] = new UTF8Encoding(false).GetBytes(string.Join("\r\n", got) + "\r\n");
            }
            if (missing > 0) log($"media: {missing} pieces of SI wording not found in SI's files (left blank; the game shows text from the language file)");

            foreach (var a in Archives)
            {
                var outBytes = vanilla[a].Rebuild(repl[a]);
                var check = new FmfArchive(outBytes);
                foreach (var kv in repl[a]) if (!Same(check.Read(kv.Key), kv.Value)) throw new InvalidDataException("read-back check failed for " + kv.Key);
                var p = ArchivePath(s, a); var tmp = p + ".fm27mm-new";
                File.WriteAllBytes(tmp, outBytes);
                File.Move(tmp, p, true);
                st[a + ".vanilla"] = Md5(vanBytes[a]); st[a + ".out"] = Md5(outBytes); st[a + ".data"] = dataVer;
                log($"media: {a} built ({repl[a].Count} files changed, {outBytes.Length} bytes)");
            }
            return true;
        }

        // the files we patch must not already hold mod content (our 800000-range text ids)
        static void CheckClean(string archive, FmfArchive ar, string patchFile)
        {
            foreach (var l in File.ReadLines(patchFile))
            {
                if (!l.StartsWith("FILE\t", StringComparison.Ordinal)) continue;
                var p = l.Split('\t'); if (p[1] != archive || !ar.Has(p[2])) continue;
                if (ModIds.IsMatch(Encoding.UTF8.GetString(ar.Read(p[2]))))
                    throw new InvalidDataException($"{archive} already holds mod content ({p[2]}); use Steam's 'Verify integrity of game files' and start FM again");
            }
        }

        static string BackupPath(BuildSettings s, string a, string md5) => Path.Combine(s.StateDir, "backups", Path.GetFileNameWithoutExtension(a) + " (SI " + md5 + ").fmf");

        static void MoveCaches(BuildSettings s, Action<string> log)
        {
            try
            {
                if (string.IsNullOrEmpty(s.CachesDir) || !Directory.Exists(s.CachesDir)) return;
                var parent = Path.GetDirectoryName(s.CachesDir);
                var dest = Path.Combine(parent, "_to_delete", "caches before FM27 Media Mod build " + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                Directory.Move(s.CachesDir, dest);
                log("caches moved aside so FM rebuilds them: " + dest);
            }
            catch (Exception e) { log("caches: could not move them (" + e.Message + "); if old questions show, clear " + s.CachesDir + " with FM closed"); }
        }

        // ---------------- language ----------------
        static void BuildLanguage(BuildSettings s, Dictionary<string, string> st, string dataVer, Action<string> log)
        {
            var langsFmf = Path.Combine(s.GameDir, "data", "game_simulation", "languages.fmf");
            var tmpl = Path.Combine(s.LanguagesDir, "example.ltf");
            if (!File.Exists(tmpl)) { log("language: FM's template " + tmpl + " is not there yet; start FM once, close it, and start it again"); return; }
            var la = new FmfArchive(langsFmf);
            FmfEntry eng = null;
            foreach (var e in la.Entries) if (e.Path.EndsWith("/english.ltc", StringComparison.OrdinalIgnoreCase)) eng = e;
            if (eng == null) { log("language: english.ltc not found in languages.fmf"); return; }
            var english = la.Read(eng);
            var engMd5 = Md5(english);
            var fi = new FileInfo(tmpl); var tsig = fi.Length + "/" + fi.LastWriteTimeUtc.Ticks;
            var baseName = s.ModName + " " + dataVer;
            var outFile = Path.Combine(s.LanguagesDir, baseName + ".ltf");
            var langName = "English (" + baseName + ")";
            if (File.Exists(outFile) && Get(st, "lang.english") == engMd5 && Get(st, "lang.template") == tsig && Get(st, "lang.data") == dataVer && Get(st, "lang.file") == outFile)
            { log("language: up to date (" + langName + ")"); return; }
            var r = LanguageBuilder.Build(english, tmpl, s.DataDir, langName, outFile);
            if (r.EditsSkipped > 0) log($"language: {r.EditsSkipped} small edits skipped because SI has reworded those lines");
            // FM compiles the .ltf into an .ltc of the same name the first time it is chosen: move a stale one aside
            var ltc = Path.ChangeExtension(outFile, ".ltc");
            if (File.Exists(ltc)) { var old = ltc + ".old"; if (File.Exists(old)) File.Delete(old); File.Move(ltc, old); log("language: old compiled file moved aside so FM compiles the new one"); }
            // 1.0.1: an earlier data version's language stays where it is (FM's behaviour when the chosen language disappears
            // is untested), but the log says which one to choose
            var prev = Get(st, "lang.file");
            if (!string.IsNullOrEmpty(prev)) prev = Path.Combine(s.LanguagesDir, Path.GetFileName(prev));
            if (!string.IsNullOrEmpty(prev) && !string.Equals(prev, outFile, StringComparison.OrdinalIgnoreCase) && File.Exists(prev))
                log("language: the earlier " + Path.GetFileNameWithoutExtension(prev) + " is still in your languages folder; choose " + langName + " instead (you can delete the old .ltf and .ltc with FM closed)");
            st["lang.english"] = engMd5; st["lang.template"] = tsig; st["lang.data"] = dataVer; st["lang.file"] = outFile;
            log($"language: built \"{langName}\" ({r.Rows} mod lines, {r.Appended} new lines); choose it in Preferences > Language");
        }

        // ---------------- helpers ----------------
        static string ReadDataVersion(string dir)
        {
            var f = Path.Combine(dir, "data.txt");
            if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) if (l.StartsWith("version=", StringComparison.Ordinal)) return l.Substring(8).Trim();
            return "1";
        }
        static Dictionary<string, string> ReadState(string f)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) { int t = l.IndexOf('\t'); if (t > 0) d[l.Substring(0, t)] = l.Substring(t + 1); }
            return d;
        }
        static void WriteState(string f, Dictionary<string, string> d)
        {
            var sb = new StringBuilder();
            foreach (var kv in d) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
        }
        static string Get(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v : "";
        // a fingerprint for change detection (FNV-1a 64 over the bytes, plus the length); no crypto library needed
        static string Md5(byte[] b) { ulong h = 0xcbf29ce484222325UL; foreach (var x in b) { h ^= x; h *= 0x100000001b3UL; } return h.ToString("x16") + "-" + b.Length.ToString(); }
        static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
