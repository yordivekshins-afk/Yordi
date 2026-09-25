using System;
using System.Collections.Generic;
using System.IO;

namespace Deadhaul.Core
{
    public enum VehicleType : byte { Auto, Pickup, Roeiboot, Motorboot }

    public enum VehiclePart : byte { Accu, Bougies, Banden, Brandstofpomp }

    public sealed class VehicleDef
    {
        public VehicleType Type;
        public string Name;
        public bool Boat, NeedsFuel;
        public float MaxSpeed, Accel, Turn, Length, Width, Height;
        public float FuelCapacity, FuelPerKm;
        public VehiclePart[] Parts;
        public int Seats;
    }

    /// <summary>Een voertuig in de wereld, met onderdelen, brandstof en schade. Wordt opgeslagen.</summary>
    public sealed class Vehicle
    {
        public long Key;                 // plek waar hij gespawnd is (uniek)
        public VehicleDef Def;
        public V3 Pos;                   // midden onderkant
        public float Yaw, Pitch, Roll, Speed, Steer;
        public float Fuel;               // liter
        public float Health = 100;
        public bool[] PartOk = new bool[4];
        public byte Paint;
        public bool Occupied;

        public bool Needs(VehiclePart p) => Array.IndexOf(Def.Parts, p) >= 0 && !PartOk[(int)p];
        public bool Drivable => Health > 0 && MissingParts().Count == 0 && (!Def.NeedsFuel || Fuel > 0);

        public List<VehiclePart> MissingParts()
        {
            var l = new List<VehiclePart>();
            foreach (var p in Def.Parts) if (!PartOk[(int)p]) l.Add(p);
            return l;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Key); w.Write((byte)Def.Type); w.Write(Pos.X); w.Write(Pos.Y); w.Write(Pos.Z); w.Write(Yaw);
            w.Write(Fuel); w.Write(Health); w.Write(Paint);
            for (int i = 0; i < 4; i++) w.Write(PartOk[i]);
        }

