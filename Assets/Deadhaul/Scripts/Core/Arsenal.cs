using System;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    [Flags]
    public enum WeaponClass : byte { None = 0, Pistool = 1, SMG = 2, Geweer = 4, Shotgun = 8, Sniper = 16, Boog = 32 }

    public enum AttachSlot : byte { Loop, Optiek, Onderloop, Zijkant, Riem, Magazijn }

    [Flags]
    public enum AttachMask : byte { None = 0, Loop = 1, Optiek = 2, Onderloop = 4, Zijkant = 8, Riem = 16, Magazijn = 32, Alles = 63 }

    public enum EquipSlot : sbyte { None = -1, Hoofd, Gezicht, Torso, Vest, Rug, Benen, Voeten }

    /// <summary>Effectieve waarden van een wapen mét attachments.</summary>
    public struct WeaponStats
    {
        public float Damage, Interval, Spread, RecoilV, RecoilH, Velocity, Noise, Zoom, AdsTime, ReloadTime, Penetration;
        public int MagSize, Pellets;
        public bool Automatic, Flashlight, Laser, HidesFlash, Arrow;
    }

    /// <summary>
    /// Alle vuurwapens, attachments, munitie en kleding. Wapenmodellen zijn opgebouwd uit blokken
    /// (1 eenheid = 1,5 cm) naar de vorm van echte wapens.
    /// </summary>
    public static class Arsenal
    {
        public const int AttachSlotCount = 6;
        public const float ModelUnit = 0.015f;

        public static void Register(Action<ItemDef> add)
        {
            // ------------------------------------------------ munitie (penetratie in Ammo-tabel hieronder)
            add(new ItemDef { Id = "9mm", Name = "9×19 mm", Kind = ItemKind.Ammo, MaxStack = 90, Weight = 0.012f, IconBlock = B.Brass });
            add(new ItemDef { Id = "556", Name = "5,56×45 mm", Kind = ItemKind.Ammo, MaxStack = 90, Weight = 0.012f, IconBlock = B.Brass });
            add(new ItemDef { Id = "762", Name = "7,62×39 mm", Kind = ItemKind.Ammo, MaxStack = 90, Weight = 0.016f, IconBlock = B.Brass });
            add(new ItemDef { Id = "308", Name = ".308 Winchester", Kind = ItemKind.Ammo, MaxStack = 40, Weight = 0.025f, IconBlock = B.Brass });
            add(new ItemDef { Id = "12g", Name = "12-gauge hagel", Kind = ItemKind.Ammo, MaxStack = 40, Weight = 0.04f, IconBlock = B.CarRed });
            add(new ItemDef { Id = "pijl", Name = "Pijl", Kind = ItemKind.Ammo, MaxStack = 30, Weight = 0.03f, IconBlock = B.Wood, Description = "Raapbaar: pijlen blijven steken waar ze landen." });

            // ------------------------------------------------ stille wapens
            add(new ItemDef { Id = "boog", Name = "Recurveboog", Kind = ItemKind.Weapon, Class = WeaponClass.Boog, Weight = 1.1f, AmmoId = "pijl", MagSize = 1,
                GunDamage = 78, FireInterval = 0.3f, Spread = 0.9f, RecoilV = 0.3f, RecoilH = 0.1f, MuzzleVelocity = 64, ReloadTime = 0.6f, Noise = 5,
                Mounts = AttachMask.Optiek, IconBlock = B.Wood, MeleeDamage = 8, Description = "Houd de linkermuisknop vast om te spannen, laat los om te schieten. Vrijwel geluidloos." });
            add(new ItemDef { Id = "kruisboog", Name = "Kruisboog", Kind = ItemKind.Weapon, Class = WeaponClass.Boog, Weight = 3.2f, AmmoId = "pijl", MagSize = 1,
                GunDamage = 115, FireInterval = 0.3f, Spread = 0.7f, RecoilV = 0.8f, RecoilH = 0.15f, MuzzleVelocity = 95, ReloadTime = 2.6f, Noise = 12,
                Mounts = AttachMask.Optiek | AttachMask.Zijkant | AttachMask.Riem, IconBlock = B.Polymer, MeleeDamage = 14, Description = "Zwaar en traag te laden, maar een pijl gaat dwars door een helm." });

            // ------------------------------------------------ vuurwapens
            add(new ItemDef { Id = "pistool", Name = "G19-pistool", Kind = ItemKind.Weapon, Class = WeaponClass.Pistool, Weight = 0.9f, AmmoId = "9mm", MagSize = 17,
                GunDamage = 32, FireInterval = 0.13f, Spread = 1.6f, RecoilV = 1.4f, RecoilH = 0.5f, MuzzleVelocity = 360, ReloadTime = 1.8f, Noise = 150,
                Mounts = AttachMask.Loop | AttachMask.Optiek | AttachMask.Zijkant | AttachMask.Magazijn, IconBlock = B.Polymer, MeleeDamage = 12 });
            add(new ItemDef { Id = "mp5", Name = "MP5", Kind = ItemKind.Weapon, Class = WeaponClass.SMG, Weight = 2.6f, AmmoId = "9mm", MagSize = 30, Automatic = true,
                GunDamage = 27, FireInterval = 0.075f, Spread = 2.1f, RecoilV = 0.55f, RecoilH = 0.28f, MuzzleVelocity = 400, ReloadTime = 2.4f, Noise = 160,
                Mounts = AttachMask.Alles, IconBlock = B.Steel, MeleeDamage = 15 });
            add(new ItemDef { Id = "ak", Name = "AKM", Kind = ItemKind.Weapon, Class = WeaponClass.Geweer, Weight = 3.3f, AmmoId = "762", MagSize = 30, Automatic = true,
                GunDamage = 46, FireInterval = 0.1f, Spread = 2.3f, RecoilV = 1.15f, RecoilH = 0.5f, MuzzleVelocity = 715, ReloadTime = 2.6f, Noise = 260,
                Mounts = AttachMask.Loop | AttachMask.Optiek | AttachMask.Onderloop | AttachMask.Riem | AttachMask.Magazijn, IconBlock = B.Wood, MeleeDamage = 18 });
            add(new ItemDef { Id = "m4", Name = "M4A1", Kind = ItemKind.Weapon, Class = WeaponClass.Geweer, Weight = 3.1f, AmmoId = "556", MagSize = 30, Automatic = true,
                GunDamage = 38, FireInterval = 0.08f, Spread = 1.9f, RecoilV = 0.72f, RecoilH = 0.32f, MuzzleVelocity = 880, ReloadTime = 2.3f, Noise = 250,
                Mounts = AttachMask.Alles, IconBlock = B.Polymer, MeleeDamage = 18 });
            add(new ItemDef { Id = "shotgun", Name = "Pompgeweer", Kind = ItemKind.Weapon, Class = WeaponClass.Shotgun, Weight = 3.4f, AmmoId = "12g", MagSize = 6,
                GunDamage = 15, Pellets = 9, FireInterval = 0.85f, Spread = 4.5f, RecoilV = 3.2f, RecoilH = 0.9f, MuzzleVelocity = 400, ReloadTime = 3.6f, Noise = 280,
                Mounts = AttachMask.Optiek | AttachMask.Zijkant | AttachMask.Riem, IconBlock = B.Steel, MeleeDamage = 20 });
            add(new ItemDef { Id = "geweer", Name = "Jachtgeweer .308", Kind = ItemKind.Weapon, Class = WeaponClass.Sniper, Weight = 4.1f, AmmoId = "308", MagSize = 5,
                GunDamage = 96, FireInterval = 1.25f, Spread = 3.5f, RecoilV = 3.6f, RecoilH = 0.6f, MuzzleVelocity = 820, ReloadTime = 3.0f, Noise = 340,
                Mounts = AttachMask.Loop | AttachMask.Optiek | AttachMask.Riem, IconBlock = B.Wood, MeleeDamage = 20 });

            // ------------------------------------------------ attachments
            var pistolLike = WeaponClass.Pistool | WeaponClass.SMG;
            var rifles = WeaponClass.Geweer | WeaponClass.Sniper;
            add(Att("demper_9mm", "9mm-demper", AttachSlot.Loop, pistolLike, 0.35f, a => { a.NoiseMul = 0.22f; a.HidesFlash = true; a.RecoilMul = 0.9f; a.VelocityMul = 0.93f; }));
            add(Att("demper_geweer", "Geweerdemper", AttachSlot.Loop, rifles, 0.6f, a => { a.NoiseMul = 0.32f; a.HidesFlash = true; a.RecoilMul = 0.85f; a.AdsTimeMul = 1.08f; }));
            add(Att("compensator", "Compensator", AttachSlot.Loop, pistolLike | rifles, 0.15f, a => { a.RecoilMul = 0.72f; a.NoiseMul = 1.15f; }));
            add(Att("reddot", "Red dot-vizier", AttachSlot.Optiek, (WeaponClass)0xFF, 0.12f, a => { a.Zoom = 1.35f; a.SpreadMul = 0.95f; }));
            add(Att("holo", "Holografisch vizier", AttachSlot.Optiek, (WeaponClass)0xFF & ~WeaponClass.Pistool, 0.3f, a => { a.Zoom = 1.5f; a.SpreadMul = 0.92f; }));
            add(Att("scope4x", "4× richtkijker", AttachSlot.Optiek, rifles | WeaponClass.SMG, 0.45f, a => { a.Zoom = 4f; a.AdsTimeMul = 1.3f; }));
            add(Att("scope8x", "8× richtkijker", AttachSlot.Optiek, rifles, 0.65f, a => { a.Zoom = 8f; a.AdsTimeMul = 1.5f; }));
            add(Att("grip_vert", "Verticale greep", AttachSlot.Onderloop, WeaponClass.SMG | WeaponClass.Geweer, 0.12f, a => { a.RecoilMul = 0.8f; }));
            add(Att("grip_hoek", "Hoekgreep", AttachSlot.Onderloop, WeaponClass.SMG | WeaponClass.Geweer, 0.1f, a => { a.AdsTimeMul = 0.8f; a.RecoilMul = 0.92f; }));
            add(Att("laser", "Laser", AttachSlot.Zijkant, (WeaponClass)0xFF, 0.08f, a => { a.SpreadMul = 0.65f; a.Laser = true; }));
            add(Att("wapenlamp", "Wapenlamp", AttachSlot.Zijkant, (WeaponClass)0xFF, 0.1f, a => { a.Flashlight = true; }));
            add(Att("sling", "Draagriem", AttachSlot.Riem, (WeaponClass)0xFF, 0.1f, a => { a.AdsTimeMul = 0.9f; }));
            add(Att("mag_groot", "Vergroot magazijn", AttachSlot.Magazijn, WeaponClass.SMG | WeaponClass.Geweer, 0.25f, a => { a.ExtraMag = 15; a.AdsTimeMul = 1.05f; }));
            add(Att("mag_pistool", "Verlengd pistoolmagazijn", AttachSlot.Magazijn, WeaponClass.Pistool, 0.08f, a => { a.ExtraMag = 8; }));

            // ------------------------------------------------ kleding en uitrusting
            // voeten
            add(Wear("sneakers", "Sneakers", EquipSlot.Voeten, B.Sneaker, B.SneakerSole, w: 0.1f, kg: 0.6f));
            add(Wear("schoenen", "Leren schoenen", EquipSlot.Voeten, B.Leather, B.Boot, w: 0.12f, kg: 0.8f));
            add(Wear("wandelschoenen", "Wandelschoenen", EquipSlot.Voeten, B.Khaki, B.Boot, w: 0.22f, kg: 1f));
            add(Wear("legerkistjes", "Legerkistjes", EquipSlot.Voeten, B.Boot, B.Rubber, w: 0.28f, armor: 0.1f, kg: 1.4f));
            // benen
            add(Wear("jeans", "Jeans", EquipSlot.Benen, B.Jeans, B.Jeans, cap: 2, w: 0.15f, kg: 0.7f));
            add(Wear("joggingbroek", "Joggingbroek", EquipSlot.Benen, B.Steel, B.Polymer, cap: 2, w: 0.12f, kg: 0.4f));
            add(Wear("cargobroek", "Cargobroek", EquipSlot.Benen, B.Khaki, B.Leather, cap: 4, w: 0.18f, kg: 0.8f));
            add(Wear("legerbroek", "Legerbroek (camo)", EquipSlot.Benen, B.Camo, B.CamoDark, cap: 4, w: 0.22f, armor: 0.08f, kg: 0.9f, style: 1));
            // romp
            add(Wear("tshirt", "T-shirt", EquipSlot.Torso, B.Bandana, B.Bandana, w: 0.05f, kg: 0.2f));
            add(Wear("hoodie", "Hoodie", EquipSlot.Torso, B.Steel, B.Polymer, cap: 2, w: 0.35f, kg: 0.6f));
            add(Wear("jas", "Werkjas", EquipSlot.Torso, B.Jacket, B.JacketDark, cap: 2, w: 0.5f, kg: 1.2f));
            add(Wear("legerjas", "Legerjas (camo)", EquipSlot.Torso, B.Camo, B.CamoDark, cap: 4, w: 0.5f, armor: 0.08f, kg: 1.4f, style: 1));
            add(Wear("winterjas", "Winterjas", EquipSlot.Torso, B.Khaki, B.Leather, cap: 2, w: 0.9f, kg: 1.8f, style: 2));
            add(Wear("hazmatpak", "Hazmatpak", EquipSlot.Torso, B.Hazmat, B.HazmatDark, w: 0.3f, rad: 0.7f, kg: 2.2f, style: 3));
            // vest
            add(Wear("chestrig", "Chest rig", EquipSlot.Vest, B.OD, B.CamoDark, cap: 6, kg: 1.1f));
            add(Wear("politievest", "Kogelwerend vest", EquipSlot.Vest, B.JacketDark, B.Polymer, cap: 2, armor: 0.3f, kg: 3.5f, style: 1));
            add(Wear("platecarrier", "Plate carrier", EquipSlot.Vest, B.Plate, B.OD, cap: 5, armor: 0.48f, kg: 7f, style: 2));
            // rug
            add(Wear("schoudertas", "Schoudertas", EquipSlot.Rug, B.Leather, B.Strap, cap: 6, kg: 0.6f));
            add(Wear("rugzak", "Rugzak", EquipSlot.Rug, B.Backpack, B.Strap, cap: 12, carry: 5, kg: 1.2f, style: 1));
            add(Wear("legerrugzak", "Legerrugzak", EquipSlot.Rug, B.Camo, B.CamoDark, cap: 20, carry: 12, kg: 2.4f, style: 2));
            // hoofd en gezicht
            add(Wear("muts", "Muts", EquipSlot.Hoofd, B.Khaki, B.Khaki, w: 0.15f, kg: 0.1f));
            add(Wear("pet", "Pet", EquipSlot.Hoofd, B.OD, B.OD, kg: 0.1f, style: 1));
            add(Wear("bouwhelm", "Bouwhelm", EquipSlot.Hoofd, B.Hazmat, B.Hazmat, armor: 0.25f, kg: 0.4f, style: 2));
            add(Wear("helm", "Gevechtshelm", EquipSlot.Hoofd, B.Helmet, B.OD, armor: 0.5f, kg: 1.4f, style: 3));
            add(Wear("bandana", "Bandana", EquipSlot.Gezicht, B.Bandana, B.Bandana, kg: 0.05f));
            add(Wear("gasmasker", "Gasmasker", EquipSlot.Gezicht, B.Rubber, B.Lens, rad: 0.5f, kg: 0.7f, style: 1));
        }

        static ItemDef Att(string id, string name, AttachSlot slot, WeaponClass fits, float kg, Action<ItemDef> f)
        {
            var d = new ItemDef { Id = id, Name = name, Kind = ItemKind.Attachment, AttachSlot = slot, FitsClasses = fits, Weight = kg, IconBlock = B.Polymer };
            f(d);
            return d;
        }

        static ItemDef Wear(string id, string name, EquipSlot slot, byte c1, byte c2, int cap = 0, float w = 0, float armor = 0, float rad = 0, float carry = 0, float kg = 0.5f, int style = 0) =>
            new ItemDef { Id = id, Name = name, Kind = ItemKind.Clothing, Equip = slot, Color1 = c1, Color2 = c2, Capacity = cap, Warmth = w, Armor = armor,
                RadProtection = rad, CarryBonus = carry, Weight = kg, IconBlock = c1, Style = style };

        public static AttachMask MaskOf(AttachSlot s) => (AttachMask)(1 << (int)s);

        /// <summary>Kan dit attachment op dit wapen?</summary>
        public static bool Fits(ItemDef weapon, ItemDef att) =>
            weapon != null && att != null && att.Kind == ItemKind.Attachment && (weapon.Mounts & MaskOf(att.AttachSlot)) != 0 && (att.FitsClasses & weapon.Class) != 0;

        /// <summary>Hoe diep een kogel van dit kaliber door materiaal gaat (zie Ballistics).</summary>
        public static float Penetration(string ammo) => ammo switch
        {
            "9mm" => 0.45f,
            "556" => 0.95f,
            "762" => 1.25f,
            "308" => 1.7f,
            "12g" => 0.3f,
            "pijl" => 0.18f,
            _ => 0.4f,
        };

        public static WeaponStats Stats(in Stack s)
        {
            var d = s.Def;
            var st = new WeaponStats
            {
                Damage = d.GunDamage, Interval = d.FireInterval, Spread = d.Spread, RecoilV = d.RecoilV, RecoilH = d.RecoilH,
                Velocity = d.MuzzleVelocity, Noise = d.Noise, Zoom = 1.15f, AdsTime = d.Class == WeaponClass.Pistool ? 0.16f : d.Class == WeaponClass.Sniper ? 0.35f : 0.24f,
                ReloadTime = d.ReloadTime, MagSize = d.MagSize, Pellets = d.Pellets, Automatic = d.Automatic, Penetration = Penetration(d.AmmoId), Arrow = d.Class == WeaponClass.Boog
            };
            if (s.Mods == null) return st;
            foreach (var id in s.Mods)
            {
                var a = Items.Get(id);
                if (a == null) continue;
                st.RecoilV *= a.RecoilMul; st.RecoilH *= a.RecoilMul;
                st.Spread *= a.SpreadMul; st.Noise *= a.NoiseMul; st.AdsTime *= a.AdsTimeMul; st.Velocity *= a.VelocityMul;
                if (a.Zoom > st.Zoom) st.Zoom = a.Zoom;
                st.MagSize += a.ExtraMag;
                st.Flashlight |= a.Flashlight; st.Laser |= a.Laser; st.HidesFlash |= a.HidesFlash;
            }
            return st;
        }

        // ============================================================ modellen
        public sealed class Blueprint
        {
            public readonly List<(int x0, int y0, int z0, int x1, int y1, int z1, byte c)> Boxes = new List<(int, int, int, int, int, int, byte)>();
            public readonly Dictionary<AttachSlot, (float x, float y, float z)> Mounts = new Dictionary<AttachSlot, (float, float, float)>();
            public void Box(int x0, int y0, int z0, int x1, int y1, int z1, byte c) => Boxes.Add((Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1), Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1), c));
            public void Mount(AttachSlot s, float x, float y, float z) => Mounts[s] = (x, y, z);

            /// <summary>Zet de blokken om in een voxelraster. Oorsprong (0,0,0) = handgreep; geeft de verschuiving terug.</summary>
            public byte[] Rasterize(out int sx, out int sy, out int sz, out int ox, out int oy, out int oz)
            {
                int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
                foreach (var b in Boxes)
                {
                    minX = Math.Min(minX, b.x0); minY = Math.Min(minY, b.y0); minZ = Math.Min(minZ, b.z0);
                    maxX = Math.Max(maxX, b.x1); maxY = Math.Max(maxY, b.y1); maxZ = Math.Max(maxZ, b.z1);
                }
                if (Boxes.Count == 0) { minX = minY = minZ = 0; maxX = maxY = maxZ = 0; }
                sx = maxX - minX + 1; sy = maxY - minY + 1; sz = maxZ - minZ + 1;
                ox = -minX; oy = -minY; oz = -minZ;
                var g = new byte[sx * sy * sz];
                foreach (var b in Boxes)
                    for (int z = b.z0; z <= b.z1; z++)
                        for (int y = b.y0; y <= b.y1; y++)
                            for (int x = b.x0; x <= b.x1; x++)
                                g[(x + ox) + sx * ((y + oy) + sy * (z + oz))] = b.c;
                return g;
            }
        }

        /// <summary>Voxelmodel van een wapen of attachment. Loop wijst naar +Z, handgreep op de oorsprong.</summary>
        public static Blueprint Model(string id)
        {
            var m = new Blueprint();
            switch (id)
            {
                case "pistool":
                    m.Box(-1, 1, -3, 1, 3, 11, B.Steel);            // slede
                    m.Box(0, 4, 9, 0, 4, 9, B.Steel);               // korrel
                    m.Box(-1, 4, -3, 1, 4, -3, B.Steel);            // vizier
                    m.Box(-1, -1, -3, 1, 0, 10, B.Polymer);         // frame
                    for (int y = -9; y <= -2; y++) m.Box(-1, y, -4 - (y + 9) / 4, 1, y, -1 - (y + 9) / 4, B.Polymer); // schuine greep
                    m.Box(-1, -10, -5, 1, -10, -2, B.Polymer);      // magazijnvoet
                    m.Box(0, -3, 0, 0, -3, 4, B.Polymer); m.Box(0, -3, 4, 0, -1, 4, B.Polymer); // beugel
                    m.Box(0, -2, 1, 0, -2, 1, B.Steel);             // trekker
                    m.Mount(AttachSlot.Loop, 0, 2, 12); m.Mount(AttachSlot.Optiek, 0, 4, 3); m.Mount(AttachSlot.Zijkant, 0, -2, 7); m.Mount(AttachSlot.Magazijn, 0, -10, -3);
                    break;
                case "mp5":
                    m.Box(-1, 0, -6, 1, 3, 15, B.Steel);            // huls
                    m.Box(-1, -1, 15, 1, 3, 25, B.Polymer);          // voorgreep
                    m.Box(0, 1, 26, 0, 2, 29, B.Steel);             // loop
                    m.Box(-1, 4, -4, 1, 5, -2, B.Steel);            // trommelvizier
                    m.Box(0, 4, 23, 0, 6, 23, B.Steel); m.Box(-1, 4, 22, 1, 4, 24, B.Steel); // korreltunnel
                    for (int y = -8; y <= -1; y++) m.Box(-1, y, -5 - (y + 8) / 4, 1, y, -2 - (y + 8) / 4, B.Polymer);
                    for (int y = -12; y <= -1; y++) { int dz = (-y) / 3; m.Box(-1, y, 3 + dz, 1, y, 5 + dz, B.Steel); } // gebogen magazijn
                    m.Box(0, 2, -22, 0, 2, -6, B.Steel); m.Box(0, -1, -22, 0, -1, -6, B.Steel); // stangen
                    m.Box(-1, -4, -23, 1, 3, -21, B.Polymer);        // kolfplaat
                    m.Mount(AttachSlot.Loop, 0, 1.5f, 30); m.Mount(AttachSlot.Optiek, 0, 4, 4); m.Mount(AttachSlot.Onderloop, 0, -2, 20);
                    m.Mount(AttachSlot.Zijkant, 2, 1, 20); m.Mount(AttachSlot.Riem, 0, 0, -21); m.Mount(AttachSlot.Magazijn, 0, -12, 8);
                    break;
                case "ak":
                    m.Box(-1, 0, -8, 1, 3, 16, B.Steel);
                    m.Box(-1, 4, -6, 1, 4, 14, B.Steel);            // stofkap
                    m.Box(-1, -1, 16, 1, 3, 28, B.Wood);            // houten voorgreep
                    m.Box(0, 4, 16, 0, 4, 30, B.Wood);              // gasbuis
                    m.Box(0, 1, 28, 0, 2, 41, B.Steel);             // loop
                    m.Box(0, 3, 39, 0, 5, 39, B.Steel);             // korrel
                    m.Box(-1, 1, 41, 1, 2, 43, B.Steel);            // mondingsrem
                    for (int y = -8; y <= -1; y++) m.Box(-1, y, -6 - (y + 8) / 3, 1, y, -3 - (y + 8) / 3, B.Wood);
                    for (int y = -14; y <= -1; y++) { int dz = (-y) / 2; m.Box(-1, y, 2 + dz, 1, y, 5 + dz, y < -12 ? B.Polymer : B.Steel); }
                    for (int z = -31; z <= -9; z++) { int lo = -2 - (-9 - z) / 4; m.Box(-1, lo, z, 1, 2, z, B.Wood); } // kolf, wordt naar achter hoger
                    m.Box(-1, -8, -31, 1, 2, -31, B.Rubber);
                    m.Mount(AttachSlot.Loop, 0, 1.5f, 44); m.Mount(AttachSlot.Optiek, 0, 5, 4); m.Mount(AttachSlot.Onderloop, 0, -2, 22);
                    m.Mount(AttachSlot.Riem, 0, -2, -28); m.Mount(AttachSlot.Magazijn, 0, -14, 10);
                    break;
                case "m4":
                    m.Box(-1, 0, -6, 1, 4, 14, B.Polymer);          // upper
                    for (int z = -6; z <= 30; z++) m.Box(-1, 5, z, 1, 5, z, z % 2 == 0 ? B.Steel : B.Polymer); // rail
                    m.Box(-2, -1, 14, 2, 4, 30, B.Polymer);          // handguard
                    m.Box(0, 1, 31, 0, 2, 43, B.Steel);
                    m.Box(-1, 1, 43, 1, 2, 46, B.Steel);            // vlamdemper
                    m.Box(0, 6, 28, 0, 8, 28, B.Steel);             // opklapkorrel
                    m.Box(-1, -2, 1, 1, -1, 7, B.Polymer);          // magazijnschacht
                    for (int y = -8; y <= -1; y++) m.Box(-1, y, -5 - (y + 8) / 3, 1, y, -2 - (y + 8) / 3, B.Polymer);
                    for (int y = -13; y <= -3; y++) { int dz = (-y) / 5; m.Box(-1, y, 2 + dz, 1, y, 5 + dz, B.Steel); }
                    m.Box(0, 1, -18, 0, 3, -6, B.Steel);            // buffer tube
                    m.Box(-1, -4, -26, 1, 4, -16, B.FDE);            // kolf
                    m.Box(-1, -5, -26, 1, 4, -26, B.Rubber);
                    m.Mount(AttachSlot.Loop, 0, 1.5f, 47); m.Mount(AttachSlot.Optiek, 0, 6, 6); m.Mount(AttachSlot.Onderloop, 0, -2, 22);
                    m.Mount(AttachSlot.Zijkant, 3, 2, 24); m.Mount(AttachSlot.Riem, 0, -4, -24); m.Mount(AttachSlot.Magazijn, 0, -13, 6);
                    break;
                case "shotgun":
                    m.Box(-1, 0, -6, 1, 4, 12, B.Steel);
                    m.Box(0, 3, 12, 0, 4, 47, B.Steel);             // loop
                    m.Box(0, 0, 12, 0, 1, 42, B.Steel);             // buismagazijn
                    m.Box(-1, -1, 22, 1, 2, 33, B.Polymer);          // pomp
                    for (int z = -31; z <= -7; z++) { int lo = -3 - (-7 - z) / 4; m.Box(-1, lo, z, 1, 3, z, B.Polymer); }
                    m.Box(-1, -9, -31, 1, 3, -31, B.Rubber);
                    m.Box(0, -3, -2, 0, -1, 3, B.Steel);            // trekkerbeugel
                    m.Mount(AttachSlot.Optiek, 0, 5, 2); m.Mount(AttachSlot.Zijkant, 2, 1, 26); m.Mount(AttachSlot.Riem, 0, -3, -26);
                    break;
                case "geweer":
                    for (int z = -34; z <= 22; z++)
                    {
                        int lo = z < -8 ? -3 - (-8 - z) / 5 : z < 0 ? -7 : -2;
                        m.Box(-1, lo, z, 1, 2, z, B.Wood);          // doorlopende houten kolf
                    }
                    m.Box(-1, 3, -6, 1, 4, 14, B.Steel);            // huls
                    m.Box(0, 3, 14, 0, 4, 55, B.Steel);             // lange loop
                    m.Box(2, 3, 2, 3, 3, 2, B.Steel); m.Box(3, 2, 2, 3, 2, 2, B.Steel); // grendel
                    m.Box(-1, -9, -34, 1, 2, -34, B.Rubber);
                    m.Mount(AttachSlot.Loop, 0, 3.5f, 56); m.Mount(AttachSlot.Optiek, 0, 5, 4); m.Mount(AttachSlot.Riem, 0, -3, -26);
                    break;

                case "boog":
                {
                    // gebogen latten boven en onder de greep, pees achteraan
                    m.Box(-1, -4, -1, 1, 4, 1, B.Leather);                          // greep
                    for (int y = 5; y <= 40; y++)
                    {
                        float t = (y - 5) / 35f;
                        int z = (int)Math.Round(-t * t * 7 + (y > 34 ? (y - 34) * 0.8f : 0));
                        byte c = y > 36 ? B.Polymer : B.Wood;
                        m.Box(0, y, z, 0, y, z + 1, c); m.Box(0, -y, z, 0, -y, z + 1, c);
                    }
                    m.Box(0, -38, -3, 0, 38, -3, B.Cloth);                           // pees
                    m.Box(0, 2, 1, 0, 2, 3, B.Steel);                                 // pijlsteun
                    m.Mount(AttachSlot.Loop, 0, 2, 6); m.Mount(AttachSlot.Optiek, -2, 6, 0);
                    break;
                }
                case "kruisboog":
                    m.Box(-1, 0, -24, 1, 3, 22, B.Polymer);                          // stok
                    m.Box(0, 4, -4, 0, 4, 22, B.Steel);                               // rail
                    for (int y = -8; y <= -1; y++) m.Box(-1, y, -8 - (y + 8) / 3, 1, y, -5 - (y + 8) / 3, B.Polymer);
                    m.Box(-1, -6, -24, 1, 3, -24, B.Rubber);
                    for (int x = 1; x <= 20; x++) { int z = 22 - (x * x) / 40; m.Box(x, 2, z, x, 3, z + 1, B.Steel); m.Box(-x, 2, z, -x, 3, z + 1, B.Steel); }
                    m.Box(-19, 3, 12, 19, 3, 12, B.Cloth);                            // gespannen pees
                    m.Box(-1, 5, 0, 1, 5, 14, B.Wood);                                // pijl op de rail
                    m.Mount(AttachSlot.Loop, 0, 4, 23); m.Mount(AttachSlot.Optiek, 0, 5, -2); m.Mount(AttachSlot.Zijkant, 2, 1, 16); m.Mount(AttachSlot.Riem, 0, -3, -20);
                    break;
                case "pijl":
                    m.Box(0, 0, -30, 0, 0, 2, B.Wood);                                // schacht
                    m.Box(0, 0, 3, 0, 0, 4, B.Steel);                                 // punt
                    m.Box(-1, 0, -29, 1, 0, -25, B.Bandana); m.Box(0, -1, -29, 0, 1, -25, B.Bandana); // veren
                    break;

                // ---------------- attachments (oorsprong = montagepunt)
                case "demper_9mm": m.Box(-1, -1, 0, 1, 1, 11, B.Polymer); break;
                case "demper_geweer": m.Box(-2, -2, 0, 2, 2, 15, B.Polymer); m.Box(-1, -1, 15, 1, 1, 15, B.Steel); break;
                case "compensator": m.Box(-1, -1, 0, 1, 1, 3, B.Steel); break;
                case "reddot":
                    m.Box(-1, 0, -2, 1, 0, 2, B.Polymer); m.Box(-1, 1, -2, -1, 3, 2, B.Polymer); m.Box(1, 1, -2, 1, 3, 2, B.Polymer);
                    m.Box(-1, 4, -2, 1, 4, 2, B.Polymer); m.Box(0, 2, 2, 0, 3, 2, B.Lens);
                    break;
                case "holo":
                    m.Box(-2, 0, -3, 2, 0, 3, B.Polymer); m.Box(-2, 1, 1, 2, 4, 3, B.Polymer); m.Box(-2, 5, -3, 2, 5, 3, B.Polymer);
                    m.Box(-2, 1, -3, -2, 4, 0, B.Polymer); m.Box(2, 1, -3, 2, 4, 0, B.Polymer); m.Box(-1, 1, 1, 1, 4, 1, B.Lens);
                    break;
                case "scope4x":
                case "scope8x":
                {
                    int L = id == "scope8x" ? 12 : 8;
                    m.Box(-1, 0, -3, 1, 0, -2, B.Steel); m.Box(-1, 0, 2, 1, 0, 3, B.Steel);       // ringen
                    m.Box(-1, 1, -L, 1, 3, L, B.Polymer);                                         // buis
                    m.Box(-2, 0, L - 3, 2, 4, L, B.Polymer); m.Box(-2, 0, -L, 2, 4, -L + 2, B.Polymer); // objectief en oculair
                    m.Box(-1, 1, L, 1, 3, L, B.Lens); m.Box(-1, 1, -L, 1, 3, -L, B.Lens);
                    m.Box(0, 4, -1, 0, 5, 1, B.Polymer); m.Box(2, 2, -1, 3, 2, 1, B.Polymer);      // torentjes
                    break;
                }
                case "grip_vert": m.Box(-1, -8, -1, 1, 0, 1, B.Polymer); break;
                case "grip_hoek": for (int y = -4; y <= 0; y++) m.Box(-1, y, -4 - y, 1, y, 2, B.Polymer); break;
                case "laser": m.Box(0, -1, 0, 2, 1, 4, B.Polymer); m.Box(1, 0, 5, 1, 0, 5, B.Bandana); break;
                case "wapenlamp": m.Box(0, -1, 0, 2, 1, 5, B.Polymer); m.Box(0, -1, 6, 2, 1, 6, B.Lens); break;
                case "sling": m.Box(0, -1, -1, 0, 0, 1, B.Strap); break;
                case "mag_groot": for (int y = -8; y <= 0; y++) m.Box(-1, y, -2, 1, y, 1, B.Steel); break;
                case "mag_pistool": m.Box(-1, -3, -2, 1, 0, 1, B.Polymer); break;
            }
            return m;
        }
    }

    /// <summary>Wat je aan hebt. Bepaalt rugzakvakken, draaggewicht, warmte, bescherming en uiterlijk.</summary>
    public sealed class Equipment
    {
        public const int BasePockets = Inventory.HotbarSize;   // zakken = de snelbalk
        public const float BaseCarry = 25f;
        public readonly Stack[] Slots = new Stack[7];

        public ItemDef this[EquipSlot s] => Slots[(int)s].Def;

        public int Capacity
        {
            get { int c = BasePockets; foreach (var s in Slots) if (!s.Empty) c += s.Def.Capacity; return Math.Min(Inventory.MaxSlots, Math.Max(Inventory.HotbarSize, c)); }
        }

        public float CarryWeight { get { float c = BaseCarry; foreach (var s in Slots) if (!s.Empty) c += s.Def.CarryBonus; return c; } }
        public float Warmth { get { float w = 0; foreach (var s in Slots) if (!s.Empty) w += s.Def.Warmth; return Math.Min(1f, w); } }
        public float Radiation { get { float r = 0; foreach (var s in Slots) if (!s.Empty) r += s.Def.RadProtection; return Math.Min(0.98f, r); } }
        public float Weight { get { float w = 0; foreach (var s in Slots) if (!s.Empty) w += s.Def.Weight; return w; } }

        /// <summary>Schadereductie per lichaamsdeel.</summary>
        public float ArmorFor(HitZone z)
        {
            ItemDef a;
            switch (z)
            {
                case HitZone.Hoofd: a = this[EquipSlot.Hoofd]; return a?.Armor ?? 0;
                case HitZone.Romp:
                    float v = this[EquipSlot.Vest]?.Armor ?? 0;
                    return Math.Max(v, this[EquipSlot.Torso]?.Armor ?? 0);
                default:
                    a = this[EquipSlot.Benen]; return a?.Armor ?? 0;
            }
        }

        /// <summary>Trekt een kledingstuk aan en geeft terug wat je eerst droeg (of None).</summary>
        public Stack Wear(Stack item)
        {
            var d = item.Def;
            if (d == null || d.Kind != ItemKind.Clothing) return item;
            var old = Slots[(int)d.Equip];
            Slots[(int)d.Equip] = item;
            return old;
        }

        public Stack TakeOff(EquipSlot s)
        {
            var old = Slots[(int)s];
            Slots[(int)s] = Stack.None;
            return old;
        }

        /// <summary>Werkt de capaciteit en het draaggewicht van de rugzak bij.</summary>
        public void Apply(Inventory inv)
        {
            inv.Capacity = Capacity;
            inv.MaxWeight = CarryWeight;
        }

        public void Write(System.IO.BinaryWriter w) { foreach (var s in Slots) s.Write(w); }
        public void Read(System.IO.BinaryReader r) { for (int i = 0; i < Slots.Length; i++) Slots[i] = Stack.Read(r); }
    }
}
