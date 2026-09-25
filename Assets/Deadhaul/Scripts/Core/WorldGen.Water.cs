using System;

namespace Deadhaul.Core
{
    /// <summary>
    /// Zee, eilanden, rivieren en het Stille Eiland. Het Vasteland ligt rond de oorsprong; verder weg
    /// breekt het land open in zee met archipels. Rivieren slingeren door het land naar zeeniveau en
    /// snijden kloven uit in de heuvels; snelwegen gaan er met bruggen overheen.
    /// </summary>
    public sealed partial class WorldGen
    {
        Simplex seaNoise, riverNoise, warpNoise;
        int stilleX = int.MinValue, stilleZ;
        const float RiverCore = 0.0085f, RiverBank = 0.034f;
        public const int IslandR = 64;                       // straal van het Stille Eiland (voxels)

        void InitWater()
        {
            seaNoise = new Simplex(Seed + 404);
            riverNoise = new Simplex(Seed + 505);
            warpNoise = new Simplex(Seed + 606);
        }

        /// <summary>0 = land, 1 = open zee. Rond de startstad is het altijd land.</summary>
        public float OceanAt(float x, float z)
        {
            // het Vasteland reikt zo'n 8–10 km rond de start; daarbuiten breekt het open in de Archipel en zee
            float d = MathF.Sqrt(x * x + z * z);
            float s = seaNoise.Fbm2(x / 16000f + 13.7f, z / 16000f - 4.2f, 2) + 0.1f * seaNoise.Noise2(x / 2200f, z / 2200f) - 0.3f;
            s += Smooth((d - 15000f) / 14000f) * 0.75f - Math.Max(0, 1 - d / 5000f) * 0.6f;
            return Smooth((s - 0.08f) / 0.18f);
        }

        /// <summary>Hoe ver een eilandtop boven de zeebodem uitsteekt.</summary>
        float IslandLift(float x, float z)
        {
            float p = n2.Noise2(x / 420f + 50, z / 420f - 80) * 0.85f + n4.Noise2(x / 90f, z / 90f) * 0.15f;
            return Math.Max(0, p - 0.5f) * 95f;
        }

        /// <summary>Afstand tot de rivierbedding in ruisruimte: 0 = midden van de rivier.</summary>
        public float RiverField(float x, float z)
        {
            float wx = warpNoise.Noise2(x / 600f, z / 600f) * 0.12f, wz = warpNoise.Noise2(x / 600f + 91, z / 600f - 17) * 0.12f;
            float f = MathF.Abs(riverNoise.Noise2(x / 2600f + wx, z / 2600f + wz));
            // maar een deel van de rivieren bestaat: elders droogt de bedding op tot er niets meer is
            float live = Smooth((riverNoise.Noise2(x / 6000f + 311, z / 6000f - 77) + 0.05f) / 0.3f);
            return live <= 0 ? 1f : f / live;
        }

        public bool IsRiver(int x, int z) => OceanAt(x, z) < 0.1f && RiverField(x, z) < RiverCore;

        /// <summary>Zee en eilanden over het basisterrein heen.</summary>
        float ApplySea(float x, float z, float h)
        {
            float oc = OceanAt(x, z);
            if (oc > 0)
            {
                float floor = World.Sea - 13 + IslandLift(x, z) + n4.Noise2(x / 40f, z / 40f) * 1.5f;
                h = h * (1 - oc) + floor * oc;
            }
            // het Stille Eiland: een verborgen eiland ver op zee
            FindStille();
            float dx = x - stilleX, dz = z - stilleZ;
            float sd = MathF.Sqrt(dx * dx + dz * dz) + n4.Noise2(x / 18f, z / 18f) * 9f;
            if (sd < IslandR + 40)
            {
                float top = World.Sea + 7 + n2.Noise2(x / 30f, z / 30f) * 2f;
                float t = Smooth((sd - IslandR * 0.55f) / (IslandR * 0.45f + 40));
                h = Math.Max(h, top + (World.Sea - 13 - top) * t);
            }
            return h;
        }

        /// <summary>Rivieren slijten een dal uit tot net onder zeeniveau (dan staat er water in).</summary>
        float ApplyRiver(float x, float z, float h)
        {
            if (h <= World.Sea) return h;
            float f = RiverField(x, z);
            if (f >= RiverBank) return h;
            float bed = World.Sea - 2;
            float t = Smooth((f - RiverCore) / (RiverBank - RiverCore));
            float carved = bed + (h - bed) * t * t;
            return Math.Min(h, carved);
        }

