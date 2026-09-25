using System;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    public enum NpcType : byte { Aaseter, Bendelid, Scherpschutter, Ghoul, Brute, Mutantwolf, Hert, Overlever }

    public enum NpcState : byte { Rust, Zwerven, Onderzoeken, Aanvallen, Vluchten, Dood }

    public sealed class NpcDef
    {
        public NpcType Type;
        public string Name;
        public Faction Faction;
        public float Health, Speed, RunSpeed, Height = 1.8f, Radius = 0.32f;
        public float SightDay = 60, SightNight = 24, Hearing = 1f;
        public float MeleeDamage, MeleeRange = 1.6f, MeleeInterval = 1.1f;
        public string Weapon;               // vuurwapen, of null
        public float Accuracy = 1f;         // spreiding in graden bij ideale omstandigheden
        public float PreferredRange = 14f;
        public float FleeBelow = 0f;        // vlucht onder dit deel van zijn gezondheid
        public float ArmorBody;
        public bool Passive;                // vlucht altijd (dieren)
        public bool Nocturnal;
        public string[] Drops;              // loot bij de dood (tabel-ID's of item-ID's)
    }

    public static class Npcs
    {
        public static readonly Dictionary<NpcType, NpcDef> Defs = new Dictionary<NpcType, NpcDef>
        {
            [NpcType.Aaseter] = new NpcDef { Type = NpcType.Aaseter, Name = "Aaseter", Faction = Faction.Raider, Health = 70, Speed = 1.6f, RunSpeed = 5.6f, MeleeDamage = 14, FleeBelow = 0.3f, Drops = new[] { "bonen", "verband", "stof", "9mm", "pijp", "jeans", "sneakers" } },
            [NpcType.Bendelid] = new NpcDef { Type = NpcType.Bendelid, Name = "Bendelid", Faction = Faction.Raider, Health = 95, Speed = 1.5f, RunSpeed = 5.2f, MeleeDamage = 16, Weapon = "pistool", Accuracy = 3.2f, PreferredRange = 12, FleeBelow = 0.2f, ArmorBody = 0.15f, Drops = new[] { "9mm", "762", "verband", "water", "chestrig", "cargobroek", "legerkistjes" } },
            [NpcType.Scherpschutter] = new NpcDef { Type = NpcType.Scherpschutter, Name = "Scherpschutter", Faction = Faction.Raider, Health = 80, Speed = 1.4f, RunSpeed = 4.8f, MeleeDamage = 12, Weapon = "geweer", Accuracy = 1.1f, PreferredRange = 55, SightDay = 110, SightNight = 30, FleeBelow = 0.35f, Drops = new[] { "308", "scope4x", "water", "legerjas" } },
            [NpcType.Ghoul] = new NpcDef { Type = NpcType.Ghoul, Name = "Ghoul", Faction = Faction.Mutant, Health = 85, Speed = 1.8f, RunSpeed = 7.2f, MeleeDamage = 18, MeleeInterval = 0.8f, SightDay = 35, SightNight = 45, Hearing = 1.6f, Nocturnal = true, Drops = new[] { "stof", "jodium" } },
            [NpcType.Brute] = new NpcDef { Type = NpcType.Brute, Name = "Brute", Faction = Faction.Mutant, Health = 380, Speed = 1.3f, RunSpeed = 4.4f, Height = 2.7f, Radius = 0.55f, MeleeDamage = 42, MeleeRange = 2.4f, MeleeInterval = 1.8f, SightDay = 40, SightNight = 40, ArmorBody = 0.2f, Drops = new[] { "jodium", "schroot", "medkit" } },
            [NpcType.Mutantwolf] = new NpcDef { Type = NpcType.Mutantwolf, Name = "Mutantwolf", Faction = Faction.Mutant, Health = 60, Speed = 2.2f, RunSpeed = 8.4f, Height = 0.95f, Radius = 0.42f, MeleeDamage = 13, MeleeRange = 1.5f, MeleeInterval = 0.7f, SightDay = 45, SightNight = 45, Hearing = 2f, Drops = new[] { "vlees", "vlees" } },
            [NpcType.Overlever] = new NpcDef { Type = NpcType.Overlever, Name = "Overlever", Faction = Faction.Overlever, Health = 90, Speed = 1.5f, RunSpeed = 5.4f, MeleeDamage = 10, SightDay = 55, SightNight = 25, FleeBelow = 0.25f, Accuracy = 2.4f, PreferredRange = 16, Drops = new[] { "doppen", "brood", "water" } },
            [NpcType.Hert] = new NpcDef { Type = NpcType.Hert, Name = "Hert", Faction = Faction.Dier, Health = 55, Speed = 1.3f, RunSpeed = 9f, Height = 1.4f, Radius = 0.45f, SightDay = 50, SightNight = 20, Hearing = 2.2f, Passive = true, Drops = new[] { "vlees", "vlees", "vlees", "vacht" } },
        };

        public static bool Hostile(Faction a, Faction b)
        {
            if (a == Faction.Dier || b == Faction.Dier) return false;
            if (a == b) return false;
            if (a == Faction.Overlever || b == Faction.Overlever) return a == Faction.Raider || b == Faction.Raider || a == Faction.Mutant || b == Faction.Mutant;
            return true;
        }
    }

    /// <summary>Een wezen in de wereld met een eenvoudig maar leesbaar brein.</summary>
    public sealed class Npc : Actor
    {
        public NpcDef Def;
        public NpcState State = NpcState.Zwerven;
        public V3 Vel, Home, Goal;
        public bool Grounded;
        public int TargetId = -1;
        public float StateTime, AttackCooldown, Awareness, SeenTime, StuckTime, Speed01, SenseTimer;
        public int SeenId = -1;             // wie hij bij de laatste waarneming zag
        public bool Angry;                  // boos op de speler (na een aanval)

        // dorpelingen
        public Settlement Home2;
        public SettlerJob Job;
        public SettlerTask Task;
        public V3 TaskPos;
        public int TaskX, TaskY, TaskZ, Carry, BedIndex;
        public float TaskTimer, SlideTimer;
        public float SlideX, SlideZ;
        public bool Sleeping, Working;
        public string Line;                 // wat hij zegt als je hem aanspreekt
        public V3 LastSeen;
        public Stack Weapon;
        public bool Looted;
        public float DeadTime;
        public int Seed;

        // uitvoer voor de runtime deze frame
        public bool FiredThisFrame, MeleeThisFrame;
        public V3 FireOrigin, FireDir;

        public Npc(NpcDef def, V3 pos, int seed)
        {
            Def = def; Faction = def.Faction; Name = def.Name;
            Health = MaxHealth = def.Health; Height = def.Height; Radius = def.Radius; ArmorBody = def.ArmorBody;
            Pos = Home = Goal = pos; Seed = seed;
            if (def.Weapon != null) { Weapon = new Stack(def.Weapon, 1); Weapon.Ammo = Items.Get(def.Weapon).MagSize; }
        }

        public override float TakeDamage(float dmg, HitZone zone, int attacker)
        {
            float d = base.TakeDamage(dmg, zone, attacker);
            Sleeping = false;
            if (Alive)
            {
                Awareness = 1f;
                if (TargetId < 0 || State != NpcState.Aanvallen) TargetId = attacker;
                if (Def.Passive) State = NpcState.Vluchten;
            }
            else State = NpcState.Dood;
            return d;
        }
    }

    /// <summary>Wat de AI over de wereld moet weten.</summary>
    public sealed class AiContext
    {
        public VoxelStore Store;
        public ActorWorld Actors;
        public NoiseEvents Noise;
        public float Daylight = 1f;         // 0 nacht .. 1 dag
        public float Hour = 12f;
        public System.Action<int, int, int, byte> SetBlock;   // voor boeren: gewassen oogsten en planten
        public Actor Player;
        public bool PlayerCrouching, PlayerFlashlight;
        public Random Rng = new Random(1);
    }

    public static class Brain
    {
        const float Gravity = 22f;

        public static void Tick(Npc n, float dt, AiContext ctx)
        {
            n.FiredThisFrame = false; n.MeleeThisFrame = false;
            if (!n.Alive) { n.State = NpcState.Dood; n.DeadTime += dt; Physics(n, dt, ctx, new V3(0, 0, 0), 0, false); return; }
            n.StateTime += dt;
            n.AttackCooldown -= dt;
            var def = n.Def;

            var seen = Sense(n, dt, ctx, out var target);
            float seenDist = seen != null ? Dist(n.Pos, seen.Pos) : float.MaxValue;

            // geluiden
            if (n.State != NpcState.Aanvallen && n.State != NpcState.Vluchten)
                foreach (var no in ctx.Noise.Recent)
                {
                    if (no.Source == n.Id) continue;
                    float d = Dist(n.Pos, no.Pos);
                    if (d < no.Radius * def.Hearing)
                    {
                        if (def.Passive) { n.State = NpcState.Vluchten; n.LastSeen = no.Pos; n.StateTime = 0; break; }
                        n.State = NpcState.Onderzoeken; n.Goal = no.Pos; n.StateTime = 0; n.Awareness = Math.Max(n.Awareness, 0.5f);
                        break;
                    }
                }

            // ---------------- beslissen
            if (target != null && n.Awareness >= 1f)
            {
                if (def.Passive || (def.FleeBelow > 0 && n.Health < n.MaxHealth * def.FleeBelow)) n.State = NpcState.Vluchten;
                else n.State = NpcState.Aanvallen;
            }
            else if (n.State == NpcState.Aanvallen && n.SeenTime > 6f) { n.State = NpcState.Onderzoeken; n.Goal = n.LastSeen; n.StateTime = 0; }
            else if (n.State == NpcState.Vluchten && n.StateTime > 12f) { n.State = NpcState.Zwerven; n.StateTime = 0; }
            else if (n.State == NpcState.Onderzoeken && (n.StateTime > 15f || Dist(n.Pos, n.Goal) < 1.5f)) { n.State = NpcState.Zwerven; n.StateTime = 0; }

            // ---------------- handelen
            V3 move = new V3(0, 0, 0); float speed = 0; bool run = false;
            switch (n.State)
            {
                case NpcState.Zwerven:
                case NpcState.Rust:
                    if (Dist(n.Pos, n.Goal) < 1.2f || n.StateTime > 20f)
                    {
                        if (ctx.Rng.NextDouble() < 0.4) { n.State = NpcState.Rust; }
                        else
                        {
                            n.State = NpcState.Zwerven;
                            float ang = (float)(ctx.Rng.NextDouble() * Math.PI * 2), r = 4 + (float)ctx.Rng.NextDouble() * 14;
                            n.Goal = new V3(n.Home.X + MathF.Cos(ang) * r, n.Pos.Y, n.Home.Z + MathF.Sin(ang) * r);
                        }
                        n.StateTime = 0;
                    }
                    if (n.State == NpcState.Zwerven) { move = n.Goal - n.Pos; speed = def.Speed; }
                    break;
                case NpcState.Onderzoeken:
                    move = n.Goal - n.Pos; speed = (def.Speed + def.RunSpeed) * 0.5f;
                    break;
                case NpcState.Vluchten:
                {
                    var from = target != null ? target.Pos : n.LastSeen;
                    move = n.Pos - from; speed = def.RunSpeed; run = true;
                    break;
                }
                case NpcState.Aanvallen:
                {
                    if (target == null) { move = n.LastSeen - n.Pos; speed = def.RunSpeed; break; }
                    float d = Dist(n.Pos, target.Pos);
                    var to = target.Pos - n.Pos;
                    n.Yaw = MathF.Atan2(to.X, to.Z) * 57.2958f;
                    bool ranged = !n.Weapon.Empty && n.Weapon.Def.GunDamage > 0;
                    if (ranged && d > 3f)
                    {
                        // op afstand blijven, zijwaarts bewegen, schieten als hij zichtbaar is
                        float pref = def.PreferredRange;
                        if (d > pref * 1.3f) { move = to; speed = def.RunSpeed * 0.8f; }
                        else if (d < pref * 0.6f) { move = n.Pos - target.Pos; speed = def.Speed * 1.6f; }
                        else
                        {
                            float side = (n.Seed & 1) == 0 ? 1 : -1;
                            if (((int)(n.StateTime / 2.5f) & 1) == 1) side = -side;
                            move = new V3(to.Z * side, 0, -to.X * side); speed = def.Speed;
                        }
                        if (seen == target && n.AttackCooldown <= 0) Shoot(n, target, d, ctx);
                    }
                    else
                    {
                        move = to; speed = def.RunSpeed; run = true;
                        if (d < def.MeleeRange + target.Radius && n.AttackCooldown <= 0)
                        {
                            n.AttackCooldown = def.MeleeInterval;
                            n.MeleeThisFrame = true;
                            float dmg = def.MeleeDamage * (0.8f + (float)ctx.Rng.NextDouble() * 0.4f);
                            target.TakeDamage(dmg / Actor.ZoneMultiplier(HitZone.Romp), HitZone.Romp, n.Id);
                            ctx.Noise.Emit(n.Pos, 12, n.Id);
                        }
                    }
                    break;
                }
            }
            if (move.X * move.X + move.Z * move.Z > 1e-4f && n.State != NpcState.Aanvallen) n.Yaw = MathF.Atan2(move.X, move.Z) * 57.2958f;
            if (run && n.State != NpcState.Vluchten && n.Def.Type != NpcType.Brute) ctx.Noise.Emit(n.Pos, 6, n.Id);
            Physics(n, dt, ctx, move, speed, true);
        }

        /// <summary>Rondkijken en besluiten wie het doelwit is. Geeft terug wie hij nu ziet.</summary>
        public static Actor Sense(Npc n, float dt, AiContext ctx, out Actor target)
        {
            var def = n.Def;
            if (!n.Angry && n.LastAttacker >= 0 && ctx.Actors.Get(n.LastAttacker)?.Faction == Faction.Speler && def.Faction == Faction.Overlever) n.Angry = true;
            target = n.TargetId >= 0 ? ctx.Actors.Get(n.TargetId) : null;
            if (target != null && !target.Alive) { target = null; n.TargetId = -1; }
            // rondkijken kost raycasts: vijf keer per seconde is genoeg
            n.SenseTimer -= dt;
            if (n.SenseTimer <= 0)
            {
                n.SenseTimer = 0.2f + (n.Seed % 5) * 0.01f;
                n.SeenId = -1; float best = float.MaxValue;
                foreach (var a in ctx.Actors.All)
                {
                    if (a == n || !a.Alive) continue;
                    bool threat = def.Passive ? a.Faction != Faction.Dier : Npcs.Hostile(n.Faction, a.Faction) || (n.Angry && a.Faction == Faction.Speler);
                    if (!threat) continue;
                    float d = Dist(n.Pos, a.Pos);
                    if (d > 130 || d >= best) continue;
                    if (CanSee(n, a, d, ctx)) { n.SeenId = a.Id; best = d; }
                }
            }
            Actor seen = n.SeenId >= 0 ? ctx.Actors.Get(n.SeenId) : null;
            if (seen != null && !seen.Alive) { seen = null; n.SeenId = -1; }
            float seenDist = seen != null ? Dist(n.Pos, seen.Pos) : float.MaxValue;
            if (seen != null)
            {
                n.Awareness = Math.Min(1f, n.Awareness + dt * (seenDist < 10 ? 3f : 1.2f));
                if (n.Awareness >= 1f) { target = seen; n.TargetId = seen.Id; n.LastSeen = seen.Pos; n.SeenTime = 0; }
            }
            else
            {
                n.Awareness = Math.Max(0f, n.Awareness - dt * 0.15f);
                n.SeenTime += dt;
            }
            if (target != null && seen == target) n.LastSeen = target.Pos;

            return seen;
        }

        static void Shoot(Npc n, Actor target, float dist, AiContext ctx)
        {
            var w = n.Weapon;
            var st = Arsenal.Stats(w);
            if (w.Ammo <= 0) { n.Weapon.Ammo = st.MagSize; n.AttackCooldown = st.ReloadTime * 1.2f; return; }
            n.Weapon.Ammo--;
            float burst = st.Automatic ? 0.35f : 1f;
            n.AttackCooldown = Math.Max(st.Interval, (0.35f + (float)ctx.Rng.NextDouble() * 0.6f) * burst) + (dist > 30 ? 0.6f : 0);
            var origin = new V3(n.Pos.X, n.Pos.Y + n.Height * 0.82f, n.Pos.Z);
            var aim = target.Chest - origin;
            // afwijking groeit met afstand, beweging en duisternis; sluipen maakt je een lastiger doelwit
            float spread = n.Def.Accuracy * (1 + dist / 45f) * (ctx.Daylight < 0.3f ? 1.6f : 1f) * (target == ctx.Player && ctx.PlayerCrouching ? 1.25f : 1f);
            float rad = spread * 0.01745f;
            float len = aim.Length;
            var dir = new V3(aim.X / len + Gauss(ctx.Rng) * rad, aim.Y / len + Gauss(ctx.Rng) * rad, aim.Z / len + Gauss(ctx.Rng) * rad);
            n.FiredThisFrame = true; n.FireOrigin = origin; n.FireDir = dir;
            ctx.Noise.Emit(origin, st.Noise, n.Id);
        }

        static float Gauss(Random r) => (float)((r.NextDouble() + r.NextDouble() + r.NextDouble()) / 3.0 - 0.5) * 2f;

        public static bool CanSee(Npc n, Actor a, float d, AiContext ctx)
        {
            var def = n.Def;
            float range = def.SightDay + (def.SightNight - def.SightDay) * (1 - ctx.Daylight);
            if (a == ctx.Player)
            {
                if (ctx.PlayerCrouching) range *= 0.55f;
                if (ctx.PlayerFlashlight && ctx.Daylight < 0.4f) range += 45f;
            }
            if (d > range) return false;
            if (d > 4f)
            {
                var to = a.Pos - n.Pos;
                float yawTo = MathF.Atan2(to.X, to.Z) * 57.2958f;
                float diff = MathF.Abs(((yawTo - n.Yaw) % 360 + 540) % 360 - 180);
                if (diff > 75) return false;
            }
            var eye = new V3(n.Pos.X, n.Pos.Y + n.Height * 0.9f, n.Pos.Z);
            var tgt = a.Chest - eye;
            float l = tgt.Length;
            var hit = ctx.Store.Raycast(eye, tgt, l - 0.3f, true);
            return !hit.Hit;
        }

        public static void Physics(Npc n, float dt, AiContext ctx, V3 move, float speed, bool canJump)
        {
            float l = MathF.Sqrt(move.X * move.X + move.Z * move.Z);
            float tx = 0, tz = 0;
            if (l > 0.05f && speed > 0) { tx = move.X / l * speed; tz = move.Z / l * speed; }
            float k = 1 - MathF.Exp(-10f * dt);
            n.Vel.X += (tx - n.Vel.X) * k;
            n.Vel.Z += (tz - n.Vel.Z) * k;
            n.Vel.Y -= Gravity * dt;
            var before = n.Pos;
            n.Grounded = VoxelPhysics.Move(ctx.Store, ref n.Pos, ref n.Vel, n.Vel * dt, n.Radius * 0.9f, n.Height, n.Grounded);
            float moved = MathF.Sqrt((n.Pos.X - before.X) * (n.Pos.X - before.X) + (n.Pos.Z - before.Z) * (n.Pos.Z - before.Z));
            n.Speed01 = dt > 0 ? moved / dt : 0;
            // vast tegen een muur: springen, en anders een nieuwe richting
            if (l > 0.05f && speed > 0 && moved < speed * dt * 0.2f)
            {
                n.StuckTime += dt;
                if (canJump && n.Grounded && n.StuckTime > 0.25f) { n.Vel.Y = 7.4f; }
                if (n.StuckTime > 1.6f && n.State != NpcState.Aanvallen)
                {
                    float ang = (float)(ctx.Rng.NextDouble() * Math.PI * 2);
                    n.Goal = new V3(n.Pos.X + MathF.Cos(ang) * 8, n.Pos.Y, n.Pos.Z + MathF.Sin(ang) * 8);
                    n.StuckTime = 0;
                }
            }
            else n.StuckTime = 0;
        }

        static float Dist(V3 a, V3 b) { float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z; return MathF.Sqrt(dx * dx + dy * dy + dz * dz); }
    }

    /// <summary>Beslist wat er rond de speler rondloopt, afhankelijk van plek, tijd en straling.</summary>
    public static class Spawner
    {
        public struct Group { public NpcType Type; public int Count; }

        /// <summary>Kiest een groep voor deze plek. Null als hier nu niets hoort te spawnen.</summary>
        public static Group? Choose(WorldGen gen, int vx, int vz, float daylight, float radiation, Random rng)
        {
            var col = gen.GetColumn(vx, vz);
            bool city = col.City != null && col.CityD < col.City.R;
            bool night = daylight < 0.3f;
            double r = rng.NextDouble();
            if (radiation > 0.2f)
            {
                if (r < 0.45) return new Group { Type = NpcType.Ghoul, Count = 2 + rng.Next(3) };
                if (r < 0.62) return new Group { Type = NpcType.Brute, Count = 1 };
                if (r < 0.85) return new Group { Type = NpcType.Mutantwolf, Count = 2 + rng.Next(3) };
                return null;
            }
            if (city)
            {
                if (night && r < 0.35) return new Group { Type = NpcType.Ghoul, Count = 1 + rng.Next(3) };
                if (r < 0.45) return new Group { Type = NpcType.Aaseter, Count = 1 + rng.Next(2) };
                if (r < 0.75) return new Group { Type = NpcType.Bendelid, Count = 2 + rng.Next(3) };
                if (r < 0.85) return new Group { Type = NpcType.Scherpschutter, Count = 1 };
                if (r < 0.9) return new Group { Type = NpcType.Brute, Count = 1 };
                return null;
            }
            if (col.Kind == ColumnKind.Highway && r < 0.35) return new Group { Type = NpcType.Bendelid, Count = 2 + rng.Next(3) };
            if (r < 0.35) return new Group { Type = NpcType.Hert, Count = 1 + rng.Next(3) };
            if (r < (night ? 0.7 : 0.5)) return new Group { Type = NpcType.Mutantwolf, Count = 2 + rng.Next(3) };
            if (r < 0.62) return new Group { Type = NpcType.Aaseter, Count = 1 + rng.Next(2) };
            if (night && r < 0.8) return new Group { Type = NpcType.Ghoul, Count = 1 + rng.Next(2) };
            return null;
        }
    }
}
