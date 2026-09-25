using System;
using System.Collections.Generic;
using System.IO;

namespace Deadhaul.Core
{
    public enum ItemKind { Material, Food, Drink, Medical, Tool, Weapon, Ammo, Block, Misc }

    public sealed class ItemDef
    {
        public string Id, Name, Description;
        public ItemKind Kind;
        public int MaxStack = 1;
        public float Weight;             // kg per stuk
        public float Food, Water, Heal;  // bij gebruik
        public bool StopsBleeding, CuresSickness;
        public byte PlaceBlock;          // voor bouwmateriaal: welk blok je neerzet
        public float MeleeDamage, MineSpeed = 1f, WoodSpeed = 1f;
        public float GunDamage;          // voor vuurwapens
        public string AmmoId;
        public int MagSize;
        public float FireInterval = 0.5f;
        public byte IconBlock;           // kleur voor het icoon
    }

    public static class Items
    {
        public static readonly Dictionary<string, ItemDef> All = new Dictionary<string, ItemDef>();

        static ItemDef Add(ItemDef d) { All[d.Id] = d; return d; }
        public static ItemDef Get(string id) => id != null && All.TryGetValue(id, out var d) ? d : null;

        static Items()
        {
            // bouwmaterialen
            Add(new ItemDef { Id = "hout", Name = "Hout", Kind = ItemKind.Block, MaxStack = 50, Weight = 0.8f, PlaceBlock = B.Planks, IconBlock = B.Planks, Description = "Planken om mee te bouwen." });
            Add(new ItemDef { Id = "steen", Name = "Steen", Kind = ItemKind.Block, MaxStack = 50, Weight = 1.2f, PlaceBlock = B.StoneWall, IconBlock = B.StoneWall, Description = "Brokken steen en beton." });
            Add(new ItemDef { Id = "schroot", Name = "Schroot", Kind = ItemKind.Block, MaxStack = 50, Weight = 1f, PlaceBlock = B.MetalWall, IconBlock = B.MetalWall, Description = "Plaatstaal en onderdelen." });
            Add(new ItemDef { Id = "aarde", Name = "Aarde", Kind = ItemKind.Block, MaxStack = 50, Weight = 1f, PlaceBlock = B.Dirt, IconBlock = B.Dirt });
            Add(new ItemDef { Id = "zand", Name = "Zand", Kind = ItemKind.Block, MaxStack = 50, Weight = 1f, PlaceBlock = B.Sand, IconBlock = B.Sand });
            Add(new ItemDef { Id = "stof", Name = "Stof", Kind = ItemKind.Material, MaxStack = 20, Weight = 0.1f, IconBlock = B.Carpet, Description = "Lappen stof. Voor verband." });
            Add(new ItemDef { Id = "rubber", Name = "Rubber", Kind = ItemKind.Material, MaxStack = 20, Weight = 0.3f, IconBlock = B.Tire });
            Add(new ItemDef { Id = "kruit", Name = "Buskruit", Kind = ItemKind.Material, MaxStack = 30, Weight = 0.05f, IconBlock = B.Gravel, Description = "Om zelf patronen te maken." });
            Add(new ItemDef { Id = "batterij", Name = "Batterij", Kind = ItemKind.Misc, MaxStack = 6, Weight = 0.05f, IconBlock = B.Metal, Description = "Houdt je zaklamp aan." });
            Add(new ItemDef { Id = "kampvuur", Name = "Kampvuur", Kind = ItemKind.Block, MaxStack = 5, Weight = 3f, PlaceBlock = B.Campfire, IconBlock = B.Campfire, Description = "Warmte en licht. Houdt de nacht op afstand." });
            // eten en drinken
            Add(new ItemDef { Id = "bonen", Name = "Blik bonen", Kind = ItemKind.Food, MaxStack = 5, Weight = 0.45f, Food = 35, Water = 5, IconBlock = B.Metal });
            Add(new ItemDef { Id = "chips", Name = "Zak chips", Kind = ItemKind.Food, MaxStack = 5, Weight = 0.15f, Food = 15, Water = -5, IconBlock = B.CarRed });
            Add(new ItemDef { Id = "groente", Name = "Wilde groente", Kind = ItemKind.Food, MaxStack = 10, Weight = 0.2f, Food = 10, Water = 5, IconBlock = B.Crop });
            Add(new ItemDef { Id = "water", Name = "Fles water", Kind = ItemKind.Drink, MaxStack = 4, Weight = 1f, Water = 45, IconBlock = B.Glass });
            Add(new ItemDef { Id = "frisdrank", Name = "Frisdrank", Kind = ItemKind.Drink, MaxStack = 4, Weight = 0.35f, Water = 25, Food = 5, IconBlock = B.CarBlue });
            // medisch
            Add(new ItemDef { Id = "verband", Name = "Verband", Kind = ItemKind.Medical, MaxStack = 6, Weight = 0.05f, Heal = 10, StopsBleeding = true, IconBlock = B.Plaster, Description = "Stopt bloedingen." });
            Add(new ItemDef { Id = "medkit", Name = "EHBO-kit", Kind = ItemKind.Medical, MaxStack = 2, Weight = 0.6f, Heal = 55, StopsBleeding = true, IconBlock = B.CarWhite });
            Add(new ItemDef { Id = "antibiotica", Name = "Antibiotica", Kind = ItemKind.Medical, MaxStack = 4, Weight = 0.05f, CuresSickness = true, IconBlock = B.Tile, Description = "Geneest ziekte van vies water." });
            // gereedschap en wapens
            Add(new ItemDef { Id = "pijp", Name = "Loden pijp", Kind = ItemKind.Weapon, Weight = 1.5f, MeleeDamage = 28, MineSpeed = 1.3f, IconBlock = B.Gunmetal });
            Add(new ItemDef { Id = "bijl", Name = "Bijl", Kind = ItemKind.Tool, Weight = 1.8f, MeleeDamage = 32, MineSpeed = 1.2f, WoodSpeed = 3.5f, IconBlock = B.Blade });
            Add(new ItemDef { Id = "breekijzer", Name = "Breekijzer", Kind = ItemKind.Tool, Weight = 1.6f, MeleeDamage = 24, MineSpeed = 3f, IconBlock = B.Rust, Description = "Sloopt steen en metaal veel sneller." });
            Add(new ItemDef { Id = "pistool", Name = "Pistool", Kind = ItemKind.Weapon, Weight = 0.9f, GunDamage = 34, AmmoId = "9mm", MagSize = 12, FireInterval = 0.28f, IconBlock = B.Gunmetal });
            Add(new ItemDef { Id = "geweer", Name = "Jachtgeweer", Kind = ItemKind.Weapon, Weight = 3.4f, GunDamage = 85, AmmoId = "308", MagSize = 5, FireInterval = 1.1f, IconBlock = B.Wood });
            Add(new ItemDef { Id = "9mm", Name = "9mm-patronen", Kind = ItemKind.Ammo, MaxStack = 60, Weight = 0.012f, IconBlock = B.RoadLine });
            Add(new ItemDef { Id = "308", Name = ".308-patronen", Kind = ItemKind.Ammo, MaxStack = 30, Weight = 0.025f, IconBlock = B.RoadLine });
        }
    }

