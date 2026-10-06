using System;
using System.IO;
using System.IO.Compression;
using Gamebreak.MiniGolf.Editor.Art;

static class Program
{
    static uint[] table;
    static uint Crc(byte[] d, int off, int len, uint crc = 0xFFFFFFFF)
    {
        if (table == null) { table = new uint[256]; for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; table[n] = c; } }
        for (int i = 0; i < len; i++) crc = table[(crc ^ d[off + i]) & 0xFF] ^ (crc >> 8);
        return crc;
    }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = BitConverter.GetBytes((uint)data.Length); Array.Reverse(len); s.Write(len, 0, 4);
        var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t, 0, 4); s.Write(data, 0, data.Length);
        var all = new byte[4 + data.Length]; Array.Copy(t, all, 4); Array.Copy(data, 0, all, 4, data.Length);
        var crc = BitConverter.GetBytes(~Crc(all, 0, all.Length)); Array.Reverse(crc); s.Write(crc, 0, 4);
    }
    public static void WritePng(string path, byte[] rgba, int w, int h)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
        var ihdr = new byte[13];
        var bw = BitConverter.GetBytes(w); Array.Reverse(bw); var bh = BitConverter.GetBytes(h); Array.Reverse(bh);
        Array.Copy(bw, 0, ihdr, 0, 4); Array.Copy(bh, 0, ihdr, 4, 4); ihdr[8] = 8; ihdr[9] = 6;
        Chunk(fs, "IHDR", ihdr);
        var raw = new byte[(w * 4 + 1) * h];
        for (int y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; Array.Copy(rgba, (h - 1 - y) * w * 4, raw, y * (w * 4 + 1) + 1, w * 4); }
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
        Chunk(fs, "IDAT", ms.ToArray()); Chunk(fs, "IEND", new byte[0]);
    }
    static byte[] Tile(byte[] src, int size, int times)   // repeat to show seams
    {
        int w = size * times; var o = new byte[w * w * 4];
        for (int y = 0; y < w; y++) for (int x = 0; x < w; x++) Array.Copy(src, ((y % size) * size + (x % size)) * 4, o, (y * w + x) * 4, 4);
        return o;
    }
    static void Main(string[] a)
    {
        string dir = a.Length > 0 ? a[0] : "out"; Directory.CreateDirectory(dir);
        void Dump(string name, PbrSet s, float ns, int tile = 2)
        {
            WritePng($"{dir}/{name}_albedo.png", Tile(s.AlbedoBytes(), s.Size, tile), s.Size * tile, s.Size * tile);
            WritePng($"{dir}/{name}_normal.png", s.NormalBytes(ns), s.Size, s.Size);
            var g = s.GlowBytes(); if (g != null) WritePng($"{dir}/{name}_glow.png", g, s.Size, s.Size);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"{name}: {s.Size}px ok");
        }
        Dump("turf", ProceduralPbr.Turf(), 2f);
        Dump("sandstone", ProceduralPbr.TempleSandstone(), 3f);
        Dump("limestone", ProceduralPbr.Limestone(), 3f);
        Dump("limestone_wet", ProceduralPbr.Limestone(512, 33, true), 3f);
        Dump("basalt", ProceduralPbr.Basalt(), 3f);
        Dump("rope", ProceduralPbr.Rope(), 3f);
        Dump("metal", ProceduralPbr.BrushedMetal(), 2f);
        Dump("cupliner", ProceduralPbr.CupLiner(), 2f, 1);
        Dump("ball", ProceduralPbr.BallDimples(), 3f);
        Dump("cloth", ProceduralPbr.Cloth(), 2f);
        Dump("stoneblocks", ProceduralPbr.StoneBlocks(), 3f);
        Dump("sunrelief", ProceduralPbr.SunRelief(), 3f, 1);
        Dump("lava", ProceduralPbr.Lava(), 2f);
    }
}
