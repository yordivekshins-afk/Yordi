using System;
using System.Collections.Generic;
using System.IO;

namespace Deadhaul.Core
{
    /// <summary>
    /// Stroom in je basis. Generatoren verbranden brandstof (luid), zonnepanelen laden overdag op en
    /// geven 's nachts terug (stil). Bouwlampen binnen bereik van een bron gaan 's nachts aan, zolang
    /// er capaciteit is: de dichtstbijzijnde bron neemt ze eerst.
    /// </summary>
    public sealed class PowerGrid
    {
        public sealed class Source
        {
            public long Key; public int X, Y, Z;
            public bool Generator;
            public float Fuel;           // liter (generator)
            public bool Running = true;  // aan/uit (generator)
            public float Charge;         // lampuren (zonnepaneel)
            public int Served;           // lampen deze tik
            public bool Active;          // levert nu stroom
        }

        public const int Range = 60;                     // voxels (30 m)
        public const float FuelPerHour = 1.2f;           // liter per speluur
        public const int GeneratorLamps = 16, SolarLamps = 4;
        public const float SolarCapacity = 12f;          // lampuren
        public const float SolarChargePerHour = 3f;      // lampuren per zonnig speluur

        public readonly Dictionary<long, Source> Sources = new Dictionary<long, Source>();
        public readonly Dictionary<long, (int x, int y, int z)> Lamps = new Dictionary<long, (int, int, int)>();
        readonly List<(Source s, float d)> near = new List<(Source, float)>();

        public Source AddSource(int x, int y, int z, bool generator)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (!Sources.TryGetValue(k, out var s)) Sources[k] = s = new Source { Key = k, X = x, Y = y, Z = z, Generator = generator };
            return s;
        }

        public void Remove(long key) { Sources.Remove(key); Lamps.Remove(key); }
        public void AddLamp(int x, int y, int z) => Lamps[VoxelStore.VoxelKey(x, y, z)] = (x, y, z);

        /// <summary>
        /// Laat de tijd lopen en bepaalt welke lampen branden. sun: 0..1 zonlicht op de panelen.
        /// </summary>
        public void Tick(float hours, float sun, bool night, HashSet<long> lit)
        {
            lit.Clear();
            foreach (var s in Sources.Values)
            {
                s.Served = 0;
                if (s.Generator) s.Active = s.Running && s.Fuel > 0;
                else
                {
                    s.Charge = Math.Min(SolarCapacity, s.Charge + hours * SolarChargePerHour * sun);
                    s.Active = s.Charge > 0.01f;
                }
            }
            if (night)
                foreach (var kv in Lamps)
                {
                    var (x, y, z) = kv.Value;
                    near.Clear();
                    foreach (var s in Sources.Values)
                    {
                        if (!s.Active) continue;
                        int cap = s.Generator ? GeneratorLamps : SolarLamps;
                        if (s.Served >= cap) continue;
                        float dx = s.X - x, dy = s.Y - y, dz = s.Z - z;
                        float d = dx * dx + dy * dy + dz * dz;
                        if (d <= Range * Range) near.Add((s, d));
                    }
                    if (near.Count == 0) continue;
                    near.Sort((a, b) => a.d.CompareTo(b.d));
                    near[0].s.Served++;
                    lit.Add(kv.Key);
                }
            foreach (var s in Sources.Values)
            {
                if (s.Generator) { if (s.Active) s.Fuel = Math.Max(0, s.Fuel - hours * FuelPerHour); }
                else s.Charge = Math.Max(0, s.Charge - hours * s.Served);
            }
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Sources.Count);
            foreach (var s in Sources.Values) { w.Write(s.X); w.Write(s.Y); w.Write(s.Z); w.Write(s.Generator); w.Write(s.Fuel); w.Write(s.Running); w.Write(s.Charge); }
        }

        public void Read(BinaryReader r)
        {
            Sources.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var s = AddSource(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadBoolean());
                s.Fuel = r.ReadSingle(); s.Running = r.ReadBoolean(); s.Charge = r.ReadSingle();
            }
        }
    }

    /// <summary>Opslagkisten met blijvende inhoud, en de plek waar je na je dood wakker wordt.</summary>
    public sealed class BaseState
    {
        public const int ChestSlots = 24;
        public readonly Dictionary<long, List<Stack>> Chests = new Dictionary<long, List<Stack>>();
        public readonly PowerGrid Power = new PowerGrid();
        public bool HasBed;
        public V3 BedSpawn;

        public List<Stack> Chest(int x, int y, int z)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (!Chests.TryGetValue(k, out var list)) Chests[k] = list = new List<Stack>();
            return list;
        }

        /// <summary>Kist gesloopt: geeft de inhoud terug.</summary>
        public List<Stack> TakeChest(int x, int y, int z)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (!Chests.TryGetValue(k, out var list)) return new List<Stack>();
            Chests.Remove(k);
            return list;
        }

        /// <summary>Biodiesel stoken: 4 frituurvet, of 6 maïs + 2 water, in een lege jerrycan.</summary>
        public static string Distill(Inventory inv)
        {
            if (inv.Count("jerrycan_leeg") < 1) return "Je hebt een lege jerrycan nodig.";
            if (inv.Count("frituurvet") >= 4) inv.Remove("frituurvet", 4);
            else if (inv.Count("mais") >= 6 && inv.Count("water") >= 2) { inv.Remove("mais", 6); inv.Remove("water", 2); }
            else return "Nodig: 4 frituurvet, of 6 maïs en 2 water.";
            inv.Remove("jerrycan_leeg", 1);
            inv.Add("jerrycan", 1);
            return null;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Chests.Count);
            foreach (var kv in Chests)
            {
                w.Write(kv.Key);
                w.Write(kv.Value.Count);
                foreach (var s in kv.Value) s.Write(w);
            }
            Power.Write(w);
            w.Write(HasBed); w.Write(BedSpawn.X); w.Write(BedSpawn.Y); w.Write(BedSpawn.Z);
        }

        public void Read(BinaryReader r)
        {
            Chests.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                long k = r.ReadInt64();
                int c = r.ReadInt32();
                var list = new List<Stack>(c);
                for (int j = 0; j < c; j++) list.Add(Stack.Read(r));
                Chests[k] = list;
            }
            Power.Read(r);
            HasBed = r.ReadBoolean(); BedSpawn = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
    }
}