        public static Vehicle Read(BinaryReader r)
        {
            var v = new Vehicle { Key = r.ReadInt64(), Def = Vehicles.Defs[(VehicleType)r.ReadByte()] };
            v.Pos = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); v.Yaw = r.ReadSingle();
            v.Fuel = r.ReadSingle(); v.Health = r.ReadSingle(); v.Paint = r.ReadByte();
            for (int i = 0; i < 4; i++) v.PartOk[i] = r.ReadBoolean();
            return v;
        }
    }

    public struct VehicleSpawn { public long Key; public VehicleType Type; public V3 Pos; public float Yaw; public byte Paint; public int Seed; }

    public static class Vehicles
    {
        public static readonly Dictionary<VehicleType, VehicleDef> Defs = new Dictionary<VehicleType, VehicleDef>
        {
            [VehicleType.Auto] = new VehicleDef { Type = VehicleType.Auto, Name = "Sedan", NeedsFuel = true, MaxSpeed = 32, Accel = 7, Turn = 1.9f, Length = 4.5f, Width = 2f, Height = 1.5f, FuelCapacity = 45, FuelPerKm = 0.09f, Seats = 4, Parts = new[] { VehiclePart.Accu, VehiclePart.Bougies, VehiclePart.Banden, VehiclePart.Brandstofpomp } },
            [VehicleType.Pickup] = new VehicleDef { Type = VehicleType.Pickup, Name = "Pick-up", NeedsFuel = true, MaxSpeed = 27, Accel = 6, Turn = 1.6f, Length = 5f, Width = 2.1f, Height = 1.8f, FuelCapacity = 70, FuelPerKm = 0.13f, Seats = 2, Parts = new[] { VehiclePart.Accu, VehiclePart.Bougies, VehiclePart.Banden, VehiclePart.Brandstofpomp } },
            [VehicleType.Roeiboot] = new VehicleDef { Type = VehicleType.Roeiboot, Name = "Roeiboot", Boat = true, MaxSpeed = 3.2f, Accel = 1.5f, Turn = 0.9f, Length = 3.5f, Width = 1.5f, Height = 0.7f, Seats = 2, Parts = new VehiclePart[0] },
            [VehicleType.Motorboot] = new VehicleDef { Type = VehicleType.Motorboot, Name = "Motorboot", Boat = true, NeedsFuel = true, MaxSpeed = 16, Accel = 3.5f, Turn = 0.9f, Length = 5f, Width = 2f, Height = 1.1f, FuelCapacity = 40, FuelPerKm = 0.2f, Seats = 3, Parts = new[] { VehiclePart.Accu, VehiclePart.Bougies } },
        };

        public static string PartItem(VehiclePart p) => p switch
        {
            VehiclePart.Accu => "accu",
            VehiclePart.Bougies => "bougies",
            VehiclePart.Banden => "band",
            _ => "brandstofpomp",
        };

        public static int PartCount(VehiclePart p) => p == VehiclePart.Banden ? 4 : 1;

        public static Vehicle Create(in VehicleSpawn s)
        {
            var def = Defs[s.Type];
            var v = new Vehicle { Key = s.Key, Def = def, Pos = s.Pos, Yaw = s.Yaw, Paint = s.Paint };
            var rng = new Random(s.Seed);
            // wrakken missen meestal twee à drie onderdelen; roeiboten zijn meteen bruikbaar
            foreach (var p in def.Parts) v.PartOk[(int)p] = rng.NextDouble() < 0.3;
            if (def.Parts.Length > 0 && v.MissingParts().Count == 0) v.PartOk[(int)def.Parts[rng.Next(def.Parts.Length)]] = false;
            v.Fuel = def.NeedsFuel ? (float)(rng.NextDouble() < 0.3 ? rng.NextDouble() * def.FuelCapacity * 0.3 : 0) : 0;
            return v;
        }
    }

    /// <summary>Rijden en varen: arcade-achtig, maar met hellingen, obstakels en water.</summary>
    public static class VehiclePhysics
    {
        public struct Input { public float Throttle, Steer; public bool Brake; }

        /// <summary>Grondhoogte (meter) onder een punt, gezocht rond de huidige hoogte.</summary>
        public static float Ground(VoxelStore store, float x, float y, float z)
        {
            int vx = (int)MathF.Floor(x / World.VoxelSize), vz = (int)MathF.Floor(z / World.VoxelSize);
            int vy = (int)MathF.Floor(y / World.VoxelSize) + 2;
            for (int k = 0; k < 8; k++, vy--)
            {
                byte b = store.Get(vx, vy, vz);
                if (Blocks.Solid[b] && !B.IsPlant(b) && b != B.Leaves && b != B.DeadLeaves) return (vy + 1) * World.VoxelSize;
            }
            return y - 4 * World.VoxelSize;
        }

        public static bool IsWater(VoxelStore store, float x, float z)
        {
            int vx = (int)MathF.Floor(x / World.VoxelSize), vz = (int)MathF.Floor(z / World.VoxelSize);
            return store.Get(vx, World.Sea, vz) == B.Water && store.Get(vx, World.Sea + 1, vz) == B.Air;
        }

        /// <summary>Eén stap. Geeft de afgelegde afstand (meter) terug en of hij iets raakte (botssnelheid).</summary>
        public static float Step(Vehicle v, Input input, float dt, VoxelStore store, out float crash)
        {
            crash = 0;
            var d = v.Def;
            bool canDrive = v.Drivable && v.Occupied;
            float throttle = canDrive ? input.Throttle : 0;
            if (v.Def.NeedsFuel && v.Fuel <= 0) throttle = 0;
            float target = throttle * d.MaxSpeed * (throttle < 0 ? 0.35f : 1f);
            float acc = (input.Brake ? d.Accel * 2.5f : d.Accel);
            if (MathF.Abs(throttle) < 0.01f && !input.Brake) acc = d.Accel * 0.35f;   // uitrollen
            v.Speed += Math.Clamp(target - v.Speed, -acc * dt, acc * dt);
            if (input.Brake) v.Speed = MoveTowards(v.Speed, 0, d.Accel * 3 * dt);
            v.Steer = MoveTowards(v.Steer, canDrive ? input.Steer : 0, dt * 3f);
            float turnRate = d.Turn * v.Steer * Math.Clamp(MathF.Abs(v.Speed) / 5f, 0, 1) * MathF.Sign(v.Speed == 0 ? 1 : v.Speed) * (d.Boat ? 1 : 1 / (1 + MathF.Abs(v.Speed) * 0.03f));
            v.Yaw += turnRate * dt * 57.2958f;
            float yr = v.Yaw * 0.0174533f;
            float fx = MathF.Sin(yr), fz = MathF.Cos(yr);
            float dist = v.Speed * dt;
            var next = new V3(v.Pos.X + fx * dist, v.Pos.Y, v.Pos.Z + fz * dist);

            if (d.Boat)
            {
                // boten blijven op het water; ondiep of land = vast
                float bowX = next.X + fx * d.Length * 0.5f * MathF.Sign(v.Speed == 0 ? 1 : v.Speed), bowZ = next.Z + fz * d.Length * 0.5f * MathF.Sign(v.Speed == 0 ? 1 : v.Speed);
                if (!IsWater(store, bowX, bowZ)) { crash = MathF.Abs(v.Speed); v.Speed *= -0.2f; return 0; }
                v.Pos = new V3(next.X, World.SeaLevelMeters - 0.15f, next.Z);
                v.Roll = MoveTowards(v.Roll, -v.Steer * MathF.Min(1, MathF.Abs(v.Speed) / 8f) * 8f, dt * 20);
                v.Pitch = MoveTowards(v.Pitch, -MathF.Min(6, MathF.Abs(v.Speed) * 0.5f), dt * 10);
                if (v.Def.NeedsFuel && throttle != 0) v.Fuel = MathF.Max(0, v.Fuel - MathF.Abs(dist) / 1000f * d.FuelPerKm * 100f / 10f);
                return MathF.Abs(dist);
            }

            // vier wielen: grond onder elk wiel bepaalt hoogte, stampen en rollen
            float hl = d.Length * 0.4f, hw = d.Width * 0.45f;
            float rx = fz, rz = -fx;
            float gFL = Ground(store, next.X + fx * hl - rx * hw, next.Y, next.Z + fz * hl - rz * hw);
            float gFR = Ground(store, next.X + fx * hl + rx * hw, next.Y, next.Z + fz * hl + rz * hw);
            float gBL = Ground(store, next.X - fx * hl - rx * hw, next.Y, next.Z - fz * hl - rz * hw);
            float gBR = Ground(store, next.X - fx * hl + rx * hw, next.Y, next.Z - fz * hl + rz * hw);
            float front = (gFL + gFR) * 0.5f, back = (gBL + gBR) * 0.5f;
            float ground = MathF.Max((front + back) * 0.5f, MathF.Max(front, back) - 0.6f);
            // obstakel: muur of stap hoger dan een halve meter voor de neus
            float noseX = next.X + fx * (hl + 0.4f) * MathF.Sign(v.Speed == 0 ? 1 : v.Speed), noseZ = next.Z + fz * (hl + 0.4f) * MathF.Sign(v.Speed == 0 ? 1 : v.Speed);
            float noseGround = Ground(store, noseX, v.Pos.Y + 1.5f, noseZ);
            if (noseGround - v.Pos.Y > 0.6f)
            {
                crash = MathF.Abs(v.Speed);
                v.Speed *= -0.15f;
                return 0;
            }
            float fall = next.Y - ground;
            next.Y = fall > 0.05f ? next.Y - MathF.Min(fall, 9.81f * dt * 1.5f) : ground;
            v.Pitch = MoveTowards(v.Pitch, MathF.Atan2(back - front, d.Length * 0.8f) * 57.2958f, dt * 60);
            v.Roll = MoveTowards(v.Roll, MathF.Atan2(((gFL + gBL) - (gFR + gBR)) * 0.5f, d.Width * 0.9f) * 57.2958f, dt * 60);
            v.Pos = next;
            if (v.Def.NeedsFuel && throttle != 0) v.Fuel = MathF.Max(0, v.Fuel - MathF.Abs(dist) / 1000f * d.FuelPerKm * 100f / 10f);
            return MathF.Abs(dist);
        }

        static float MoveTowards(float a, float b, float d) => MathF.Abs(b - a) <= d ? b : a + MathF.Sign(b - a) * d;
    }

    /// <summary>Vissen: vangst hangt af van water, straling en toeval.</summary>
    public static class Fishing
    {
        static readonly (string id, float w)[] Clean = { ("baars", 4), ("karper", 3), ("snoek", 1.5f), ("oude_schoen", 0.5f) };
        static readonly (string id, float w)[] Irradiated = { ("gloeivis", 4), ("baars", 1), ("oude_schoen", 0.6f) };

        public static string Catch(float radiation, Random rng)
        {
            var t = radiation > 0.15f ? Irradiated : Clean;
            float total = 0; foreach (var x in t) total += x.w;
            float pick = (float)rng.NextDouble() * total;
            foreach (var x in t) { pick -= x.w; if (pick <= 0) return x.id; }
            return t[0].id;
        }

        /// <summary>Hoe lang je moet wachten op een beet (seconden). Aas helpt.</summary>
        public static float WaitTime(bool bait, Random rng) => (float)(bait ? 3 + rng.NextDouble() * 7 : 8 + rng.NextDouble() * 16);
    }
}
