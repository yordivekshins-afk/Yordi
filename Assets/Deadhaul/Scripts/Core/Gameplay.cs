using System;
using System.Collections.Generic;
using System.IO;

namespace Deadhaul.Core
{
    public enum ItemKind { Material, Food, Drink, Medical, Tool, Weapon, Ammo, Block, Misc, Attachment, Clothing }

    public sealed class ItemDef
    {
        public string Id, Name, Description;
        public ItemKind Kind;
        public int MaxStack = 1;
        public float Weight;             // kg per stuk
        public float Food, Water, Heal;  // bij gebruik
        public bool StopsBleeding, CuresSickness;
        public float SickChance;         // kans op ziekte bij eten (rauw vlees)
        public byte PlaceBlock;          // voor bouwmateriaal: welk blok je neerzet
        public float MeleeDamage, MineSpeed = 1f, WoodSpeed = 1f;
        public float MeleeInterval = 0.55f, BackstabMul = 2.5f;   // tijd tussen slagen; schade bij een sluipaanval
        public float GunDamage;          // voor vuurwapens: schade per kogel (per hagelkorrel bij shotguns)
        public string AmmoId;
        public int MagSize;
        public float FireInterval = 0.5f;
        public byte IconBlock;           // kleur voor het icoon
        public int Value;                // handelswaarde in doppen (0 = automatisch)

        // vuurwapens (zie Arsenal)
        public WeaponClass Class;
        public bool Automatic;
        public int Pellets = 1;
        public float Spread = 1f;        // graden, heupvuur in stilstand
        public float RecoilV = 1f, RecoilH = 0.3f;
        public float MuzzleVelocity = 380f;
        public float ReloadTime = 2f;
        public float Noise = 150f;       // meter
        public AttachMask Mounts;        // welke attachments passen

        // attachments
        public AttachSlot AttachSlot;
        public float RecoilMul = 1f, SpreadMul = 1f, NoiseMul = 1f, AdsTimeMul = 1f, VelocityMul = 1f;
        public float Zoom = 1f;          // vergroting bij richten
        public int ExtraMag;
        public bool Flashlight, Laser, HidesFlash;
        public WeaponClass FitsClasses = (WeaponClass)0xFF;

