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
        }

        public ActorWorld Actors { get; } = new ActorWorld();
        public NoiseEvents Noise { get; } = new NoiseEvents();
        public PlayerActor PlayerActor { get; private set; }
        public int Kills;
        GameState game;
        AiContext ctx;
        readonly List<Bullet> bullets = new List<Bullet>();
        readonly List<BulletEvent> events = new List<BulletEvent>();
        readonly List<NpcView> npcs = new List<NpcView>();
        readonly System.Random rng = new System.Random();
        float spawnTimer = 4f;
        int seedCounter = 1;

        public void Init(GameState g)
        {
            game = g;
            PlayerActor = Actors.Add(new PlayerActor { Game = g, Faction = Faction.Speler, Name = "Jij", Height = 1.75f, Radius = 0.3f });
            ctx = new AiContext { Store = g.Chunks.Store, Actors = Actors, Noise = Noise, Player = PlayerActor, Rng = new System.Random(7) };
        }

        // ------------------------------------------------ schieten
        public void Fire(V3 origin, V3 dir, in WeaponStats st, int owner, bool tracer)
        {
            bullets.Add(Ballistics.Fire(origin, dir, st, owner, tracer));
        }

        void Update()
        {
            if (game == null || !game.Ready) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var pp = game.Player.Pos;
            PlayerActor.Pos = pp;
            PlayerActor.Yaw = game.Player.Yaw;
            PlayerActor.Health = game.Stats.Health;
            PlayerActor.Height = game.Player.Crouching ? PlayerController.CrouchHeight : PlayerController.Height;
            Noise.Tick(dt);

            ctx.Daylight = Mathf.Clamp01((game.Clock.Ambient - 0.35f) / 0.4f);
            ctx.PlayerCrouching = game.Player.Crouching;
            ctx.PlayerFlashlight = game.Player.Flashlight;

            Spawn(dt);
            TickNpcs(dt);
            StepBullets(dt);
        }

        void Spawn(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0) return;
            spawnTimer = 3f;
            var pp = game.Player.Pos;
            // opruimen
            for (int i = npcs.Count - 1; i >= 0; i--)
            {
                var v = npcs[i];
                float d = Dist(v.Npc.Pos, pp);
                if (d > 160 || (!v.Npc.Alive && v.Npc.DeadTime > 240)) Despawn(i);
            }
            int vx = Mathf.FloorToInt(pp.X / World.VoxelSize), vz = Mathf.FloorToInt(pp.Z / World.VoxelSize);
            float rad = game.Gen.RadiationAt(vx, vz, out _);
            int alive = 0; foreach (var v in npcs) if (v.Npc.Alive) alive++;
            int budget = ctx.Daylight < 0.3f ? 12 : 8;
            if (rad > 0.1f) budget += 4;
            if (alive >= budget) return;

            float ang = (float)(rng.NextDouble() * Mathf.PI * 2), dist = 45 + (float)rng.NextDouble() * 45;
            float sx = pp.X + Mathf.Cos(ang) * dist, sz = pp.Z + Mathf.Sin(ang) * dist;
            if (!game.Chunks.IsLoaded(new Vector3(sx, 0, sz))) return;
            int svx = Mathf.FloorToInt(sx / World.VoxelSize), svz = Mathf.FloorToInt(sz / World.VoxelSize);
            float srad = game.Gen.RadiationAt(svx, svz, out _);
            var group = Spawner.Choose(game.Gen, svx, svz, ctx.Daylight, srad, rng);
            if (group == null) return;
            var g = group.Value;
            for (int k = 0; k < g.Count; k++)
            {
                float ox = sx + (float)(rng.NextDouble() - 0.5) * 6, oz = sz + (float)(rng.NextDouble() - 0.5) * 6;
                float gy = PlayerController.GroundAt(game.Chunks.Store, ox, oz);
                if (gy < World.SeaLevelMeters + 0.3f) continue;       // niet in het water
                SpawnNpc(g.Type, new V3(ox, gy + 0.02f, oz));
            }
        }

        void SpawnNpc(NpcType type, V3 pos)
        {
            var def = Npcs.Defs[type];
            var n = Actors.Add(new Npc(def, pos, seedCounter++));
            n.Yaw = (float)rng.NextDouble() * 360f;
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
                    else if (type == NpcType.Aaseter) n.Weapon = new Stack(rng.NextDouble() < 0.5 ? "pijp" : "bijl", 1);
                    v.Loot = new List<Stack>();
                    foreach (var s in eq.Slots) if (!s.Empty && rng.NextDouble() < 0.45) v.Loot.Add(s);
                    var skin = rng.NextDouble() < 0.5 ? B.Skin : B.SkinDark;
                    v.Human = VoxelCharacter.Build(v.Root, VoxelCharacter.Look.FromEquipment(eq, skin, B.Hair));
                    v.Muzzle = v.Human.SetTool(n.Weapon, out _);
                    break;
                }
            }
            SyncView(v, 0);
            npcs.Add(v);
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
                Brain.Tick(n, dt, ctx);
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
                    Fx.Instance.MuzzleFlash(mz, ToV(n.FireDir).normalized, st.HidesFlash);
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
        }

        void OnDeath(NpcView v)
        {
            var n = v.Npc;
            if (v.Human) v.Human.Die();
            if (v.Animal) v.Animal.Die();
            if (n.LastAttacker == PlayerActor.Id)
            {
                Kills++;
                game.Hud.Message($"{n.Def.Name} uitgeschakeld.");
            }
            v.Loot ??= new List<Stack>();
            if (!n.Weapon.Empty) v.Loot.Add(n.Weapon);
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
            v.Root.position = ToV(n.Pos);
            v.Root.rotation = Quaternion.Slerp(v.Root.rotation, Quaternion.Euler(0, n.Yaw, 0), 1 - Mathf.Exp(-10 * dt));
            if (v.Human) v.Human.Animate(n.Speed01, false, n.Grounded, n.State == NpcState.Aanvallen && v.Muzzle != null, n.MeleeThisFrame ? 1 : 0, dt);
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
                if (b.Tracer) Fx.Instance.Streak(ToV(from), ToV(b.Pos), B.Tracer, 0.012f, 0.03f);
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

        /// <summary>Melee van de speler: raakt de dichtstbijzijnde vijand in de kijkrichting.</summary>
        public bool Melee(Vector3 eye, Vector3 dir, float damage)
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
            best.TakeDamage(damage, HitZone.Romp, PlayerActor.Id);
            Fx.Instance.Blood(ToV(best.Chest), -dir, 8);
            Sfx.Instance.Play("slag", ToV(best.Chest), 0.9f);
            game.Hud.HitMarker(wasAlive && !best.Alive, false);
            Noise.Emit(best.Pos, 10, PlayerActor.Id);
            return true;
        }

        public void ClearAll()
        {
            for (int i = npcs.Count - 1; i >= 0; i--) Despawn(i);
            bullets.Clear();
        }

        public int HostilesNear(float radius)
        {
            int n = 0;
            foreach (var v in npcs) if (v.Npc.Alive && v.Npc.State == NpcState.Aanvallen && Dist(v.Npc.Pos, game.Player.Pos) < radius) n++;
            return n;
        }

        static string ShotSound(Stack w)
        {
            var st = Arsenal.Stats(w);
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
