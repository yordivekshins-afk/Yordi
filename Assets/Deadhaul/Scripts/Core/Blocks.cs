namespace Deadhaul.Core
{
    /// <summary>Wereldconstanten. Eén voxel = 0,5 m.</summary>
    public static class World
    {
        public const float VoxelSize = 0.5f;
        public const int ChunkSize = 32;              // voxels (16 m)
        public const int Height = 112;                // voxels (56 m)
        public const int Sea = 22;                    // zeeniveau in voxels
        public const int Pad = ChunkSize + 2;         // chunk + rand van 1 voxel voor het meshen
        public const int ChunkVolume = ChunkSize * ChunkSize * Height;
        public const int PadVolume = Pad * Pad * Height;

        /// <summary>Hoogte van het wateroppervlak in meter.</summary>
        public const float SeaLevelMeters = (Sea + 1) * VoxelSize - 0.08f;

        public static int IdxLocal(int lx, int y, int lz) => y + Height * (lx + ChunkSize * lz);
        public static int IdxPad(int px, int y, int pz) => y + Height * (px + Pad * pz);

        public static int FloorDiv(int a, int b) { int q = a / b; return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q; }
        public static int Mod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }
    }

    /// <summary>Bloktypes (byte-ID's).</summary>
    public static class B
    {
        public const byte Air = 0, Grass = 1, Dirt = 2, Stone = 3, Sand = 4, Water = 5, Log = 6, Leaves = 7,
            Asphalt = 8, RoadLine = 9, Concrete = 10, Brick = 11, Glass = 12, Metal = 13, Planks = 14,
            Rubble = 15, Sidewalk = 16, CarRed = 17, CarBlue = 18, Tire = 19, Campfire = 20, Crate = 21,
            DeadGrass = 22, Roof = 23, Gravel = 24, Plaster = 25, Lamp = 26, Rust = 27, Moss = 28,
            Bedrock = 29, CarGrey = 30, CarWhite = 31, DeadLeaves = 32, Tile = 33, Carpet = 34,
            Shelf = 35, StoneWall = 36, WoodWall = 37, MetalWall = 38, Barrel = 39, Fence = 40, Crop = 41,
            Ash = 42, RadCrystal = 43, Sandbag = 44, AmmoCrate = 45, Scorched = 46;
        public const int Count = 47;

        // Kleuren voor personages en voorwerpen (komen niet in de wereld voor)
        public const byte Skin = 200, SkinDark = 201, Hair = 202, Jacket = 203, JacketDark = 204, Jeans = 205,
            Boot = 206, Backpack = 207, Bandana = 208, Gunmetal = 209, Wood = 210, Eye = 211, RaiderCoat = 212,
            RaiderPants = 213, Strap = 214, Blade = 215,
            Polymer = 216, Steel = 217, FDE = 218, OD = 219, Brass = 220, Lens = 221, Rubber = 222, Camo = 223, CamoDark = 224,
            Hazmat = 225, HazmatDark = 226, Sneaker = 227, SneakerSole = 228, Leather = 229, Khaki = 230, Helmet = 231, Plate = 232,
            Spark = 233, Flash = 234, Tracer = 235, Blood = 236, Dust = 237, MutantSkin = 238, MutantFlesh = 239, Glow = 240,
            Bone = 241, Fur = 242, FurDark = 243;
    }

    [System.Flags]
    public enum BlockFlags : byte
    {
        None = 0,
        Emissive = 1,
        Foliage = 2,
        Container = 4,
        Unbreakable = 8,
    }

    public sealed class BlockInfo
    {
        public byte Id;
        public string Name;
        public byte R, G, Bl;
        public bool Solid;
        public float Hardness;      // seconden hakken met blote handen
        public string Drop;         // item-ID, of null
        public int DropCount;
        public BlockFlags Flags;
        public float Smoothness;    // voor het HDRP-materiaal (0 = mat, 1 = spiegelend)
        public float Metallic;
    }

    public static class Blocks
    {
        public static readonly BlockInfo[] Info = new BlockInfo[256];
        public static readonly bool[] Solid = new bool[256];
        public static readonly bool[] Opaque = new bool[256];

        static void Def(byte id, string name, int r, int g, int b, bool solid, float hard, string drop,
            BlockFlags flags = BlockFlags.None, int dropN = 1, float smooth = 0.1f, float metal = 0f)
        {
            Info[id] = new BlockInfo
            {
                Id = id, Name = name, R = (byte)r, G = (byte)g, Bl = (byte)b, Solid = solid, Hardness = hard,
                Drop = drop, DropCount = dropN, Flags = flags, Smoothness = smooth, Metallic = metal
            };
        }

        static Blocks()
        {
            Def(B.Air, "lucht", 0, 0, 0, false, 0, null);
            Def(B.Grass, "gras", 92, 116, 58, true, 0.6f, "aarde");
            Def(B.Dirt, "aarde", 104, 80, 58, true, 0.6f, "aarde");
            Def(B.Stone, "steen", 118, 116, 112, true, 1.8f, "steen", smooth: 0.15f);
            Def(B.Sand, "zand", 196, 178, 130, true, 0.5f, "zand");
            Def(B.Water, "water", 40, 80, 96, false, 0, null);
            Def(B.Log, "boomstam", 92, 66, 44, true, 1.2f, "hout", dropN: 2);
            Def(B.Leaves, "bladeren", 70, 98, 46, true, 0.2f, null, BlockFlags.Foliage, smooth: 0.25f);
            Def(B.Asphalt, "asfalt", 52, 53, 56, true, 2.0f, "steen", smooth: 0.2f);
            Def(B.RoadLine, "wegmarkering", 200, 188, 150, true, 2.0f, "steen", smooth: 0.25f);
            Def(B.Concrete, "beton", 150, 148, 142, true, 2.4f, "steen");
            Def(B.Brick, "baksteen", 138, 72, 54, true, 2.1f, "steen");
            Def(B.Glass, "glas", 150, 180, 190, true, 0.2f, null, smooth: 0.95f);
            Def(B.Metal, "metaal", 110, 114, 120, true, 2.8f, "schroot", smooth: 0.45f, metal: 0.9f);
            Def(B.Planks, "planken", 150, 112, 72, true, 0.9f, "hout");
            Def(B.Rubble, "puin", 112, 106, 98, true, 0.9f, "steen");
            Def(B.Sidewalk, "stoeptegel", 128, 126, 120, true, 2.0f, "steen");
            Def(B.CarRed, "autowrak", 128, 44, 36, true, 2.8f, "schroot", dropN: 2, smooth: 0.6f, metal: 0.6f);
            Def(B.CarBlue, "autowrak", 48, 70, 104, true, 2.8f, "schroot", dropN: 2, smooth: 0.6f, metal: 0.6f);
            Def(B.Tire, "band", 28, 28, 30, true, 1.2f, "rubber");
            Def(B.Campfire, "kampvuur", 255, 150, 60, true, 0.6f, "hout", BlockFlags.Emissive);
            Def(B.Crate, "kist", 120, 90, 52, true, 0.9f, null, BlockFlags.Container);
            Def(B.DeadGrass, "dor gras", 132, 122, 74, true, 0.6f, "aarde");
            Def(B.Roof, "dakpan", 70, 60, 58, true, 1.5f, "steen", smooth: 0.2f);
            Def(B.Gravel, "grind", 120, 112, 104, true, 0.9f, "steen");
            Def(B.Plaster, "pleister", 182, 172, 150, true, 1.5f, "steen");
            Def(B.Lamp, "lamp", 255, 214, 150, true, 0.6f, "schroot", BlockFlags.Emissive);
            Def(B.Rust, "roest", 124, 70, 40, true, 2.4f, "schroot", metal: 0.3f);
            Def(B.Moss, "mos", 70, 90, 50, true, 0.6f, "aarde");
            Def(B.Bedrock, "rotsbodem", 40, 40, 44, true, 999, null, BlockFlags.Unbreakable);
            Def(B.CarGrey, "autowrak", 96, 98, 102, true, 2.8f, "schroot", dropN: 2, smooth: 0.6f, metal: 0.6f);
            Def(B.CarWhite, "autowrak", 190, 190, 184, true, 2.8f, "schroot", dropN: 2, smooth: 0.6f, metal: 0.6f);
            Def(B.DeadLeaves, "dode bladeren", 118, 92, 52, true, 0.2f, null, BlockFlags.Foliage);
            Def(B.Tile, "tegelvloer", 170, 168, 160, true, 1.5f, "steen", smooth: 0.5f);
            Def(B.Carpet, "tapijt", 96, 52, 48, true, 0.6f, "stof");
            Def(B.Shelf, "rek", 90, 92, 96, true, 0.9f, null, BlockFlags.Container, metal: 0.5f);
            Def(B.StoneWall, "stenen muur", 124, 122, 116, true, 3.0f, "steen");
            Def(B.WoodWall, "houten muur", 138, 100, 62, true, 1.8f, "hout");
            Def(B.MetalWall, "metalen plaat", 96, 104, 110, true, 4.2f, "schroot", metal: 0.8f, smooth: 0.4f);
            Def(B.Barrel, "ton", 60, 90, 70, true, 1.8f, null, BlockFlags.Container, metal: 0.5f);
            Def(B.Fence, "hek", 80, 82, 86, true, 1.5f, "schroot", metal: 0.7f);
            Def(B.Crop, "wilde groente", 110, 150, 60, false, 0.1f, "groente", BlockFlags.Foliage);
            Def(B.Ash, "as", 70, 68, 66, true, 0.5f, "aarde");
            Def(B.RadCrystal, "stralingskristal", 150, 255, 90, true, 2.5f, "jodium", BlockFlags.Emissive, smooth: 0.8f);
            Def(B.Sandbag, "zandzakken", 150, 136, 98, true, 1.4f, "zand");
            Def(B.AmmoCrate, "munitiekist", 72, 84, 56, true, 1.2f, null, BlockFlags.Container, metal: 0.3f);
            Def(B.Scorched, "verschroeide grond", 46, 40, 36, true, 0.6f, "aarde");
            Def(B.Skin, "huid", 206, 160, 124, true, 1, null);
            Def(B.SkinDark, "huid", 142, 98, 70, true, 1, null);
            Def(B.Hair, "haar", 48, 36, 28, true, 1, null);
            Def(B.Jacket, "jas", 74, 86, 62, true, 1, null);
            Def(B.JacketDark, "jas", 52, 60, 44, true, 1, null);
            Def(B.Jeans, "broek", 58, 66, 86, true, 1, null);
            Def(B.Boot, "laars", 44, 34, 28, true, 1, null);
            Def(B.Backpack, "rugzak", 110, 92, 60, true, 1, null);
            Def(B.Bandana, "bandana", 150, 30, 26, true, 1, null);
            Def(B.Gunmetal, "wapenstaal", 40, 42, 46, true, 1, null, smooth: 0.55f, metal: 0.9f);
            Def(B.Wood, "hout", 116, 80, 48, true, 1, null);
            Def(B.Eye, "oog", 20, 20, 22, true, 1, null, smooth: 0.8f);
            Def(B.RaiderCoat, "raiderjas", 46, 40, 38, true, 1, null);
            Def(B.RaiderPants, "raiderbroek", 70, 60, 48, true, 1, null);
            Def(B.Strap, "riem", 60, 44, 30, true, 1, null);
            Def(B.Blade, "staal", 170, 176, 184, true, 1, null, smooth: 0.7f, metal: 1f);
            Def(B.Polymer, "polymeer", 30, 31, 33, true, 1, null, smooth: 0.3f);
            Def(B.Steel, "staal", 62, 64, 68, true, 1, null, smooth: 0.55f, metal: 0.95f);
            Def(B.FDE, "zandkleur", 164, 136, 96, true, 1, null, smooth: 0.25f);
            Def(B.OD, "legergroen", 82, 90, 60, true, 1, null);
            Def(B.Brass, "messing", 196, 156, 70, true, 1, null, smooth: 0.7f, metal: 1f);
            Def(B.Lens, "lens", 40, 70, 110, true, 1, null, smooth: 0.98f);
            Def(B.Rubber, "rubber", 22, 22, 24, true, 1, null);
            Def(B.Camo, "camouflage", 98, 100, 72, true, 1, null);
            Def(B.CamoDark, "camouflage", 58, 62, 42, true, 1, null);
            Def(B.Hazmat, "hazmat", 206, 176, 40, true, 1, null, smooth: 0.45f);
            Def(B.HazmatDark, "hazmat", 88, 78, 30, true, 1, null);
            Def(B.Sneaker, "sneaker", 214, 214, 208, true, 1, null);
            Def(B.SneakerSole, "zool", 236, 236, 232, true, 1, null);
            Def(B.Leather, "leer", 88, 58, 38, true, 1, null, smooth: 0.35f);
            Def(B.Khaki, "kaki", 152, 138, 102, true, 1, null);
            Def(B.Helmet, "helm", 74, 82, 60, true, 1, null, smooth: 0.2f);
            Def(B.Plate, "plaat", 70, 72, 66, true, 1, null);
            Def(B.Spark, "vonk", 255, 196, 110, true, 1, null, BlockFlags.Emissive);
            Def(B.Flash, "mondingsvuur", 255, 226, 160, true, 1, null, BlockFlags.Emissive);
            Def(B.Tracer, "lichtspoor", 255, 160, 70, true, 1, null, BlockFlags.Emissive);
            Def(B.Blood, "bloed", 104, 8, 8, true, 1, null, smooth: 0.6f);
            Def(B.Dust, "stof", 150, 140, 122, true, 1, null);
            Def(B.MutantSkin, "mutantenhuid", 118, 132, 96, true, 1, null, smooth: 0.4f);
            Def(B.MutantFlesh, "rauw vlees", 146, 58, 58, true, 1, null, smooth: 0.5f);
            Def(B.Glow, "straling", 150, 255, 90, true, 1, null, BlockFlags.Emissive);
            Def(B.Bone, "bot", 220, 212, 186, true, 1, null);
            Def(B.Fur, "vacht", 96, 80, 62, true, 1, null);
            Def(B.FurDark, "vacht", 56, 48, 40, true, 1, null);
            for (int i = 0; i < 256; i++)
            {
                if (Info[i] == null) Def((byte)i, "?", 255, 0, 255, true, 1, null);
                Solid[i] = Info[i].Solid;
                Opaque[i] = Info[i].Solid;
            }
            Opaque[B.Glass] = false; Opaque[B.Fence] = false;
            Opaque[B.Leaves] = false; Opaque[B.DeadLeaves] = false;
        }

        public static bool Is(byte id, BlockFlags f) => (Info[id].Flags & f) != 0;
    }
}