        // kleding en uitrusting
        public EquipSlot Equip = EquipSlot.None;
        public int Capacity;             // extra rugzakvakken
        public float CarryBonus;         // extra draaggewicht in kg
        public float Warmth;             // 0..1
        public float Armor;              // 0..1 schadereductie op het bedekte lichaamsdeel
        public float RadProtection;      // 0..1
        public byte Color1, Color2;      // kleuren op het personage
        public int Style;                // variant van het model
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
            Add(new ItemDef { Id = "vlees", Name = "Rauw vlees", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.5f, Food = 14, SickChance = 0.45f, IconBlock = B.MutantFlesh, Description = "Bak het boven een kampvuur." });
            Add(new ItemDef { Id = "gebakken_vlees", Name = "Gebakken vlees", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.4f, Food = 40, Water = -3, IconBlock = B.Leather });
            Add(new ItemDef { Id = "vacht", Name = "Vacht", Kind = ItemKind.Material, MaxStack = 5, Weight = 0.8f, IconBlock = B.Fur, Description = "Voor warme kleding." });
            Add(new ItemDef { Id = "water", Name = "Fles water", Kind = ItemKind.Drink, MaxStack = 4, Weight = 1f, Water = 45, IconBlock = B.Glass });
            Add(new ItemDef { Id = "frisdrank", Name = "Frisdrank", Kind = ItemKind.Drink, MaxStack = 4, Weight = 0.35f, Water = 25, Food = 5, IconBlock = B.CarBlue });
            // medisch
            Add(new ItemDef { Id = "verband", Name = "Verband", Kind = ItemKind.Medical, MaxStack = 6, Weight = 0.05f, Heal = 10, StopsBleeding = true, IconBlock = B.Plaster, Description = "Stopt bloedingen." });
            Add(new ItemDef { Id = "medkit", Name = "EHBO-kit", Kind = ItemKind.Medical, MaxStack = 2, Weight = 0.6f, Heal = 55, StopsBleeding = true, IconBlock = B.CarWhite });
            Add(new ItemDef { Id = "jodium", Name = "Jodiumtabletten", Kind = ItemKind.Medical, MaxStack = 10, Weight = 0.02f, IconBlock = B.Glow, Description = "Verlaagt de opgenomen straling." });
            Add(new ItemDef { Id = "antibiotica", Name = "Antibiotica", Kind = ItemKind.Medical, MaxStack = 4, Weight = 0.05f, CuresSickness = true, IconBlock = B.Tile, Description = "Geneest ziekte van vies water." });
            // gereedschap en wapens
            Add(new ItemDef { Id = "pijp", Name = "Loden pijp", Kind = ItemKind.Weapon, Weight = 1.5f, MeleeDamage = 28, MineSpeed = 1.3f, IconBlock = B.Gunmetal });
            Add(new ItemDef { Id = "bijl", Name = "Bijl", Kind = ItemKind.Tool, Weight = 1.8f, MeleeDamage = 32, MineSpeed = 1.2f, WoodSpeed = 3.5f, IconBlock = B.Blade });
            Add(new ItemDef { Id = "mes", Name = "Gevechtsmes", Kind = ItemKind.Weapon, Weight = 0.3f, MeleeDamage = 30, MeleeInterval = 0.32f, BackstabMul = 8f, IconBlock = B.Blade, Description = "Snel en stil. Van achteren of ongezien: één steek is genoeg." });
            Add(new ItemDef { Id = "machete", Name = "Machete", Kind = ItemKind.Weapon, Weight = 0.7f, MeleeDamage = 42, MeleeInterval = 0.5f, BackstabMul = 4f, WoodSpeed = 2.2f, IconBlock = B.Blade, Description = "Hakt door struiken, hout en ghouls." });
            Add(new ItemDef { Id = "breekijzer", Name = "Breekijzer", Kind = ItemKind.Tool, Weight = 1.6f, MeleeDamage = 24, MineSpeed = 3f, IconBlock = B.Rust, Description = "Sloopt steen en metaal veel sneller." });
            // gewassen: eten en zaden
            string[] cropNames = { "Aardappel", "Graan", "Maïs", "Kool", "Wortel", "Tomaat", "Pompoen" };
            float[] cropFood = { 14, 4, 12, 12, 9, 8, 20 }, cropWater = { 2, 0, 4, 6, 5, 10, 6 };
            for (int i = 0; i < 7; i++)
            {
                Add(new ItemDef { Id = B.CropItems[i], Name = cropNames[i], Kind = ItemKind.Food, MaxStack = 20, Weight = 0.25f, Food = cropFood[i], Water = cropWater[i], IconBlock = B.Crops[i], Value = 3 });
                Add(new ItemDef { Id = "zaad_" + B.CropItems[i], Name = "Zaad: " + cropNames[i].ToLowerInvariant(), Kind = ItemKind.Block, MaxStack = 30, Weight = 0.01f, PlaceBlock = B.Seedlings[i], IconBlock = B.Seedlings[i], Value = 2, Description = "Plant op akkergrond, gras of aarde." });
            }
            Add(new ItemDef { Id = "brood", Name = "Brood", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.4f, Food = 38, Water = -4, IconBlock = B.Wheat, Value = 12 });
            Add(new ItemDef { Id = "soep", Name = "Groentesoep", Kind = ItemKind.Food, MaxStack = 4, Weight = 0.6f, Food = 45, Water = 30, IconBlock = B.Pumpkin, Value = 18 });
            Add(new ItemDef { Id = "gebakken_aardappel", Name = "Gepofte aardappel", Kind = ItemKind.Food, MaxStack = 8, Weight = 0.2f, Food = 22, IconBlock = B.Leather, Value = 6 });
            Add(new ItemDef { Id = "doppen", Name = "Doppen", Kind = ItemKind.Misc, MaxStack = 999, Weight = 0.002f, IconBlock = B.Brass, Value = 1, Description = "Het geld van de nieuwe wereld." });
            Add(new ItemDef { Id = "akkergrond", Name = "Akkergrond", Kind = ItemKind.Block, MaxStack = 50, Weight = 1f, PlaceBlock = B.Farmland, IconBlock = B.Farmland, Value = 1 });

