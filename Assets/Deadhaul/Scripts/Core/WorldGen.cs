using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    public sealed class City
    {
        public int I, J;
        public int X, Z;          // centrum in voxels
        public float R;           // straal van de kern in voxels
        public int Base;          // grondhoogte in voxels
        public string Name;
        public string Kind;       // capital, stad, dorp, gehucht
    }

    public enum ColumnKind : byte { Nature, Highway, Street, Sidewalk, Lot, Bridge, Settlement }

    public struct Column
    {
        public int H;
        public ColumnKind Kind;
        public int RoadD;
        public bool RoadNS;
        public int RoadH;
        public City City;
        public float CityD;
        public float Rad;           // straling 0..1 (kraters)
        public float CraterD;       // afstand tot het kratercentrum / straal (0 = midden, 1 = rand)
    }

    public sealed class Crater { public int X, Z; public float R; }

    public enum LotType { Park, Parkeerplaats, Ruine, Benzinestation, Apotheek, Politie, Winkel, Flat, Huis, Militair }

    public sealed class Lot
    {
        public LotType Type;
        public int Kx, Kz, X0, Z0, Base;
        public City City;
        public int W, D, Floors;
        public byte Wall;
        public int Bx, Bz;
        public float Damage;
        public int Front;
    }

    /// <summary>
    /// Procedurele wereld van Deadhaul: heuvels, meren, bossen, dorre vlaktes, verwoeste steden
    /// op een raster en de snelwegen ertussen ("de Haul"). Volledig deterministisch uit de seed,
    /// zodat een save alleen de verschillen hoeft te bewaren. Thread-safe.
    /// </summary>
    public sealed partial class WorldGen
    {
        public const int CityCell = 1344;  // stadscel in voxels (672 m); snelwegen lopen op de celgrenzen
        public const int Street = 48;      // bouwblok + straat (24 m)
        const int Half = CityCell / 2;
        const int HW = 6;                  // halve breedte van de snelweg

        static readonly string[] SylA = { "Rot", "As", "Grauw", "Zwart", "Kraai", "Stil", "Dor", "Roest", "IJzer", "Mist", "Brand", "Lood", "Kolen", "Wolf", "Doorn", "Koud", "Veen", "Schor" };
        static readonly string[] SylB = { "vaart", "dam", "hoven", "veld", "broek", "kerke", "haven", "wijk", "dijk", "rade", "mond", "heim", "burg", "loo", "berg", "poort", "sluis", "stede" };

        public readonly int Seed;
        readonly Simplex n1, n2, n3, n4;
        readonly ConcurrentDictionary<long, City> cities = new ConcurrentDictionary<long, City>();
        static readonly City NoCity = new City();

        public WorldGen(int seed)
        {
            Seed = seed;
            n1 = new Simplex(seed);
            n2 = new Simplex(seed + 101);
            n3 = new Simplex(seed + 202);
            n4 = new Simplex(seed + 303);
            InitWater();
        }

        static float Smooth(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3 - 2 * t); }
        static int Mod(int a, int n) => World.Mod(a, n);
        static int FloorDiv(int a, int b) => World.FloorDiv(a, b);

        // ------------------------------------------------------------ terrein
        public float BaseHeight(float x, float z)
        {
            float cont = n1.Fbm2(x / 1100f, z / 1100f, 3);
            float hills = n2.Fbm2(x / 260f, z / 260f, 3);
            float h = World.Sea + 5 + cont * 16 + hills * 9 * (0.55f + 0.45f * Math.Max(0, cont + 0.3f));
            if (cont > 0.25f)
            {
                float r = 1 - Math.Abs(n3.Noise2(x / 380f, z / 380f));
                h += r * r * r * 34 * Math.Min(1, (cont - 0.25f) * 2.5f);
            }
            return ApplySea(x, z, h);
        }

        public float RawHeight(int x, int z) =>
            BaseHeight(x, z) + n4.Noise2(x / 34f, z / 34f) * 1.6f + n4.Noise2(x / 9f, z / 9f) * 0.5f;

        public float Moisture(int x, int z) => n3.Fbm2(x / 520f + 40, z / 520f - 20, 2);

        public enum Biome : byte { Vlakte, Bos, Dor, Woestijn, Moeras, Hoogland }

        /// <summary>Biotoop op deze plek, uit vocht en hoogte.</summary>
        public Biome BiomeAt(int x, int z, int h, float m)
        {
            if (h > World.Sea + 34) return Biome.Hoogland;
            if (m < -0.42f) return Biome.Woestijn;
            if (m < -0.22f) return Biome.Dor;
            if (m > 0.38f && h < World.Sea + 7) return Biome.Moeras;
            if (m > 0.18f) return Biome.Bos;
            return Biome.Vlakte;
        }

        public City GetCity(int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            var c = cities.GetOrAdd(key, _ => MakeCity(i, j) ?? NoCity);
            return ReferenceEquals(c, NoCity) ? null : c;
        }

        City MakeCity(int i, int j)
        {
            float h = Hash.H2(i, j, Seed ^ 0x5eed);
            int cx = i * CityCell + Half, cz = j * CityCell + Half;
            bool start = i == 0 && j == 0;
            if (!(h < 0.7f || start)) return null;
            float bh = BaseHeight(cx, cz);
            if (!start && (bh <= World.Sea + 1 || bh >= World.Sea + 34 || OceanAt(cx, cz) > 0.02f)) return null;
            float size = start ? 0.72f : Hash.H2(i, j, Seed ^ 0xc17);
            float R = 110 + MathF.Pow(size, 1.5f) * 420;
            string name = SylA[(int)(Hash.H2(i, j, 7) * SylA.Length)] + SylB[(int)(Hash.H2(j, i, 9) * SylB.Length)];
            return new City
            {
                I = i, J = j, X = cx, Z = cz, R = R, Base = Math.Max(World.Sea + 3, (int)MathF.Round(bh)), Name = name,
                Kind = R > 400 ? "capital" : R > 250 ? "stad" : R > 160 ? "dorp" : "gehucht"
            };
        }

        /// <summary>Stad waar (x,z) in ligt, met afstand tot het centrum en invloed f (1 = kern).</summary>
        public City CityAt(int x, int z, out float d, out float f)
        {
            d = 1e9f; f = 0;
            var c = GetCity(FloorDiv(x, CityCell), FloorDiv(z, CityCell));
            if (c == null) return null;
            d = MathF.Sqrt((float)(x - c.X) * (x - c.X) + (float)(z - c.Z) * (z - c.Z));
            if (d >= c.R + 70) return null;
            f = 1 - Smooth((d - c.R) / 70f);
            return c;
        }

        // ------------------------------------------------------------ kraters
        public const int CraterCell = CityCell * 3;
        readonly ConcurrentDictionary<long, Crater> craters = new ConcurrentDictionary<long, Crater>();
        static readonly Crater NoCrater = new Crater();

        public Crater GetCrater(int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            var c = craters.GetOrAdd(key, _ =>
            {
                if (i == 0 && j == 0) return NoCrater;                 // de startregio blijft gespaard
                if (Hash.H2(i, j, Seed ^ 0xb0b) > 0.55f) return NoCrater;
                int x = i * CraterCell + 600 + (int)(Hash.H2(i, j, Seed ^ 0xb1) * (CraterCell - 1200));
                int z = j * CraterCell + 600 + (int)(Hash.H2(j, i, Seed ^ 0xb2) * (CraterCell - 1200));
                return new Crater { X = x, Z = z, R = 90 + Hash.H2(i, j, Seed ^ 0xb3) * 110 };
            });
            return ReferenceEquals(c, NoCrater) ? null : c;
        }

        /// <summary>Straling op deze plek (0..1) en de relatieve afstand tot het dichtstbijzijnde kratercentrum.</summary>
        public float RadiationAt(int x, int z, out float rel)
        {
            rel = 99;
            var c = GetCrater(FloorDiv(x, CraterCell), FloorDiv(z, CraterCell));
            if (c == null) return 0;
            float d = MathF.Sqrt((float)(x - c.X) * (x - c.X) + (float)(z - c.Z) * (z - c.Z));
            rel = d / c.R;
            float t = 1 - d / (c.R * 1.9f);
            return t <= 0 ? 0 : MathF.Pow(t, 1.4f);
        }

        // ------------------------------------------------------------ nederzettingen
        readonly ConcurrentDictionary<long, Settlement> settlements = new ConcurrentDictionary<long, Settlement>();
        static readonly Settlement NoSettlement = new Settlement();

        /// <summary>Nederzetting halverwege twee steden, net ten noorden van de oost-westsnelweg.</summary>
        public Settlement GetSettlement(int i, int j)
        {
            long key = ((long)i << 32) ^ (uint)j;
            var st = settlements.GetOrAdd(key, k =>
            {
                if (Hash.H2(i, j, Seed ^ 0x5e77) > 0.68f && !(i == 0 && j == 0)) return NoSettlement;
                int x0 = i * CityCell - Settlement.W / 2, z0 = j * CityCell + Half + HW + 8;
                int cx = x0 + Settlement.W / 2, cz = z0 + Settlement.D / 2;
                if (RadiationAt(cx, cz, out _) > 0) return NoSettlement;
                if (CityAt(cx, cz, out _, out _) != null) return NoSettlement;
                float bh = BaseHeight(cx, cz);
                if (bh < World.Sea + 2 || bh > World.Sea + 32 || OceanAt(cx, cz) > 0.02f) return NoSettlement;
                return Settlement.Layout(i, j, x0, z0, (int)MathF.Round(Math.Max(World.Sea + 3, bh)), Seed);
            });
            return ReferenceEquals(st, NoSettlement) ? null : st;
        }

        /// <summary>De nederzetting waar (x,z) in of vlak naast ligt; dist = afstand buiten de rand (0 = binnen).</summary>
        public Settlement SettlementNear(int x, int z, out float dist)
        {
            dist = 1e9f;
            int i = FloorDiv(x + CityCell / 2, CityCell), j = FloorDiv(z - Half, CityCell);
            var st = GetSettlement(i, j);
            if (st == null) return null;
            float dx = Math.Max(0, Math.Max(st.X0 - x, x - (st.X0 + Settlement.W - 1)));
            float dz = Math.Max(0, Math.Max(st.Z0 - z, z - (st.Z0 + Settlement.D - 1)));
            dist = MathF.Sqrt(dx * dx + dz * dz);
            return dist < 20 ? st : null;
        }

        /// <summary>Afstand tot de dichtstbijzijnde snelweglijn.</summary>
        public int RoadAt(int x, int z, out bool ns, out int line)
        {
            int mx = Mod(x - Half, CityCell), mz = Mod(z - Half, CityCell);
            int dx = Math.Min(mx, CityCell - mx), dz = Math.Min(mz, CityCell - mz);
            if (dx <= dz) { ns = true; line = x - (mx <= Half ? mx : mx - CityCell); return dx; }
            ns = false; line = z - (mz <= Half ? mz : mz - CityCell); return dz;
        }

        public int RoadHeight(int x, int z, bool ns)
        {
            float h = ns ? BaseHeight(x, MathF.Round(z / 6f) * 6) : BaseHeight(MathF.Round(x / 6f) * 6, z);
            return Math.Max(World.Sea + 3, (int)MathF.Round(h));
        }

        public Column GetColumn(int x, int z)
        {
            float h = RawHeight(x, z);
            float mo = Moisture(x, z);
            if (mo < -0.42f && h > World.Sea + 2)
            {
                // duinen: lange ruggen in de windrichting
                float dune = MathF.Abs(n2.Noise2(x / 26f + n4.Noise2(x / 90f, z / 90f) * 0.8f, z / 70f));
                h += (1 - dune) * 5f * Math.Min(1, (-0.42f - mo) * 8f);
            }
            h = ApplyRiver(x, z, h);
            var col = new Column { Kind = ColumnKind.Nature, RoadH = -1, CityD = 1e9f };
            var city = CityAt(x, z, out float cd, out float cf);
            if (city != null) { h += (city.Base - h) * cf; col.City = city; col.CityD = cd; }
            int rd = RoadAt(x, z, out bool ns, out int line);
            if (rd <= HW + 10 && OceanAt(x, z) > 0.45f) rd = 999;          // de Haul eindigt aan de kust
            col.RoadD = rd; col.RoadNS = ns;
            if (rd <= HW + 10)
            {
                int roadH = ns ? RoadHeight(line, z, true) : RoadHeight(x, line, false);
                if (city != null) roadH = (int)MathF.Round(roadH + (city.Base - roadH) * cf);
                col.RoadH = roadH;
                if (rd <= HW)
                {
                    if (h < roadH - 3) col.Kind = ColumnKind.Bridge;
                    else { h = roadH; col.Kind = ColumnKind.Highway; }
                }
                else
                {
                    float t = Smooth((rd - HW) / 10f);
                    if (h >= roadH - 3) h = roadH + (h - roadH) * t;
                }
            }
            if (city != null && cd < city.R && col.Kind == ColumnKind.Nature)
            {
                int sx = Mod(x + 4, Street), sz = Mod(z + 4, Street);
                if (sx < 8 || sz < 8) col.Kind = ColumnKind.Street;
                else if (sx < 10 || sx >= 46 || sz < 10 || sz >= 46) col.Kind = ColumnKind.Sidewalk;
                else col.Kind = ColumnKind.Lot;
                h = city.Base;
            }
            var settle = SettlementNear(x, z, out float sd);
            if (settle != null && col.Kind != ColumnKind.Highway && col.Kind != ColumnKind.Bridge)
            {
                float sf = 1 - Smooth(sd / 18f);
                h += (settle.Base - h) * sf;
                if (sd <= 0) { col.Kind = ColumnKind.Settlement; h = settle.Base; }
            }
            float rad = RadiationAt(x, z, out float rel);
            col.Rad = rad; col.CraterD = rel;
            if (rel < 1.5f && col.Kind != ColumnKind.Highway && col.Kind != ColumnKind.Bridge)
            {
                var cr = GetCrater(FloorDiv(x, CraterCell), FloorDiv(z, CraterCell));
                float depth = cr.R * 0.1f;
                if (rel < 1f) h -= depth * (1 - rel * rel);
                float rim = 1 - MathF.Abs(rel - 1.08f) / 0.3f;
                if (rim > 0) h += rim * rim * 5f;
                h += n4.Noise2(x / 6f, z / 6f) * 1.2f * Math.Max(0, 1.2f - rel);
            }
            col.H = (int)MathF.Round(h);
            return col;
        }

        // ------------------------------------------------------------ kavels
        public Lot GetLot(int kx, int kz)
        {
            int x0 = kx * Street + 6, z0 = kz * Street + 6;
            var c = CityAt(x0 + 18, z0 + 18, out float d, out _);
            if (c == null) return null;
            // de kavel moet in de stadskern liggen (snelwegen vallen altijd samen met een straat)
            if (d > c.R - 20) return null;
            float r = Hash.H2(kx, kz, Seed ^ 0x107);
            float r2 = Hash.H2(kz, kx, Seed ^ 0x2b1);
            float centre = 1 - d / c.R;
            LotType type;
            if (r < 0.08f) type = LotType.Park;
            else if (r < 0.15f) type = LotType.Parkeerplaats;
            else if (r < 0.2f) type = LotType.Ruine;
            else if (r < 0.25f) type = LotType.Benzinestation;
            else if (r < 0.31f) type = LotType.Apotheek;
            else if (r < 0.36f) type = LotType.Politie;
            else if (r < 0.39f && (c.Kind == "capital" || c.Kind == "stad")) type = LotType.Militair;
            else if (r < 0.52f) type = LotType.Winkel;
            else if (r < 0.52f + 0.35f * centre + (c.Kind == "capital" ? 0.12f : 0)) type = LotType.Flat;
            else type = LotType.Huis;
            var s = new Lot { Type = type, Kx = kx, Kz = kz, X0 = x0, Z0 = z0, Base = c.Base, City = c };
            float rw = Hash.H2(kx * 3, kz * 7, Seed), rdd = Hash.H2(kx * 5, kz * 11, Seed);
            switch (type)
            {
                case LotType.Flat:
                    s.W = 20 + (int)(rw * 8) * 2; s.D = 20 + (int)(rdd * 8) * 2;
                    s.Floors = 3 + (int)MathF.Round((centre * 0.8f + r2 * 0.5f) * (c.Kind == "capital" ? 9 : 5));
                    s.Wall = r2 < 0.5f ? B.Concrete : B.Brick;
                    break;
                case LotType.Huis:
                    s.W = 14 + (int)(rw * 5) * 2; s.D = 14 + (int)(rdd * 4) * 2; s.Floors = r2 < 0.6f ? 1 : 2;
                    s.Wall = r2 < 0.3f ? B.Brick : r2 < 0.7f ? B.Plaster : B.Planks;
                    break;
                case LotType.Winkel:
                case LotType.Apotheek:
                case LotType.Politie:
                    s.W = 22 + (int)(rw * 6) * 2; s.D = 18 + (int)(rdd * 6) * 2;
                    s.Floors = type == LotType.Politie ? 2 : 1 + (r2 < 0.4f ? 1 : 0);
                    s.Wall = type == LotType.Politie ? B.Concrete : r2 < 0.5f ? B.Brick : B.Plaster;
                    break;
                case LotType.Benzinestation:
                    s.W = 14; s.D = 12; s.Floors = 1; s.Wall = B.Plaster;
                    break;
                default:
                    s.W = 36; s.D = 36; s.Floors = 0; s.Wall = B.Concrete;
                    break;
            }
            s.W = Math.Min(s.W, 36); s.D = Math.Min(s.D, 36);
            s.Bx = x0 + ((36 - s.W) >> 1); s.Bz = z0 + ((36 - s.D) >> 1);
            if (type == LotType.Benzinestation) { s.Bx = x0 + 20; s.Bz = z0 + 22; }
            s.Damage = MathF.Pow(Hash.H2(kx * 13, kz * 17, Seed ^ 0xda), 1.6f);
            float lotRad = RadiationAt(x0 + 18, z0 + 18, out _);
            if (lotRad > 0) s.Damage = Math.Min(1f, Math.Max(s.Damage, lotRad * 1.4f));
            s.Front = (int)(Hash.H2(kx, kz * 3, Seed ^ 0xf0) * 4);
            return s;
        }

        public Lot LotAtVoxel(int x, int z) => GetLot(FloorDiv(x - 4, Street), FloorDiv(z - 4, Street));

        static bool IsShop(Lot s) => s.Type == LotType.Winkel || s.Type == LotType.Apotheek;

        /// <summary>Blok van het gebouw op deze plek, of -1 als het gebouw hier niets plaatst.</summary>
        public int BuildingBlock(Lot s, int x, int y, int z)
        {
            int lx = x - s.Bx, lz = z - s.Bz, w = s.W, d = s.D;
            if (lx < 0 || lz < 0 || lx >= w || lz >= d) return -1;
            int ly = y - s.Base;
            const int FH = 6;
            int top = s.Floors * FH;
            bool edge = lx == 0 || lz == 0 || lx == w - 1 || lz == d - 1;
            bool corner = (lx == 0 || lx == w - 1) && (lz == 0 || lz == d - 1);
            if (ly < 0) return -1;

            if (s.Type == LotType.Huis)
            {
                if (ly > top)
                {
                    int k = ly - top;
                    int rh = Math.Min(lz, d - 1 - lz) + 1;
                    if (k > rh) return -1;
                    if (k == rh) return B.Roof;
                    if (lx == 0 || lx == w - 1) return s.Wall;
                    return B.Air;
                }
            }
            else if (ly > top)
            {
                if (ly == top + 1 && edge && s.Floors > 0) return s.Wall;
                return -1;
            }

            // schade: gaten in gevels en vloeren, bovenaan het meest
            if (ly > 0 && s.Damage > 0.25f)
            {
                float n = n4.Noise3(x / 7f, y / 6f, z / 7f);
                if (n + (ly / (float)Math.Max(1, top)) * s.Damage * 0.9f > 1.05f - s.Damage * 0.55f) return B.Air;
            }

            int fl = ly / FH, r = ly - fl * FH;
            bool stairs = w >= 12 && lx >= 1 && lx <= 6 && lz >= 1 && lz <= 2 && s.Floors > 1;
            if (stairs && r > 0 && fl < s.Floors - (s.Type == LotType.Huis ? 1 : 0)) return r <= lx ? B.Concrete : B.Air;
            if (r == 0)
            {
                if (stairs && ly > 0 && lx <= 5) return B.Air;
                if (ly == 0)
                {
                    if (edge) return s.Wall;
                    if (s.Type == LotType.Flat || s.Type == LotType.Huis) return (lx + lz) % 7 != 0 ? B.Planks : B.Carpet;
                    return B.Tile;
                }
                return B.Concrete;
            }
            if (edge)
            {
                int along = (lx == 0 || lx == w - 1) ? lz : lx;
                int side = lz == 0 ? 0 : lx == w - 1 ? 1 : lz == d - 1 ? 2 : 3;
                int mid = (side == 0 || side == 2) ? w >> 1 : d >> 1;
                if (fl == 0 && side == s.Front && Math.Abs(along - mid + 0.5f) < 1.6f && r <= 4) return B.Air;
                bool shopFront = fl == 0 && side == s.Front && IsShop(s);
                int sideLen = (side % 2 == 1) ? d : w;
                if (!corner && ((r >= 2 && r <= 4 && (along % 4 == 1 || along % 4 == 2)) || (shopFront && r >= 1 && r <= 4 && along > 1 && along < sideLen - 2)))
                    return Hash.H3(x, y, z, Seed) < 0.55f ? B.Air : B.Glass;
                return s.Wall;
            }
            // interieur
            bool shop = IsShop(s);
            if (r == 1 && !shop && s.Type != LotType.Politie && lz > 3)
            {
                bool againstWall = lx == 1 || lx == w - 2 || lz == d - 2;
                float fh = Hash.H3(lx * 3 + fl, fl * 7, lz * 11, Seed ^ 0xfa);
                if (againstWall && fh < 0.07f) return B.Cabinet;
                if (againstWall && fh > 0.985f && (s.Type == LotType.Huis || s.Type == LotType.Flat)) return B.Fridge;
            }
            if (r == 2 && !shop && lz > 3)
            {
                bool againstWall = lx == 1 || lx == w - 2 || lz == d - 2;
                float fh = Hash.H3(lx * 3 + fl, fl * 7, lz * 11, Seed ^ 0xfa);
                if (againstWall && fh > 0.985f && (s.Type == LotType.Huis || s.Type == LotType.Flat)) return B.Fridge;
            }
            if (r == 1)
            {
                float h = Hash.H3(lx * 7 + fl, fl, lz * 5, Seed ^ 0xc4);
                bool inner = lx > 8 && lz > 3 && lx < w - 2 && lz < d - 2;
                if (inner && h < (shop ? 0.035f : 0.012f)) return shop ? B.Shelf : B.Crate;
            }
            if (shop && fl == 0 && (r == 1 || r == 2) && lz % 5 == 0 && lx > 8 && lx < w - 3 && lz > 3 && lz < d - 3) return B.Shelf;
            if (w > 16 && lx == (w >> 1) + 2 && !(lz >= (d >> 1) - 1 && lz <= (d >> 1) + 1 && r <= 4))
                return s.Wall == B.Planks ? B.Planks : B.Plaster;
            return B.Air;
        }

        // ------------------------------------------------------------ chunk
        /// <summary>
        /// Genereert een chunk inclusief een rand van 1 voxel (Pad × Height × Pad),
        /// zodat de mesher de buren kent zonder ze te laden.
        /// </summary>
        public byte[] GenerateChunk(int cx, int cz)
        {
            var o = new ChunkWriter(new byte[World.PadVolume], cx * World.ChunkSize - 1, cz * World.ChunkSize - 1);
            const int P = World.Pad;
            var cols = new Column[P * P];
            var heights = new int[(P + 2) * (P + 2)];
            for (int pz = -1; pz <= P; pz++)
                for (int px = -1; px <= P; px++)
                {
                    var c = GetColumn(o.Ox + px, o.Oz + pz);
                    heights[(px + 1) + (pz + 1) * (P + 2)] = c.H;
                    if (px >= 0 && pz >= 0 && px < P && pz < P) cols[px + pz * P] = c;
                }

            // 1. terrein
            for (int pz = 0; pz < P; pz++)
                for (int px = 0; px < P; px++)
                {
                    int x = o.Ox + px, z = o.Oz + pz;
                    var c = cols[px + pz * P];
                    int h = Math.Clamp(c.H, 1, World.Height - 20);
                    int hi = (px + 1) + (pz + 1) * (P + 2);
                    int slope = Math.Max(Math.Abs(heights[hi + 1] - heights[hi - 1]), Math.Abs(heights[hi + P + 2] - heights[hi - P - 2]));
                    float m = Moisture(x, z);
                    byte top;
                    switch (c.Kind)
                    {
                        case ColumnKind.Highway:
                        {
                            int along = c.RoadNS ? z : x;
                            top = c.RoadD == 0 && Mod(along, 10) < 5 ? B.RoadLine : (c.RoadD == HW ? B.RoadLine : B.Asphalt);
                            if (Hash.H2(x, z, Seed ^ 0xa5) < 0.03f) top = B.Gravel;
                            break;
                        }
                        case ColumnKind.Street:
                        {
                            int sx = Mod(x + 4, Street), sz = Mod(z + 4, Street);
                            top = ((sx == 4 && Mod(z, 8) < 4 && sz >= 8) || (sz == 4 && Mod(x, 8) < 4 && sx >= 8)) ? B.RoadLine : B.Asphalt;
                            if (Hash.H2(x, z, Seed ^ 0xa6) < 0.05f) top = B.Gravel;
                            break;
                        }
                        case ColumnKind.Sidewalk: top = B.Sidewalk; break;
                        case ColumnKind.Lot: top = c.Rad > 0.3f ? B.Ash : m < -0.2f ? B.DeadGrass : B.Grass; break;
                        case ColumnKind.Bridge: top = B.Stone; break;
                        case ColumnKind.Settlement: top = Hash.H2(x, z, Seed ^ 0x5e) < 0.25f ? B.Dirt : B.Grass; break;
                        default:
                        {
                            var biome = BiomeAt(x, z, h, m);
                            if (h <= World.Sea + 1) top = h < World.Sea - 2 ? B.Gravel : biome == Biome.Moeras ? B.Mud : B.Sand;
                            else if (biome == Biome.Hoogland) top = slope > 5 ? B.Stone : B.Snow;
                            else if (slope > 4 || h > World.Sea + 44) top = B.Stone;
                            else if (biome == Biome.Woestijn) top = Hash.H2(x, z, Seed ^ 3) < 0.05f ? B.Gravel : B.Sand;
                            else if (biome == Biome.Dor) top = Hash.H2(x, z, Seed ^ 3) < 0.08f ? B.Gravel : B.DeadGrass;
                            else if (biome == Biome.Moeras) top = Hash.H2(x, z, Seed ^ 7) < 0.3f ? B.Moss : B.Mud;
                            else top = Hash.H2(x, z, Seed ^ 4) < 0.02f ? B.Moss : B.Grass;
                            if (c.Rad > 0.12f && h > World.Sea + 1)
                            {
                                float n = Hash.H2(x, z, Seed ^ 0xa51);
                                if (c.Rad > 0.55f) top = n < 0.5f ? B.Scorched : B.Ash;
                                else if (n < c.Rad * 1.6f) top = n < c.Rad ? B.Ash : B.DeadGrass;
                            }
                            break;
                        }
                    }
                    int b = World.IdxPad(px, 0, pz);
                    var a = o.Data;
                    a[b] = B.Bedrock;
                    int stoneTop = h - 3 - (int)(Hash.H2(x, z, Seed ^ 9) * 2);
                    byte under = top == B.Sand ? B.Sand : B.Dirt;
                    for (int y = 1; y < h; y++) a[b + y] = y < stoneTop ? B.Stone : under;
                    a[b + h] = top;
                    for (int y = h + 1; y <= World.Sea; y++) a[b + y] = B.Water;
                    // grotten: tunnels waar twee ruisvelden allebei bijna nul zijn (alleen onder heuvels)
                    if (c.Kind == ColumnKind.Nature && h > World.Sea + 5)
                    {
                        for (int y = World.Sea + 2; y <= h; y++)
                        {
                            float n1v = n1.Noise3(x / 24f, y / 13f, z / 24f), n2v = n2.Noise3(x / 24f + 71, y / 13f, z / 24f - 33);
                            if (n1v * n1v + n2v * n2v < 0.011f) a[b + y] = y == h ? (Hash.H2(x, z, Seed) < 0.5f ? B.Air : top) : B.Air;
                        }
                    }
                    if (c.Kind == ColumnKind.Bridge)
                    {
                        int along = c.RoadNS ? z : x;
                        int deck = c.RoadH;
                        a[b + deck] = c.RoadD == 0 && Mod(along, 10) < 5 ? B.RoadLine : B.Asphalt;
                        a[b + deck - 1] = B.Concrete;
                        if (c.RoadD == HW) a[b + deck + 1] = B.Fence;
                        if (Mod(along, 24) < 2 && c.RoadD <= 4) for (int y = h; y < deck - 1; y++) a[b + y] = B.Concrete;
                    }
                }

            // 2. gebouwen
            int kx0 = FloorDiv(o.Ox - 4, Street) - 1, kx1 = FloorDiv(o.Ox + P - 4, Street) + 1;
            int kz0 = FloorDiv(o.Oz - 4, Street) - 1, kz1 = FloorDiv(o.Oz + P - 4, Street) + 1;
            for (int kz = kz0; kz <= kz1; kz++)
                for (int kx = kx0; kx <= kx1; kx++)
                {
                    var s = GetLot(kx, kz);
                    if (s != null) WriteLot(s, ref o);
                }

            // 2b. nederzettingen
            {
                var seen = new HashSet<Settlement>();
                for (int k = 0; k < 4; k++)
                {
                    int sx = k % 2 == 0 ? o.Ox : o.Ox + P - 1, sz = k < 2 ? o.Oz : o.Oz + P - 1;
                    var st = SettlementNear(sx, sz, out float sdist);
                    if (st == null || !seen.Add(st)) continue;
                    WriteSettlement(st, ref o);
                }
            }

            // 2c. het Stille Eiland en bunkers
            WriteStille(ref o);
            WriteBunkers(ref o);

            // 3. straatlantaarns
            for (int pz = 0; pz < P; pz++)
                for (int px = 0; px < P; px++)
                {
                    int x = o.Ox + px, z = o.Oz + pz;
                    var c = cols[px + pz * P];
                    if (c.Kind != ColumnKind.Sidewalk) continue;
                    int sx = Mod(x + 4, Street), sz = Mod(z + 4, Street);
                    if (((sx == 9 || sx == 46) && Mod(z, 24) == 5 && sz >= 10 && sz < 46) || ((sz == 9 || sz == 46) && Mod(x, 24) == 5 && sx >= 10 && sx < 46))
                    {
                        float bh = Hash.H2(x, z, Seed ^ 0xb1b);
                        if (bh < 0.5f) o.Set(x, c.H + 1, z, B.Bin);
                        else if (bh < 0.56f) o.Set(x, c.H + 1, z, B.ExplosiveBarrel);
                    }
                    if (((sx == 9 || sx == 46) && Mod(z, 24) == 12 && sz >= 10 && sz < 46) || ((sz == 9 || sz == 46) && Mod(x, 24) == 12 && sx >= 10 && sx < 46))
                    {
                        bool broken = Hash.H2(x, z, Seed ^ 0x1a) < 0.35f;
                        int hh = broken ? 3 + (int)(Hash.H2(z, x, Seed) * 3) : 9;
                        for (int y = 1; y <= hh; y++) o.Set(x, c.H + y, z, B.Metal);
                        if (!broken) o.Set(x, c.H + 9, z, Hash.H2(x, z, Seed ^ 0x1b) < 0.4f ? B.Lamp : B.Metal);
                    }
                }

            // 4. autowrakken
            WriteCars(ref o, cols);

            // 5. bomen
            const int TC = 7;
            int tx0 = FloorDiv(o.Ox - 6, TC), tx1 = FloorDiv(o.Ox + P + 6, TC);
            int tz0 = FloorDiv(o.Oz - 6, TC), tz1 = FloorDiv(o.Oz + P + 6, TC);
            for (int tz = tz0; tz <= tz1; tz++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    float r = Hash.H2(tx, tz, Seed ^ 0x7ee);
                    int x = tx * TC + (int)(Hash.H2(tx, tz, Seed ^ 1) * (TC - 2)) + 1;
                    int z = tz * TC + (int)(Hash.H2(tx, tz, Seed ^ 2) * (TC - 2)) + 1;
                    float m = Moisture(x, z);
                    float dens = m > 0.38f ? 0.35f : m > 0.18f ? 0.75f : m < -0.42f ? 0.012f : m < -0.22f ? 0.07f : 0.14f;
                    if (r > dens) continue;
                    var c = GetColumn(x, z);
                    if (c.H > World.Sea + 34) continue;      // boven de boomgrens
                    bool allowed = c.Kind == ColumnKind.Nature;
                    if (c.Kind == ColumnKind.Lot) { var s = LotAtVoxel(x, z); allowed = s != null && s.Type == LotType.Park; }
                    if (!allowed || c.H <= World.Sea + 1 || c.H > World.Sea + 46) continue;
                    if (c.Rad > 0.6f) continue;
                    if (c.Kind == ColumnKind.Nature && c.RoadD <= HW + 3) continue;
                    if (BunkerAt(x, z) != null) continue;
                    int kind = m < -0.22f || c.Rad > 0.12f ? 0 : (c.H > World.Sea + 26 || Hash.H2(tx, tz, Seed ^ 5) < 0.3f) ? 1 : 2;
                    WriteTree(kind, x, c.H + 1, z, Hash.H2(tz, tx, Seed ^ 6), ref o);
                }

            // 6. stralingskristallen in kraters
            for (int pz = 0; pz < P; pz++)
                for (int px = 0; px < P; px++)
                {
                    var c = cols[px + pz * P];
                    if (c.CraterD > 0.95f) continue;
                    int x = o.Ox + px, z = o.Oz + pz;
                    if (Hash.H2(x, z, Seed ^ 0xc75) > 0.006f) continue;
                    int hh = 1 + (int)(Hash.H2(z, x, Seed ^ 0xc76) * 4);
                    int top = Math.Max(c.H, World.Sea);
                    for (int y = 1; y <= hh; y++) o.Set(x, top + y, z, B.RadCrystal);
                    if (hh > 2) { o.Set(x + 1, top + 1, z, B.RadCrystal); o.Set(x, top + 1, z - 1, B.RadCrystal); }
                }

            // 6b. grotvloeren: gloeizwammen en een enkele vergeten kist
            for (int pz = 1; pz < P - 1; pz++)
                for (int px = 1; px < P - 1; px++)
                {
                    var c = cols[px + pz * P];
                    if (c.Kind != ColumnKind.Nature || c.H <= World.Sea + 5) continue;
                    int x = o.Ox + px, z = o.Oz + pz;
                    var a = o.Data;
                    for (int y = World.Sea + 2; y < c.H - 2; y++)
                    {
                        int i = World.IdxPad(px, y, pz);
                        if (a[i] != B.Air || !Blocks.Solid[a[i - 1]] || a[i + 1] != B.Air) continue;
                        float hsh = Hash.H3(x, y, z, Seed ^ 0xca5);
                        if (hsh < 0.035f) a[i] = B.Mushroom;
                        else if (hsh > 0.9994f) a[i] = B.Crate;
                    }
                }

            // 7. planten en losse stenen
            for (int pz = 0; pz < P; pz++)
                for (int px = 0; px < P; px++)
                {
                    int x = o.Ox + px, z = o.Oz + pz;
                    var c = cols[px + pz * P];
                    if (c.Kind != ColumnKind.Nature || c.H <= World.Sea + 1 || c.H >= World.Height - 2 || c.Rad > 0.3f) continue;
                    int i = World.IdxPad(px, c.H, pz);
                    var a = o.Data;
                    if ((a[i] == B.Grass || a[i] == B.DeadGrass) && a[i + 1] == B.Air && Hash.H2(x, z, Seed ^ 0xc0) < 0.004f) a[i + 1] = B.Crop;
                    else if (a[i] == B.Grass && a[i + 1] == B.Air && Hash.H2(x, z, Seed ^ 0xc1) < 0.0015f) a[i + 1] = B.Rubble;
                }
            return o.Data;
        }

        /// <summary>Schrijft voxels in een gepadde chunkbuffer, met grenscontrole.</summary>
        public struct ChunkWriter
        {
            public readonly byte[] Data;
            public readonly int Ox, Oz;
            public ChunkWriter(byte[] data, int ox, int oz) { Data = data; Ox = ox; Oz = oz; }

            public void Set(int x, int y, int z, byte b)
            {
                int px = x - Ox, pz = z - Oz;
                if ((uint)px >= World.Pad || (uint)pz >= World.Pad || (uint)y >= World.Height) return;
                Data[World.IdxPad(px, y, pz)] = b;
            }

            public byte Get(int x, int y, int z)
            {
                int px = x - Ox, pz = z - Oz;
                if ((uint)px >= World.Pad || (uint)pz >= World.Pad || (uint)y >= World.Height) return 0;
                return Data[World.IdxPad(px, y, pz)];
            }
        }

        void WriteLot(Lot s, ref ChunkWriter o)
        {
            const int P = World.Pad;
            int x0 = Math.Max(s.X0 - 2, o.Ox), x1 = Math.Min(s.X0 + 38, o.Ox + P - 1);
            int z0 = Math.Max(s.Z0 - 2, o.Oz), z1 = Math.Min(s.Z0 + 38, o.Oz + P - 1);
            if (x0 > x1 || z0 > z1) return;
            int bse = s.Base;
            switch (s.Type)
            {
                case LotType.Park:
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int lx = x - s.X0, lz = z - s.Z0;
                            if (lx < 0 || lz < 0 || lx >= 36 || lz >= 36) continue;
                            if ((lx == 0 || lz == 0 || lx == 35 || lz == 35) && Hash.H2(x, z, Seed) < 0.8f) o.Set(x, bse + 1, z, B.Fence);
                            if (Math.Abs(lx - 17.5f) < 1.5f || Math.Abs(lz - 17.5f) < 1.5f) o.Set(x, bse, z, B.Gravel);
                        }
                    return;
                case LotType.Parkeerplaats:
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int lx = x - s.X0, lz = z - s.Z0;
                            if (lx < 0 || lz < 0 || lx >= 36 || lz >= 36) continue;
                            o.Set(x, bse, z, (lz % 12 == 0 && lx % 6 != 0) ? B.RoadLine : B.Asphalt);
                        }
                    for (int row = 0; row < 3; row++)
                        for (int k = 0; k < 6; k++)
                        {
                            if (Hash.H2(s.Kx * 31 + row, s.Kz * 17 + k, Seed ^ 0xca) > 0.45f) continue;
                            if (Restorable(s.Kx * 31 + row, s.Kz * 17 + k, 5)) continue;
                            Car(s.X0 + k * 6 + 1, bse + 1, s.Z0 + row * 12 + 2, false, Hash.H2(k, row + s.Kx, Seed), ref o);
                        }
                    return;
                case LotType.Ruine:
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int lx = x - s.X0 - 18, lz = z - s.Z0 - 18;
                            float dd = MathF.Sqrt(lx * lx + lz * lz);
                            float hgt = Math.Max(0, (16 - dd) * 0.6f + n4.Noise2(x / 5f, z / 5f) * 3);
                            for (int y = 1; y <= hgt; y++)
                                o.Set(x, bse + y, z, Hash.H3(x, y, z, Seed) < 0.15f ? B.Brick : Hash.H3(x, y, z, Seed ^ 1) < 0.1f ? B.Rust : B.Rubble);
                            if ((Math.Abs(lx) == 12 || Math.Abs(lz) == 12) && Math.Abs(lx) <= 12 && Math.Abs(lz) <= 12)
                            {
                                float wh = (n4.Noise2(x / 4f, z / 4f) + 1) * 8;
                                for (int y = 1; y <= wh; y++) o.Set(x, bse + y, z, B.Concrete);
                            }
                        }
                    o.Set(s.X0 + 18, bse + 1, s.Z0 + 30, B.Crate);
                    return;
                case LotType.Militair:
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int lx = x - s.X0, lz = z - s.Z0;
                            if (lx < 0 || lz < 0 || lx >= 36 || lz >= 36) continue;
                            o.Set(x, bse, z, (lx + lz) % 9 == 0 ? B.Gravel : B.Concrete);
                            bool ring = lx == 1 || lz == 1 || lx == 34 || lz == 34;
                            bool gate = (lz == 1 || lz == 34) && lx >= 15 && lx <= 20;
                            if (ring && !gate)
                            {
                                o.Set(x, bse + 1, z, B.Sandbag); o.Set(x, bse + 2, z, B.Sandbag);
                                if ((lx + lz) % 2 == 0 && Hash.H2(x, z, Seed) > s.Damage * 0.6f) o.Set(x, bse + 3, z, B.Sandbag);
                            }
                            if ((lx == 0 || lz == 0 || lx == 35 || lz == 35) && !gate) { o.Set(x, bse + 1, z, B.Fence); o.Set(x, bse + 2, z, B.Fence); o.Set(x, bse + 3, z, B.Fence); }
                            // twee legertenten
                            for (int t = 0; t < 2; t++)
                            {
                                int tx0 = 5 + t * 15, tz0 = 6;
                                if (lx >= tx0 && lx < tx0 + 10 && lz >= tz0 && lz < tz0 + 12)
                                {
                                    int ex = lx - tx0, ez = lz - tz0;
                                    int roofH = 4 - Math.Abs(ex - 4) / 2;
                                    bool wall = ez == 0 || ez == 11;
                                    for (int y = 1; y <= roofH; y++)
                                        if (y == roofH || ((ex == 0 || ex == 9) && y <= 2) || (wall && !(ez == 11 && ex >= 3 && ex <= 6 && y <= 3))) o.Set(x, bse + y, z, B.OD);
                                    if (ez == 2 && (ex == 2 || ex == 7) && Hash.H2(x, z, Seed ^ 0xac) < 0.8f) o.Set(x, bse + 1, z, B.AmmoCrate);
                                }
                            }
                            // wachttoren
                            if (lx >= 28 && lx <= 31 && lz >= 26 && lz <= 29)
                            {
                                bool corner = (lx == 28 || lx == 31) && (lz == 26 || lz == 29);
                                if (corner) for (int y = 1; y <= 9; y++) o.Set(x, bse + y, z, B.Log);
                                o.Set(x, bse + 10, z, B.Planks);
                                if (lx == 28 || lx == 31 || lz == 26 || lz == 29) o.Set(x, bse + 11, z, B.Sandbag);
                                if (lx == 30 && lz == 27) o.Set(x, bse + 11, z, B.AmmoCrate);
                            }
                            if (lx == 29 && lz >= 16 && lz <= 25) o.Set(x, bse + 1 + (lz - 16), z, B.Planks);   // trap naar de toren
                        }
                    Car(s.X0 + 6, bse + 1, s.Z0 + 22, false, 0.45f, ref o);
                    return;
                case LotType.Benzinestation:
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int lx = x - s.X0, lz = z - s.Z0;
                            if (lx < 0 || lz < 0 || lx >= 36 || lz >= 36) continue;
                            o.Set(x, bse, z, B.Concrete);
                            if (lx >= 2 && lx < 18 && lz >= 4 && lz < 20)
                            {
                                if (Hash.H3(x, 7, z, Seed) > s.Damage * 0.5f) o.Set(x, bse + 7, z, B.Metal);
                                if ((lx == 3 || lx == 16) && (lz == 5 || lz == 18)) for (int y = 1; y < 7; y++) o.Set(x, bse + y, z, B.Concrete);
                                if ((lx == 8 || lx == 11) && lz >= 9 && lz <= 14) { o.Set(x, bse + 1, z, B.Metal); o.Set(x, bse + 2, z, lz == 11 ? B.CarRed : B.Metal); }
                            }
                            if (lx >= 24 && lx < 34 && lz >= 2 && lz < 6 && Hash.H2(lx, lz, Seed ^ s.Kx) < 0.25f) o.Set(x, bse + 1, z, Hash.H2(lz, lx, Seed) < 0.4f ? B.ExplosiveBarrel : B.Barrel);
                        }
                    break;
            }
            int top = s.Floors * 6 + (s.Type == LotType.Huis ? (s.D >> 1) + 2 : 2);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    for (int ly = 0; ly <= top; ly++)
                    {
                        int b = BuildingBlock(s, x, bse + ly, z);
                        if (b >= 0) o.Set(x, bse + ly, z, (byte)b);
                    }
            if (s.Damage > 0.4f)
            {
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int lx = x - s.Bx, lz = z - s.Bz;
                        bool outside = lx < 0 || lz < 0 || lx >= s.W || lz >= s.D;
                        bool near = lx >= -3 && lz >= -3 && lx < s.W + 3 && lz < s.D + 3;
                        if (outside && near && Hash.H2(x, z, Seed ^ 0x99) < s.Damage * 0.5f)
                        {
                            o.Set(x, bse + 1, z, B.Rubble);
                            if (Hash.H2(z, x, Seed ^ 0x98) < 0.3f) o.Set(x, bse + 2, z, B.Rubble);
                        }
                    }
            }
        }

        void WriteSettlement(Settlement st, ref ChunkWriter o)
        {
            const int P = World.Pad;
            int x0 = Math.Max(st.X0, o.Ox), x1 = Math.Min(st.X0 + Settlement.W - 1, o.Ox + P - 1);
            int z0 = Math.Max(st.Z0, o.Oz), z1 = Math.Min(st.Z0 + Settlement.D - 1, o.Oz + P - 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    for (int ly = 0; ly <= 16; ly++)
                    {
                        int b = st.BlockAt(x - st.X0, ly, z - st.Z0, Seed);
                        if (b >= 0) o.Set(x, st.Base + ly, z, (byte)b);
                    }
        }

        void WriteCars(ref ChunkWriter o, Column[] cols)
        {
            const int P = World.Pad, SEG = 26;
            int i0 = FloorDiv(o.Ox - 20 - Half, CityCell), i1 = FloorDiv(o.Ox + P + 20 - Half, CityCell);
            for (int i = i0; i <= i1 + 1; i++)
            {
                int lx = i * CityCell + Half;
                if (lx < o.Ox - 12 || lx > o.Ox + P + 12) continue;
                for (int s = FloorDiv(o.Oz - 12, SEG); s <= FloorDiv(o.Oz + P + 12, SEG); s++)
                {
                    float r = Hash.H2(i * 97, s, Seed ^ 0xcab);
                    if (r > 0.32f) continue;
                    int z = s * SEG + (int)(Hash.H2(s, i, Seed) * 10);
                    int lane = r < 0.16f ? -4 : 1;
                    if (Restorable(i * 97, s, 1)) continue;          // dit wordt een echt voertuig
                    var c = GetColumn(lx, z);
                    if (c.Kind != ColumnKind.Highway && c.Kind != ColumnKind.Bridge) continue;
                    int y = (c.Kind == ColumnKind.Bridge ? c.RoadH : c.H) + 1;
                    Car(lx + lane, y, z, true, Hash.H2(s, i * 7, Seed ^ 3), ref o);
                }
            }
            int j0 = FloorDiv(o.Oz - 20 - Half, CityCell), j1 = FloorDiv(o.Oz + P + 20 - Half, CityCell);
            for (int j = j0; j <= j1 + 1; j++)
            {
                int lz = j * CityCell + Half;
                if (lz < o.Oz - 12 || lz > o.Oz + P + 12) continue;
                for (int s = FloorDiv(o.Ox - 12, SEG); s <= FloorDiv(o.Ox + P + 12, SEG); s++)
                {
                    float r = Hash.H2(s, j * 89, Seed ^ 0xcac);
                    if (r > 0.32f) continue;
                    int x = s * SEG + (int)(Hash.H2(j, s, Seed) * 10);
                    int lane = r < 0.16f ? -4 : 1;
                    if (Restorable(s, j * 89, 2)) continue;
                    var c = GetColumn(x, lz);
                    if (c.Kind != ColumnKind.Highway && c.Kind != ColumnKind.Bridge) continue;
                    int y = (c.Kind == ColumnKind.Bridge ? c.RoadH : c.H) + 1;
                    Car(x, y, lz + lane, false, Hash.H2(j * 5, s, Seed ^ 4), ref o);
                }
            }
            // stadsstraten (auto's kunnen 9 voxels uitsteken, dus kijk ook net buiten de chunk)
            for (int pz = -9; pz < P; pz++)
                for (int px = -9; px < P; px++)
                {
                    int x = o.Ox + px, z = o.Oz + pz;
                    int sx = Mod(x + 4, Street), sz = Mod(z + 4, Street);
                    bool ns = sx == 1 && Mod(z, 20) == 3 && sz >= 10;
                    bool ew = sz == 1 && Mod(x, 20) == 3 && sx >= 10;
                    if (!ns && !ew) continue;
                    var c = (px >= 0 && pz >= 0) ? cols[px + pz * P] : GetColumn(x, z);
                    if (c.Kind != ColumnKind.Street) continue;
                    if (ns && Hash.H2(x, z, Seed ^ 0xcad) < 0.35f && !Restorable(x, z, 3)) Car(x, c.H + 1, z, true, Hash.H2(z, x, Seed), ref o);
                    if (ew && Hash.H2(x, z, Seed ^ 0xcae) < 0.35f && !Restorable(x, z, 4)) Car(x, c.H + 1, z, false, Hash.H2(x, z, Seed ^ 8), ref o);
                }
        }

        static readonly byte[] Paints = { B.CarRed, B.CarBlue, B.CarGrey, B.CarWhite, B.Rust };

        /// <summary>Is deze auto nog te redden? Dan komt hij als voertuig in de wereld in plaats van als blokkenwrak.</summary>
        public bool Restorable(int a, int b, int kind) => Hash.H2(a * 7 + kind, b * 13 - kind, Seed ^ 0xfeed) < (kind == 5 ? 0.18f : 0.1f);

        /// <summary>
        /// Voertuigen waarvan de oorsprong in deze chunk ligt: redbare auto's (snelweg, straat, parkeerplaats)
        /// en boten langs de oever. Deterministisch, zodat ze na herladen op dezelfde plek staan.
        /// </summary>
        public List<VehicleSpawn> VehicleSpawns(int cx, int cz)
        {
            var list = new List<VehicleSpawn>();
            int x0 = cx * World.ChunkSize, z0 = cz * World.ChunkSize, x1 = x0 + World.ChunkSize, z1 = z0 + World.ChunkSize;
            bool In(int x, int z) => x >= x0 && x < x1 && z >= z0 && z < z1;
            const int SEG = 26;
            float vs = World.VoxelSize;
            VehicleType CarType(float r) => r < 0.35f ? VehicleType.Pickup : VehicleType.Auto;
            byte Paint(float r) => Paints[Math.Min(3, (int)(r * 4))];
            // snelwegen
            for (int i = FloorDiv(x0 - Half, CityCell) - 1; i <= FloorDiv(x1 - Half, CityCell) + 1; i++)
            {
                int lx = i * CityCell + Half;
                for (int sg = FloorDiv(z0 - 12, SEG); sg <= FloorDiv(z1 + 12, SEG); sg++)
                {
                    float r = Hash.H2(i * 97, sg, Seed ^ 0xcab);
                    if (r > 0.32f || !Restorable(i * 97, sg, 1)) continue;
                    int z = sg * SEG + (int)(Hash.H2(sg, i, Seed) * 10);
                    int lane = r < 0.16f ? -4 : 1;
                    if (!In(lx + lane, z)) continue;
                    var c = GetColumn(lx, z);
                    if (c.Kind != ColumnKind.Highway && c.Kind != ColumnKind.Bridge) continue;
                    int y = (c.Kind == ColumnKind.Bridge ? c.RoadH : c.H) + 1;
                    float pr = Hash.H2(sg, i * 7, Seed ^ 3);
                    list.Add(new VehicleSpawn { Key = VoxelStore.VoxelKey(lx + lane, y, z), Type = CarType(pr), Pos = new V3((lx + lane + 2) * vs, y * vs, (z + 4.5f) * vs), Yaw = lane < 0 ? 180 : 0, Paint = Paint(pr), Seed = sg * 31 + i });
                }
            }
            for (int j = FloorDiv(z0 - Half, CityCell) - 1; j <= FloorDiv(z1 - Half, CityCell) + 1; j++)
            {
                int lz = j * CityCell + Half;
                for (int sg = FloorDiv(x0 - 12, SEG); sg <= FloorDiv(x1 + 12, SEG); sg++)
                {
                    float r = Hash.H2(sg, j * 89, Seed ^ 0xcac);
                    if (r > 0.32f || !Restorable(sg, j * 89, 2)) continue;
                    int x = sg * SEG + (int)(Hash.H2(j, sg, Seed) * 10);
                    int lane = r < 0.16f ? -4 : 1;
                    if (!In(x, lz + lane)) continue;
                    var c = GetColumn(x, lz);
                    if (c.Kind != ColumnKind.Highway && c.Kind != ColumnKind.Bridge) continue;
                    int y = (c.Kind == ColumnKind.Bridge ? c.RoadH : c.H) + 1;
                    float pr = Hash.H2(j * 5, sg, Seed ^ 4);
                    list.Add(new VehicleSpawn { Key = VoxelStore.VoxelKey(x, y, lz + lane), Type = CarType(pr), Pos = new V3((x + 4.5f) * vs, y * vs, (lz + lane + 2) * vs), Yaw = lane < 0 ? 270 : 90, Paint = Paint(pr), Seed = sg * 37 + j });
                }
            }
            // stadsstraten
            for (int z = z0; z < z1; z++)
                for (int x = x0; x < x1; x++)
                {
                    int sx = Mod(x + 4, Street), sz = Mod(z + 4, Street);
                    bool ns = sx == 1 && Mod(z, 20) == 3 && sz >= 10 && Hash.H2(x, z, Seed ^ 0xcad) < 0.35f && Restorable(x, z, 3);
                    bool ew = sz == 1 && Mod(x, 20) == 3 && sx >= 10 && Hash.H2(x, z, Seed ^ 0xcae) < 0.35f && Restorable(x, z, 4);
                    if (!ns && !ew) continue;
                    var c = GetColumn(x, z);
                    if (c.Kind != ColumnKind.Street) continue;
                    float pr = Hash.H2(z, x, Seed);
                    list.Add(ns
                        ? new VehicleSpawn { Key = VoxelStore.VoxelKey(x, c.H + 1, z), Type = CarType(pr), Pos = new V3((x + 2) * vs, (c.H + 1) * vs, (z + 4.5f) * vs), Yaw = 0, Paint = Paint(pr), Seed = x * 7 + z }
                        : new VehicleSpawn { Key = VoxelStore.VoxelKey(x, c.H + 1, z) + 1, Type = CarType(pr), Pos = new V3((x + 4.5f) * vs, (c.H + 1) * vs, (z + 2) * vs), Yaw = 90, Paint = Paint(pr), Seed = x * 5 + z });
                }
            // parkeerplaatsen
            for (int kz = FloorDiv(z0 - 4, Street) - 1; kz <= FloorDiv(z1 - 4, Street); kz++)
                for (int kx = FloorDiv(x0 - 4, Street) - 1; kx <= FloorDiv(x1 - 4, Street); kx++)
                {
                    var lot = GetLot(kx, kz);
                    if (lot == null || lot.Type != LotType.Parkeerplaats) continue;
                    for (int row = 0; row < 3; row++)
                        for (int k = 0; k < 6; k++)
                        {
                            if (Hash.H2(lot.Kx * 31 + row, lot.Kz * 17 + k, Seed ^ 0xca) > 0.45f || !Restorable(lot.Kx * 31 + row, lot.Kz * 17 + k, 5)) continue;
                            int x = lot.X0 + k * 6 + 1, z = lot.Z0 + row * 12 + 2;
                            if (!In(x, z)) continue;
                            float pr = Hash.H2(k, row + lot.Kx, Seed);
                            list.Add(new VehicleSpawn { Key = VoxelStore.VoxelKey(x, lot.Base + 1, z), Type = CarType(pr), Pos = new V3((x + 4.5f) * vs, (lot.Base + 1) * vs, (z + 2) * vs), Yaw = 90, Paint = Paint(pr), Seed = kx * 13 + kz * 7 + row * 3 + k });
                        }
                }
            // boten aan de oever: per cel van 16 voxels één kandidaat
            for (int gz = z0; gz < z1; gz += 16)
                for (int gx = x0; gx < x1; gx += 16)
                {
                    int x = gx + (int)(Hash.H2(gx, gz, Seed ^ 0xb0a) * 16), z = gz + (int)(Hash.H2(gz, gx, Seed ^ 0xb0b) * 16);
                    if (Hash.H2(x, z, Seed ^ 0xb0c) > 0.3f) continue;
                    var c = GetColumn(x, z);
                    if (c.H > World.Sea - 1 || c.Kind != ColumnKind.Nature) continue;     // moet in het water liggen
                    // zoek land binnen 8 meter
                    int landDir = -1;
                    for (int dir = 0; dir < 4 && landDir < 0; dir++)
                    {
                        int dx = dir == 0 ? 1 : dir == 1 ? -1 : 0, dz = dir == 2 ? 1 : dir == 3 ? -1 : 0;
                        for (int step = 2; step <= 16; step += 2) if (GetColumn(x + dx * step, z + dz * step).H > World.Sea) { landDir = dir; break; }
                    }
                    if (landDir < 0) continue;
                    float yaw = landDir switch { 0 => 270, 1 => 90, 2 => 180, _ => 0 };     // met de neus van het land af
                    float bt = Hash.H2(z, x, Seed ^ 0xb0d);
                    // aan de open zee liggen soms zeewaardige vissersboten
                    var type = OceanAt(x, z) > 0.3f && bt < 0.55f ? VehicleType.Kotter : bt < 0.3f ? VehicleType.Motorboot : VehicleType.Roeiboot;
                    list.Add(new VehicleSpawn { Key = VoxelStore.VoxelKey(x, World.Sea, z) + 2, Type = type, Pos = new V3((x + 0.5f) * vs, World.SeaLevelMeters - 0.15f, (z + 0.5f) * vs), Yaw = yaw, Paint = B.CarWhite, Seed = x * 3 + z });
                }
            return list;
        }

        /// <summary>Autowrak van 4 × 9 × 3 voxels.</summary>
        static void Car(int x, int y, int z, bool ns, float r, ref ChunkWriter o)
        {
            byte paint = Paints[Math.Min(4, (int)(r * 5))];
            bool burnt = r > 0.8f;
            for (int a = 0; a < 9; a++)
                for (int b = 0; b < 4; b++)
                {
                    int wx = ns ? x + b : x + a, wz = ns ? z + a : z + b;
                    bool wheel = (a == 1 || a == 7) && (b == 0 || b == 3);
                    o.Set(wx, y, wz, wheel ? B.Tire : (burnt ? B.Rust : paint));
                    o.Set(wx, y + 1, wz, burnt ? B.Rust : paint);
                    if (a >= 2 && a <= 6)
                    {
                        bool glass = a == 2 || a == 6 || b == 0 || b == 3;
                        o.Set(wx, y + 2, wz, glass ? (burnt ? B.Air : B.Glass) : (burnt ? B.Rust : paint));
                    }
                }
        }

        /// <summary>kind: 0 = dode boom, 1 = den, 2 = loofboom.</summary>
        void WriteTree(int kind, int x, int y, int z, float r, ref ChunkWriter o)
        {
            if (kind == 0)
            {
                int hgt = 6 + (int)(r * 6);
                for (int i = 0; i < hgt; i++) o.Set(x, y + i, z, B.Log);
                int rot = (int)(r * 4);
                int[] dxs = { 1, -1, 0, 0 }, dzs = { 0, 0, 1, -1 };
                for (int k = 0; k < 3; k++)
                {
                    int dx = dxs[(k + rot) & 3], dz = dzs[(k + rot) & 3];
                    int by = y + 3 + k * 2;
                    for (int l = 1; l <= 2 + k % 2; l++) o.Set(x + dx * l, by + (l >> 1), z + dz * l, B.Log);
                    if (Hash.H2(x + k, z, Seed) < 0.5f) o.Set(x + dx * 3, by + 2, z + dz * 3, B.DeadLeaves);
                }
                return;
            }
            if (kind == 1)
            {
                int hgt = 11 + (int)(r * 7);
                for (int i = 0; i < hgt; i++) o.Set(x, y + i, z, B.Log);
                for (int i = 3; i <= hgt; i++)
                {
                    float rad = Math.Max(0, (hgt - i) * 0.33f + (i % 3 == 0 ? 0.9f : 0.2f));
                    int R = (int)MathF.Ceiling(rad);
                    for (int dz = -R; dz <= R; dz++)
                        for (int dx = -R; dx <= R; dx++)
                        {
                            if (dx * dx + dz * dz > rad * rad + 0.3f) continue;
                            if (dx == 0 && dz == 0 && i < hgt) continue;
                            if (o.Get(x + dx, y + i, z + dz) == B.Air) o.Set(x + dx, y + i, z + dz, B.Leaves);
                        }
                }
                o.Set(x, y + hgt, z, B.Leaves); o.Set(x, y + hgt + 1, z, B.Leaves);
                return;
            }
            {
                int hgt = 7 + (int)(r * 5);
                for (int i = 0; i < hgt; i++) o.Set(x, y + i, z, B.Log);
                float rad = 3.2f + r * 1.6f;
                int cy = y + hgt - 1;
                int R = (int)MathF.Ceiling(rad);
                bool autumn = Hash.H2(x, z, Seed ^ 0xa0) < 0.25f;
                for (int dy = -R; dy <= R + 1; dy++)
                    for (int dz = -R; dz <= R; dz++)
                        for (int dx = -R; dx <= R; dx++)
                        {
                            float dd = (dx * dx + dz * dz) / (rad * rad) + (dy * dy) / (rad * rad * 0.7f);
                            if (dd > 1) continue;
                            if (dd > 0.6f && Hash.H3(x + dx, cy + dy, z + dz, Seed) < 0.35f) continue;
                            if (o.Get(x + dx, cy + dy, z + dz) == B.Air)
                                o.Set(x + dx, cy + dy, z + dz, autumn && Hash.H3(dx, dy, dz, Seed) < 0.6f ? B.DeadLeaves : B.Leaves);
                        }
            }
        }

        /// <summary>Hoogste niet-lucht voxel in een kolom (alleen terrein, zonder bouwwerken) — voor spawn en kaart.</summary>
        public int SurfaceHeight(int x, int z)
        {
            var c = GetColumn(x, z);
            return c.Kind == ColumnKind.Bridge ? c.RoadH : Math.Max(c.H, World.Sea);
        }
    }
}
