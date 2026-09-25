using System;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    public enum HitZone : byte { Hoofd, Romp, Benen }

    public enum Faction : byte { Speler, Overlever, Raider, Mutant, Dier }

    /// <summary>Alles wat geraakt kan worden: speler, NPC's, raiders, mutanten, dieren.</summary>
    public class Actor
    {
        public int Id;
        public Faction Faction;
        public string Name;
        public V3 Pos;                  // voeten
        public float Yaw;
        public float Health = 100, MaxHealth = 100;
        public float Radius = 0.32f, Height = 1.8f;
        public float ArmorHead, ArmorBody, ArmorLegs;
        public bool Alive => Health > 0;
        public int LastAttacker = -1;

        public V3 Head => new V3(Pos.X, Pos.Y + Height * 0.92f, Pos.Z);
        public V3 Chest => new V3(Pos.X, Pos.Y + Height * 0.7f, Pos.Z);

        public static float ZoneMultiplier(HitZone z) => z == HitZone.Hoofd ? 2.4f : z == HitZone.Benen ? 0.7f : 1f;

        /// <summary>Verwerkt een treffer en geeft de werkelijke schade terug.</summary>
        public virtual float TakeDamage(float dmg, HitZone zone, int attacker)
        {
            if (!Alive) return 0;
            float armor = zone == HitZone.Hoofd ? ArmorHead : zone == HitZone.Romp ? ArmorBody : ArmorLegs;
            float d = dmg * ZoneMultiplier(zone) * (1 - armor);
            Health = Math.Max(0, Health - d);
            LastAttacker = attacker;
            return d;
        }

        /// <summary>Snijpunt van een lijnstuk met de staande cilinder van deze actor.</summary>
        public bool Intersect(V3 o, V3 dir, float len, out float t, out HitZone zone)
        {
            t = 0; zone = HitZone.Romp;
            float dx = o.X - Pos.X, dz = o.Z - Pos.Z;
            float a = dir.X * dir.X + dir.Z * dir.Z;
            float tHit;
            if (a < 1e-8f)
            {
                if (dx * dx + dz * dz > Radius * Radius) return false;
                // recht omhoog of omlaag
                float ty = dir.Y > 0 ? (Pos.Y - o.Y) / dir.Y : (Pos.Y + Height - o.Y) / dir.Y;
                tHit = Math.Max(0, ty);
            }
            else
            {
                float b = 2 * (dx * dir.X + dz * dir.Z);
                float c = dx * dx + dz * dz - Radius * Radius;
                float disc = b * b - 4 * a * c;
                if (disc < 0) return false;
                float sq = MathF.Sqrt(disc);
                float t0 = (-b - sq) / (2 * a), t1 = (-b + sq) / (2 * a);
                if (t1 < 0) return false;
                tHit = t0 >= 0 ? t0 : 0;
                // hoogte controleren; eventueel verder in de cilinder zoeken
                float y0 = o.Y + dir.Y * tHit;
                if (y0 < Pos.Y || y0 > Pos.Y + Height)
                {
                    float yEnd = o.Y + dir.Y * t1;
                    if ((y0 < Pos.Y && yEnd < Pos.Y) || (y0 > Pos.Y + Height && yEnd > Pos.Y + Height)) return false;
                    float target = y0 < Pos.Y ? Pos.Y : Pos.Y + Height;
                    tHit = (target - o.Y) / dir.Y;
                }
            }
            if (tHit > len) return false;
            t = tHit;
            float hy = (o.Y + dir.Y * t - Pos.Y) / Height;
            zone = hy > 0.84f ? HitZone.Hoofd : hy > 0.48f ? HitZone.Romp : HitZone.Benen;
            return true;
        }
    }

    public sealed class ActorWorld
    {
        public readonly List<Actor> All = new List<Actor>();
        int nextId = 1;

        public T Add<T>(T a) where T : Actor { a.Id = nextId++; All.Add(a); return a; }
        public void Remove(Actor a) => All.Remove(a);
        public Actor Get(int id) { foreach (var a in All) if (a.Id == id) return a; return null; }

        public Actor Raycast(V3 o, V3 dir, float len, int ignoreId, out float t, out HitZone zone)
        {
            Actor best = null; t = len; zone = HitZone.Romp;
            foreach (var a in All)
            {
                if (a.Id == ignoreId || !a.Alive) continue;
                if (a.Intersect(o, dir, t, out float at, out var z) && at < t) { best = a; t = at; zone = z; }
            }
            return best;
        }
    }

    public struct Bullet
    {
        public V3 Pos, Vel;
        public float Damage, Penetration, Traveled, Life;
        public int Owner;
        public bool Tracer, Alive, Arrow;
    }

    public enum BulletEventType : byte { Impact, Actor, Glass, Penetrate }

    public struct BulletEvent
    {
        public BulletEventType Type;
        public V3 Pos, Dir;
        public int Nx, Ny, Nz;
        public int X, Y, Z;          // voxel
        public bool Arrow;
        public byte Block;
        public Actor Victim;
        public HitZone Zone;
        public float Damage;
        public int Owner;
    }

    /// <summary>
    /// Kogels met snelheid, zwaartekracht, luchtweerstand en penetratie door zachte materialen.
    /// Glas breekt, bladeren houden niets tegen, hout en dun plaatstaal alleen bij zwaar kaliber.
    /// </summary>
    public static class Ballistics
    {
        public const float Gravity = 9.81f, Drag = 0.00012f, MaxLife = 3f;

        public static float Density(byte b)
        {
            switch (b)
            {
                case B.Leaves: case B.DeadLeaves: case B.Crop: return 0.02f;
                case B.Glass: return 0.05f;
                case B.Carpet: return 0.1f;
                case B.Tire: return 0.35f;
                case B.Planks: case B.Shelf: case B.Crate: return 0.45f;
                case B.Plaster: case B.Fence: return 0.55f;
                case B.WoodWall: case B.Barrel: return 0.7f;
                case B.CarRed: case B.CarBlue: case B.CarGrey: case B.CarWhite: case B.Rust: return 0.8f;
                case B.Log: return 1.3f;
                case B.Brick: case B.MetalWall: return 2.2f;
                default: return 99f;
            }
        }

        public static Bullet Fire(V3 origin, V3 dir, in WeaponStats st, int owner, bool tracer)
        {
            float l = dir.Length;
            return new Bullet
            {
                Pos = origin, Vel = new V3(dir.X / l * st.Velocity, dir.Y / l * st.Velocity, dir.Z / l * st.Velocity),
                Damage = st.Damage, Penetration = st.Penetration, Owner = owner, Tracer = tracer, Alive = true, Arrow = st.Arrow
            };
        }

        public static void Step(ref Bullet b, float dt, VoxelStore store, ActorWorld actors, List<BulletEvent> events)
        {
            if (!b.Alive) return;
            b.Life += dt;
            if (b.Life > MaxLife) { b.Alive = false; return; }
            float speed = b.Vel.Length;
            var dir = new V3(b.Vel.X / speed, b.Vel.Y / speed, b.Vel.Z / speed);
            float remaining = speed * dt;
            var origin = b.Pos;
            for (int guard = 0; guard < 8 && remaining > 1e-4f; guard++)
            {
                var vh = store.Raycast(origin, dir, remaining, false);
                var victim = actors.Raycast(origin, dir, remaining, b.Owner, out float at, out var zone);
                if (victim != null && (!vh.Hit || at <= vh.Distance))
                {
                    var p = origin + dir * at;
                    float dmg = RangeFalloff(b) * b.Damage;
                    events.Add(new BulletEvent { Type = BulletEventType.Actor, Pos = p, Dir = dir, Victim = victim, Zone = zone, Damage = dmg, Owner = b.Owner, Arrow = b.Arrow });
                    b.Alive = false; b.Pos = p;
                    return;
                }
                if (!vh.Hit) { origin = origin + dir * remaining; b.Traveled += remaining; remaining = 0; break; }
                var hp = origin + dir * vh.Distance;
                float dens = Density(vh.Block);
                var ev = new BulletEvent { Pos = hp, Dir = dir, Nx = vh.Nx, Ny = vh.Ny, Nz = vh.Nz, X = vh.X, Y = vh.Y, Z = vh.Z, Block = vh.Block, Owner = b.Owner, Arrow = b.Arrow };
                if (vh.Block == B.Glass)
                {
                    ev.Type = BulletEventType.Glass; events.Add(ev);
                    b.Damage *= 0.92f;
                }
                else if (dens < b.Penetration)
                {
                    ev.Type = BulletEventType.Penetrate; events.Add(ev);
                    b.Damage *= 1f - 0.55f * dens / b.Penetration;
                    b.Penetration -= dens;
                }
                else
                {
                    ev.Type = BulletEventType.Impact; events.Add(ev);
                    b.Alive = false; b.Pos = hp;
                    return;
                }
                // voorbij deze voxel verder
                float skip = vh.Distance + World.VoxelSize * 1.05f;
                origin = origin + dir * skip;
                b.Traveled += skip;
                remaining -= skip;
            }
            b.Pos = origin;
            b.Traveled += Math.Max(0, remaining);
            // zwaartekracht en luchtweerstand
            b.Vel.Y -= Gravity * dt;
            float k = 1f - Drag * speed * dt;
            b.Vel = b.Vel * k;
            if (b.Pos.Y < -5) b.Alive = false;
        }

        static float RangeFalloff(in Bullet b) => b.Traveled < 80 ? 1f : Math.Max(0.45f, 1f - (b.Traveled - 80f) / 420f);
    }

    /// <summary>Geluiden die vijanden kunnen horen: schoten, rennen, slopen.</summary>
    public sealed class NoiseEvents
    {
        public struct Noise { public V3 Pos; public float Radius; public float Time; public int Source; }
        public readonly List<Noise> Recent = new List<Noise>();
        public float Now;

        public void Emit(V3 pos, float radius, int source) => Recent.Add(new Noise { Pos = pos, Radius = radius, Time = Now, Source = source });

        public void Tick(float dt)
        {
            Now += dt;
            Recent.RemoveAll(n => Now - n.Time > 1.5f);
        }
    }
}
