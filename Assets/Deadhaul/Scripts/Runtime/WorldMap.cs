using System.Collections.Generic;
using System.Threading.Tasks;
using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>
    /// Kaart van de omgeving, op de achtergrond berekend uit de wereldgenerator (reliëf, water, wegen,
    /// gebouwen, akkers, straling), met markeringen voor steden, nederzettingen en kraters.
    /// </summary>
    public sealed class WorldMap
    {
        public const int Size = 384;
        public const float MetersPerPixel = 4f;

        public struct Marker { public Vector2 Pos; public string Name; public int Kind; }   // 0 stad, 1 nederzetting, 2 krater

        public Texture2D Texture { get; private set; }
        public Vector2 Center { get; private set; }            // wereldcoördinaat (meter) van het midden
        public readonly List<Marker> Markers = new List<Marker>();
        public bool Building { get; private set; }
        Task<Color32[]> job;
        Vector2 jobCenter;
        List<Marker> jobMarkers;

        /// <summary>Start een nieuwe kaart als de speler ver genoeg is verplaatst.</summary>
        public void Request(WorldGen gen, Vector2 playerMeters)
        {
            if (Building) return;
            if (Texture != null && (playerMeters - Center).magnitude < Size * MetersPerPixel * 0.2f) return;
            Building = true;
            jobCenter = playerMeters;
            var c = jobCenter;
            job = Task.Run(() => Render(gen, c, out jobMarkers));
        }

        /// <summary>Hoofdthread: als de achtergrondtaak klaar is, de texture maken.</summary>
        public void Poll()
        {
            if (!Building || job == null || !job.IsCompleted) return;
            Building = false;
            if (job.IsFaulted) { Debug.LogException(job.Exception); return; }
            if (Texture == null) Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Texture.SetPixels32(job.Result);
            Texture.Apply(false);
            Center = jobCenter;
            Markers.Clear();
            Markers.AddRange(jobMarkers);
        }

        static Color32[] Render(WorldGen gen, Vector2 center, out List<Marker> markers)
        {
            var px = new Color32[Size * Size];
            var hgt = new float[Size * Size];
            float step = MetersPerPixel / World.VoxelSize;
            int x0 = (int)(center.x / World.VoxelSize - Size / 2f * step), z0 = (int)(center.y / World.VoxelSize - Size / 2f * step);
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    int x = x0 + (int)(i * step), z = z0 + (int)(j * step);
                    var c = gen.GetColumn(x, z);
                    hgt[i + j * Size] = c.H;
                    byte blk;
                    switch (c.Kind)
                    {
                        case ColumnKind.Highway: case ColumnKind.Bridge: case ColumnKind.Street: blk = B.Asphalt; break;
                        case ColumnKind.Sidewalk: blk = B.Sidewalk; break;
                        case ColumnKind.Lot:
                        {
                            var lot = gen.LotAtVoxel(x, z);
                            blk = B.Grass;
                            if (lot != null)
                            {
                                if (x >= lot.Bx && z >= lot.Bz && x < lot.Bx + lot.W && z < lot.Bz + lot.D && lot.Floors > 0) blk = lot.Type == LotType.Huis ? B.Roof : lot.Wall;
                                else if (lot.Type == LotType.Parkeerplaats || lot.Type == LotType.Benzinestation || lot.Type == LotType.Militair) blk = B.Concrete;
                                else if (lot.Type == LotType.Ruine) blk = B.Rubble;
                            }
                            break;
                        }
                        case ColumnKind.Settlement:
                        {
                            var st = gen.SettlementNear(x, z, out _);
                            blk = B.Grass;
                            if (st != null)
                                for (int ly = 12; ly >= 0; ly--)
                                {
                                    int bb = st.BlockAt(x - st.X0, ly, z - st.Z0, gen.Seed);
                                    if (bb > 0) { blk = B.IsSeedling((byte)bb) ? B.Farmland : (byte)bb; break; }
                                }
                            break;
                        }
                        default:
                        {
                            float m = gen.Moisture(x, z);
                            if (c.H <= World.Sea) blk = B.Water;
                            else
                            {
                                var bm = gen.BiomeAt(x, z, c.H, m);
                                blk = bm switch
                                {
                                    WorldGen.Biome.Woestijn => B.Sand,
                                    WorldGen.Biome.Moeras => B.Mud,
                                    WorldGen.Biome.Hoogland => B.Snow,
                                    WorldGen.Biome.Dor => B.DeadGrass,
                                    WorldGen.Biome.Bos => B.Leaves,
                                    _ => B.Grass,
                                };
                                if (c.H <= World.Sea + 1) blk = B.Sand;
                            }
                            break;
                        }
                    }
                    if (c.Rad > 0.12f && blk != B.Water && (c.Kind == ColumnKind.Nature || c.Kind == ColumnKind.Lot)) blk = c.Rad > 0.55f ? B.Scorched : B.Ash;
                    var info = Blocks.Info[blk];
                    Color32 col = new Color32(info.R, info.G, info.Bl, 255);
                    if (blk == B.Water)
                    {
                        float depth = Mathf.Clamp01((World.Sea - c.H) / 10f);
                        col = c.Rad > 0.12f ? new Color32(70, 110, 60, 255) : new Color32((byte)(58 - depth * 28), (byte)(100 - depth * 38), (byte)(120 - depth * 28), 255);
                    }
                    px[i + j * Size] = col;
                }
            // reliëfschaduw
            var shaded = new Color32[Size * Size];
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float hx = hgt[Mathf.Min(Size - 1, i + 1) + j * Size] - hgt[Mathf.Max(0, i - 1) + j * Size];
                    float hz = hgt[i + Mathf.Min(Size - 1, j + 1) * Size] - hgt[i + Mathf.Max(0, j - 1) * Size];
                    float k = Mathf.Clamp(1f + (-hx - hz) * 0.05f, 0.6f, 1.25f);
                    var c = px[i + j * Size];
                    shaded[i + j * Size] = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);
                }
            // markeringen
            markers = new List<Marker>();
            float half = Size * MetersPerPixel * 0.5f;
            float vx0 = center.x - half, vz0 = center.y - half;
            int ci0 = World.FloorDiv((int)(vx0 / World.VoxelSize), WorldGen.CityCell) - 1, ci1 = World.FloorDiv((int)((vx0 + 2 * half) / World.VoxelSize), WorldGen.CityCell) + 1;
            int cj0 = World.FloorDiv((int)(vz0 / World.VoxelSize), WorldGen.CityCell) - 1, cj1 = World.FloorDiv((int)((vz0 + 2 * half) / World.VoxelSize), WorldGen.CityCell) + 1;
            for (int j = cj0; j <= cj1; j++)
                for (int i = ci0; i <= ci1; i++)
                {
                    var city = gen.GetCity(i, j);
                    if (city != null) markers.Add(new Marker { Pos = new Vector2(city.X, city.Z) * World.VoxelSize, Name = city.Name, Kind = 0 });
                    var st = gen.GetSettlement(i, j);
                    if (st != null) markers.Add(new Marker { Pos = new Vector2(st.X0 + Settlement.W / 2f, st.Z0 + Settlement.D / 2f) * World.VoxelSize, Name = st.Name, Kind = 1 });
                }
            int ki0 = World.FloorDiv((int)(vx0 / World.VoxelSize), WorldGen.CraterCell) - 1, ki1 = World.FloorDiv((int)((vx0 + 2 * half) / World.VoxelSize), WorldGen.CraterCell) + 1;
            int kj0 = World.FloorDiv((int)(vz0 / World.VoxelSize), WorldGen.CraterCell) - 1, kj1 = World.FloorDiv((int)((vz0 + 2 * half) / World.VoxelSize), WorldGen.CraterCell) + 1;
            for (int j = kj0; j <= kj1; j++)
                for (int i = ki0; i <= ki1; i++)
                {
                    var cr = gen.GetCrater(i, j);
                    if (cr != null) markers.Add(new Marker { Pos = new Vector2(cr.X, cr.Z) * World.VoxelSize, Name = "Inslagkrater", Kind = 2 });
                }
            return shaded;
        }

        /// <summary>Wereldpositie (meter) naar pixel op de kaart (0..Size, y omlaag voor IMGUI).</summary>
        public Vector2 ToPixel(Vector2 world)
        {
            float half = Size * MetersPerPixel * 0.5f;
            return new Vector2((world.x - (Center.x - half)) / MetersPerPixel, Size - (world.y - (Center.y - half)) / MetersPerPixel);
        }
    }
}
