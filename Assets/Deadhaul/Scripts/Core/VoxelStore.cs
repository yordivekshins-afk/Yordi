using System;
using System.Collections.Generic;
using System.IO;

namespace Deadhaul.Core
{
    public struct V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, float s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public override string ToString() => $"({X:0.00}, {Y:0.00}, {Z:0.00})";
    }

    public struct RayHit
    {
        public bool Hit;
        public int X, Y, Z;          // geraakte voxel
        public int Nx, Ny, Nz;       // normaal van het geraakte vlak
        public float Distance;       // in meter
        public byte Block;
    }

    /// <summary>
    /// Alle geladen chunks (gepad, zodat meshen geen buren nodig heeft) plus de wijzigingen van de speler.
    /// Alleen de wijzigingen worden opgeslagen; de rest komt opnieuw uit de generator.
    /// Niet thread-safe: alleen vanaf de hoofdthread gebruiken.
    /// </summary>
    public sealed class VoxelStore
    {
        readonly Dictionary<long, byte[]> chunks = new Dictionary<long, byte[]>();
        readonly Dictionary<long, Dictionary<int, byte>> edits = new Dictionary<long, Dictionary<int, byte>>();
        readonly HashSet<long> lootedContainers = new HashSet<long>();

        public static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
        public static long VoxelKey(int x, int y, int z) => ((long)(x & 0xFFFFFF) << 40) | ((long)(z & 0xFFFFFF) << 16) | (uint)(y & 0xFFFF);
        public static int ChunkOf(int v) => World.FloorDiv(v, World.ChunkSize);

        public int LoadedCount => chunks.Count;
        public bool IsLoaded(int cx, int cz) => chunks.ContainsKey(Key(cx, cz));
        public byte[] GetPadded(int cx, int cz) => chunks.TryGetValue(Key(cx, cz), out var c) ? c : null;
        public void Add(int cx, int cz, byte[] padded) => chunks[Key(cx, cz)] = padded;
        public void Remove(int cx, int cz) => chunks.Remove(Key(cx, cz));
        public IEnumerable<long> LoadedKeys => chunks.Keys;

        /// <summary>Bloktype op voxelpositie. Onbekend (niet geladen) = null.</summary>
        public byte? TryGet(int x, int y, int z)
        {
            if (y < 0) return B.Bedrock;
            if (y >= World.Height) return B.Air;
            int cx = ChunkOf(x), cz = ChunkOf(z);
            if (!chunks.TryGetValue(Key(cx, cz), out var c)) return null;
            return c[World.IdxPad(x - cx * World.ChunkSize + 1, y, z - cz * World.ChunkSize + 1)];
        }

        public byte Get(int x, int y, int z) => TryGet(x, y, z) ?? B.Air;

        /// <summary>Voor botsingen: niet-geladen gebied telt als vast, zodat je niet door de wereld valt.</summary>
        public bool IsSolid(int x, int y, int z)
        {
            var b = TryGet(x, y, z);
            return b == null || Blocks.Solid[b.Value];
        }

        /// <summary>Zet een blok en geeft de chunks terug die opnieuw gemesht moeten worden.</summary>
        public List<(int cx, int cz)> Set(int x, int y, int z, byte b)
        {
            var dirty = new List<(int, int)>();
            if (y < 0 || y >= World.Height) return dirty;
            int cx = ChunkOf(x), cz = ChunkOf(z);
            int lx = x - cx * World.ChunkSize, lz = z - cz * World.ChunkSize;
            long k = Key(cx, cz);
            if (!edits.TryGetValue(k, out var e)) edits[k] = e = new Dictionary<int, byte>();
            e[World.IdxLocal(lx, y, lz)] = b;
            // de voxel staat in zijn eigen chunk én in de rand van maximaal 3 buren
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ncx = cx + dx, ncz = cz + dz;
                    int px = x - ncx * World.ChunkSize + 1, pz = z - ncz * World.ChunkSize + 1;
                    if ((uint)px >= World.Pad || (uint)pz >= World.Pad) continue;
                    if (!chunks.TryGetValue(Key(ncx, ncz), out var c)) continue;
                    c[World.IdxPad(px, y, pz)] = b;
                    dirty.Add((ncx, ncz));
                }
            return dirty;
        }

        /// <summary>Wijzigingen die in de gepadde regio van deze chunk vallen (om na generatie toe te passen).</summary>
        public List<(int x, int y, int z, byte b)> EditsAround(int cx, int cz)
        {
            var list = new List<(int, int, int, byte)>();
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!edits.TryGetValue(Key(cx + dx, cz + dz), out var e)) continue;
                    int bx = (cx + dx) * World.ChunkSize, bz = (cz + dz) * World.ChunkSize;
                    foreach (var kv in e)
                    {
                        int idx = kv.Key;
                        int y = idx % World.Height;
                        int rest = idx / World.Height;
                        int lx = rest % World.ChunkSize, lz = rest / World.ChunkSize;
                        list.Add((bx + lx, y, bz + lz, kv.Value));
                    }
                }
            return list;
        }

        public static void ApplyEdits(byte[] padded, int cx, int cz, List<(int x, int y, int z, byte b)> list)
        {
            int ox = cx * World.ChunkSize - 1, oz = cz * World.ChunkSize - 1;
            foreach (var (x, y, z, b) in list)
            {
                int px = x - ox, pz = z - oz;
                if ((uint)px >= World.Pad || (uint)pz >= World.Pad || (uint)y >= World.Height) continue;
                padded[World.IdxPad(px, y, pz)] = b;
            }
        }

        public bool IsLooted(int x, int y, int z) => lootedContainers.Contains(VoxelKey(x, y, z));
        public void MarkLooted(int x, int y, int z) => lootedContainers.Add(VoxelKey(x, y, z));

        /// <summary>Voxel-DDA vanaf een punt in meters. Planten en water worden overgeslagen.</summary>
        public RayHit Raycast(V3 origin, V3 dir, float maxDist, bool hitFoliage = true)
        {
            var hit = new RayHit();
            float len = dir.Length;
            if (len < 1e-6f) return hit;
            float dx = dir.X / len, dy = dir.Y / len, dz = dir.Z / len;
            float inv = 1f / World.VoxelSize;
            float ox = origin.X * inv, oy = origin.Y * inv, oz = origin.Z * inv;
            int x = (int)MathF.Floor(ox), y = (int)MathF.Floor(oy), z = (int)MathF.Floor(oz);
            int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1, sz = dz > 0 ? 1 : -1;
            float tdx = dx != 0 ? MathF.Abs(1f / dx) : float.MaxValue;
            float tdy = dy != 0 ? MathF.Abs(1f / dy) : float.MaxValue;
            float tdz = dz != 0 ? MathF.Abs(1f / dz) : float.MaxValue;
            float tmx = dx != 0 ? ((sx > 0 ? x + 1 - ox : ox - x) * tdx) : float.MaxValue;
            float tmy = dy != 0 ? ((sy > 0 ? y + 1 - oy : oy - y) * tdy) : float.MaxValue;
            float tmz = dz != 0 ? ((sz > 0 ? z + 1 - oz : oz - z) * tdz) : float.MaxValue;
            float maxT = maxDist * inv, t = 0;
            int nx = 0, ny = 0, nz = 0;
            while (t <= maxT)
            {
                byte b = Get(x, y, z);
                if (Blocks.Solid[b] || (hitFoliage && b == B.Crop))
                {
                    hit.Hit = true; hit.X = x; hit.Y = y; hit.Z = z; hit.Nx = nx; hit.Ny = ny; hit.Nz = nz;
                    hit.Distance = t * World.VoxelSize; hit.Block = b;
                    return hit;
                }
                if (tmx < tmy && tmx < tmz) { x += sx; t = tmx; tmx += tdx; nx = -sx; ny = 0; nz = 0; }
                else if (tmy < tmz) { y += sy; t = tmy; tmy += tdy; nx = 0; ny = -sy; nz = 0; }
                else { z += sz; t = tmz; tmz += tdz; nx = 0; ny = 0; nz = -sz; }
            }
            return hit;
        }

        // ------------------------------------------------------------ opslaan
        public void WriteEdits(BinaryWriter w)
        {
            w.Write(edits.Count);
            foreach (var kv in edits)
            {
                w.Write(kv.Key);
                w.Write(kv.Value.Count);
                foreach (var e in kv.Value) { w.Write(e.Key); w.Write(e.Value); }
            }
            w.Write(lootedContainers.Count);
            foreach (var l in lootedContainers) w.Write(l);
        }

        public void ReadEdits(BinaryReader r)
        {
            edits.Clear(); lootedContainers.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                long k = r.ReadInt64();
                int m = r.ReadInt32();
                var d = new Dictionary<int, byte>(m);
                for (int j = 0; j < m; j++) { int idx = r.ReadInt32(); d[idx] = r.ReadByte(); }
                edits[k] = d;
            }
            int l = r.ReadInt32();
            for (int i = 0; i < l; i++) lootedContainers.Add(r.ReadInt64());
        }

        public int EditCount { get { int n = 0; foreach (var e in edits.Values) n += e.Count; return n; } }
    }

    /// <summary>
    /// Botsing van een rechtopstaande doos (voeten = positie) tegen de voxelwereld,
    /// per as, met automatisch opstappen van één voxel (trappen, stoepranden).
    /// </summary>
    public static class VoxelPhysics
    {
        public const float StepHeight = World.VoxelSize + 0.02f;

        public static bool Overlaps(VoxelStore w, V3 feet, float halfW, float height)
        {
            float inv = 1f / World.VoxelSize;
            int x0 = (int)MathF.Floor((feet.X - halfW) * inv), x1 = (int)MathF.Floor((feet.X + halfW - 1e-4f) * inv);
            int y0 = (int)MathF.Floor(feet.Y * inv), y1 = (int)MathF.Floor((feet.Y + height - 1e-4f) * inv);
            int z0 = (int)MathF.Floor((feet.Z - halfW) * inv), z1 = (int)MathF.Floor((feet.Z + halfW - 1e-4f) * inv);
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        if (w.IsSolid(x, y, z)) return true;
            return false;
        }

        /// <summary>Verplaatst en corrigeert. Geeft terug of de voeten op de grond staan.</summary>
        public static bool Move(VoxelStore w, ref V3 pos, ref V3 vel, V3 delta, float halfW, float height, bool canStep)
        {
            bool grounded = false;
            // Y eerst
            if (delta.Y != 0)
            {
                var p = pos; p.Y += delta.Y;
                if (Overlaps(w, p, halfW, height))
                {
                    if (delta.Y < 0)
                    {
                        grounded = true;
                        float vs = World.VoxelSize;
                        p.Y = MathF.Floor(p.Y / vs) * vs + vs;     // op de bovenkant van de voxel eronder
                        if (Overlaps(w, p, halfW, height)) p.Y = pos.Y;
                    }
                    else p.Y = pos.Y;
                    vel.Y = 0;
                }
                pos = p;
            }
            // dan X en Z, met opstappen
            for (int axis = 0; axis < 2; axis++)
            {
                float d = axis == 0 ? delta.X : delta.Z;
                if (d == 0) continue;
                var p = pos;
                if (axis == 0) p.X += d; else p.Z += d;
                if (!Overlaps(w, p, halfW, height)) { pos = p; continue; }
                if (canStep)
                {
                    var up = p; up.Y += StepHeight;
                    var head = pos; head.Y += StepHeight;
                    if (!Overlaps(w, head, halfW, height) && !Overlaps(w, up, halfW, height))
                    {
                        // zak weer tot op de trede
                        float vs = World.VoxelSize;
                        up.Y = MathF.Floor(up.Y / vs) * vs;
                        while (!Overlaps(w, new V3(up.X, up.Y - 0.05f, up.Z), halfW, height) && up.Y > p.Y) up.Y -= 0.05f;
                        pos = up;
                        continue;
                    }
                }
                if (axis == 0) vel.X = 0; else vel.Z = 0;
            }
            if (!grounded)
            {
                var below = pos; below.Y -= 0.03f;
                grounded = vel.Y <= 0.01f && Overlaps(w, below, halfW, height);
            }
            return grounded;
        }
    }
}
