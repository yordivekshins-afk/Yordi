using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>De speler als doelwit: schade gaat via bepantsering naar de survivalmeters.</summary>
    public sealed class PlayerActor : Actor
    {
        public GameState Game;
        public V3 LastHitFrom;
        public float LastHitTime = -10;
        readonly System.Random rng = new System.Random();

        public override float TakeDamage(float dmg, HitZone zone, int attacker)
        {
            if (Game.Stats.Dead) return 0;
            float armor = Game.Equipment.ArmorFor(zone);
            float d = dmg * ZoneMultiplier(zone) * (1 - armor);
            Game.Stats.Hurt(d, zone == HitZone.Hoofd ? 0.1f : 0.35f * (1 - armor), rng);
            Health = Game.Stats.Health;
            var a = Game.Combat.Actors.Get(attacker);
            if (a != null) LastHitFrom = a.Pos;
            LastHitTime = Time.time;
            return d;
        }
    }

    /// <summary>
    /// Kogels, vijanden en dieren: spawnen rond de speler, AI laten denken, kogels vliegen,
    /// treffers verwerken, lijken doorzoeken en alles opruimen wat te ver weg is.
    /// </summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        sealed class NpcView
        {
            public Npc Npc;
            public Transform Root;
            public VoxelCharacter Human;
            public VoxelAnimal Animal;
            public Transform Muzzle;
            public List<Stack> Loot;
            public float GrowlTimer;
            public int Arrows;              // pijlen in het lichaam, te recupereren bij het lijk
            public float StepDist;
        }

        const int MaxStuckArrows = 60;
        const int MaxFollowers = 3;
        int groupCounter;
        readonly HashSet<int> announced = new HashSet<int>(), demanding = new HashSet<int>(), fighting = new HashSet<int>();
        struct SavedFollower { public string Name; public Stack Weapon; public float Health; public List<Stack> Loot; }
        readonly List<SavedFollower> pendingFollowers = new List<SavedFollower>();
        readonly List<Transform> stuckArrows = new List<Transform>();

        public ActorWorld Actors { get; } = new ActorWorld();
        public NoiseEvents Noise { get; } = new NoiseEvents();
        public PlayerActor PlayerActor { get; private set; }
        public int Kills;
        GameState game;
        AiContext ctx;
        readonly List<Bullet> bullets = new List<Bullet>();
        struct Grenade { public Vector3 Pos, Vel; public float Fuse; public int Owner; }
        readonly List<Grenade> grenades = new List<Grenade>();
        readonly List<(V3 pos, float delay)> pendingBooms = new List<(V3, float)>();
        readonly List<BulletEvent> events = new List<BulletEvent>();
        readonly List<NpcView> npcs = new List<NpcView>();
        readonly System.Random rng = new System.Random();
        float spawnTimer = 4f;
        int seedCounter = 1;
        readonly Dictionary<Settlement, List<NpcView>> settled = new Dictionary<Settlement, List<NpcView>>();
        readonly HashSet<(Settlement, int)> deadSettlers = new HashSet<(Settlement, int)>();

        public void Init(GameState g)
        {
            game = g;
            PlayerActor = Actors.Add(new PlayerActor { Game = g, Faction = Faction.Speler, Name = "Jij", Height = 1.75f, Radius = 0.3f });
            ctx = new AiContext { Store = g.Chunks.Store, Actors = Actors, Noise = Noise, Player = PlayerActor, Rng = new System.Random(7) };
            ctx.SetBlock = (x, y, z, b) => g.Chunks.SetBlock(x, y, z, b);
        }

        // ------------------------------------------------ schieten
        public void Fire(V3 origin, V3 dir, in WeaponStats st, int owner, bool tracer)
        {
            bullets.Add(Ballistics.Fire(origin, dir, st, owner, tracer));
        }

        void Update()
        {
            if (game == null || !game.Ready || game.InMenu) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var pp = game.Player.Pos;
            PlayerActor.Pos = pp;
            PlayerActor.Yaw = game.Player.Yaw;
            PlayerActor.Health = game.Stats.Health;
            PlayerActor.Height = game.Player.Crouching ? PlayerController.CrouchHeight : PlayerController.Height;
            Noise.Tick(dt);

            ctx.Daylight = Mathf.Clamp01((game.Clock.Ambient - 0.35f) / 0.4f);
            ctx.Hour = game.Clock.HourOfDay;
            ctx.PlayerCrouching = game.Player.Crouching;
            ctx.PlayerFlashlight = game.Player.Flashlight;

            Spawn(dt);
            SpawnPendingFollowers();
            TickNpcs(dt);
            StepBullets(dt);
            StepGrenades(dt);
        }

        void Spawn(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0) return;
            spawnTimer = 3f;
            var pp = game.Player.Pos;
            SpawnSettlements(pp);
            // opruimen (bewoners worden per nederzetting opgeruimd)
            for (int i = npcs.Count - 1; i >= 0; i--)
            {
                var v = npcs[i];
                if (v.Npc.Home2 != null) continue;
                if (v.Npc.LeaderId >= 0 && v.Npc.Alive) continue;      // metgezellen blijven
                float d = Dist(v.Npc.Pos, pp);
                if (d > 160 || (!v.Npc.Alive && v.Npc.DeadTime > 240)) Despawn(i);
            }
            int vx = Mathf.FloorToInt(pp.X / World.VoxelSize), vz = Mathf.FloorToInt(pp.Z / World.VoxelSize);
            float rad = game.Gen.RadiationAt(vx, vz, out _);
            int alive = 0; foreach (var v in npcs) if (v.Npc.Alive && v.Npc.Home2 == null && v.Npc.LeaderId < 0) alive++;
            int budget = ctx.Daylight < 0.3f ? 12 : 8;
            if (rad > 0.1f) budget += 4;
            if (alive >= budget) return;

            float ang = (float)(rng.NextDouble() * Mathf.PI * 2), dist = 45 + (float)rng.NextDouble() * 45;
            float sx = pp.X + Mathf.Cos(ang) * dist, sz = pp.Z + Mathf.Sin(ang) * dist;
            if (!game.Chunks.IsLoaded(new Vector3(sx, 0, sz))) return;
            int svx = Mathf.FloorToInt(sx / World.VoxelSize), svz = Mathf.FloorToInt(sz / World.VoxelSize);
            if (game.Gen.SettlementNear(svx, svz, out _) != null) return;     // niet binnen de palissade
            float srad = game.Gen.RadiationAt(svx, svz, out _);
            var group = Spawner.Choose(game.Gen, svx, svz, ctx.Daylight, srad, rng);
            if (group == null) return;
            var g = group.Value;
            int gid = ++groupCounter;
            // sommige bendes schieten niet meteen maar eisen tol
            int toll = g.Type == NpcType.Bendelid && rng.NextDouble() < 0.45 ? 30 + rng.Next(4) * 15 : 0;
            for (int k = 0; k < g.Count; k++)
            {
                float ox = sx + (float)(rng.NextDouble() - 0.5) * 6, oz = sz + (float)(rng.NextDouble() - 0.5) * 6;
                float gy = PlayerController.GroundAt(game.Chunks.Store, ox, oz);
                if (gy < World.SeaLevelMeters + 0.3f) continue;       // niet in het water
                var nv = SpawnNpc(g.Type, new V3(ox, gy + 0.02f, oz));
                nv.Npc.Group = gid; nv.Npc.Toll = toll;
            }
        }

        void SpawnSettlements(V3 pp)
        {
            int vx = Mathf.FloorToInt(pp.X / World.VoxelSize), vz = Mathf.FloorToInt(pp.Z / World.VoxelSize);
            int ci = World.FloorDiv(vx + WorldGen.CityCell / 2, WorldGen.CityCell), cj = World.FloorDiv(vz - WorldGen.CityCell / 2, WorldGen.CityCell);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    var st = game.Gen.GetSettlement(ci + di, cj + dj);
                    if (st == null) continue;
                    var c = st.Point(Settlement.W / 2f, Settlement.D / 2f);
                    float d = Mathf.Sqrt((c.X - pp.X) * (c.X - pp.X) + (c.Z - pp.Z) * (c.Z - pp.Z));
                    bool have = settled.ContainsKey(st);
                    if (d < 170 && !have && game.Chunks.IsLoaded(new Vector3(c.X, 0, c.Z))) SpawnSettlers(st);
                    else if (d > 240 && have) DespawnSettlers(st);
                }
        }

        void SpawnSettlers(Settlement st)
        {
            var list = new List<NpcView>();
            settled[st] = list;
            st.TraderStock ??= st.MakeTraderStock(rng);
            float h = game.Clock.HourOfDay;
            for (int k = 0; k < Settlement.Population.Length; k++)
            {
                if (deadSettlers.Contains((st, k))) continue;
                var job = Settlement.Population[k];
                var probe = new Npc(Npcs.Defs[NpcType.Overlever], default, 0) { Job = job, BedIndex = k };
                V3 pos;
                if (SettlerBrain.IsSleepTime(probe, h)) { var bed = st.Beds[k % st.Beds.Count]; pos = st.Point(bed.x - st.X0, bed.z - st.Z0 - 1.2f); }
                else { float ang = k * 0.72f; pos = st.Point(st.Fire.x - st.X0 + Mathf.Cos(ang) * 3f, st.Fire.z - st.Z0 + Mathf.Sin(ang) * 3f); }
                pos.Y = (st.Base + 1) * World.VoxelSize + 0.02f;
                var v = SpawnNpc(NpcType.Overlever, pos, st, job, k);
                list.Add(v);
            }
        }

        void DespawnSettlers(Settlement st)
        {
            foreach (var v in settled[st])
            {
                if (!v.Npc.Alive) deadSettlers.Add((st, v.Npc.BedIndex));
                int i = npcs.IndexOf(v);
                if (i >= 0) Despawn(i);
            }
            settled.Remove(st);
        }

        NpcView SpawnNpc(NpcType type, V3 pos) => SpawnNpc(type, pos, null, SettlerJob.Boer, 0);

        NpcView SpawnNpc(NpcType type, V3 pos, Settlement home, SettlerJob job, int index)
        {
            var def = Npcs.Defs[type];
            var n = Actors.Add(new Npc(def, pos, seedCounter++));
            n.Yaw = (float)rng.NextDouble() * 360f;
            if (home != null) { n.Home2 = home; n.Job = job; n.BedIndex = index; n.Name = home.PersonName(index); n.Home = pos; }
            var v = new NpcView { Npc = n, GrowlTimer = (float)rng.NextDouble() * 6 };
            v.Root = new GameObject(def.Name).transform;
            v.Root.SetParent(transform, false);
            switch (type)
            {
                case NpcType.Mutantwolf:
                case NpcType.Hert:
                    v.Animal = VoxelAnimal.Build(v.Root, type);
                    break;
                case NpcType.Ghoul:
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.Ghoul);
                    break;
                case NpcType.Brute:
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.Brute);
                    break;
                case NpcType.Zwerver:
                {
                    var eq = SettlerGear(rng.NextDouble() < 0.5 ? SettlerJob.Sjouwer : SettlerJob.Boer);
                    n.ArmorHead = eq.ArmorFor(HitZone.Hoofd); n.ArmorBody = eq.ArmorFor(HitZone.Romp); n.ArmorLegs = eq.ArmorFor(HitZone.Benen);
                    n.Name = Settlement.People[rng.Next(Settlement.People.Length)];
                    double r = rng.NextDouble();
                    n.Weapon = r < 0.45 ? Loot.MakeItem("pistool", rng) : r < 0.6 ? Loot.MakeItem("shotgun", rng) : r < 0.8 ? new Stack("boog", 1) : new Stack(rng.NextDouble() < 0.5 ? "mes" : "machete", 1);
                    if (n.Weapon.Def.GunDamage > 0) n.Weapon.Ammo = Arsenal.Stats(n.Weapon).MagSize;
                    v.Loot = new List<Stack> { new Stack("water", 1), new Stack("doppen", 5 + rng.Next(20)) };
                    if (rng.NextDouble() < 0.5) v.Loot.Add(new Stack(rng.NextDouble() < 0.5 ? "bonen" : "brood", 1));
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.FromEquipment(eq, rng.NextDouble() < 0.5 ? B.Skin : B.SkinDark, B.Hair));
                    v.Muzzle = v.Human.SetTool(n.Weapon, out _);
                    break;
                }
                case NpcType.Overlever:
                {
                    var eq = SettlerGear(job);
                    n.ArmorHead = eq.ArmorFor(HitZone.Hoofd); n.ArmorBody = eq.ArmorFor(HitZone.Romp); n.ArmorLegs = eq.ArmorFor(HitZone.Benen);
                    if (job == SettlerJob.Bewaker) { n.Weapon = Loot.MakeItem(rng.NextDouble() < 0.5 ? "geweer" : "ak", rng); n.Weapon.Ammo = Arsenal.Stats(n.Weapon).MagSize; }
                    else n.Weapon = Stack.None;
                    v.Loot = new List<Stack>();
                    foreach (var sl in eq.Slots) if (!sl.Empty && rng.NextDouble() < 0.4) v.Loot.Add(sl);
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.FromEquipment(eq, rng.NextDouble() < 0.5 ? B.Skin : B.SkinDark, rng.NextDouble() < 0.3 ? B.Khaki : B.Hair));
                    v.Muzzle = v.Human.SetTool(n.Weapon, out _);
                    break;
                }
                default:
                {
                    var eq = RaiderGear(type);
                    n.ArmorHead = eq.ArmorFor(HitZone.Hoofd); n.ArmorBody = Mathf.Max(def.ArmorBody, eq.ArmorFor(HitZone.Romp)); n.ArmorLegs = eq.ArmorFor(HitZone.Benen);
                    if (type == NpcType.Bendelid)
                    {
                        string[] guns = { "pistool", "pistool", "mp5", "ak", "shotgun", "m4" };
                        n.Weapon = Loot.MakeItem(guns[rng.Next(guns.Length)], rng);
                        n.Weapon.Ammo = Arsenal.Stats(n.Weapon).MagSize;
                    }
                    else if (type == NpcType.Aaseter)
                    {
                        double r = rng.NextDouble();
                        n.Weapon = r < 0.3 ? new Stack("boog", 1) { Ammo = 1 } : new Stack(r < 0.55 ? "mes" : r < 0.8 ? "pijp" : "bijl", 1);
                    }
                    v.Loot = new List<Stack>();
                    foreach (var s in eq.Slots) if (!s.Empty && rng.NextDouble() < 0.45) v.Loot.Add(s);
                    if (n.Weapon.Id == "boog") v.Loot.Add(new Stack("pijl", rng.Next(3, 9)));
                    var skin = rng.NextDouble() < 0.5 ? B.Skin : B.SkinDark;
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.FromEquipment(eq, skin, B.Hair));
                    v.Muzzle = v.Human.SetTool(n.Weapon, out _);
                    break;
                }
            }
            SyncView(v, 0);
            npcs.Add(v);
            return v;
        }

        Equipment SettlerGear(SettlerJob job)
        {
            var eq = new Equipment();
            string Pick(params string[] a) => a[rng.Next(a.Length)];
            switch (job)
            {
                case SettlerJob.Boer:
                    eq.Wear(new Stack(Pick("jeans", "cargobroek"), 1)); eq.Wear(new Stack(Pick("wandelschoenen", "schoenen"), 1));
                    eq.Wear(new Stack(Pick("jas", "hoodie", "tshirt"), 1)); if (rng.NextDouble() < 0.6) eq.Wear(new Stack(Pick("pet", "muts"), 1));
                    break;
                case SettlerJob.Bewaker:
                    eq.Wear(new Stack("legerbroek", 1)); eq.Wear(new Stack("legerkistjes", 1)); eq.Wear(new Stack(Pick("legerjas", "jas"), 1));
                    eq.Wear(new Stack(Pick("politievest", "chestrig"), 1)); eq.Wear(new Stack(Pick("helm", "pet"), 1));
                    break;
                case SettlerJob.Kok:
                    eq.Wear(new Stack("jeans", 1)); eq.Wear(new Stack("schoenen", 1)); eq.Wear(new Stack("tshirt", 1)); eq.Wear(new Stack("bandana", 1));
                    break;
                case SettlerJob.Sjouwer:
                    eq.Wear(new Stack("cargobroek", 1)); eq.Wear(new Stack("legerkistjes", 1)); eq.Wear(new Stack("hoodie", 1)); eq.Wear(new Stack("rugzak", 1));
                    break;
                default:
                    eq.Wear(new Stack("cargobroek", 1)); eq.Wear(new Stack("wandelschoenen", 1)); eq.Wear(new Stack("winterjas", 1)); eq.Wear(new Stack("schoudertas", 1)); eq.Wear(new Stack("pet", 1));
                    break;
            }
            return eq;
        }

        Equipment RaiderGear(NpcType t)
        {
            var eq = new Equipment();
            string Pick(params string[] a) => a[rng.Next(a.Length)];
            switch (t)
            {
                case NpcType.Aaseter:
                    eq.Wear(new Stack(Pick("jeans", "joggingbroek", "cargobroek"), 1));
                    eq.Wear(new Stack(Pick("sneakers", "schoenen"), 1));
                    eq.Wear(new Stack(Pick("hoodie", "tshirt", "jas"), 1));
                    if (rng.NextDouble() < 0.5) eq.Wear(new Stack("bandana", 1));
                    if (rng.NextDouble() < 0.3) eq.Wear(new Stack("schoudertas", 1));
                    break;
                case NpcType.Bendelid:
                    eq.Wear(new Stack(Pick("cargobroek", "legerbroek", "jeans"), 1));
                    eq.Wear(new Stack(Pick("legerkistjes", "wandelschoenen"), 1));
                    eq.Wear(new Stack(Pick("legerjas", "jas", "hoodie"), 1));
                    eq.Wear(new Stack(Pick("chestrig", "politievest", "chestrig"), 1));
                    if (rng.NextDouble() < 0.4) eq.Wear(new Stack(Pick("bandana", "gasmasker"), 1));
                    if (rng.NextDouble() < 0.5) eq.Wear(new Stack(Pick("pet", "muts", "helm"), 1));
                    if (rng.NextDouble() < 0.4) eq.Wear(new Stack("rugzak", 1));
                    break;
                default:
                    eq.Wear(new Stack("legerbroek", 1)); eq.Wear(new Stack("legerkistjes", 1)); eq.Wear(new Stack("legerjas", 1));
                    eq.Wear(new Stack(Pick("pet", "helm"), 1)); eq.Wear(new Stack("legerrugzak", 1));
                    break;
            }
            return eq;
        }

        void Despawn(int i)
        {
            var v = npcs[i];
            Actors.Remove(v.Npc);
            Destroy(v.Root.gameObject);
            npcs.RemoveAt(i);
        }

        void TickNpcs(float dt)
        {
            var pp = game.Player.Pos;
            foreach (var v in npcs)
            {
                var n = v.Npc;
                bool wasAlive = n.Alive;
                // alleen chunks waar de NPC staat moeten geladen zijn
                if (!game.Chunks.IsLoaded(new Vector3(n.Pos.X, 0, n.Pos.Z))) continue;
                if (n.Home2 != null) SettlerBrain.Tick(n, dt, ctx); else Brain.Tick(n, dt, ctx);
                if (n.FiredThisFrame)
                {
                    var st = Arsenal.Stats(n.Weapon);
                    for (int p = 0; p < st.Pellets; p++)
                    {
                        var dir = n.FireDir;
                        if (st.Pellets > 1) { float s = st.Spread * 0.0175f; dir = new V3(dir.X + R() * s, dir.Y + R() * s, dir.Z + R() * s); }
                        Fire(n.FireOrigin, dir, st, n.Id, rng.NextDouble() < 0.4);
                    }
                    var mz = v.Muzzle ? v.Muzzle.position : ToV(n.FireOrigin);
                    if (!st.Arrow) Fx.Instance.MuzzleFlash(mz, ToV(n.FireDir).normalized, st.HidesFlash);
                    Sfx.Instance.Play(ShotSound(n.Weapon), mz, 1f, 1f, st.Noise * 2);
                }
                if (n.MeleeThisFrame)
                {
                    Sfx.Instance.Play(n.Def.Faction == Faction.Mutant ? "grom" : "slag", ToV(n.Pos) + Vector3.up, 0.9f, n.Def.Type == NpcType.Brute ? 0.6f : 1.1f);
                    var t = Actors.Get(n.TargetId);
                    if (t != null) Fx.Instance.Blood(ToV(t.Chest), Vector3.up, 6);
                }
                if (n.Def.Faction == Faction.Mutant && n.Alive)
                {
                    v.GrowlTimer -= dt;
                    if (v.GrowlTimer <= 0) { v.GrowlTimer = 4 + (float)rng.NextDouble() * 6; Sfx.Instance.Play("grom", ToV(n.Pos) + Vector3.up, 0.6f, n.Def.Type == NpcType.Brute ? 0.55f : n.Def.Type == NpcType.Mutantwolf ? 1.5f : 1f, 60f); }
                }
                if (wasAlive && !n.Alive) OnDeath(v);
                SyncView(v, dt);
            }
            // bendes: samen tol eisen, samen aanvallen
            demanding.Clear(); fighting.Clear();
            foreach (var v in npcs)
            {
                var n = v.Npc;
                if (!n.Alive || n.Group == 0) continue;
                if (n.State == NpcState.Eisen) demanding.Add(n.Group);
                if (n.State == NpcState.Aanvallen && n.TargetId == PlayerActor.Id) fighting.Add(n.Group);
            }
            foreach (var v in npcs)
            {
                var n = v.Npc;
                if (!n.Alive || n.Group == 0) continue;
                if (fighting.Contains(n.Group) && n.State == NpcState.Eisen) { n.State = NpcState.Aanvallen; n.StateTime = 0; }
                else if (demanding.Contains(n.Group) && !fighting.Contains(n.Group) && !n.Paid && (n.State == NpcState.Zwerven || n.State == NpcState.Rust || n.State == NpcState.Onderzoeken))
                { n.State = NpcState.Eisen; n.TargetId = PlayerActor.Id; n.Awareness = 1; n.StateTime = 0; }
            }
            foreach (var gid in demanding)
            {
                if (!announced.Add(gid)) continue;
                foreach (var v in npcs)
                    if (v.Npc.Group == gid && v.Npc.State == NpcState.Eisen)
                    {
                        game.Hud.Message($"{v.Npc.Def.Name}: \"Halt! {v.Npc.Toll} doppen, of je bent er geweest.\"  (E om te praten)");
                        Sfx.Instance.Play("slag", ToV(v.Npc.Chest), 0.2f, 1.6f, 30f);
                        break;
                    }
            }

            // wie één bewoner aanvalt, krijgt het hele dorp tegen zich
            foreach (var kv in settled)
            {
                bool angry = false;
                foreach (var v in kv.Value) if (v.Npc.Angry) angry = true;
                if (!angry) continue;
                foreach (var v in kv.Value) if (!v.Npc.Angry) { v.Npc.Angry = true; v.Npc.Awareness = 1; v.Npc.TargetId = PlayerActor.Id; }
            }
        }

        /// <summary>De speler steelt van de akker: wie het ziet, wordt boos.</summary>
        public void ReportTheft(Settlement st, V3 at)
        {
            if (!settled.TryGetValue(st, out var list)) return;
            foreach (var v in list)
            {
                var n = v.Npc;
                if (!n.Alive || n.Sleeping) continue;
                if (Dist(n.Pos, at) < 28 && Brain.CanSee(n, PlayerActor, Dist(n.Pos, PlayerActor.Pos), ctx))
                {
                    n.Angry = true;
                    game.Hud.Message($"{n.Name}: \"Hé! Blijf van onze oogst af!\"");
                    return;
                }
            }
        }

        /// <summary>Spreek een bewoner aan: handelaar opent de winkel, anderen zeggen iets.</summary>
        public bool TryTalk(Vector3 eye, Vector3 dir)
        {
            NpcView best = null; float bestScore = float.MaxValue;
            foreach (var v in npcs)
            {
                var nn = v.Npc;
                if (!nn.Alive) continue;
                bool demand = nn.State == NpcState.Eisen;
                if (nn.Home2 == null && nn.Def.Type != NpcType.Zwerver && !demand) continue;
                var c = ToV(nn.Chest);
                float d = Vector3.Distance(eye, c);
                float dot = Vector3.Dot((c - eye).normalized, dir);
                if (d < (demand ? 16f : 3.2f) && dot > (demand ? 0.9f : 0.6f) && d < bestScore) { best = v; bestScore = d; }
            }
            if (best == null) return false;
            var n = best.Npc;
            if (n.State == NpcState.Eisen) { Negotiate(best); return true; }
            if (n.Def.Type == NpcType.Zwerver)
            {
                if (n.Angry) { game.Hud.Message($"{n.Name} richt zijn wapen op je. Hier valt niet meer te praten."); return true; }
                if (n.LeaderId == PlayerActor.Id) FollowerDialog(best); else RecruitDialog(best);
                return true;
            }
            if (n.Angry) { game.Hud.Message($"{n.Name} wil niets meer met je te maken hebben."); return true; }
            if (n.Sleeping) { game.Hud.Message($"{n.Name} slaapt."); return true; }
            float h = game.Clock.HourOfDay;
            if (n.Job == SettlerJob.Handelaar && h >= 8 && h < 19) { game.Hud.OpenTrade(n.Home2, n.Name); return true; }
            string job = n.Job.ToString().ToLowerInvariant();
            game.Hud.Message($"{n.Name} ({job}): \"{SettlerBrain.Line(n, rng)}\"");
            if (n.Job == SettlerJob.Handelaar) game.Hud.Message("De markt is open van 8 tot 19 uur.");
            return true;
        }

        // ------------------------------------------------ metgezellen en onderhandelen
        public int FollowerCount { get { int c = 0; foreach (var v in npcs) if (v.Npc.Alive && v.Npc.LeaderId == PlayerActor.Id) c++; return c; } }

        static readonly string[] WandererLines =
        {
            "Alleen overleef je hier niet lang. Met z'n tweeën misschien wel.",
            "Ik zoek al weken naar een reden om niet op te geven. Heb je eten?",
            "Ik kan schieten, sjouwen en mijn mond houden. Wat zoek je?",
            "De ghouls komen elke nacht dichterbij. Ik wil hier weg.",
        };

        void RecruitDialog(NpcView v)
        {
            var n = v.Npc; var inv = game.Inventory;
            bool full = FollowerCount >= MaxFollowers;
            int food = FoodSlot();
            var ch = new List<Hud.Choice>
            {
                new Hud.Choice("\"Reis met me mee.\"  (1 maaltijd + 1 water)", () => { inv.TakeFromSlot(FoodSlot()); inv.Remove("water", 1); Recruit(v); }, !full && food >= 0 && inv.Count("water") > 0),
                new Hud.Choice("\"Reis met me mee.\"  (40 doppen)", () => { inv.Remove("doppen", 40); Recruit(v); }, !full && inv.Count("doppen") >= 40),
                new Hud.Choice("\"Succes daar buiten.\"", null),
            };
            string weapon = n.Weapon.Empty ? "ongewapend" : n.Weapon.Def.Name.ToLowerInvariant();
            game.Hud.OpenDialog($"{n.Name}, zwerver ({weapon})", $"\"{WandererLines[n.Seed % WandererLines.Length]}\"" + (full ? "\n(Je reist al met drie metgezellen.)" : ""), ch);
        }

        int FoodSlot()
        {
            var inv = game.Inventory;
            for (int i = 0; i < inv.Capacity; i++) { var d = inv.Slots[i].Def; if (d != null && d.Kind == ItemKind.Food && d.Food > 0) return i; }
            return -1;
        }

        void Recruit(NpcView v)
        {
            var n = v.Npc;
            n.LeaderId = PlayerActor.Id; n.Waiting = false; n.Angry = false; n.TargetId = -1; n.State = NpcState.Zwerven;
            game.Hud.Message($"{n.Name} reist nu met je mee. Praat met {n.Name} (E) voor bevelen.");
        }

        void FollowerDialog(NpcView v)
        {
            var n = v.Npc; var inv = game.Inventory;
            int sel = game.Player.Selected;
            var held = inv.Slots[sel];
            bool canGive = !held.Empty && held.Def.Kind == ItemKind.Weapon;
            var ch = new List<Hud.Choice>
            {
                n.Waiting ? new Hud.Choice("\"Volg me.\"", () => { n.Waiting = false; game.Hud.Message($"{n.Name} volgt je weer."); })
                          : new Hud.Choice("\"Wacht hier.\"", () => { n.Waiting = true; n.WaitPos = n.Pos; game.Hud.Message($"{n.Name} wacht hier."); }),
                new Hud.Choice(canGive ? $"\"Neem dit.\"  (geef {held.Def.Name})" : "\"Neem dit.\"  (houd een wapen vast om het te geven)", () =>
                {
                    var old = n.Weapon;
                    n.Weapon = inv.Slots[sel];
                    if (n.Weapon.Def.GunDamage > 0) n.Weapon.Ammo = Arsenal.Stats(n.Weapon).MagSize;
                    inv.Slots[sel] = old;
                    v.Muzzle = v.Human.SetTool(n.Weapon, out _);
                    game.Player.RefreshTool();
                    game.Hud.Message($"{n.Name} neemt de {n.Weapon.Def.Name.ToLowerInvariant()} aan" + (old.Empty ? "." : $" en geeft je de {old.Def.Name.ToLowerInvariant()}."));
                }, canGive),
                new Hud.Choice("\"Wat draag je bij je?\"", () => { v.Loot ??= new List<Stack>(); game.Hud.OpenLoot(v.Loot, "tas van " + n.Name); }),
                new Hud.Choice("\"Ga je eigen weg.\"", () => { n.LeaderId = -1; n.Waiting = false; n.Home = n.Pos; game.Hud.Message($"{n.Name} gaat alleen verder."); }),
            };
            float hp = n.Health / n.MaxHealth;
            game.Hud.OpenDialog($"{n.Name}, metgezel", $"Gezondheid {hp * 100:0}%  ·  {(n.Weapon.Empty ? "ongewapend" : n.Weapon.Def.Name)}\n\"{(hp < 0.4f ? "Ik heb een verband nodig…" : n.Waiting ? "Ik hou de boel hier in de gaten." : "Ik ben er klaar voor.")}\"", ch);
        }

        void Negotiate(NpcView v)
        {
            var n = v.Npc; var inv = game.Inventory;
            int toll = n.Toll;
            float armor = game.Equipment.ArmorFor(HitZone.Romp);
            bool gun = game.Player.HasGun && game.Player.Weapon.Damage > 35;
            float chance = Mathf.Clamp(0.12f + armor * 0.6f + (gun ? 0.15f : 0) + FollowerCount * 0.12f + Mathf.Min(0.2f, Kills * 0.01f), 0.05f, 0.9f);
            int group = n.Group;
            var ch = new List<Hud.Choice>
            {
                new Hud.Choice($"Betalen  ({toll} doppen)", () =>
                {
                    inv.Remove("doppen", toll);
                    foreach (var o in npcs) if (o.Npc.Group == group) { o.Npc.Paid = true; if (o.Npc.State == NpcState.Eisen) o.Npc.State = NpcState.Zwerven; o.Npc.TargetId = -1; }
                    game.Hud.Message("Ze tellen de doppen en laten je gaan.");
                }, inv.Count("doppen") >= toll),
                new Hud.Choice($"Dreigen  (kans {chance * 100:0}%)", () =>
                {
                    if (rng.NextDouble() < chance)
                    {
                        foreach (var o in npcs) if (o.Npc.Group == group && o.Npc.Alive) { o.Npc.Paid = true; o.Npc.State = NpcState.Vluchten; o.Npc.StateTime = 0; o.Npc.LastSeen = PlayerActor.Pos; }
                        game.Hud.Message($"{n.Def.Name}: \"Rustig, rustig… we gaan al.\"");
                    }
                    else { GroupAttack(group); game.Hud.Message($"{n.Def.Name}: \"Jij? Ons bang maken? Vuur!\""); }
                }),
                new Hud.Choice("Weigeren", () => { GroupAttack(group); game.Hud.Message($"{n.Def.Name}: \"Verkeerde keuze.\""); }),
            };
            game.Hud.OpenDialog($"{n.Def.Name} eist tol", $"\"Dit stuk is van ons. {toll} doppen en je loopt door. Anders…\"\nJe hebt {inv.Count("doppen")} doppen.", ch);
        }

        void GroupAttack(int group)
        {
            foreach (var o in npcs)
                if (o.Npc.Group == group && o.Npc.Alive) { o.Npc.State = NpcState.Aanvallen; o.Npc.TargetId = PlayerActor.Id; o.Npc.Awareness = 1; o.Npc.Paid = false; o.Npc.Toll = 0; o.Npc.StateTime = 0; }
        }

        public void WriteFollowers(System.IO.BinaryWriter w)
        {
            var list = new List<NpcView>();
            foreach (var v in npcs) if (v.Npc.Alive && v.Npc.LeaderId == PlayerActor.Id) list.Add(v);
            w.Write(list.Count);
            foreach (var v in list)
            {
                w.Write(v.Npc.Name ?? "Zwerver");
                v.Npc.Weapon.Write(w);
                w.Write(v.Npc.Health);
                var loot = v.Loot ?? new List<Stack>();
                w.Write(loot.Count);
                foreach (var s in loot) s.Write(w);
            }
        }

        public void ReadFollowers(System.IO.BinaryReader r)
        {
            pendingFollowers.Clear();
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var f = new SavedFollower { Name = r.ReadString(), Weapon = Stack.Read(r), Health = r.ReadSingle(), Loot = new List<Stack>() };
                int lc = r.ReadInt32();
                for (int k = 0; k < lc; k++) f.Loot.Add(Stack.Read(r));
                pendingFollowers.Add(f);
            }
        }

        void SpawnPendingFollowers()
        {
            if (pendingFollowers.Count == 0) return;
            var pp = game.Player.Pos;
            for (int i = 0; i < pendingFollowers.Count; i++)
            {
                var f = pendingFollowers[i];
                float ang = i * 2.1f;
                float x = pp.X + Mathf.Cos(ang) * 2.5f, z = pp.Z + Mathf.Sin(ang) * 2.5f;
                var v = SpawnNpc(NpcType.Zwerver, new V3(x, PlayerController.GroundAt(game.Chunks.Store, x, z) + 0.02f, z));
                var n = v.Npc;
                n.Name = f.Name; n.Weapon = f.Weapon; n.Health = Mathf.Max(10, f.Health); n.LeaderId = PlayerActor.Id;
                v.Loot = f.Loot;
                v.Muzzle = v.Human.SetTool(n.Weapon, out _);
            }
            pendingFollowers.Clear();
        }

        void OnDeath(NpcView v)
        {
            var n = v.Npc;
            if (v.Human) v.Human.Die();
            if (v.Animal) v.Animal.Die();
            if (n.LastAttacker == PlayerActor.Id)
            {
                Kills++;
                game.Hud.Message(n.Home2 != null ? $"Je hebt {n.Name} gedood. {n.Home2.Name} zal dit niet vergeten." : $"{n.Def.Name} uitgeschakeld.");
            }
            if (n.Home2 != null) deadSettlers.Add((n.Home2, n.BedIndex));
            if (n.LeaderId == PlayerActor.Id) game.Hud.Banner(n.Name + " is gesneuveld", "je metgezel is dood");
            v.Loot ??= new List<Stack>();
            if (!n.Weapon.Empty) v.Loot.Add(n.Weapon);
            if (v.Arrows > 0) v.Loot.Add(new Stack("pijl", v.Arrows));
            foreach (var d in n.Def.Drops)
            {
                if (rng.NextDouble() > 0.55) continue;
                var def = Items.Get(d);
                if (def == null) continue;
                int count = def.Kind == ItemKind.Ammo ? rng.Next(4, 18) : 1;
                if (def.MaxStack == 1) v.Loot.Add(Loot.MakeItem(d, rng)); else v.Loot.Add(new Stack(d, count));
            }
        }

        void SyncView(NpcView v, float dt)
        {
            var n = v.Npc;
            // voetstappen: zo hoor je iemand aankomen
            float spd = new Vector2(n.Vel.X, n.Vel.Z).magnitude;
            if (n.Alive && n.Grounded && spd > 0.5f && Dist(n.Pos, game.Player.Pos) < 30f)
            {
                v.StepDist += spd * dt;
                float stride = v.Animal ? 0.9f : n.Def.Type == NpcType.Brute ? 2.2f : 1.3f;
                if (v.StepDist > stride)
                {
                    v.StepDist = 0;
                    byte under = game.Chunks.Store.Get(Mathf.FloorToInt(n.Pos.X / World.VoxelSize), Mathf.FloorToInt((n.Pos.Y - 0.05f) / World.VoxelSize), Mathf.FloorToInt(n.Pos.Z / World.VoxelSize));
                    float loud = n.Def.Type == NpcType.Brute ? 1.6f : v.Animal ? 0.5f : spd > 3f ? 1f : 0.6f;
                    Sfx.Instance.Footstep(Blocks.SurfaceOf(under), ToV(n.Pos), loud, false);
                }
            }
            v.Root.position = ToV(n.Pos);
            v.Root.rotation = Quaternion.Slerp(v.Root.rotation, Quaternion.Euler(0, n.Yaw, 0), 1 - Mathf.Exp(-10 * dt));
            if (v.Human)
            {
                bool still = n.Speed01 < 0.3f;
                v.Human.Sleeping = n.Sleeping;
                v.Human.Sitting = still && (n.Task == SettlerTask.Eten || n.Task == SettlerTask.Praten) && n.Job != SettlerJob.Kok && n.Home2 != null;
                v.Human.Working = still && n.Working && !v.Human.Sitting;
                v.Human.Carrying = n.Home2 != null && n.Carry > 0 && (n.Job == SettlerJob.Sjouwer || n.Task == SettlerTask.Afleveren);
                v.Human.Animate(n.Speed01, false, n.Grounded, (n.State == NpcState.Aanvallen || n.State == NpcState.Eisen) && v.Muzzle != null, n.MeleeThisFrame ? 1 : 0, dt);
            }
            if (v.Animal) v.Animal.Animate(n.Speed01, dt, n.MeleeThisFrame);
        }

        void StepBullets(float dt)
        {
            events.Clear();
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                var from = b.Pos;
                Ballistics.Step(ref b, dt, game.Chunks.Store, Actors, events);
                if (b.Arrow) Fx.Instance.Streak(ToV(from), ToV(b.Pos), B.Wood, 0.014f, 0.025f);
                else if (b.Tracer) Fx.Instance.Streak(ToV(from), ToV(b.Pos), B.Tracer, 0.012f, 0.03f);
                if (b.Alive) bullets[i] = b; else bullets.RemoveAt(i);
            }
            foreach (var e in events)
            {
                var p = ToV(e.Pos);
                var nrm = new Vector3(e.Nx, e.Ny, e.Nz);
                switch (e.Type)
                {
                    case BulletEventType.Impact:
                    case BulletEventType.Penetrate:
                        if (e.Block == B.ExplosiveBarrel)
                        {
                            pendingBooms.Add((new V3((e.X + 0.5f) * World.VoxelSize, (e.Y + 0.5f) * World.VoxelSize, (e.Z + 0.5f) * World.VoxelSize), 0.15f));
                            break;
                        }
                        if (e.Arrow)
                        {
                            if (e.Type == BulletEventType.Impact)
                            {
                                StickArrow(p, ToV(e.Dir));
                                Sfx.Instance.Play("pijl_inslag", p, 0.6f, 1f, 40f);
                                Fx.Instance.Burst(p, nrm, e.Block, 3, 1.2f, 0.03f, 0.6f);
                            }
                            Noise.Emit(e.Pos, 4, e.Owner);
                            break;
                        }
                        Fx.Instance.Impact(p, nrm, e.Block);
                        if (e.Type == BulletEventType.Impact) Sfx.Instance.Play("inslag", p, 0.5f, 1f, 60f);
                        Noise.Emit(e.Pos, 8, e.Owner);
                        break;
                    case BulletEventType.Glass:
                        game.Chunks.SetBlock(e.X, e.Y, e.Z, B.Air);
                        Fx.Instance.Burst(p, nrm, B.Glass, 14, 2.5f, 0.04f, 1.4f);
                        Sfx.Instance.Play("glas", p, 0.8f, 1f, 80f);
                        Noise.Emit(e.Pos, 25, e.Owner);
                        break;
                    case BulletEventType.Actor:
                    {
                        var victim = e.Victim;
                        bool wasAlive = victim.Alive;
                        float dealt = victim.TakeDamage(e.Damage, e.Zone, e.Owner);
                        Fx.Instance.Blood(p, -nrm + Vector3.up * 0.3f, e.Zone == HitZone.Hoofd ? 16 : 9);
                        if (e.Arrow)
                        {
                            Sfx.Instance.Play("pijl_inslag", p, 0.5f, 0.7f, 30f);
                            if (rng.NextDouble() < 0.7) foreach (var nv in npcs) if (nv.Npc == victim) { nv.Arrows++; break; }
                        }
                        if (e.Owner == PlayerActor.Id)
                        {
                            game.Hud.HitMarker(!victim.Alive && wasAlive, e.Zone == HitZone.Hoofd);
                            Sfx.Instance.Play2D("treffer", 0.35f, e.Zone == HitZone.Hoofd ? 1.4f : 1f);
                        }
                        break;
                    }
                }
            }
        }

        public void ThrowGrenade(Vector3 from, Vector3 vel, int owner) => grenades.Add(new Grenade { Pos = from, Vel = vel, Fuse = 3f, Owner = owner });

        void StepGrenades(float dt)
        {
            var store = game.Chunks.Store;
            for (int i = grenades.Count - 1; i >= 0; i--)
            {
                var g = grenades[i];
                g.Fuse -= dt;
                g.Vel.y -= 9.81f * dt;
                // per as bewegen en stuiteren tegen voxels
                var p = g.Pos;
                for (int axis = 0; axis < 3; axis++)
                {
                    var np = p;
                    np[axis] += g.Vel[axis] * dt;
                    if (Blocks.Solid[store.Get(Mathf.FloorToInt(np.x / World.VoxelSize), Mathf.FloorToInt(np.y / World.VoxelSize), Mathf.FloorToInt(np.z / World.VoxelSize))])
                    {
                        var v = g.Vel; v[axis] *= -0.35f; v *= 0.8f; g.Vel = v;
                        if (Mathf.Abs(g.Vel[axis]) > 1.5f) Sfx.Instance.Play("tik", p, 0.6f, 0.5f, 40f);
                    }
                    else p = np;
                }
                g.Pos = p;
                Fx.Instance.Emit(g.Pos, Vector3.zero, B.OD, 0.09f, 0.02f, 0, 0);
                if (g.Fuse <= 0) { grenades.RemoveAt(i); Explode(new V3(p.x, p.y, p.z), 2.6f, 1.3f, g.Owner); }
                else grenades[i] = g;
            }
            for (int i = pendingBooms.Count - 1; i >= 0; i--)
            {
                var (pos, delay) = pendingBooms[i];
                delay -= dt;
                if (delay <= 0) { pendingBooms.RemoveAt(i); Explode(pos, 3.2f, 1.5f, PlayerActor.Id); }
                else pendingBooms[i] = (pos, delay);
            }
        }

        /// <summary>Knal: voxels weg, puin, vuur, rook, schade, geluid en een schokgolf voor de camera.</summary>
        public void Explode(V3 center, float radius, float power, int owner)
        {
            var c = ToV(center);
            var res = Explosion.Carve(game.Chunks.Store, center, radius, power);
            var edits = new List<(int x, int y, int z, byte b)>();
            foreach (var (x, y, z, b) in res.Removed)
            {
                edits.Add((x, y, z, B.Air));
                if (edits.Count % 3 == 0)
                {
                    var bp = new Vector3((x + 0.5f) * World.VoxelSize, (y + 0.5f) * World.VoxelSize, (z + 0.5f) * World.VoxelSize);
                    Fx.Instance.Emit(bp, (bp - c).normalized * Random.Range(4f, 11f) + Vector3.up * 3, b, Random.Range(0.06f, 0.14f), Random.Range(1.2f, 2.5f));
                }
                if (Blocks.Is(b, BlockFlags.Container)) game.Chunks.Store.MarkLooted(x, y, z);
            }
            game.Chunks.SetBlocks(edits);
            foreach (var chain in res.Chain) pendingBooms.Add((chain, 0.25f + Random.Range(0f, 0.2f)));
            // vuurbal en rook
            for (int i = 0; i < 40; i++) Fx.Instance.Emit(c, Random.insideUnitSphere * 7f, i % 3 == 0 ? B.Spark : B.Flash, Random.Range(0.08f, 0.22f), Random.Range(0.15f, 0.4f), 0, 3f);
            for (int i = 0; i < 25; i++) Fx.Instance.Emit(c + Random.insideUnitSphere, Random.insideUnitSphere * 2f + Vector3.up * 2.5f, B.Dust, Random.Range(0.3f, 0.6f), Random.Range(2f, 4f), -0.4f, 0.8f);
            Fx.Instance.MuzzleFlash(c, Vector3.up, false);
            Sfx.Instance.Play("explosie", c, 1f, 1f, 600f);
            Noise.Emit(center, 320f, owner);
            // schade aan iedereen in de buurt
            foreach (var a in Actors.All)
            {
                if (!a.Alive) continue;
                float d = (a.Chest - center).Length;
                float dmg = Explosion.Damage(d, radius, 160f);
                if (dmg <= 0) continue;
                bool wasAlive = a.Alive;
                a.TakeDamage(dmg, HitZone.Romp, owner);
                if (owner == PlayerActor.Id && a != PlayerActor) game.Hud.HitMarker(wasAlive && !a.Alive, false);
            }
            float pd = Vector3.Distance(game.Player.Cam.transform.position, c);
            game.Player.Shake(Mathf.Clamp01(1.5f - pd / 25f));
        }

        /// <summary>Het dichtstbijzijnde lijk voor de speler, om te doorzoeken.</summary>
        public bool TryLootCorpse(Vector3 from, Vector3 dir)
        {
            NpcView best = null; float bestD = 2.6f;
            foreach (var v in npcs)
            {
                if (v.Npc.Alive) continue;
                var c = ToV(v.Npc.Pos) + Vector3.up * 0.3f;
                float d = Vector3.Distance(from, c);
                if (d < bestD && Vector3.Dot((c - from).normalized, dir) > 0.3f) { best = v; bestD = d; }
            }
            if (best == null) return false;
            if (best.Loot == null || best.Loot.Count == 0) { game.Hud.Message($"De {best.Npc.Def.Name.ToLowerInvariant()} heeft niets bij zich."); return true; }
            game.Hud.OpenLoot(best.Loot, best.Npc.Def.Name.ToLowerInvariant());
            return true;
        }

        // ------------------------------------------------ pijlen
        void StickArrow(Vector3 at, Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-4f) return;
            if (stuckArrows.Count >= MaxStuckArrows) { Destroy(stuckArrows[0].gameObject); stuckArrows.RemoveAt(0); }
            var go = new GameObject("Pijl");
            go.transform.SetParent(transform, false);
            go.transform.position = at + dir.normalized * 0.04f;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-4f, 4f), Random.Range(0, 360f));
            go.AddComponent<MeshFilter>().sharedMesh = WeaponView.MeshFor("pijl");
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            stuckArrows.Add(go.transform);
        }

        int ArrowIndex(Vector3 eye, Vector3 dir)
        {
            int best = -1; float bestD = 3.5f;
            for (int i = 0; i < stuckArrows.Count; i++)
            {
                var c = stuckArrows[i].position - stuckArrows[i].forward * 0.2f;
                float d = Vector3.Distance(eye, c);
                if (d < bestD && Vector3.Dot((c - eye).normalized, dir) > 0.93f) { best = i; bestD = d; }
            }
            return best;
        }

        public bool ArrowLookedAt(Vector3 eye, Vector3 dir) => ArrowIndex(eye, dir) >= 0;

        /// <summary>Een pijl die ergens in steekt terugpakken.</summary>
        public bool TryPickArrow(Vector3 eye, Vector3 dir)
        {
            int i = ArrowIndex(eye, dir);
            if (i < 0) return false;
            if (game.Inventory.Add("pijl", 1) > 0) { game.Hud.Message("Je rugzak zit vol."); return true; }
            Destroy(stuckArrows[i].gameObject);
            stuckArrows.RemoveAt(i);
            Sfx.Instance.Play2D("tik", 0.4f, 0.8f);
            game.Hud.Message("+1 Pijl");
            return true;
        }

        /// <summary>
        /// Melee van de speler: raakt de dichtstbijzijnde vijand in de kijkrichting. Van achteren of
        /// ongezien is het een sluipaanval (met een mes meestal dodelijk).
        /// </summary>
        public bool Melee(Vector3 eye, Vector3 dir, float damage, float backstabMul = 2f)
        {
            Npc best = null; float bestD = 2.4f;
            foreach (var v in npcs)
            {
                var n = v.Npc;
                if (!n.Alive) continue;
                var c = ToV(n.Chest);
                float d = Vector3.Distance(eye, c);
                if (d < bestD && Vector3.Dot((c - eye).normalized, dir) > 0.55f) { best = n; bestD = d; }
            }
            if (best == null) return false;
            bool wasAlive = best.Alive;
            bool sneak = Brain.Unaware(best, game.Player.Pos);
            if (sneak) damage *= backstabMul;
            best.TakeDamage(damage, HitZone.Romp, PlayerActor.Id);
            Fx.Instance.Blood(ToV(best.Chest), -dir, sneak ? 18 : 8);
            Sfx.Instance.Play("slag", ToV(best.Chest), sneak ? 0.5f : 0.9f);
            if (sneak && wasAlive) game.Hud.Message(best.Alive ? "Sluipaanval!" : "Sluipaanval — stil uitgeschakeld.");
            game.Hud.HitMarker(wasAlive && !best.Alive, false);
            Noise.Emit(best.Pos, sneak ? 3 : 10, PlayerActor.Id);
            return true;
        }

        public void ClearAll()
        {
            for (int i = npcs.Count - 1; i >= 0; i--) Despawn(i);
            settled.Clear();
            pendingFollowers.Clear();
            announced.Clear();
            bullets.Clear();
            foreach (var a in stuckArrows) Destroy(a.gameObject);
            stuckArrows.Clear();
        }

        public int HostilesNear(float radius)
        {
            int n = 0;
            foreach (var v in npcs) if (v.Npc.Alive && v.Npc.State == NpcState.Aanvallen && v.Npc.TargetId == PlayerActor.Id && Dist(v.Npc.Pos, game.Player.Pos) < radius) n++;
            return n;
        }

        static string ShotSound(Stack w)
        {
            var st = Arsenal.Stats(w);
            if (st.Arrow) return "pees";
            if (st.Noise < 100) return "schot_gedempt";
            var d = w.Def;
            if (d.Class == WeaponClass.Shotgun) return "schot_hagel";
            return d.AmmoId == "9mm" ? "schot_licht" : "schot_zwaar";
        }

        public static string SoundFor(Stack w) => ShotSound(w);

        float R() => (float)(rng.NextDouble() - 0.5) * 2f;
        static float Dist(V3 a, V3 b) => (a - b).Length;
        public static Vector3 ToV(V3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
