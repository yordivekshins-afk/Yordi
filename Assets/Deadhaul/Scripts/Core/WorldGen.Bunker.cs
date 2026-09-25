using System;
using System.Collections.Concurrent;

namespace Deadhaul.Core
{
    /// <summary>Een verlaten schuilkelder: betonnen hut boven, trap naar beneden, zaal met kamers eronder.</summary>
    public sealed class Bunker
    {
        public int X, Z;             // hoek van de toegangshut (voxels)
        public int Top;              // maaiveld bij de hut
        public int Floor;            // vloer van de bunker
        public int Seed;
        public int StairLen => Top - Floor;
        public int HallZ => Z + 1 + StairLen;                   // begin van de zaal
        public const int HallW = 17, HallD = 15, HutW = 5, HutD = 7;     // de hut overdekt het open deel van de trap

        /// <summary>Kleine omhullende doos (voxels) om snel te kunnen overslaan.</summary>
        public bool Touches(int x0, int z0, int x1, int z1) => !(X + HutW + 8 < x0 || X - 8 > x1 || Z - 1 > z1 || HallZ + HallD + 1 < z0);
    }

    public sealed partial class WorldGen
    {
        public const int BunkerCell = 896;          // 448 m
        readonly ConcurrentDictionary<long, Bunker> bunkers = new ConcurrentDictionary<long, Bunker>();
        static readonly Bunker NoBunker = new Bunker();

        public Bunker GetBunker(int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            var b = bunkers.GetOrAdd(key, k =>
            {
                if (Hash.H2(i, j, Seed ^ 0xb4c) > 0.5f) return NoBunker;
                int x = i * BunkerCell + 120 + (int)(Hash.H2(j, i, Seed ^ 0xb4d) * (BunkerCell - 240));
                int z = j * BunkerCell + 120 + (int)(Hash.H2(i * 3, j * 5, Seed ^ 0xb4e) * (BunkerCell - 240));
                // vlak genoeg, buiten steden, wegen, water en straling
                int lo = int.MaxValue, hi = int.MinValue;
                for (int dz = -2; dz <= Bunker.HutD + 1; dz += 2)
                    for (int dx = -2; dx <= Bunker.HutW + 1; dx += 2)
                    {
                        var c = GetColumn(x + dx, z + dz);
                        if (c.Kind != ColumnKind.Nature || c.RoadD <= HW + 12 || c.Rad > 0 || c.CityD < (c.City?.R ?? 0) + 60) return NoBunker;
                        lo = Math.Min(lo, c.H); hi = Math.Max(hi, c.H);
                    }
                if (hi - lo > 3 || lo <= World.Sea + 3 || hi > World.Sea + 40) return NoBunker;
                if (SettlementNear(x, z, out float sd) != null && sd < 60) return NoBunker;
                if (IsRiver(x, z) || OceanAt(x, z) > 0) return NoBunker;
                int top = GetColumn(x + 2, z + 2).H;
                return new Bunker { X = x, Z = z, Top = top, Floor = Math.Max(4, top - 14), Seed = i * 7919 + j };
            });
            return ReferenceEquals(b, NoBunker) ? null : b;
        }

        /// <summary>Ligt deze voxel in (of op) een bunker? Voor militaire loot.</summary>
        public Bunker BunkerAt(int x, int z)
        {
            for (int j = FloorDiv(z, BunkerCell) - 1; j <= FloorDiv(z, BunkerCell); j++)
                for (int i = FloorDiv(x, BunkerCell) - 1; i <= FloorDiv(x, BunkerCell); i++)
                {
                    var b = GetBunker(i, j);
                    if (b != null && b.Touches(x, z, x, z)) return b;
                }
            return null;
        }

        void WriteBunkers(ref ChunkWriter o)
        {
            const int P = World.Pad;
            for (int j = FloorDiv(o.Oz, BunkerCell) - 1; j <= FloorDiv(o.Oz + P, BunkerCell); j++)
                for (int i = FloorDiv(o.Ox, BunkerCell) - 1; i <= FloorDiv(o.Ox + P, BunkerCell); i++)
                {
                    var b = GetBunker(i, j);
                    if (b == null || !b.Touches(o.Ox, o.Oz, o.Ox + P - 1, o.Oz + P - 1)) continue;
                    for (int z = b.Z - 1; z <= b.HallZ + Bunker.HallD + 1; z++)
                        for (int x = b.X - 8; x <= b.X + Bunker.HutW + 8; x++)
                        {
                            if (x < o.Ox || z < o.Oz || x >= o.Ox + P || z >= o.Oz + P) continue;
                            for (int y = b.Floor - 1; y <= b.Top + 5; y++)
                            {
                                int v = BunkerBlock(b, x, y, z);
                                if (v >= 0) o.Set(x, y, z, (byte)v);
                            }
                        }
                }
        }