        /// <summary>Zoekt (eenmalig) de plek van het Stille Eiland: open zee, ver van de kust van het Vasteland.</summary>
        void FindStille()
        {
            if (stilleX != int.MinValue) return;
            lock (warpNoise)
            {
                if (stilleX != int.MinValue) return;
                int bx = 0, bz = 0;
                bool found = false;
                for (int ring = 0; ring < 60 && !found; ring++)
                {
                    float r = 22000 + ring * 900;
                    for (int k = 0; k < 48 && !found; k++)
                    {
                        float a = (k / 48f + Hash.H2(ring, 3, Seed) * 0.1f) * MathF.PI * 2;
                        int x = (int)(MathF.Cos(a) * r), z = (int)(MathF.Sin(a) * r);
                        // helemaal open water rondom
                        bool open = true;
                        for (int s = 0; s < 8 && open; s++)
                        {
                            float sa = s / 8f * MathF.PI * 2;
                            if (OceanAt(x + MathF.Cos(sa) * 400, z + MathF.Sin(sa) * 400) < 0.99f) open = false;
                        }
                        if (open && OceanAt(x, z) >= 0.99f && IslandLift(x, z) == 0) { bx = x; bz = z; found = true; }
                    }
                }
                if (!found) { bx = 30000; bz = 30000; }
                stilleZ = bz;
                stilleX = bx;
            }
        }

        /// <summary>Midden van het Stille Eiland (voxels).</summary>
        public (int x, int z) StilleEiland { get { FindStille(); return (stilleX, stilleZ); } }

        /// <summary>Vuurtoren, hut en steiger op het Stille Eiland.</summary>
        void WriteStille(ref ChunkWriter o)
        {
            FindStille();
            const int P = World.Pad;
            int cx = stilleX, cz = stilleZ;
            if (cx + IslandR + 30 < o.Ox || cx - IslandR - 30 > o.Ox + P || cz + IslandR + 30 < o.Oz || cz - IslandR - 30 > o.Oz + P) return;
            int baseY = GetColumn(cx, cz).H;
            // vuurtoren: ronde toren, rood-witte banden, lamp bovenin
            for (int y = 1; y <= 34; y++)
                for (int dz = -4; dz <= 4; dz++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int rr = dx * dx + dz * dz;
                        float rad = y < 30 ? 3.6f - y * 0.03f : 3.2f;
                        if (rr > rad * rad) continue;
                        bool wall = rr > (rad - 1.2f) * (rad - 1.2f);
                        byte band = (y / 5) % 2 == 0 ? B.Plaster : B.CarRed;
                        byte b = 0;
                        if (y < 30) b = wall ? band : B.Air;
                        else if (y == 30) b = B.Metal;
                        else if (y < 34) b = wall ? (dx == 0 || dz == 0 ? B.Glass : B.Metal) : (dx == 0 && dz == 0 ? B.Lamp : B.Air);
                        else b = B.Metal;
                        // wenteltrap: een traptree per laag die rondloopt
                        if (y < 30 && !wall)
                        {
                            float ang = MathF.Atan2(dz, dx);
                            int step = (int)((ang + MathF.PI) / (MathF.PI * 2) * 8);
                            if (step == y % 8 && rr >= 1) b = B.Planks;
                        }
                        if (dz == 3 && dx >= -1 && dx <= 1 && y >= 1 && y <= 4) b = B.Air;                   // deur
                        o.Set(cx + dx, baseY + y, cz + dz, b);
                    }
            // vissershut met bed, kast en wat de vorige bewoner achterliet
            int hx = cx + 10, hz = cz - 6;
            for (int y = 0; y <= 5; y++)
                for (int dz = 0; dz < 7; dz++)
                    for (int dx = 0; dx < 8; dx++)
                    {
                        bool wall = dx == 0 || dz == 0 || dx == 7 || dz == 6;
                        byte b = y == 0 ? B.Planks : y == 5 ? B.Roof : wall ? B.WoodWall : B.Air;
                        if (wall && y == 2 && (dx == 3 || dz == 3)) b = B.Glass;
                        if (dz == 0 && (dx == 3 || dx == 4) && y >= 1 && y <= 4) b = B.Air;
                        o.Set(hx + dx, baseY + y, hz + dz, b);
                    }
            o.Set(hx + 1, baseY + 1, hz + 4, B.Bed); o.Set(hx + 1, baseY + 1, hz + 5, B.Bed);
            o.Set(hx + 6, baseY + 1, hz + 5, B.Cabinet); o.Set(hx + 6, baseY + 1, hz + 1, B.AmmoCrate);
            o.Set(hx + 5, baseY + 1, hz + 5, B.Crate); o.Set(hx + 3, baseY + 1, hz + 5, B.Campfire);
            // steiger de zee in
            for (int k = 0; k < 44; k++)
                for (int w = -1; w <= 1; w++)
                {
                    int x = cx + w, z = cz + 8 + k;
                    if (GetColumn(x, z).H > World.Sea + 1) continue;
                    o.Set(x, World.Sea + 1, z, B.Planks);
                    if (w != 0 && k % 5 == 0) for (int y = World.Sea - 4; y <= World.Sea + 2; y++) o.Set(x, y, z, B.Log);
                }
        }
    }
}
