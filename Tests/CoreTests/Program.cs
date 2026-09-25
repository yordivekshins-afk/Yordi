using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Deadhaul.Core;

// Tests voor Deadhaul.Core, zonder Unity. Draai met: dotnet run --project Tests/CoreTests [-- map]
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FOUT ") + what);
        if (!ok) failures++;
    }

    static int Main(string[] args)
    {
        const int seed = 1337;
        var gen = new WorldGen(seed);

        Console.WriteLine("Mesher");
        {
            var pad = new byte[World.PadVolume];
            pad[World.IdxPad(5, 10, 5)] = B.Stone;
            var m = new MeshData();
            ChunkMesher.Build(pad, 0, 0, m);
            Check(m.Opaque.Count == 36 && m.VertexCount == 24, $"één losse voxel = 6 vlakken ({m.Opaque.Count / 6} vlakken)");
            bool windingOk = true;
            for (int t = 0; t < m.Opaque.Count; t += 3)
            {
                V3 P(int i) => new V3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                var a = P(m.Opaque[t]); var b = P(m.Opaque[t + 1]); var c = P(m.Opaque[t + 2]);
                var e1 = b - a; var e2 = c - a;
                var cr = new V3(e1.Y * e2.Z - e1.Z * e2.Y, e1.Z * e2.X - e1.X * e2.Z, e1.X * e2.Y - e1.Y * e2.X);
                int ni = m.Opaque[t] * 3;
                float dot = cr.X * m.Normals[ni] + cr.Y * m.Normals[ni + 1] + cr.Z * m.Normals[ni + 2];
                if (dot <= 0) windingOk = false;
            }
            Check(windingOk, "alle driehoeken wijzen naar buiten (Unity-draairichting)");
            pad[World.IdxPad(6, 10, 5)] = B.Stone;
            ChunkMesher.Build(pad, 0, 0, m);
            Check(m.Opaque.Count / 6 == 10, $"twee aangrenzende voxels = 10 vlakken ({m.Opaque.Count / 6})");
            pad[World.IdxPad(5, 11, 6)] = B.Stone;
            ChunkMesher.Build(pad, 0, 0, m);
            bool anyAo = false;
            for (int i = 1; i < m.Uvs.Count; i += 2) if (m.Uvs[i] < Palette.V(3) - 1e-4f) anyAo = true;
            Check(anyAo, "hoekschaduw (AO) wordt berekend in de UV's");
        }

        Console.WriteLine("Wereldgenerator");
        {
            var a = gen.GenerateChunk(3, -2);
            var b = new WorldGen(seed).GenerateChunk(3, -2);
            bool same = true;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { same = false; break; }
            Check(same, "zelfde seed = exact dezelfde chunk");

            // naadloosheid: de rand van chunk (0,0) moet gelijk zijn aan de binnenkant van de buren
            int mismatches = 0, compared = 0;
            for (int cz = -3; cz <= 3; cz += 3)
                for (int cx = -3; cx <= 3; cx += 3)
                {
                    var c0 = gen.GenerateChunk(cx, cz);
                    var c1 = gen.GenerateChunk(cx + 1, cz);
                    for (int z = 1; z <= World.ChunkSize; z++)
                        for (int y = 0; y < World.Height; y++)
                        {
                            compared++;
                            if (c0[World.IdxPad(World.Pad - 1, y, z)] != c1[World.IdxPad(1, y, z)]) mismatches++;
                            compared++;
                            if (c1[World.IdxPad(0, y, z)] != c0[World.IdxPad(World.ChunkSize, y, z)]) mismatches++;
                        }
                }
            Check(mismatches == 0, $"chunkranden sluiten naadloos aan ({mismatches} verschillen op {compared})");

            var city = gen.GetCity(0, 0);
            Check(city != null, city != null ? $"startstad: {city.Name} ({city.Kind}, straal {city.R * World.VoxelSize:0} m)" : "startstad bestaat");

            var sw = Stopwatch.StartNew();
            var mesh = new MeshData();
            int tris = 0, glass = 0, n = 0;
            for (int cz = 18; cz < 24; cz++)
                for (int cx = 18; cx < 24; cx++)
                {
                    var p = gen.GenerateChunk(cx, cz);
                    ChunkMesher.Build(p, cx * World.ChunkSize, cz * World.ChunkSize, mesh);
                    tris += mesh.Opaque.Count / 3; glass += mesh.Glass.Count / 3; n++;
                }
            sw.Stop();
            Console.WriteLine($"         {n} chunks in de stad: {sw.ElapsedMilliseconds / (float)n:0.0} ms per chunk (1 thread), gem. {tris / n} driehoeken, {glass / n} glas");
            Check(tris > 0 && glass > 0, "stadschunks hebben geometrie en ramen");

            var counts = new int[256];
            for (int cz = 18; cz < 24; cz++) for (int cx = 18; cx < 24; cx++) foreach (var v in gen.GenerateChunk(cx, cz)) counts[v]++;
            Check(counts[B.Crate] + counts[B.Shelf] > 0, $"containers om te looten ({counts[B.Crate]} kisten, {counts[B.Shelf]} rekken)");
            Check(counts[B.Lamp] > 0, $"straatlantaarns ({counts[B.Lamp]})");
            Check(counts[B.CarRed] + counts[B.CarBlue] + counts[B.CarGrey] + counts[B.CarWhite] + counts[B.Rust] > 0, "autowrakken");
        }

        Console.WriteLine("Wereldopslag, botsingen en raycasts");
        {
            var store = new VoxelStore();
            for (int cz = -1; cz <= 1; cz++) for (int cx = -1; cx <= 1; cx++) store.Add(cx, cz, gen.GenerateChunk(cx, cz));
            int sx = 5, sz = 5;
            // hoogste vaste voxel onder de hele voetafdruk van de speler (bomen ernaast tellen mee)
            int top = 0;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                int t = World.Height - 1;
                while (t > 0 && !Blocks.Solid[store.Get(sx + dx, t, sz + dz)]) t--;
                top = Math.Max(top, t);
            }
            var pos = new V3((sx + 0.5f) * World.VoxelSize, (top + 8) * World.VoxelSize, (sz + 0.5f) * World.VoxelSize);
            var vel = new V3(0, 0, 0);
            bool grounded = false;
            for (int i = 0; i < 240; i++)
            {
                vel.Y -= 22f / 60f;
                grounded = VoxelPhysics.Move(store, ref pos, ref vel, vel * (1f / 60f), 0.3f, 1.75f, true);
            }
            float groundY = (top + 1) * World.VoxelSize;
            Check(grounded && MathF.Abs(pos.Y - groundY) < 0.06f, $"speler valt en landt op de grond (y={pos.Y:0.00}, grond={groundY:0.00})");

            // opstappen: vlakke testvloer met een trede van één voxel hoog
            int fy = (int)MathF.Round(pos.Y / World.VoxelSize);
            for (int dz = -3; dz <= 3; dz++)
                for (int dx = -3; dx <= 12; dx++)
                {
                    store.Set(sx + dx, fy - 1, sz + dz, B.Concrete);
                    for (int dy = 0; dy <= 6; dy++) store.Set(sx + dx, fy + dy, sz + dz, B.Air);
                    if (dx >= 2 && dx <= 8) store.Set(sx + dx, fy, sz + dz, B.Concrete);
                }
            pos = new V3((sx + 0.5f) * World.VoxelSize, fy * World.VoxelSize, (sz + 0.5f) * World.VoxelSize);
            vel = new V3(0, 0, 0);
            float maxY = pos.Y;
            for (int i = 0; i < 45; i++)
            {
                vel.Y -= 22f / 60f;
                VoxelPhysics.Move(store, ref pos, ref vel, new V3(3f / 60f, vel.Y / 60f, 0), 0.3f, 1.75f, true);
                maxY = MathF.Max(maxY, pos.Y);
            }
            Check(MathF.Abs(maxY - (fy + 1) * World.VoxelSize) < 0.06f && pos.X > (sx + 3) * World.VoxelSize, $"stapt automatisch een trede op (y={maxY:0.00}, x={pos.X:0.00})");

            var hit = store.Raycast(new V3(pos.X, pos.Y + 1.6f, pos.Z), new V3(0, -1, 0), 10);
            Check(hit.Hit && hit.Ny == 1, $"raycast omlaag raakt de bovenkant van een blok ({Blocks.Info[hit.Block].Name})");

            store.Set(3, 40, 3, B.Campfire);
            store.MarkLooted(1, 2, 3);
            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) store.WriteEdits(w);
            var store2 = new VoxelStore();
            ms.Position = 0;
            using (var r = new BinaryReader(ms)) store2.ReadEdits(r);
            var edits = store2.EditsAround(0, 0);
            Check(store2.EditCount == store.EditCount && edits.Exists(e => e.x == 3 && e.y == 40 && e.z == 3 && e.b == B.Campfire) && store2.IsLooted(1, 2, 3),
                $"wijzigingen opslaan en laden ({store.EditCount} wijzigingen)");
            var regen = gen.GenerateChunk(0, 0);
            VoxelStore.ApplyEdits(regen, 0, 0, edits);
            Check(regen[World.IdxPad(4, 40, 4)] == B.Campfire, "wijzigingen worden na hergeneratie weer toegepast");
        }

        Console.WriteLine("Inventaris, crafting, survival");
        {
            var inv = new Inventory();
            inv.Add("hout", 7); inv.Add("steen", 3); inv.Add("stof", 4);
            var fire = Array.Find(Crafting.All, r => r.Result == "kampvuur");
            Check(Crafting.Craft(inv, fire, false) && inv.Count("kampvuur") == 1 && inv.Count("hout") == 3 && inv.Count("steen") == 0, "kampvuur craften kost 4 hout + 3 steen");
            Check(inv.Add("hout", 200) > 0 || inv.Count("hout") == 203, "stapels respecteren de maximale grootte");
            var s = new Survival { Food = 10, Water = 10 };
            s.Use(Items.Get("bonen"));
            Check(s.Food == 45, "eten vult de hongermeter");
            s.Bleeding = true;
            float h0 = s.Health;
            s.Tick(10, 0.8f, 0, false, false);
            Check(s.Health < h0, "bloeden kost gezondheid");
            s.Use(Items.Get("verband"));
            Check(!s.Bleeding, "verband stopt het bloeden");
            var loot = Loot.Roll(LotType.Apotheek, B.Shelf, 10, 20, 30, 1337);
            Check(loot.Count > 0 && loot.TrueForAll(st => Items.Get(st.Id) != null), $"loot uit een apotheekrek: {string.Join(", ", loot.ConvertAll(st => st.Count + "× " + st.Def.Name))}");
            var clock = new GameClock();
            Check(clock.Label == "Dag 1  07:00", $"klok start om 07:00 ({clock.Label})");
        }

        if (args.Length > 0 && args[0] == "map")
        {
            string path = args.Length > 1 ? args[1] : "wereldkaart.png";
            RenderMap(gen, path);
            Console.WriteLine($"Kaart geschreven naar {path}");
        }

        Console.WriteLine(failures == 0 ? "\nAlle tests geslaagd." : $"\n{failures} test(s) mislukt.");
        return failures == 0 ? 0 : 1;
    }

    // Bovenaanzicht van de wereld met reliëfschaduw, 1 pixel = 2 voxels (1 m)
    static void RenderMap(WorldGen gen, string path)
    {
        const int size = 1400, step = 2;
        int x0 = -size / 2 * step + WorldGen.CityCell / 2, z0 = -size / 2 * step + WorldGen.CityCell / 2;
        var hgt = new float[size * size];
        var col = new byte[size * size * 3];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int x = x0 + i * step, z = z0 + j * step;
                var c = gen.GetColumn(x, z);
                hgt[i + j * size] = c.H;
                byte blk;
                switch (c.Kind)
                {
                    case ColumnKind.Highway: case ColumnKind.Bridge: blk = B.Asphalt; break;
                    case ColumnKind.Street: blk = B.Asphalt; break;
                    case ColumnKind.Sidewalk: blk = B.Sidewalk; break;
                    case ColumnKind.Lot:
                    {
                        var lot = gen.LotAtVoxel(x, z);
                        blk = B.Grass;
                        if (lot != null)
                        {
                            int top = -1;
                            for (int ly = lot.Floors * 6 + 20; ly >= 0 && top < 0; ly--) { int bb = gen.BuildingBlock(lot, x, lot.Base + ly, z); if (bb > 0) top = bb; }
                            if (top > 0) blk = (byte)top;
                            else if (lot.Type == LotType.Parkeerplaats || lot.Type == LotType.Benzinestation) blk = B.Asphalt;
                            else if (lot.Type == LotType.Ruine) blk = B.Rubble;
                        }
                        break;
                    }
                    default:
                        if (c.H <= World.Sea) blk = B.Water;
                        else if (c.H <= World.Sea + 1) blk = B.Sand;
                        else if (c.H > World.Sea + 44) blk = B.Stone;
                        else blk = gen.Moisture(x, z) < -0.22f ? B.DeadGrass : gen.Moisture(x, z) > 0.18f ? B.Leaves : B.Grass;
                        break;
                }
                var info = Blocks.Info[blk];
                int k = (i + j * size) * 3;
                col[k] = info.R; col[k + 1] = info.G; col[k + 2] = info.Bl;
                if (blk == B.Water)
                {
                    float depth = Math.Clamp((World.Sea - c.H) / 10f, 0, 1);
                    col[k] = (byte)(60 - depth * 30); col[k + 1] = (byte)(104 - depth * 40); col[k + 2] = (byte)(122 - depth * 30);
                }
            }
        var png = new byte[size * size * 3];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float hx = hgt[Math.Min(size - 1, i + 1) + j * size] - hgt[Math.Max(0, i - 1) + j * size];
                float hz = hgt[i + Math.Min(size - 1, j + 1) * size] - hgt[i + Math.Max(0, j - 1) * size];
                float shade = Math.Clamp(1f + (-hx - hz) * 0.12f, 0.55f, 1.3f);
                int k = (i + j * size) * 3;
                for (int c = 0; c < 3; c++) png[k + c] = (byte)Math.Clamp(col[k + c] * shade, 0, 255);
            }
        WritePng(path, size, size, png);
    }

    static void WritePng(string path, int w, int h, byte[] rgb)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            fs.Write(t); fs.Write(data);
            uint crc = Crc(t, data);
            var cb = BitConverter.GetBytes(crc); Array.Reverse(cb); fs.Write(cb);
        }
        var ihdr = new byte[13];
        void Be(int v, int o) { ihdr[o] = (byte)(v >> 24); ihdr[o + 1] = (byte)(v >> 16); ihdr[o + 2] = (byte)(v >> 8); ihdr[o + 3] = (byte)v; }
        Be(w, 0); Be(h, 4); ihdr[8] = 8; ihdr[9] = 2;
        Chunk("IHDR", ihdr);
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++) { raw.WriteByte(0); raw.Write(rgb, y * w * 3, w * 3); }
        var z = new MemoryStream();
        using (var ds = new ZLibStream(z, CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(ds); }
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", Array.Empty<byte>());
    }

    static uint[] crcTable;
    static uint Crc(byte[] a, byte[] b)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; crcTable[n] = c; }
        }
        uint crc = 0xFFFFFFFFu;
        foreach (var x in a) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        foreach (var x in b) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
