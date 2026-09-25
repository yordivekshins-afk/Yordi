using System;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    public enum SettlerJob : byte { Boer, Sjouwer, Bewaker, Kok, Handelaar }

    public enum SettlerTask : byte { Geen, Lopen, Oogsten, Afleveren, Slapen, Eten, Wachtlopen, Koken, Handelen, Sjouwen, Praten }

    /// <summary>
    /// Een ommuurde nederzetting van overlevers langs de snelweg: akkers, huisjes, voorraadschuur,
    /// markt, waterput, kookvuur en wachttorens. De indeling is deterministisch per seed.
    /// </summary>
    public sealed class Settlement
    {
        public const int W = 120, D = 100;
        public int I, J, X0, Z0, Base;
        public string Name;
        public readonly List<(int x, int z, int type)> Fields = new List<(int, int, int)>();
        public readonly List<(int x0, int z0)> Houses = new List<(int, int)>();
        public readonly List<(int x, int z)> Beds = new List<(int, int)>();
        public readonly List<(int x, int z)> Patrol = new List<(int, int)>();
        public (int x, int z) Fire, Storage, Market, Well, Gate;
        public readonly int[] Stock = new int[7];
        public List<Stack> TraderStock;
        public int Seed;

        static readonly string[] Names = { "Nieuwhoop", "Laatste Oogst", "Kraaienhof", "De Schans", "Groenwal", "Morgenrood", "Het Bolwerk", "Stille Akker", "Vrijgrond", "De Put" };
        public static readonly string[] People = { "Milo", "Sanne", "Joost", "Fenna", "Bram", "Lotte", "Ruben", "Noor", "Daan", "Eva", "Thijs", "Mila", "Koen", "Iris", "Sem", "Lieke", "Gijs", "Roos", "Teun", "Jet" };

        /// <summary>Wereldcoördinaat (voxels) van een lokaal punt.</summary>
        public (int x, int z) ToWorld(int lx, int lz) => (X0 + lx, Z0 + lz);
        public V3 Point(float lx, float lz) => new V3((X0 + lx + 0.5f) * World.VoxelSize, (Base + 1) * World.VoxelSize, (Z0 + lz + 0.5f) * World.VoxelSize);
        public bool Contains(int x, int z) => x >= X0 && z >= Z0 && x < X0 + W && z < Z0 + D;

        public static Settlement Layout(int i, int j, int x0, int z0, int baseY, int seed)
        {
            var s = new Settlement { I = i, J = j, X0 = x0, Z0 = z0, Base = baseY, Seed = seed };
            s.Name = Names[(int)(Hash.H2(i, j, seed ^ 0x5e7) * Names.Length)];
            // akkers: twee velden met rijen per gewas, paden ertussen
            for (int f = 0; f < 2; f++)
            {
                int fx0 = f == 0 ? 8 : 68;
                for (int lz = 8; lz <= 54; lz++)
                {
                    int r = lz - 8;
                    if (r % 3 == 2) continue;
                    int type = (f * 3 + r / 6 + (int)(Hash.H2(i, j, seed) * 7)) % 7;
                    for (int lx = fx0; lx <= fx0 + 44; lx++) s.Fields.Add((x0 + lx, z0 + lz, type));
                }
            }
            foreach (int hx in new[] { 8, 30, 76, 98 })
            {
                s.Houses.Add((hx, 64));
                s.Beds.Add((x0 + hx + 3, z0 + 75)); s.Beds.Add((x0 + hx + 10, z0 + 75));
            }
            s.Fire = (x0 + 60, z0 + 72);
            s.Storage = (x0 + 51, z0 + 82);      // voor de schuurdeur
            s.Market = (x0 + 68, z0 + 95);       // achter de toonbank
            s.Well = (x0 + 67, z0 + 87);
            s.Gate = (x0 + 60, z0 + 2);
            foreach (var (px, pz) in new[] { (60, 4), (60, 58), (6, 58), (60, 58), (113, 58), (60, 58), (61, 97), (60, 58) }) s.Patrol.Add((x0 + px, z0 + pz));
            return s;
        }

        /// <summary>Blok van de nederzetting op lokale positie (lx, ly boven de grond, lz), of -1.</summary>
        public int BlockAt(int lx, int ly, int lz, int seed)
        {
            if (lx < 0 || lz < 0 || lx >= W || lz >= D) return -1;
            int wx = X0 + lx, wz = Z0 + lz;
            // grond
            if (ly == 0)
            {
                if (lz >= 8 && lz <= 54 && ((lx >= 8 && lx <= 52) || (lx >= 68 && lx <= 112))) return (lz - 8) % 3 == 2 ? B.Gravel : B.Farmland;
                if (lx >= 57 && lx <= 62 && lz < 70) return B.Gravel;
                return -1;
            }
            // palissade met poort
            bool wall = lx == 0 || lz == 0 || lx == W - 1 || lz == D - 1;
            if (wall)
            {
                bool gate = lz == 0 && lx >= 55 && lx <= 64;
                if (gate) return ly == 6 ? B.Log : -1;
                bool post = (lx + lz) % 6 == 0;
                if (ly <= 4) return post ? B.Log : B.WoodWall;
                if (ly == 5 && post) return B.Log;
                return -1;
            }
            if (ly == 6 && lz == 0) return -1;
            // gewassen
            if (ly == 1 && lz >= 8 && lz <= 54 && ((lx >= 8 && lx <= 52) || (lx >= 68 && lx <= 112)) && (lz - 8) % 3 != 2)
            {
                int f = lx >= 68 ? 1 : 0;
                int type = (f * 3 + (lz - 8) / 6 + (int)(Hash.H2(I, J, seed) * 7)) % 7;
                return Hash.H2(wx, wz, seed ^ 0xf4) < 0.62f ? B.Crops[type] : B.Seedlings[type];
            }
            // wachttorens in twee hoeken
            for (int t = 0; t < 2; t++)
            {
                int tx = t == 0 ? 2 : W - 6, tz = t == 0 ? 2 : D - 6;
                int ex = lx - tx, ez = lz - tz;
                if (ex < 0 || ez < 0 || ex > 3 || ez > 3) continue;
                bool corner = (ex == 0 || ex == 3) && (ez == 0 || ez == 3);
                if (corner && ly <= 10) return B.Log;
                if (ly == 10) return B.Planks;
                bool opening = ez == 2 && (t == 0 ? ex == 3 : ex == 0);      // waar de trap uitkomt
                if (ly == 11 && (ex == 0 || ex == 3 || ez == 0 || ez == 3) && !opening) return B.WoodWall;
                return -1;
            }
            // trappen naar de torens (één voxel per trede)
            if (lz == 4 && lx >= 6 && lx <= 15 && ly == 16 - lx) return B.Planks;
            if (lz == D - 4 && lx >= W - 16 && lx <= W - 7 && ly == lx - (W - 17)) return B.Planks;
            // huisjes
            foreach (var (hx, hz) in Houses)
            {
                int ex = lx - hx, ez = lz - hz;
                if (ex < 0 || ez < 0 || ex > 14 || ez > 13) continue;
                bool edge = ex == 0 || ez == 0 || ex == 14 || ez == 13;
                int roofBase = 6;
                if (ly > roofBase)
                {
                    int k = ly - roofBase, rh = Math.Min(ex, 14 - ex) + 1;
                    if (k > rh) return -1;
                    if (k == rh) return B.Roof;
                    if (ez == 0 || ez == 13) return B.Planks;
                    return B.Air;
                }
                if (ly == roofBase) return edge ? B.Log : B.Planks;
                if (edge)
                {
                    if (ez == 0 && ex >= 6 && ex <= 8 && ly <= 4) return B.Air;                         // deur
                    if ((ez == 0 || ez == 13) && (ex == 3 || ex == 11) && ly >= 2 && ly <= 3) return B.Glass;
                    if ((ex == 0 || ex == 14) && ez % 4 == 2 && ly >= 2 && ly <= 3) return B.Glass;
                    return (ex == 0 || ex == 14) && (ez == 0 || ez == 13) ? B.Log : B.Planks;
                }
                if (ly == 1 && ez >= 10 && ez <= 12 && (ex == 3 || ex == 4 || ex == 10 || ex == 11)) return B.Bed;
                if (ly == 1 && ex == 7 && ez == 11) return B.Planks;     // nachtkastje
                return B.Air;
            }
            // voorraadschuur
            {
                int ex = lx - 46, ez = lz - 84;
                if (ex >= 0 && ez >= 0 && ex <= 11 && ez <= 11)
                {
                    bool edge = ex == 0 || ez == 0 || ex == 11 || ez == 11;
                    if (ly == 6) return B.MetalWall;
                    if (ly > 6) return -1;
                    if (edge) return ez == 0 && ex >= 4 && ex <= 7 && ly <= 4 ? B.Air : B.Planks;
                    if (ly <= 2 && ez >= 7 && ez <= 10 && (ex <= 3 || ex >= 8)) return ly == 1 ? B.Barrel : B.Cloth;
                    return B.Air;
                }
            }
            // marktkraam op het noordelijke erf
            {
                int ex = lx - 64, ez = lz - 92;
                if (ex >= 0 && ez >= 0 && ex <= 9 && ez <= 5)
                {
                    bool pole = (ex == 0 || ex == 9) && (ez == 0 || ez == 5);
                    if (pole && ly <= 5) return B.Log;
                    if (ly == 6) return (ex + ez) % 2 == 0 ? B.Cloth : B.Bandana;
                    if (ez == 1 && ly <= 2 && ex >= 1 && ex <= 8) return ly == 2 ? (ex % 3 == 0 ? B.Pumpkin : ex % 3 == 1 ? B.Cabbage : B.Planks) : B.Planks;
                    return -1;
                }
            }
            // waterput
            {
                int ex = lx - 66, ez = lz - 86;
                if (ex >= 0 && ez >= 0 && ex <= 2 && ez <= 2)
                {
                    if (ex == 1 && ez == 1) return ly == 1 ? B.Well : ly == 5 ? B.Planks : -1;
                    if ((ex == 0 || ex == 2) && ez == 1 && ly <= 5) return B.Log;
                    if (ly == 1 || ly == 2) return B.StoneWall;
                    if (ly == 5) return B.Planks;
                    return -1;
                }
            }
            // kookvuur met banken
            if (ly == 1)
            {
                int fx = lx - (Fire.x - X0), fz = lz - (Fire.z - Z0);
                if (fx == 0 && fz == 0) return B.Campfire;
                if ((Math.Abs(fx) == 4 && Math.Abs(fz) <= 1) || (Math.Abs(fz) == 4 && Math.Abs(fx) <= 1)) return B.Log;
            }
            return -1;
        }

        public string PersonName(int k) => People[(int)(Hash.H2(I * 31 + k, J, Seed ^ 0x9e) * People.Length)];

        /// <summary>Welke bewoners er wonen: 4 boeren, 1 sjouwer, 2 bewakers, 1 kok, 1 handelaar.</summary>
        public static readonly SettlerJob[] Population =
            { SettlerJob.Boer, SettlerJob.Boer, SettlerJob.Boer, SettlerJob.Boer, SettlerJob.Sjouwer, SettlerJob.Bewaker, SettlerJob.Bewaker, SettlerJob.Kok, SettlerJob.Handelaar };

        public List<Stack> MakeTraderStock(Random rng)
        {
            var list = new List<Stack>();
            void Add(string id, int n) { var d = Items.Get(id); if (d == null) return; if (d.MaxStack == 1) for (int k = 0; k < n; k++) list.Add(Loot.MakeItem(id, rng)); else list.Add(new Stack(id, n)); }
            foreach (var c in B.CropItems) { Add(c, 3 + rng.Next(8)); Add("zaad_" + c, 4 + rng.Next(8)); }
            Add("brood", 2 + rng.Next(4)); Add("water", 2 + rng.Next(4)); Add("soep", 1 + rng.Next(2));
            Add("verband", 2 + rng.Next(4)); if (rng.NextDouble() < 0.6) Add("medkit", 1); Add("jodium", 1 + rng.Next(3));
            Add("9mm", 20 + rng.Next(40)); Add("12g", 6 + rng.Next(12)); if (rng.NextDouble() < 0.5) Add("556", 15 + rng.Next(30)); if (rng.NextDouble() < 0.5) Add("762", 15 + rng.Next(30));
            string[] guns = { "pistool", "shotgun", "mp5", "geweer" }; Add(guns[rng.Next(guns.Length)], 1);
            string[] gear = { "rugzak", "cargobroek", "wandelschoenen", "winterjas", "chestrig", "gasmasker", "hoodie", "legerkistjes" };
            for (int k = 0; k < 3; k++) Add(gear[rng.Next(gear.Length)], 1);
            string[] atts = { "reddot", "sling", "wapenlamp", "grip_vert", "compensator" }; Add(atts[rng.Next(atts.Length)], 1);
            Add("batterij", 2 + rng.Next(4)); Add("akkergrond", 10 + rng.Next(20));
            return list;
        }
    }

    /// <summary>Het dagritme en de banen van de bewoners.</summary>
    public static class SettlerBrain
    {
        static readonly string[] BoerLines = { "De aardappels doen het goed dit jaar. Als de ghouls ze niet vertrappen.", "Zaad is goud. Verspil het niet.", "Kom je helpen oogsten? Nee? Dacht ik al.", "Graan, maïs, kool… we eten deze winter tenminste." };
        static readonly string[] BewakerLines = { "Blijf bij de poort weg met dat wapen.", "Vannacht weer ogen in het bos gezien. Gloeiende ogen.", "Rustig blijven, vreemdeling. Dan blijven wij ook rustig.", "Vanaf die toren zie je de krater gloeien." };
        static readonly string[] KokLines = { "Soep is bijna klaar. Eén kom per persoon.", "Wie z'n bord niet leeg eet, gaat de nacht in.", "Breng me vlees en ik bak het voor je." };
        static readonly string[] SjouwerLines = { "Die kisten tillen zichzelf niet.", "Alles van de akker gaat naar de schuur, alles van de schuur naar de markt." };
        static readonly string[] HandelaarLines = { "Doppen of ruilwaar. Ik ben niet kieskeurig.", "Kijk rustig rond. Niet aanraken zonder te betalen." };

        public static string Line(Npc n, Random rng)
        {
            var a = n.Job switch
            {
                SettlerJob.Boer => BoerLines,
                SettlerJob.Bewaker => BewakerLines,
                SettlerJob.Kok => KokLines,
                SettlerJob.Sjouwer => SjouwerLines,
                _ => HandelaarLines,
            };
            return a[rng.Next(a.Length)];
        }

        public static bool IsSleepTime(Npc n, float h) => (n.Job == SettlerJob.Bewaker && n.BedIndex % 2 == 1) ? (h >= 8 && h < 15) : (h >= 22 || h < 6);
        public static bool IsMealTime(float h) => (h >= 6.5f && h < 7.3f) || (h >= 12 && h < 12.8f) || (h >= 19 && h < 20);

        public static void Tick(Npc n, float dt, AiContext ctx)
        {
            n.FiredThisFrame = false; n.MeleeThisFrame = false;
            if (!n.Alive) { Brain.Tick(n, dt, ctx); return; }
            var s = n.Home2;
            var seen = Brain.Sense(n, dt, ctx, out var target);
            bool threat = target != null && n.Awareness >= 1f;
            if (threat || n.State == NpcState.Aanvallen)
            {
                n.Sleeping = false; n.Working = false;
                if (n.Job == SettlerJob.Bewaker || n.Angry) { Brain.Tick(n, dt, ctx); return; }
                // burgers vluchten naar huis
                GoHome(n, n.Def.RunSpeed, dt, ctx);
                n.State = NpcState.Vluchten;
                if (target == null || n.SeenTime > 20) n.State = NpcState.Zwerven;
                return;
            }
            n.State = NpcState.Zwerven;
            float h = ctx.Hour;
            if (IsSleepTime(n, h)) { Sleep(n, dt, ctx); return; }
            n.Sleeping = false;
            if (IsMealTime(h) && n.Job != SettlerJob.Bewaker) { AtFire(n, dt, ctx, true); return; }
            if (h >= 20 && h < 22) { AtFire(n, dt, ctx, false); return; }
            switch (n.Job)
            {
                case SettlerJob.Boer: Farm(n, dt, ctx); break;
                case SettlerJob.Sjouwer: Haul(n, dt, ctx); break;
                case SettlerJob.Bewaker: Guard(n, dt, ctx); break;
                case SettlerJob.Kok: AtFire(n, dt, ctx, true); n.Task = SettlerTask.Koken; break;
                default:
                    if (h >= 8 && h < 19) { var m = s.Point(s.Market.x - s.X0, s.Market.z - s.Z0 + 1); n.Task = SettlerTask.Handelen; if (MoveTo(n, m, n.Def.Speed, dt, ctx)) { n.Yaw = 180; n.Working = false; } }
                    else AtFire(n, dt, ctx, false);
                    break;
            }
        }

        /// <summary>
        /// Tussenpunt op weg naar p: de huizen staan in een rij tussen de akkers (zuid) en het erf (noord);
        /// de open strook in het midden (x 46..74) verbindt ze. Wie in een huis staat, gaat eerst via de deur naar buiten.
        /// </summary>
        static V3 Route(Npc n, V3 p)
        {
            var s = n.Home2;
            if (s == null) return p;
            float lx = n.Pos.X / World.VoxelSize - s.X0, lz = n.Pos.Z / World.VoxelSize - s.Z0;
            float tx = p.X / World.VoxelSize - s.X0, tz = p.Z / World.VoxelSize - s.Z0;
            foreach (var (hx, hz) in s.Houses)
            {
                bool inside = lx > hx - 0.5f && lx < hx + 14.5f && lz > hz - 0.6f && lz < hz + 13.5f;
                bool targetInside = tx > hx && tx < hx + 14.5f && tz > hz && tz < hz + 13.5f;
                if (inside && !targetInside) return lz > hz + 3f ? s.Point(hx + 7, hz + 0.5f) : s.Point(hx + 7, hz - 3.5f);
            }
            const float rowS = 63.5f, rowN = 78.5f;       // de huizenrij ligt tussen deze lijnen
            const float crossX = 59f, lane = 55.5f;       // oversteek over het grindpad; looproute ten zuiden van de marktkraam
            bool south = lz < rowS, targetSouth = tz < rowS;
            if (south != targetSouth && !(lz > rowN && tz > rowN))
            {
                bool atCross = MathF.Abs(lx - crossX) < 1.1f;
                if (!atCross) return s.Point(crossX, south ? lane : Math.Max(lz, 64.5f));
                return s.Point(crossX, targetSouth ? lane : 64.5f);
            }
            return p;
        }

        /// <summary>Loopt naar een punt; true als hij er is.</summary>
        public static bool MoveTo(Npc n, V3 p, float speed, float dt, AiContext ctx, bool route = true, float arrive = 0.7f)
        {
            if (route)
            {
                var w = Route(n, p);
                if (w.X != p.X || w.Z != p.Z) { MoveTo(n, w, speed, dt, ctx, false, 0.12f); return false; }
            }
            var d = p - n.Pos;
            float dist = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            if (dist < arrive) { Brain.Physics(n, dt, ctx, new V3(0, 0, 0), 0, true); return true; }
            // langs muren glijden: kijk welke as geblokkeerd is (een trede van één voxel telt niet)
            // en houd de gekozen kant even vast, zodat hij niet blijft twijfelen
            float side = (n.Seed & 1) == 0 ? 1f : -1f;
            bool bx = MathF.Abs(d.X) > 0.05f && Blocked(n, ctx, MathF.Sign(d.X) * 0.3f, 0);
            bool bz = MathF.Abs(d.Z) > 0.05f && Blocked(n, ctx, 0, MathF.Sign(d.Z) * 0.3f);
            n.SlideTimer -= dt;
            if (bx && !bz)
            {
                if (n.SlideTimer <= 0 || n.SlideZ == 0) { n.SlideZ = MathF.Abs(d.Z) < 0.4f ? side : MathF.Sign(d.Z); n.SlideX = 0; n.SlideTimer = 1f; }
                d = new V3(0, 0, n.SlideZ);
            }
            else if (bz && !bx)
            {
                if (n.SlideTimer <= 0 || n.SlideX == 0) { n.SlideX = MathF.Abs(d.X) < 0.4f ? side : MathF.Sign(d.X); n.SlideZ = 0; n.SlideTimer = 1f; }
                d = new V3(n.SlideX, 0, 0);
            }
            else if (bx && bz) d = new V3(-MathF.Sign(d.X), 0, -MathF.Sign(d.Z));
            else if (n.SlideTimer > 0) d = new V3(d.X + n.SlideX * 0.6f, 0, d.Z + n.SlideZ * 0.6f);   // nog even doorzetten
            else { n.SlideX = 0; n.SlideZ = 0; }
            n.Yaw = MathF.Atan2(d.X, d.Z) * 57.2958f;
            Brain.Physics(n, dt, ctx, d, speed, true);
            n.Working = false;
            return false;
        }

        static bool Blocked(Npc n, AiContext ctx, float dx, float dz)
        {
            var flat = new V3(n.Pos.X + dx, n.Pos.Y + 0.01f, n.Pos.Z + dz);
            if (!VoxelPhysics.Overlaps(ctx.Store, flat, n.Radius * 0.9f, n.Height)) return false;          // gewoon doorlopen
            var up = new V3(n.Pos.X + dx, n.Pos.Y + VoxelPhysics.StepHeight + 0.02f, n.Pos.Z + dz);
            return VoxelPhysics.Overlaps(ctx.Store, up, n.Radius * 0.9f, n.Height);                          // ook niet opstappen
        }

        static void Sleep(Npc n, float dt, AiContext ctx)
        {
            n.Task = SettlerTask.Slapen;
            if (GoHome(n, n.Def.Speed, dt, ctx)) n.Sleeping = true;
        }

        /// <summary>Naar het eigen bed, via de voordeur. True als hij bij het bed is.</summary>
        public static bool GoHome(Npc n, float speed, float dt, AiContext ctx)
        {
            var s = n.Home2;
            int house = (n.BedIndex % s.Beds.Count) / 2;
            var (hx, hz) = s.Houses[house];
            var bed = s.Beds[n.BedIndex % s.Beds.Count];
            float lx = n.Pos.X / World.VoxelSize - s.X0, lz = n.Pos.Z / World.VoxelSize - s.Z0;
            bool inside = lx > hx + 0.5f && lx < hx + 14 && lz > hz + 0.8f && lz < hz + 13;
            if (inside) return MoveTo(n, s.Point(bed.x - s.X0, bed.z - s.Z0 - 1.2f), speed, dt, ctx, false);
            bool atDoor = MathF.Abs(lx - (hx + 7.5f)) < 1.6f && lz > hz - 3 && lz < hz + 1.5f;
            if (atDoor) { MoveTo(n, s.Point(hx + 7, hz + 3), speed, dt, ctx, false, 0.12f); return false; }
            MoveTo(n, s.Point(hx + 7, hz - 2), speed, dt, ctx);
            return false;
        }

        static void AtFire(Npc n, float dt, AiContext ctx, bool eating)
        {
            var s = n.Home2;
            float ang = n.BedIndex * 0.72f;
            var seat = s.Point(s.Fire.x - s.X0 + MathF.Cos(ang) * 3f, s.Fire.z - s.Z0 + MathF.Sin(ang) * 3f);
            n.Task = eating ? SettlerTask.Eten : SettlerTask.Praten;
            if (MoveTo(n, seat, n.Def.Speed, dt, ctx))
            {
                var f = s.Point(s.Fire.x - s.X0, s.Fire.z - s.Z0) - n.Pos;
                n.Yaw = MathF.Atan2(f.X, f.Z) * 57.2958f;
                n.Working = eating;
            }
        }

        static void Farm(Npc n, float dt, AiContext ctx)
        {
            var s = n.Home2;
            if (n.Carry >= 4 || n.Task == SettlerTask.Afleveren)
            {
                n.Task = SettlerTask.Afleveren;
                if (MoveTo(n, s.Point(s.Storage.x - s.X0, s.Storage.z - s.Z0), n.Def.Speed, dt, ctx))
                {
                    n.Carry = 0; n.Task = SettlerTask.Geen;
                }
                return;
            }
            if (n.Task != SettlerTask.Oogsten && n.Task != SettlerTask.Lopen)
            {
                // zoek een rijp gewas, begin op een willekeurige plek in de akker
                int start = ctx.Rng.Next(s.Fields.Count);
                for (int k = 0; k < 400; k++)
                {
                    var t = s.Fields[(start + k * 37) % s.Fields.Count];
                    byte b = ctx.Store.Get(t.x, s.Base + 1, t.z);
                    if (B.IsCrop(b)) { n.Task = SettlerTask.Oogsten; n.TaskX = t.x; n.TaskY = s.Base + 1; n.TaskZ = t.z; n.TaskTimer = 0; break; }
                }
                if (n.Task != SettlerTask.Oogsten)
                {
                    var t = s.Fields[start];
                    n.Task = SettlerTask.Lopen; n.TaskX = t.x; n.TaskY = s.Base + 1; n.TaskZ = t.z; n.TaskTimer = 0;
                }
            }
            var target = new V3((n.TaskX + 0.5f) * World.VoxelSize, n.Pos.Y, (n.TaskZ - 1 + 0.5f) * World.VoxelSize);
            if (!MoveTo(n, target, n.Def.Speed, dt, ctx)) return;
            n.Yaw = 0; n.Working = true;
            n.TaskTimer += dt;
            if (n.TaskTimer < 2.5f) return;
            if (n.Task == SettlerTask.Oogsten)
            {
                byte b = ctx.Store.Get(n.TaskX, n.TaskY, n.TaskZ);
                if (B.IsCrop(b))
                {
                    int type = B.CropIndex(b);
                    ctx.SetBlock?.Invoke(n.TaskX, n.TaskY, n.TaskZ, B.Seedlings[type]);
                    s.Stock[type]++;
                    n.Carry++;
                }
            }
            n.Task = SettlerTask.Geen; n.Working = false;
        }

        static void Haul(Npc n, float dt, AiContext ctx)
        {
            var s = n.Home2;
            bool toMarket = n.Carry > 0;
            var p = toMarket ? s.Point(s.Market.x - s.X0 - 6, s.Market.z - s.Z0 - 4) : s.Point(s.Storage.x - s.X0, s.Storage.z - s.Z0 - 2);
            n.Task = SettlerTask.Sjouwen;
            if (!MoveTo(n, p, n.Def.Speed, dt, ctx)) return;
            n.Working = true;
            n.TaskTimer += dt;
            if (n.TaskTimer < 2f) return;
            n.TaskTimer = 0;
            n.Carry = toMarket ? 0 : 1;
        }

        static void Guard(Npc n, float dt, AiContext ctx)
        {
            var s = n.Home2;
            var pt = s.Patrol[(n.TaskX + n.BedIndex) % s.Patrol.Count];
            n.Task = SettlerTask.Wachtlopen;
            if (!MoveTo(n, s.Point(pt.x - s.X0, pt.z - s.Z0), n.Def.Speed, dt, ctx)) return;
            n.TaskTimer += dt;
            n.Yaw += dt * 25f;          // om zich heen kijken
            if (n.TaskTimer > 4f) { n.TaskTimer = 0; n.TaskX++; }
        }
    }

    /// <summary>Gewassen groeien: elke zaailing heeft per tik een kleine kans om rijp te worden.</summary>
    public static class Farming
    {
        /// <summary>Gemiddeld ~1 speldag (24 min) van zaailing tot rijp gewas.</summary>
        public const float GrowChancePerSecond = 1f / (24 * 60 * 0.8f);

        public static byte Grown(byte seedling) => B.IsSeedling(seedling) ? B.Crops[seedling - B.SeedPotato] : seedling;

        /// <summary>Mag je hier planten? Op akkergrond, gras of aarde met lucht erboven.</summary>
        public static bool CanPlantOn(byte ground, byte above) => (ground == B.Farmland || ground == B.Grass || ground == B.Dirt) && above == B.Air;
    }
}