    [Serializable]
    public struct Stack
    {
        public string Id;
        public int Count;
        public bool Empty => Id == null || Count <= 0;
        public ItemDef Def => Items.Get(Id);
        public static readonly Stack None = new Stack();
        public Stack(string id, int count) { Id = id; Count = count; }
    }

    /// <summary>Rugzak met vakken; de eerste 6 zijn de snelbalk.</summary>
    public sealed class Inventory
    {
        public const int HotbarSize = 6;
        public readonly Stack[] Slots;
        public float MaxWeight = 30f;

        public Inventory(int size = 30) { Slots = new Stack[size]; }

        public float Weight
        {
            get { float w = 0; foreach (var s in Slots) if (!s.Empty) w += s.Def.Weight * s.Count; return w; }
        }

        public int Count(string id) { int n = 0; foreach (var s in Slots) if (s.Id == id) n += s.Count; return n; }

        /// <summary>Voegt toe; geeft terug hoeveel er níet paste.</summary>
        public int Add(string id, int count)
        {
            var def = Items.Get(id);
            if (def == null) return count;
            for (int i = 0; i < Slots.Length && count > 0; i++)
                if (Slots[i].Id == id && Slots[i].Count < def.MaxStack)
                {
                    int take = Math.Min(count, def.MaxStack - Slots[i].Count);
                    Slots[i].Count += take; count -= take;
                }
            for (int i = 0; i < Slots.Length && count > 0; i++)
                if (Slots[i].Empty)
                {
                    int take = Math.Min(count, def.MaxStack);
                    Slots[i] = new Stack(id, take); count -= take;
                }
            return count;
        }

        public bool Remove(string id, int count)
        {
            if (Count(id) < count) return false;
            for (int i = Slots.Length - 1; i >= 0 && count > 0; i--)
                if (Slots[i].Id == id)
                {
                    int take = Math.Min(count, Slots[i].Count);
                    Slots[i].Count -= take; count -= take;
                    if (Slots[i].Count <= 0) Slots[i] = Stack.None;
                }
            return true;
        }

        public void TakeFromSlot(int i, int n = 1)
        {
            Slots[i].Count -= n;
            if (Slots[i].Count <= 0) Slots[i] = Stack.None;
        }

        public void Swap(int a, int b) { var t = Slots[a]; Slots[a] = Slots[b]; Slots[b] = t; }