            // voertuigonderdelen en vissen
            Add(new ItemDef { Id = "accu", Name = "Auto-accu", Kind = ItemKind.Material, MaxStack = 1, Weight = 14f, IconBlock = B.Polymer, Value = 90, Description = "Nodig om een motor te starten." });
            Add(new ItemDef { Id = "bougies", Name = "Bougies", Kind = ItemKind.Material, MaxStack = 4, Weight = 0.2f, IconBlock = B.Steel, Value = 40 });
            Add(new ItemDef { Id = "band", Name = "Autoband", Kind = ItemKind.Material, MaxStack = 4, Weight = 9f, IconBlock = B.Tire, Value = 30 });
            Add(new ItemDef { Id = "brandstofpomp", Name = "Brandstofpomp", Kind = ItemKind.Material, MaxStack = 1, Weight = 1.5f, IconBlock = B.Metal, Value = 70 });
            Add(new ItemDef { Id = "jerrycan", Name = "Jerrycan brandstof (20 l)", Kind = ItemKind.Material, MaxStack = 1, Weight = 16f, IconBlock = B.CarRed, Value = 60, Description = "Tank een voertuig of generator (E). Je houdt de lege jerrycan." });
            Add(new ItemDef { Id = "jerrycan_leeg", Name = "Lege jerrycan", Kind = ItemKind.Material, MaxStack = 1, Weight = 1.5f, IconBlock = B.Rust, Value = 12, Description = "Vul hem bij een destilleerketel met zelfgestookte biodiesel." });
            Add(new ItemDef { Id = "frituurvet", Name = "Frituurvet", Kind = ItemKind.Material, MaxStack = 10, Weight = 1f, IconBlock = B.Hazmat, Description = "Oud vet uit keukens en snackbars. Grondstof voor biodiesel." });
            Add(new ItemDef { Id = "generator", Name = "Generator", Kind = ItemKind.Block, MaxStack = 1, Weight = 22f, PlaceBlock = B.Generator, IconBlock = B.Generator, Value = 90, Description = "Draait op brandstof en voedt bouwlampen in de buurt (30 m). Lawaaierig: trekt aandacht." });
            Add(new ItemDef { Id = "zonnepaneel", Name = "Zonnepaneel", Kind = ItemKind.Block, MaxStack = 4, Weight = 6f, PlaceBlock = B.SolarPanel, IconBlock = B.SolarPanel, Value = 70, Description = "Laadt overdag op en voedt 's nachts een paar lampen. Stil." });
            Add(new ItemDef { Id = "bouwlamp", Name = "Bouwlamp", Kind = ItemKind.Block, MaxStack = 10, Weight = 1f, PlaceBlock = B.WorkLamp, IconBlock = B.WorkLampOn, Value = 15, Description = "Gaat 's nachts vanzelf aan als er stroom in de buurt is." });
            Add(new ItemDef { Id = "destilleerketel", Name = "Destilleerketel", Kind = ItemKind.Block, MaxStack = 1, Weight = 18f, PlaceBlock = B.Distiller, IconBlock = B.Distiller, Value = 80, Description = "Stook biodiesel uit frituurvet of maïs in een lege jerrycan." });
            Add(new ItemDef { Id = "opslagkist", Name = "Opslagkist", Kind = ItemKind.Block, MaxStack = 5, Weight = 5f, PlaceBlock = B.StorageChest, IconBlock = B.StorageChest, Value = 20, Description = "Bewaar je spullen. De inhoud blijft bewaard." });
            Add(new ItemDef { Id = "bed", Name = "Bed", Kind = ItemKind.Block, MaxStack = 1, Weight = 8f, PlaceBlock = B.Bed, IconBlock = B.Bed, Value = 25, Description = "Slaap de nacht door en begin hier opnieuw als je sterft." });
            Add(new ItemDef { Id = "hengel", Name = "Hengel", Kind = ItemKind.Tool, Weight = 0.8f, IconBlock = B.Wood, Value = 25, Description = "Linkermuis op water om uit te werpen, klik bij een beet." });
            Add(new ItemDef { Id = "aas", Name = "Wormen (aas)", Kind = ItemKind.Material, MaxStack = 20, Weight = 0.01f, IconBlock = B.MutantFlesh, Value = 1, Description = "Vis bijt sneller. Vind ze door aarde te spitten." });
            Add(new ItemDef { Id = "baars", Name = "Baars", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.4f, Food = 12, SickChance = 0.3f, IconBlock = B.Steel, Value = 8 });
            Add(new ItemDef { Id = "karper", Name = "Karper", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.9f, Food = 16, SickChance = 0.3f, IconBlock = B.Khaki, Value = 10 });
            Add(new ItemDef { Id = "snoek", Name = "Snoek", Kind = ItemKind.Food, MaxStack = 6, Weight = 1.6f, Food = 22, SickChance = 0.3f, IconBlock = B.OD, Value = 16 });
            Add(new ItemDef { Id = "gloeivis", Name = "Gloeivis", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.7f, Food = 14, SickChance = 0.6f, IconBlock = B.Glow, Value = 22, Description = "Gemuteerd. Je kunt hem eten… als je durft." });
            Add(new ItemDef { Id = "granaat", Name = "Handgranaat", Kind = ItemKind.Weapon, MaxStack = 5, Weight = 0.4f, IconBlock = B.OD, Value = 45, Description = "Linkermuis om te gooien. Ontploft na 3 seconden." });
            Add(new ItemDef { Id = "oude_schoen", Name = "Oude schoen", Kind = ItemKind.Misc, MaxStack = 3, Weight = 0.5f, IconBlock = B.Leather, Value = 1 });
            Add(new ItemDef { Id = "gebakken_vis", Name = "Gebakken vis", Kind = ItemKind.Food, MaxStack = 6, Weight = 0.4f, Food = 30, Water = 2, IconBlock = B.Leather, Value = 14 });

