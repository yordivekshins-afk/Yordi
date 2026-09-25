using System.Collections.Generic;

namespace Deadhaul.Core
{
    /// <summary>
    /// Resultaat van het meshen: posities (meter, lokaal in de chunk), normalen, palet-UV's
    /// en twee submeshes (0 = vast, 1 = glas).
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<float> Positions = new List<float>(1 << 16);
        public readonly List<float> Normals = new List<float>(1 << 16);
        public readonly List<float> Uvs = new List<float>(1 << 15);
        public readonly List<int> Opaque = new List<int>(1 << 15);
        public readonly List<int> Glass = new List<int>(256);
        public int VertexCount => Positions.Count / 3;

        public void Clear()
        {
            Positions.Clear(); Normals.Clear(); Uvs.Clear(); Opaque.Clear(); Glass.Clear();
        }
    }

    /// <summary>
    /// Palet-atlas: kolom = blok × 4 kleurvariaties, rij = AO-niveau (0 = donkerste hoek, 3 = vrij).
    /// Met bilineaire filtering en UV's op kolommiddens geeft dit vloeiende hoekschaduw zonder kleurlekken.
    /// </summary>
    public static class Palette
    {
        public const int Variations = 4;
        public const int Width = 256 * Variations;
        public const int Rows = 4;
        public static readonly float[] AoBase = { 0.62f, 0.76f, 0.88f, 1f };   // op de albedo (subtiel)
        public static readonly float[] AoMask = { 0.35f, 0.58f, 0.8f, 1f };    // op de indirecte belichting

        public static float U(byte id, int variation) => (id * Variations + variation + 0.5f) / Width;
        public static float V(int ao) => (ao + 0.5f) / Rows;

        /// <summary>RGBA-pixels (sRGB) voor de basiskleur-atlas.</summary>
        public static byte[] BaseColorPixels()
        {
            var px = new byte[Width * Rows * 4];
            for (int row = 0; row < Rows; row++)
                for (int id = 0; id < 256; id++)
                    for (int v = 0; v < Variations; v++)
                    {
                        var info = Blocks.Info[id];
                        float jitter = 1f + (v - 1.5f) * 0.045f + (Hash.H2(id, v, 77) - 0.5f) * 0.03f;
                        float k = AoBase[row] * jitter;
                        int i = ((row * Width) + id * Variations + v) * 4;
                        px[i] = Clamp(info.R * k);
                        px[i + 1] = Clamp(info.G * k * (id == B.Grass || id == B.Leaves ? 1f + (v - 1.5f) * 0.03f : 1f));
                        px[i + 2] = Clamp(info.Bl * k);
                        px[i + 3] = 255;
                    }
            return px;
        }

        /// <summary>RGBA-pixels (lineair) voor HDRP's mask map: R metallic, G AO, B detail, A smoothness.</summary>
        public static byte[] MaskPixels()
        {
            var px = new byte[Width * Rows * 4];
            for (int row = 0; row < Rows; row++)
                for (int id = 0; id < 256; id++)
                    for (int v = 0; v < Variations; v++)
                    {
                        var info = Blocks.Info[id];
                        int i = ((row * Width) + id * Variations + v) * 4;
                        px[i] = Clamp(info.Metallic * 255);
                        px[i + 1] = Clamp(AoMask[row] * 255);
                        px[i + 2] = 0;
                        px[i + 3] = Clamp((info.Smoothness + (v - 1.5f) * 0.02f) * 255);
                    }
            return px;
        }

        /// <summary>RGB-emissie (lineair, 0..1) per kolom; alleen lichtgevende blokken zijn niet zwart.</summary>
        public static byte[] EmissionPixels()
        {
            var px = new byte[Width * Rows * 4];
            for (int row = 0; row < Rows; row++)
                for (int id = 0; id < 256; id++)
                    for (int v = 0; v < Variations; v++)
                    {
                        int i = ((row * Width) + id * Variations + v) * 4;
                        px[i + 3] = 255;
                        if (!Blocks.Is((byte)id, BlockFlags.Emissive)) continue;
                        var info = Blocks.Info[id];
                        px[i] = info.R; px[i + 1] = info.G; px[i + 2] = info.Bl;
                    }
            return px;
        }

        static byte Clamp(float f) => (byte)(f < 0 ? 0 : f > 255 ? 255 : f);
    }

    public static class ChunkMesher
    {
        // Per vlak: normaal n, raakvectoren t1 en t2 met cross(t1, t2) = n (Unity: met de klok mee = voorkant),
        // en de hoek van de voxel waar het vlak begint.
        static readonly int[,] N = { { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 } };
        static readonly int[,] T1 = { { 0, 1, 0 }, { 0, 0, 1 }, { 0, 0, 1 }, { 1, 0, 0 }, { 1, 0, 0 }, { 0, 1, 0 } };
        static readonly int[,] T2 = { { 0, 0, 1 }, { 0, 1, 0 }, { 1, 0, 0 }, { 0, 0, 1 }, { 0, 1, 0 }, { 1, 0, 0 } };
        static readonly int[,] O = { { 1, 0, 0 }, { 0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 0 }, { 0, 0, 1 }, { 0, 0, 0 } };
        // hoeken van het vlak in (t1, t2)-eenheden: a, b, c, d
        static readonly int[] C1 = { 0, 1, 1, 0 };
        static readonly int[] C2 = { 0, 0, 1, 1 };

        /// <summary>Mesht een gepadde chunk (Pad × Height × Pad). ox/oz = wereldcoördinaat van de binnenste hoek.</summary>
        public static void Build(byte[] pad, int ox, int oz, MeshData m)
        {
            m.Clear();
            const int H = World.Height, CS = World.ChunkSize;
            float vs = World.VoxelSize;
            var opaque = Blocks.Opaque;
            for (int lz = 0; lz < CS; lz++)
                for (int lx = 0; lx < CS; lx++)
                {
                    int px = lx + 1, pz = lz + 1;
                    int colBase = World.IdxPad(px, 0, pz);
                    for (int y = 0; y < H; y++)
                    {
                        byte b = pad[colBase + y];
                        if (b == B.Air || b == B.Water) continue;
                        int variation = (int)(Hash.H3(ox + lx, y, oz + lz, 9127) * Palette.Variations) & 3;
                        if (B.IsPlant(b))
                        {
                            EmitPlant(m, lx, y, lz, b, variation);
                            continue;
                        }
                        if (b == B.Bed)
                        {
                            EmitBox(m, lx, y, lz, 0.02f, 0f, 0.02f, 0.98f, 0.45f, 0.98f, B.Bed, variation);
                            EmitBox(m, lx, y, lz, 0.1f, 0.45f, 0.62f, 0.9f, 0.58f, 0.95f, B.SneakerSole, variation);
                            continue;
                        }
                        bool glass = b == B.Glass;
                        for (int f = 0; f < 6; f++)
                        {
                            int nx = px + N[f, 0], ny = y + N[f, 1], nz = pz + N[f, 2];
                            byte nb = (uint)ny < H ? pad[World.IdxPad(nx, ny, nz)] : B.Air;
                            if (opaque[nb]) continue;
                            if (nb == b && !opaque[b]) continue;      // glas-glas, blad-blad: niet tekenen
                            if (ny < 0) continue;
                            EmitFace(m, pad, f, px, y, pz, lx, lz, b, variation, glass, vs);
                        }
                    }
                }
        }

        static bool Occ(byte[] pad, int x, int y, int z)
        {
            if ((uint)y >= World.Height || (uint)x >= World.Pad || (uint)z >= World.Pad) return false;
            return Blocks.Opaque[pad[World.IdxPad(x, y, z)]];
        }

        static void EmitFace(MeshData m, byte[] pad, int f, int px, int y, int pz, int lx, int lz, byte b, int variation, bool glass, float vs)
        {
            int nx = N[f, 0], ny = N[f, 1], nz = N[f, 2];
            int ax = px + nx, ay = y + ny, az = pz + nz;     // de laag vóór het vlak
            int start = m.VertexCount;
            float u = Palette.U(b, variation);
            int ao0 = 3, ao1 = 3, ao2 = 3, ao3 = 3;
            for (int k = 0; k < 4; k++)
            {
                int c1 = C1[k], c2 = C2[k];
                float vx = lx + O[f, 0] + T1[f, 0] * c1 + T2[f, 0] * c2;
                float vy = y + O[f, 1] + T1[f, 1] * c1 + T2[f, 1] * c2;
                float vz = lz + O[f, 2] + T1[f, 2] * c1 + T2[f, 2] * c2;
                m.Positions.Add(vx * vs); m.Positions.Add(vy * vs); m.Positions.Add(vz * vs);
                m.Normals.Add(nx); m.Normals.Add(ny); m.Normals.Add(nz);

                int ao = 3;
                if (!glass)
                {
                    int s1x = c1 == 1 ? 1 : -1, s2x = c2 == 1 ? 1 : -1;
                    int d1x = T1[f, 0] * s1x, d1y = T1[f, 1] * s1x, d1z = T1[f, 2] * s1x;
                    int d2x = T2[f, 0] * s2x, d2y = T2[f, 1] * s2x, d2z = T2[f, 2] * s2x;
                    bool s1 = Occ(pad, ax + d1x, ay + d1y, az + d1z);
                    bool s2 = Occ(pad, ax + d2x, ay + d2y, az + d2z);
                    bool cc = Occ(pad, ax + d1x + d2x, ay + d1y + d2y, az + d1z + d2z);
                    ao = s1 && s2 ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (cc ? 1 : 0));
                }
                m.Uvs.Add(u); m.Uvs.Add(Palette.V(ao));
                switch (k) { case 0: ao0 = ao; break; case 1: ao1 = ao; break; case 2: ao2 = ao; break; default: ao3 = ao; break; }
            }
            var list = glass ? m.Glass : m.Opaque;
            if (ao0 + ao2 >= ao1 + ao3)
            {
                list.Add(start); list.Add(start + 1); list.Add(start + 2);
                list.Add(start); list.Add(start + 2); list.Add(start + 3);
            }
            else
            {
                list.Add(start); list.Add(start + 1); list.Add(start + 3);
                list.Add(start + 1); list.Add(start + 2); list.Add(start + 3);
            }
        }

        /// <summary>Een doosje binnen een voxel (fracties 0..1, hoogte mag boven 1 uitkomen).</summary>
        static void EmitBox(MeshData m, int lx, int y, int lz, float fx0, float fy0, float fz0, float fx1, float fy1, float fz1, byte color, int variation)
        {
            float vs = World.VoxelSize;
            float x0 = (lx + fx0) * vs, x1 = (lx + fx1) * vs, y0 = (y + fy0) * vs, y1 = (y + fy1) * vs, z0 = (lz + fz0) * vs, z1 = (lz + fz1) * vs;
            float u = Palette.U(color, variation), v = Palette.V(3);
            for (int f = 0; f < 6; f++)
            {
                if (f == 3 && fy0 <= 0.001f) continue;
                int start = m.VertexCount;
                for (int k = 0; k < 4; k++)
                {
                    int c1 = C1[k], c2 = C2[k];
                    float px = O[f, 0] + T1[f, 0] * c1 + T2[f, 0] * c2;
                    float py = O[f, 1] + T1[f, 1] * c1 + T2[f, 1] * c2;
                    float pz = O[f, 2] + T1[f, 2] * c1 + T2[f, 2] * c2;
                    m.Positions.Add(px > 0.5f ? x1 : x0); m.Positions.Add(py > 0.5f ? y1 : y0); m.Positions.Add(pz > 0.5f ? z1 : z0);
                    m.Normals.Add(N[f, 0]); m.Normals.Add(N[f, 1]); m.Normals.Add(N[f, 2]);
                    m.Uvs.Add(u); m.Uvs.Add(v);
                }
                m.Opaque.Add(start); m.Opaque.Add(start + 1); m.Opaque.Add(start + 2);
                m.Opaque.Add(start); m.Opaque.Add(start + 2); m.Opaque.Add(start + 3);
            }
        }

        /// <summary>Planten en gewassen, elk met een eigen silhouet.</summary>
        static void EmitPlant(MeshData m, int lx, int y, int lz, byte b, int v)
        {
            float j = (v - 1.5f) * 0.04f;   // kleine variatie per plant
            switch (b)
            {
                case B.Potato:
                    EmitBox(m, lx, y, lz, 0.15f, 0, 0.15f, 0.85f, 0.38f + j, 0.85f, B.Potato, v); break;
                case B.Wheat:
                    for (int k = 0; k < 3; k++) { float o = 0.15f + k * 0.28f; EmitBox(m, lx, y, lz, o, 0, 0.2f + k * 0.05f, o + 0.12f, 0.9f + j + k * 0.04f, 0.8f, B.Wheat, v); }
                    break;
                case B.Corn:
                    EmitBox(m, lx, y, lz, 0.42f, 0, 0.42f, 0.58f, 1.7f + j, 0.58f, B.Corn, v);
                    EmitBox(m, lx, y, lz, 0.2f, 0.6f, 0.45f, 0.8f, 0.7f, 0.55f, B.Corn, v);
                    EmitBox(m, lx, y, lz, 0.55f, 0.85f, 0.4f, 0.72f, 1.15f, 0.6f, B.Wheat, v);
                    break;
                case B.Cabbage:
                    EmitBox(m, lx, y, lz, 0.18f, 0, 0.18f, 0.82f, 0.5f + j, 0.82f, B.Cabbage, v);
                    EmitBox(m, lx, y, lz, 0.1f, 0, 0.3f, 0.9f, 0.25f, 0.7f, B.Potato, v);
                    break;
                case B.Carrot:
                    EmitBox(m, lx, y, lz, 0.38f, 0, 0.38f, 0.62f, 0.12f, 0.62f, B.Pumpkin, v);
                    EmitBox(m, lx, y, lz, 0.3f, 0.12f, 0.3f, 0.7f, 0.5f + j, 0.7f, B.Carrot, v);
                    break;
                case B.Tomato:
                    EmitBox(m, lx, y, lz, 0.46f, 0, 0.46f, 0.54f, 1.1f, 0.54f, B.Wood, v);          // stok
                    EmitBox(m, lx, y, lz, 0.25f, 0.2f, 0.25f, 0.75f, 0.95f + j, 0.75f, B.Tomato, v);
                    EmitBox(m, lx, y, lz, 0.2f, 0.45f, 0.62f, 0.36f, 0.6f, 0.78f, B.CarRed, v);     // tomaten
                    EmitBox(m, lx, y, lz, 0.6f, 0.62f, 0.2f, 0.76f, 0.77f, 0.36f, B.CarRed, v);
                    break;
                case B.Pumpkin:
                    EmitBox(m, lx, y, lz, 0.05f, 0, 0.3f, 0.95f, 0.35f, 0.7f, B.Potato, v);
                    EmitBox(m, lx, y, lz, 0.2f, 0, 0.2f, 0.8f, 0.55f + j, 0.8f, B.Pumpkin, v);
                    break;
                case B.Crop:
                    EmitBox(m, lx, y, lz, 0.28f, 0, 0.28f, 0.72f, 0.55f, 0.72f, B.Crop, v); break;
                default: // zaailingen
                    EmitBox(m, lx, y, lz, 0.4f, 0, 0.4f, 0.6f, 0.22f + j, 0.6f, b, v);
                    EmitBox(m, lx, y, lz, 0.28f, 0.14f, 0.45f, 0.72f, 0.2f, 0.55f, b, v);
                    break;
            }
        }

        /// <summary>
        /// Mesht een los voxelmodel (personage, wapen) met dezelfde palet-atlas.
        /// grid[x + sx*(y + sy*z)] = blok-ID of 0; schaal = grootte van één voxel in meter; pivot in voxels.
        /// </summary>
        public static void BuildModel(byte[] grid, int sx, int sy, int sz, float scale, float pivotX, float pivotY, float pivotZ, MeshData m)
        {
            m.Clear();
            for (int z = 0; z < sz; z++)
                for (int y = 0; y < sy; y++)
                    for (int x = 0; x < sx; x++)
                    {
                        byte b = grid[x + sx * (y + sy * z)];
                        if (b == 0) continue;
                        int variation = (int)(Hash.H3(x, y, z, 311) * Palette.Variations) & 3;
                        for (int f = 0; f < 6; f++)
                        {
                            int nx = x + N[f, 0], ny = y + N[f, 1], nz = z + N[f, 2];
                            bool inside = (uint)nx < sx && (uint)ny < sy && (uint)nz < sz;
                            if (inside && grid[nx + sx * (ny + sy * nz)] != 0) continue;
                            int start = m.VertexCount;
                            float u = Palette.U(b, variation);
                            for (int k = 0; k < 4; k++)
                            {
                                int c1 = C1[k], c2 = C2[k];
                                float vx = x + O[f, 0] + T1[f, 0] * c1 + T2[f, 0] * c2 - pivotX;
                                float vy = y + O[f, 1] + T1[f, 1] * c1 + T2[f, 1] * c2 - pivotY;
                                float vz = z + O[f, 2] + T1[f, 2] * c1 + T2[f, 2] * c2 - pivotZ;
                                m.Positions.Add(vx * scale); m.Positions.Add(vy * scale); m.Positions.Add(vz * scale);
                                m.Normals.Add(N[f, 0]); m.Normals.Add(N[f, 1]); m.Normals.Add(N[f, 2]);
                                m.Uvs.Add(u); m.Uvs.Add(Palette.V(3));
                            }
                            var list = b == B.Glass ? m.Glass : m.Opaque;
                            list.Add(start); list.Add(start + 1); list.Add(start + 2);
                            list.Add(start); list.Add(start + 2); list.Add(start + 3);
                        }
                    }
        }
    }
}
