// FM27 Media Mod builder: SI's .fmf archives (zstd). Personal and public use under the mod's MIT licence; reads and writes
// only the player's own copies of the game's archives. Layout (found 3 Nov 2026, the same in every FM27 archive):
//   26-byte header: 9 marker bytes, u64 at 9 = (index position - 9), rest kept as is
//   data area from byte 26; each file is a zstd frame (or stored bytes)
//   the 9 marker bytes again, then the index as one zstd frame:
//   dir: str name, u32 nfiles, nfiles * (str name, str ext, u64 offset, u64 csize, u64 usize, u64, u64), u32 ndirs, dirs
//   (str = u32 length + UTF-8 bytes; offsets are relative to byte 26)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FM27MediaMod.Builder
{
    internal sealed class FmfEntry
    {
        public string Path = "";   // e.g. "media/press conferences/post match questions/result.xml"
        public int IndexPos;       // where offset/csize/usize sit in the decompressed index
        public long Offset, CSize, USize;
    }

    internal sealed class FmfArchive
    {
        const int Hdr = 26;
        readonly byte[] raw;
        readonly byte[] index;
        readonly long idxPtr;
        public readonly List<FmfEntry> Entries = new List<FmfEntry>();
        readonly Dictionary<string, FmfEntry> byPath = new Dictionary<string, FmfEntry>(StringComparer.Ordinal);

        public FmfArchive(string file) : this(File.ReadAllBytes(file)) { }
        public FmfArchive(byte[] bytes)
        {
            raw = bytes;
            idxPtr = BitConverter.ToInt64(raw, 9);
            long idxAbs = idxPtr + 18;
            for (int k = 0; k < 9; k++)
                if (raw[idxPtr + 9 + k] != raw[k]) throw new InvalidDataException("unexpected .fmf layout");
            index = Zstd.Decompress(raw, (int)idxAbs, raw.Length - (int)idxAbs);
            int p = 0;
            Walk(ref p, "");
            if (p != index.Length) throw new InvalidDataException("index not fully read");
            foreach (var e in Entries) byPath[e.Path] = e;
        }

        uint U32(ref int p) { uint v = BitConverter.ToUInt32(index, p); p += 4; return v; }
        string Str(ref int p) { int n = (int)U32(ref p); string s = Encoding.UTF8.GetString(index, p, n); p += n; return s; }
        void Walk(ref int p, string path)
        {
            string here = (path.Length > 0 ? path + "/" : "") + Str(ref p);
            uint nf = U32(ref p);
            for (uint i = 0; i < nf; i++)
            {
                string name = Str(ref p), ext = Str(ref p);
                var e = new FmfEntry { Path = here + "/" + name + ext, IndexPos = p };
                e.Offset = BitConverter.ToInt64(index, p); e.CSize = BitConverter.ToInt64(index, p + 8); e.USize = BitConverter.ToInt64(index, p + 16);
                p += 40;
                Entries.Add(e);
            }
            uint nd = U32(ref p);
            for (uint i = 0; i < nd; i++) Walk(ref p, here);
        }

        public bool Has(string path) => byPath.ContainsKey(path);
        public FmfEntry Entry(string path) => byPath.TryGetValue(path, out var e) ? e : null;

        public byte[] Read(FmfEntry e)
        {
            int at = Hdr + (int)e.Offset, n = (int)e.CSize;
            bool z = n >= 4 && raw[at] == 0x28 && raw[at + 1] == 0xB5 && raw[at + 2] == 0x2F && raw[at + 3] == 0xFD;
            byte[] blob;
            if (z) blob = Zstd.Decompress(raw, at, n);
            else { blob = new byte[n]; Buffer.BlockCopy(raw, at, blob, 0, n); }
            if (blob.Length != e.USize) throw new InvalidDataException("size mismatch for " + e.Path);
            return blob;
        }
        public byte[] Read(string path) => Read(byPath[path]);

        // A new archive: every file not replaced keeps its original compressed bytes (as repack_fmf.py does).
        public byte[] Rebuild(IDictionary<string, byte[]> replace)
        {
            var idx = (byte[])index.Clone();
            var order = new List<FmfEntry>(Entries);
            order.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            var data = new MemoryStream();
            int done = 0;
            foreach (var e in order)
            {
                byte[] blob; long us = e.USize;
                if (replace.TryGetValue(e.Path, out var content))
                {
                    blob = Zstd.Compress(content); us = content.Length; done++;
                }
                else
                {
                    blob = new byte[e.CSize]; Buffer.BlockCopy(raw, Hdr + (int)e.Offset, blob, 0, (int)e.CSize);
                }
                WriteI64(idx, e.IndexPos, data.Length); WriteI64(idx, e.IndexPos + 8, blob.Length); WriteI64(idx, e.IndexPos + 16, us);
                data.Write(blob, 0, blob.Length);
            }
            if (done != replace.Count) throw new InvalidDataException("not every replaced file is in the archive");
            var zidx = Zstd.Compress(idx);
            var outp = new MemoryStream();
            var header = new byte[Hdr]; Buffer.BlockCopy(raw, 0, header, 0, Hdr);
            WriteI64(header, 9, Hdr + data.Length - 9);
            outp.Write(header, 0, Hdr);
            data.Position = 0; data.CopyTo(outp);
            outp.Write(raw, 0, 9);
            outp.Write(zidx, 0, zidx.Length);
            return outp.ToArray();
        }
        static void WriteI64(byte[] b, int at, long v) { var x = BitConverter.GetBytes(v); Buffer.BlockCopy(x, 0, b, at, 8); }
    }

    internal static class Zstd
    {
        public static byte[] Decompress(byte[] src, int at, int len)
        {
            using var d = new ZstdSharp.Decompressor();
            return d.Unwrap(new ReadOnlySpan<byte>(src, at, len)).ToArray();
        }
        public static byte[] Compress(byte[] src)
        {
            using var c = new ZstdSharp.Compressor(3);
            c.SetParameter(ZstdSharp.Unsafe.ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
            return c.Wrap(src).ToArray();
        }
    }

    // SI's compiled language (.ltc): u32 at 8 + 8 = table; u32 count; count * (u32 key, u32 offset);
    // id = key & 0xFFFFFF, flags = (key >> 24) | ((offset >> 31) << 8); text at (offset & 0x7FFFFFFF) + 8: 1 byte, u32 length, UTF-8.
    internal static class Ltc
    {
        public static List<(int id, int flags, string text)> Read(byte[] d)
        {
            var o = new List<(int, int, string)>();
            int tab = (int)BitConverter.ToUInt32(d, 8) + 8;
            int count = (int)BitConverter.ToUInt32(d, tab);
            for (int p = tab + 4; p < tab + 4 + 8 * count; p += 8)
            {
                uint k = BitConverter.ToUInt32(d, p), off = BitConverter.ToUInt32(d, p + 4);
                int a = (int)(off & 0x7FFFFFFF) + 8;
                int L = (int)BitConverter.ToUInt32(d, a + 1);
                o.Add(((int)(k & 0xFFFFFF), (int)((k >> 24) | ((off >> 31) << 8)), Encoding.UTF8.GetString(d, a + 5, L)));
            }
            return o;
        }
    }
}