            Arsenal.Register(d => Add(d));
        }

        /// <summary>Handelswaarde in doppen.</summary>
        public static int ValueOf(ItemDef d)
        {
            if (d.Value > 0) return d.Value;
            switch (d.Kind)
            {
                case ItemKind.Ammo: return d.Id == "308" ? 4 : d.Id == "12g" ? 3 : 2;
                case ItemKind.Weapon: return d.GunDamage > 0 ? (int)(d.GunDamage * 4 + d.MagSize * 3) : (int)(d.MeleeDamage * 2);
                case ItemKind.Attachment: return 60;
                case ItemKind.Clothing: return 15 + d.Capacity * 6 + (int)(d.Armor * 200) + (int)(d.RadProtection * 150) + (int)(d.Warmth * 30);
                case ItemKind.Medical: return (int)(8 + d.Heal * 0.8f) + (d.CuresSickness ? 30 : 0);
                case ItemKind.Food: case ItemKind.Drink: return (int)(2 + (d.Food + d.Water) * 0.25f);
                case ItemKind.Tool: return 40;
                case ItemKind.Block: return 1;
                default: return 4;
            }
        }
    }

    [Serializable]
    public struct Stack
    {
        public string Id;
        public int Count;
        public int Ammo;                 // geladen patronen (vuurwapens)
        public string[] Mods;            // attachments per AttachSlot (vuurwapens)
        public bool Empty => Id == null || Count <= 0;
        public ItemDef Def => Items.Get(Id);
        public static readonly Stack None = new Stack();
        public Stack(string id, int count) { Id = id; Count = count; Ammo = 0; Mods = null; }

        public string Mod(AttachSlot slot) => Mods == null ? null : Mods[(int)slot];

        public void SetMod(AttachSlot slot, string id)
        {
            if (Mods == null) Mods = new string[Arsenal.AttachSlotCount];
            Mods[(int)slot] = id;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Id ?? ""); w.Write(Count); w.Write(Ammo);
            int n = Mods == null ? 0 : Mods.Length;
            w.Write((byte)n);
            for (int i = 0; i < n; i++) w.Write(Mods[i] ?? "");
        }

        public static Stack Read(BinaryReader r)
        {
            var s = new Stack(r.ReadString(), r.ReadInt32()) { Ammo = r.ReadInt32() };
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                string m = r.ReadString();
                if (!string.IsNullOrEmpty(m) && i < Arsenal.AttachSlotCount) s.SetMod((AttachSlot)i, m);
            }
            if (string.IsNullOrEmpty(s.Id) || Items.Get(s.Id) == null) return None;
            return s;
        }
    }

    /// <summary>Rugzak met vakken; de eerste 6 zijn de snelbalk.</summary>
    public sealed class Inventory
    {
        public const int HotbarSize = 6;
        public const int MaxSlots = 48;
        public readonly Stack[] Slots;
        public float MaxWeight = 30f;
        /// <summary>Aantal bruikbare vakken; groeit met rugzak, rig en zakken (zie Equipment).</summary>
        public int Capacity;

        public Inventory(int size = MaxSlots) { Slots = new Stack[size]; Capacity = size; }

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
            int cap = Math.Min(Capacity, Slots.Length);
            for (int i = 0; i < cap && count > 0; i++)
                if (Slots[i].Id == id && Slots[i].Count < def.MaxStack)
                {
                    int take = Math.Min(count, def.MaxStack - Slots[i].Count);
                    Slots[i].Count += take; count -= take;
                }
            for (int i = 0; i < cap && count > 0; i++)
                if (Slots[i].Empty)
                {
                    int take = Math.Min(count, def.MaxStack);
                    Slots[i] = new Stack(id, take); count -= take;
                }
            return count;
        }

        /// <summary>Legt een hele stapel (met wapenstatus) in het eerste vrije vak. Geeft false als er geen plek is.</summary>
        public bool AddStack(Stack s)
        {
            if (s.Empty) return true;
            if (s.Def.MaxStack > 1 && s.Mods == null && s.Ammo == 0) return Add(s.Id, s.Count) == 0;
            int cap = Math.Min(Capacity, Slots.Length);
            for (int i = 0; i < cap; i++) if (Slots[i].Empty) { Slots[i] = s; return true; }
            return false;
        }

        public int FreeSlots { get { int n = 0, cap = Math.Min(Capacity, Slots.Length); for (int i = 0; i < cap; i++) if (Slots[i].Empty) n++; return n; } }

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
            foreach (var s in Slots) s.Write(w);
        }

        public void Read(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var s = Stack.Read(r);
                if (i < Slots.Length) Slots[i] = s;
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
            new Recipe { Result = "gebakken_vlees", Count = 1, Needs = new[] { ("vlees", 1) }, NeedsFire = true },
            new Recipe { Result = "brood", Count = 1, Needs = new[] { ("graan", 3) }, NeedsFire = true },
            new Recipe { Result = "gebakken_vis", Count = 1, Needs = new[] { ("baars", 1) }, NeedsFire = true },
            new Recipe { Result = "gebakken_vis", Count = 2, Needs = new[] { ("karper", 1) }, NeedsFire = true },
            new Recipe { Result = "gebakken_vis", Count = 3, Needs = new[] { ("snoek", 1) }, NeedsFire = true },
            new Recipe { Result = "hengel", Count = 1, Needs = new[] { ("hout", 2), ("stof", 2), ("schroot", 1) } },
            new Recipe { Result = "gebakken_aardappel", Count = 2, Needs = new[] { ("aardappel", 2) }, NeedsFire = true },
            new Recipe { Result = "soep", Count = 1, Needs = new[] { ("kool", 1), ("wortel", 1), ("tomaat", 1), ("water", 1) }, NeedsFire = true },
            new Recipe { Result = "winterjas", Count = 1, Needs = new[] { ("vacht", 3), ("stof", 4) } },
            new Recipe { Result = "schoudertas", Count = 1, Needs = new[] { ("stof", 6), ("vacht", 1) } },
            new Recipe { Result = "kampvuur", Count = 1, Needs = new[] { ("hout", 4), ("steen", 3) } },
            new Recipe { Result = "bijl", Count = 1, Needs = new[] { ("hout", 2), ("schroot", 3) } },
            new Recipe { Result = "breekijzer", Count = 1, Needs = new[] { ("schroot", 5) } },
            new Recipe { Result = "mes", Count = 1, Needs = new[] { ("schroot", 2), ("hout", 1), ("stof", 1) } },
            new Recipe { Result = "machete", Count = 1, Needs = new[] { ("schroot", 4), ("hout", 1), ("stof", 1) } },
            new Recipe { Result = "boog", Count = 1, Needs = new[] { ("hout", 4), ("stof", 3) } },
            new Recipe { Result = "pijl", Count = 6, Needs = new[] { ("hout", 1), ("schroot", 1), ("stof", 1) } },
            new Recipe { Result = "opslagkist", Count = 1, Needs = new[] { ("hout", 6) } },
            new Recipe { Result = "bed", Count = 1, Needs = new[] { ("hout", 4), ("stof", 4) } },
            new Recipe { Result = "bouwlamp", Count = 2, Needs = new[] { ("schroot", 1), ("batterij", 1) } },
            new Recipe { Result = "zonnepaneel", Count = 1, Needs = new[] { ("schroot", 3), ("batterij", 2), ("rubber", 1) } },
            new Recipe { Result = "generator", Count = 1, Needs = new[] { ("schroot", 8), ("bougies", 1), ("accu", 1) } },
            new Recipe { Result = "destilleerketel", Count = 1, Needs = new[] { ("schroot", 6), ("rubber", 2) } },
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
            { ("bonen", 1, 2, 3), ("chips", 1, 2, 2), ("water", 1, 1, 3), ("frisdrank", 1, 2, 2), ("stof", 1, 3, 3), ("verband", 1, 1, 1.5f), ("batterij", 1, 2, 1.5f),
              ("hout", 2, 5, 1), ("9mm", 4, 12, 0.6f), ("hengel", 1, 1, 0.2f), ("aas", 2, 6, 0.3f), ("pijp", 1, 1, 0.4f), ("bijl", 1, 1, 0.3f), ("mes", 1, 1, 0.35f), ("pistool", 1, 1, 0.15f), ("shotgun", 1, 1, 0.08f), ("12g", 3, 8, 0.4f),
              ("hoodie", 1, 1, 0.6f), ("jeans", 1, 1, 0.4f), ("joggingbroek", 1, 1, 0.4f), ("tshirt", 1, 1, 0.5f), ("sneakers", 1, 1, 0.5f), ("schoenen", 1, 1, 0.3f),
              ("muts", 1, 1, 0.4f), ("pet", 1, 1, 0.3f), ("winterjas", 1, 1, 0.2f), ("schoudertas", 1, 1, 0.3f), ("rugzak", 1, 1, 0.15f), ("bandana", 1, 1, 0.3f) };
        static readonly (string id, int min, int max, float w)[] Winkel =
            { ("bonen", 1, 3, 4), ("chips", 1, 3, 4), ("water", 1, 2, 4), ("frisdrank", 1, 3, 3), ("batterij", 1, 3, 2), ("stof", 1, 2, 1),
              ("cargobroek", 1, 1, 0.6f), ("wandelschoenen", 1, 1, 0.5f), ("sneakers", 1, 1, 0.6f), ("rugzak", 1, 1, 0.5f), ("hoodie", 1, 1, 0.6f), ("winterjas", 1, 1, 0.4f),
              ("jas", 1, 1, 0.4f), ("schoudertas", 1, 1, 0.5f), ("frituurvet", 1, 3, 1f), ("bouwlamp", 1, 2, 0.3f), ("boog", 1, 1, 0.25f), ("pijl", 4, 12, 0.6f), ("mes", 1, 1, 0.3f), ("machete", 1, 1, 0.2f) };
        static readonly (string id, int min, int max, float w)[] Apotheek =
            { ("verband", 1, 3, 4), ("medkit", 1, 1, 1.5f), ("antibiotica", 1, 2, 2), ("water", 1, 1, 1), ("stof", 1, 3, 1), ("gasmasker", 1, 1, 0.2f) };
        static readonly (string id, int min, int max, float w)[] Politie =
            { ("9mm", 8, 24, 4), ("556", 10, 30, 2), ("12g", 4, 10, 2), ("pistool", 1, 1, 1.4f), ("mp5", 1, 1, 0.7f), ("m4", 1, 1, 0.35f), ("shotgun", 1, 1, 0.8f),
              ("kruit", 2, 6, 1.5f), ("verband", 1, 2, 1.5f), ("breekijzer", 1, 1, 0.6f), ("politievest", 1, 1, 0.6f), ("helm", 1, 1, 0.3f),
              ("demper_9mm", 1, 1, 0.35f), ("reddot", 1, 1, 0.5f), ("granaat", 1, 2, 0.4f), ("holo", 1, 1, 0.35f), ("wapenlamp", 1, 1, 0.5f), ("laser", 1, 1, 0.4f),
              ("grip_vert", 1, 1, 0.4f), ("sling", 1, 1, 0.5f), ("mag_pistool", 1, 1, 0.4f), ("legerkistjes", 1, 1, 0.3f), ("chestrig", 1, 1, 0.3f) };
        static readonly (string id, int min, int max, float w)[] Industrie =
            { ("schroot", 2, 6, 4), ("kruit", 1, 4, 2), ("rubber", 1, 3, 2), ("batterij", 1, 2, 1.5f), ("breekijzer", 1, 1, 0.6f), ("bijl", 1, 1, 0.5f), ("machete", 1, 1, 0.3f), ("water", 1, 1, 1),
              ("bouwhelm", 1, 1, 0.5f), ("hazmatpak", 1, 1, 0.25f), ("gasmasker", 1, 1, 0.3f), ("wandelschoenen", 1, 1, 0.4f), ("cargobroek", 1, 1, 0.4f),
              ("accu", 1, 1, 0.5f), ("bougies", 1, 2, 0.8f), ("band", 1, 2, 0.6f), ("brandstofpomp", 1, 1, 0.4f), ("jerrycan", 1, 1, 0.7f), ("jerrycan_leeg", 1, 1, 0.8f), ("hengel", 1, 1, 0.3f), ("zonnepaneel", 1, 1, 0.15f), ("bouwlamp", 1, 2, 0.5f) };
        public static readonly (string id, int min, int max, float w)[] Militair =
            { ("556", 20, 60, 4), ("762", 20, 60, 3), ("308", 5, 15, 1.5f), ("9mm", 15, 40, 2), ("m4", 1, 1, 1f), ("ak", 1, 1, 1f), ("mp5", 1, 1, 0.6f), ("geweer", 1, 1, 0.5f),
              ("platecarrier", 1, 1, 0.6f), ("helm", 1, 1, 0.8f), ("chestrig", 1, 1, 1f), ("legerrugzak", 1, 1, 0.6f), ("legerjas", 1, 1, 1f), ("legerbroek", 1, 1, 1f),
              ("legerkistjes", 1, 1, 1f), ("gasmasker", 1, 1, 0.6f), ("demper_geweer", 1, 1, 0.4f), ("compensator", 1, 1, 0.6f), ("scope4x", 1, 1, 0.5f),
              ("scope8x", 1, 1, 0.2f), ("holo", 1, 1, 0.6f), ("grip_hoek", 1, 1, 0.5f), ("grip_vert", 1, 1, 0.5f), ("mag_groot", 1, 1, 0.5f), ("medkit", 1, 1, 1f), ("granaat", 1, 3, 1.2f), ("mes", 1, 1, 0.8f), ("kruisboog", 1, 1, 0.25f), ("pijl", 6, 18, 0.5f) };

        static readonly (string id, int min, int max, float w)[] Koelkast =
            { ("frisdrank", 1, 3, 4), ("water", 1, 2, 3), ("bonen", 1, 2, 2), ("chips", 1, 2, 1), ("tomaat", 1, 3, 1), ("kool", 1, 1, 0.5f), ("vlees", 1, 1, 0.4f), ("antibiotica", 1, 1, 0.2f), ("frituurvet", 1, 2, 0.8f) };
        static readonly (string id, int min, int max, float w)[] Afval =
            { ("stof", 1, 3, 4), ("schroot", 1, 2, 3), ("batterij", 1, 1, 1.5f), ("chips", 1, 1, 1), ("rubber", 1, 2, 1), ("oude_schoen", 1, 1, 1), ("9mm", 2, 6, 0.4f), ("doppen", 3, 15, 2), ("aas", 1, 4, 0.5f) };

        /// <summary>Deterministische inhoud van een container op deze plek.</summary>
        public static List<Stack> Roll(LotType? type, byte container, int x, int y, int z, int seed, bool military = false)
        {
            var table = military ? Militair : container == B.Barrel ? Industrie : container == B.Fridge ? Koelkast : container == B.Bin ? Afval : type switch
            {
                LotType.Winkel => Winkel,
                LotType.Apotheek => Apotheek,
                LotType.Politie => Politie,
                LotType.Benzinestation => Industrie,
                LotType.Ruine => Industrie,
                _ => Huis,
            };
            var rng = new Random((int)(Hash.H3(x, y, z, seed ^ 0x100f) * int.MaxValue));
            int rolls = 1 + rng.Next(container == B.Shelf ? 2 : 3) + (military ? 2 : 0);
            return RollTable(table, rolls, rng);
        }

        public static List<Stack> RollTable((string id, int min, int max, float w)[] table, int rolls, Random rng)
        {
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
                    var def = Items.Get(t.id);
                    if (def.MaxStack == 1)
                    {
                        for (int k = 0; k < n; k++) result.Add(MakeItem(t.id, rng));
                        break;
                    }
                    int existing = result.FindIndex(s => s.Id == t.id);
                    if (existing >= 0) { var s = result[existing]; s.Count += n; result[existing] = s; }
                    else result.Add(new Stack(t.id, n));
                    break;
                }
            }
            return result;
        }

        /// <summary>Een los voorwerp; wapens komen met een deels gevuld magazijn en soms een attachment.</summary>
        public static Stack MakeItem(string id, Random rng)
        {
            var s = new Stack(id, 1);
            var d = s.Def;
            if (d.Kind == ItemKind.Weapon && d.GunDamage > 0)
            {
                s.Ammo = rng.Next(0, d.MagSize + 1);
                if (rng.NextDouble() < 0.3)
                {
                    var options = new List<ItemDef>();
                    foreach (var a in Items.All.Values) if (Arsenal.Fits(d, a)) options.Add(a);
                    if (options.Count > 0) { var a = options[rng.Next(options.Count)]; s.SetMod(a.AttachSlot, a.Id); }
                }
            }
            return s;
        }
    }

    /// <summary>De lichaamstoestand van de overlever.</summary>
    public sealed class Survival
    {
        static readonly Random rng = new Random();
        public float Health = 100, Food = 85, Water = 85, Stamina = 100, Warmth = 100, Radiation;
        public bool Bleeding, Sick;
        public bool Dead => Health <= 0;

        // snelheden per echte seconde
        public const float FoodDrain = 100f / (26 * 60);
        public const float WaterDrain = 100f / (18 * 60);

        /// <summary>
        /// ambient: omgevingstemperatuur 0 (ijskoud) .. 1 (warm). exertion: 0 rust, 1 lopen, 2 sprinten.
        /// clothing: warmte van kleding 0..1. radDose: straling per seconde na bescherming.
        /// </summary>
        public void Tick(float dt, float ambient, float exertion, bool inWater, bool nearFire, float clothing = 0, float radDose = 0)
        {
            if (Dead) return;
            Food = Math.Max(0, Food - FoodDrain * dt * (1 + exertion * 0.35f));
            Water = Math.Max(0, Water - WaterDrain * dt * (1 + exertion * 0.5f));

            float target = nearFire ? 1f : Math.Clamp(ambient + clothing * 0.45f - (inWater ? 0.45f : 0), 0, 1);
            if (radDose > 0) Radiation = Math.Min(100, Radiation + radDose * dt);
            else Radiation = Math.Max(0, Radiation - 0.05f * dt);
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
            if (Radiation > 40) dmg += (Radiation - 40) * 0.02f;
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
                    if (d.SickChance > 0 && rng.NextDouble() < d.SickChance) { Sick = true; return $"Je eet: {d.Name}. Je maag protesteert…"; }
                    return d.Kind == ItemKind.Food ? $"Je eet: {d.Name}." : $"Je drinkt: {d.Name}.";
                case ItemKind.Medical:
                    Health = Math.Min(100, Health + d.Heal);
                    if (d.StopsBleeding) Bleeding = false;
                    if (d.CuresSickness) Sick = false;
                    if (d.Id == "jodium") Radiation = Math.Max(0, Radiation - 45);
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
            w.Write(Health); w.Write(Food); w.Write(Water); w.Write(Stamina); w.Write(Warmth); w.Write(Bleeding); w.Write(Sick); w.Write(Radiation);
        }

        public void Read(BinaryReader r)
        {
            Health = r.ReadSingle(); Food = r.ReadSingle(); Water = r.ReadSingle(); Stamina = r.ReadSingle();
            Warmth = r.ReadSingle(); Bleeding = r.ReadBoolean(); Sick = r.ReadBoolean(); Radiation = r.ReadSingle();
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