        public void Write(BinaryWriter w)
        {
            w.Write(Slots.Length);
            foreach (var s in Slots) { w.Write(s.Id ?? ""); w.Write(s.Count); }
        }

        public void Read(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                string id = r.ReadString(); int c = r.ReadInt32();
                if (i < Slots.Length) Slots[i] = string.IsNullOrEmpty(id) || Items.Get(id) == null ? Stack.None : new Stack(id, c);
            }
        }
    }

    public sealed class Recipe
    {
        public string Result; public int Count;
        public (string id, int n)[] Needs;
        public bool NeedsFire;
    }

    public static class Crafting
    {
        public static readonly Recipe[] All =
        {
            new Recipe { Result = "verband", Count = 1, Needs = new[] { ("stof", 2) } },
            new Recipe { Result = "kampvuur", Count = 1, Needs = new[] { ("hout", 4), ("steen", 3) } },
            new Recipe { Result = "bijl", Count = 1, Needs = new[] { ("hout", 2), ("schroot", 3) } },
            new Recipe { Result = "breekijzer", Count = 1, Needs = new[] { ("schroot", 5) } },
            new Recipe { Result = "9mm", Count = 6, Needs = new[] { ("schroot", 1), ("kruit", 2) } },
            new Recipe { Result = "308", Count = 3, Needs = new[] { ("schroot", 1), ("kruit", 3) } },
        };

        public static bool CanCraft(Inventory inv, Recipe r, bool nearFire)
        {
            if (r.NeedsFire && !nearFire) return false;
            foreach (var (id, n) in r.Needs) if (inv.Count(id) < n) return false;
            return true;
        }

        public static bool Craft(Inventory inv, Recipe r, bool nearFire)
        {
            if (!CanCraft(inv, r, nearFire)) return false;
            foreach (var (id, n) in r.Needs) inv.Remove(id, n);
            int left = inv.Add(r.Result, r.Count);
            return left == 0;
        }
    }

    public static class Loot
    {
        static readonly (string id, int min, int max, float w)[] Huis =
            { ("bonen", 1, 2, 3), ("chips", 1, 2, 2), ("water", 1, 1, 3), ("frisdrank", 1, 2, 2), ("stof", 1, 3, 3), ("verband", 1, 1, 1.5f), ("batterij", 1, 2, 1.5f), ("hout", 2, 5, 1), ("9mm", 3, 8, 0.6f), ("pijp", 1, 1, 0.4f), ("bijl", 1, 1, 0.3f) };
        static readonly (string id, int min, int max, float w)[] Winkel =
            { ("bonen", 1, 3, 4), ("chips", 1, 3, 4), ("water", 1, 2, 4), ("frisdrank", 1, 3, 3), ("batterij", 1, 3, 2), ("stof", 1, 2, 1) };
        static readonly (string id, int min, int max, float w)[] Apotheek =
            { ("verband", 1, 3, 4), ("medkit", 1, 1, 1.5f), ("antibiotica", 1, 2, 2), ("water", 1, 1, 1), ("stof", 1, 3, 1) };
        static readonly (string id, int min, int max, float w)[] Politie =
            { ("9mm", 6, 18, 4), ("308", 3, 8, 2), ("pistool", 1, 1, 1.2f), ("geweer", 1, 1, 0.5f), ("kruit", 2, 6, 2), ("verband", 1, 2, 1.5f), ("breekijzer", 1, 1, 0.8f) };
        static readonly (string id, int min, int max, float w)[] Industrie =
            { ("schroot", 2, 6, 4), ("kruit", 1, 4, 2), ("rubber", 1, 3, 2), ("batterij", 1, 2, 1.5f), ("breekijzer", 1, 1, 0.6f), ("bijl", 1, 1, 0.5f), ("water", 1, 1, 1) };

        /// <summary>Deterministische inhoud van een container op deze plek.</summary>
        public static List<Stack> Roll(LotType? type, byte container, int x, int y, int z, int seed)
        {
            var table = container == B.Barrel ? Industrie : type switch
            {
                LotType.Winkel => Winkel,
                LotType.Apotheek => Apotheek,
                LotType.Politie => Politie,
                LotType.Benzinestation => Industrie,
                LotType.Ruine => Industrie,
                _ => Huis,
            };
            var rng = new Random((int)(Hash.H3(x, y, z, seed ^ 0x100f) * int.MaxValue));
            int rolls = 1 + rng.Next(container == B.Shelf ? 2 : 3);
            float total = 0; foreach (var t in table) total += t.w;
            var result = new List<Stack>();
            for (int i = 0; i < rolls; i++)
            {
                float pick = (float)rng.NextDouble() * total;
                foreach (var t in table)
                {
                    pick -= t.w;
                    if (pick > 0) continue;
                    int n = rng.Next(t.min, t.max + 1);
                    int existing = result.FindIndex(s => s.Id == t.id);
                    if (existing >= 0) { var s = result[existing]; s.Count += n; result[existing] = s; }
                    else result.Add(new Stack(t.id, n));
                    break;
                }
            }
            return result;
        }
    }

    /// <summary>De lichaamstoestand van de overlever.</summary>
    public sealed class Survival
    {
        public float Health = 100, Food = 85, Water = 85, Stamina = 100, Warmth = 100;
        public bool Bleeding, Sick;
        public bool Dead => Health <= 0;

        // snelheden per echte seconde
        public const float FoodDrain = 100f / (26 * 60);
        public const float WaterDrain = 100f / (18 * 60);

        /// <summary>
        /// ambient: omgevingstemperatuur 0 (ijskoud) .. 1 (warm). exertion: 0 rust, 1 lopen, 2 sprinten.
        /// </summary>
        public void Tick(float dt, float ambient, float exertion, bool inWater, bool nearFire)
        {
            if (Dead) return;
            Food = Math.Max(0, Food - FoodDrain * dt * (1 + exertion * 0.35f));
            Water = Math.Max(0, Water - WaterDrain * dt * (1 + exertion * 0.5f));

            float target = nearFire ? 1f : Math.Clamp(ambient - (inWater ? 0.45f : 0), 0, 1);
            float warmthGoal = target * 100;
            Warmth += (warmthGoal - Warmth) * Math.Min(1, dt * (nearFire ? 0.2f : 0.025f));

            if (exertion >= 2) Stamina = Math.Max(0, Stamina - 14 * dt);
            else Stamina = Math.Min(100, Stamina + (Food > 20 ? 11 : 5) * dt);

            float dmg = 0;
            if (Food <= 0) dmg += 0.5f;
            if (Water <= 0) dmg += 0.8f;
            if (Warmth < 20) dmg += (20 - Warmth) * 0.03f;
            if (Bleeding) dmg += 0.9f;
            if (Sick) { dmg += 0.25f; Water = Math.Max(0, Water - 0.05f * dt); }
            Health -= dmg * dt;
            if (dmg == 0 && Food > 50 && Water > 50 && Warmth > 40) Health = Math.Min(100, Health + 0.35f * dt);
            if (Health < 0) Health = 0;
        }

        /// <summary>Gebruik een item; geeft een melding terug of null als het niet kan.</summary>
        public string Use(ItemDef d)
        {
            switch (d.Kind)
            {
                case ItemKind.Food:
                case ItemKind.Drink:
                    Food = Math.Clamp(Food + d.Food, 0, 100);
                    Water = Math.Clamp(Water + d.Water, 0, 100);
                    return d.Kind == ItemKind.Food ? $"Je eet: {d.Name}." : $"Je drinkt: {d.Name}.";
                case ItemKind.Medical:
                    Health = Math.Min(100, Health + d.Heal);
                    if (d.StopsBleeding) Bleeding = false;
                    if (d.CuresSickness) Sick = false;
                    return $"Gebruikt: {d.Name}.";
            }
            return null;
        }

        public void Hurt(float amount, float bleedChance, Random rng)
        {
            Health = Math.Max(0, Health - amount);
            if (rng.NextDouble() < bleedChance) Bleeding = true;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Health); w.Write(Food); w.Write(Water); w.Write(Stamina); w.Write(Warmth); w.Write(Bleeding); w.Write(Sick);
        }

        public void Read(BinaryReader r)
        {
            Health = r.ReadSingle(); Food = r.ReadSingle(); Water = r.ReadSingle(); Stamina = r.ReadSingle();
            Warmth = r.ReadSingle(); Bleeding = r.ReadBoolean(); Sick = r.ReadBoolean();
        }
    }

    /// <summary>Dag-nachtcyclus: 1 speldag = 24 echte minuten.</summary>
    public sealed class GameClock
    {
        public const float RealSecondsPerDay = 24 * 60;
        public double Time = 7.0 / 24.0;   // dagen; start om 07:00 op dag 1

        public int Day => (int)Time + 1;
        public float HourOfDay => (float)((Time - Math.Floor(Time)) * 24.0);
        public void Tick(float dt) => Time += dt / RealSecondsPerDay;

        /// <summary>0 = middernacht-koud, 1 = middag-warm.</summary>
        public float Ambient
        {
            get
            {
                float h = HourOfDay;
                float sun = MathF.Cos((h - 14f) / 24f * MathF.PI * 2f) * 0.5f + 0.5f;
                return 0.28f + sun * 0.62f;
            }
        }

        public string Label => $"Dag {Day}  {(int)HourOfDay:00}:{(int)((HourOfDay % 1) * 60):00}";
    }
}