        /// <summary>Blok van de bunker op deze plek, of -1 als de bunker hier niets verandert.</summary>
        int BunkerBlock(Bunker b, int x, int y, int z)
        {
            int lx = x - b.X, lz = z - b.Z;
            int n = b.StairLen;
            // --- toegangshut boven de grond (5 × 5, deur op het zuiden)
            if (lx >= 0 && lx < Bunker.HutW && lz >= 0 && lz < Bunker.HutD && y > b.Top)
            {
                int ly = y - b.Top;
                if (ly > 5) return -1;
                bool wall = lx == 0 || lz == 0 || lx == Bunker.HutW - 1 || lz == Bunker.HutD - 1;
                if (ly == 5) return B.Concrete;
                if (lz == 0 && lx >= 1 && lx <= 3 && ly <= 4) return B.Air;            // deuropening
                if (wall) return ly == 2 && lx == Bunker.HutW - 1 && lz == 3 ? B.MetalWall : B.Concrete;
                return B.Air;
            }
            // --- trap naar beneden (3 breed), van de hut naar het noorden
            if (lx >= 0 && lx <= 4 && lz >= 1 && lz <= n + 1 && y <= b.Top + 5 && y >= b.Floor)
            {
                int k = lz - 1;                                 // traptree k ligt op hoogte Top - k
                int stepY = b.Top - k;
                bool side = lx == 0 || lx == 4;
                if (lz < Bunker.HutD && y > b.Top) return -1;   // binnen de hut: al gedaan
                if (side) return y >= stepY - 1 && y <= stepY + 5 && y <= b.Top ? B.Concrete : -1;
                if (y == stepY) return B.Concrete;
                if (y > stepY && y <= stepY + 4) return B.Air;
                if (y == stepY + 5 && y <= b.Top) return B.Concrete;          // plafond van de trapschacht
                return -1;
            }
            // --- zaal
            int hz = z - b.HallZ, hx = x - (b.X - 6);
            if (hz < -1 || hz > Bunker.HallD || hx < -1 || hx > Bunker.HallW || y < b.Floor - 1 || y > b.Floor + 5) return -1;
            bool shell = hz == -1 || hz == Bunker.HallD || hx == -1 || hx == Bunker.HallW || y == b.Floor - 1 || y == b.Floor + 5;
            if (shell)
            {
                // opening naar de trap
                if (hz == -1 && hx >= 7 && hx <= 9 && y >= b.Floor + 1 && y <= b.Floor + 4) return B.Air;
                if (y == b.Floor + 5 && hx % 4 == 2 && hz % 4 == 2) return B.Lamp;
                return B.Concrete;
            }
            int iy = y - b.Floor;
            if (iy == 0) return B.Tile;
            // tussenmuur met deur: achterin de wapenkamer
            if (hz == 10) return hx >= 7 && hx <= 9 && iy <= 4 ? B.Air : B.Concrete;
            if (iy == 1)
            {
                float r = Hash.H3(x, y, z, Seed ^ b.Seed);
                if (hz < 10)
                {
                    if (hx == 0 && hz % 3 == 1 && hz < 9) return B.Bed;                           // stapelbedden
                    if (hx == 16 && hz >= 1 && hz <= 8) return hz % 2 == 0 ? B.Shelf : B.Cabinet;   // voorraadrekken
                    if (hx >= 6 && hx <= 10 && hz >= 4 && hz <= 5) return B.Planks;                // tafel
                    if (hx == 14 && hz == 1) return B.Fridge;
                    if (hx == 1 && hz == 9) return B.Barrel;
                    if (hx == 2 && hz == 9) return r < 0.5f ? B.ExplosiveBarrel : B.Barrel;
                }
                else
                {
                    if ((hx == 0 || hx == 16) && hz >= 11 && hz <= 14) return B.AmmoCrate;         // wapenkamer
                    if (hz == 14 && hx >= 3 && hx <= 13 && hx % 2 == 1) return r < 0.6f ? B.Crate : B.AmmoCrate;
                    if (hx >= 6 && hx <= 10 && hz == 12) return B.Metal;                            // werkbank
                }
            }
            if (iy == 2 && hx == 0 && hz % 3 == 1 && hz < 9) return B.Bed;                         // bovenste kooi
            if (iy == 2 && hx == 16 && hz >= 1 && hz <= 8 && hz % 2 == 0) return B.Shelf;
            return B.Air;
        }
    }
}
